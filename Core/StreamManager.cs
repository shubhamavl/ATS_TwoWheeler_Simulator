using System;
using System.Threading;
using System.Threading.Tasks;
using ATS_TwoWheeler_Simulator.Models;
using ATS_TwoWheeler_Simulator.Adapters;

namespace ATS_TwoWheeler_Simulator.Core
{
    /// <summary>
    /// Stream Manager - Manages total weight data streaming at configured rates
    /// </summary>
    public class StreamManager : IDisposable
    {
        private readonly STM32State _state;
        private readonly ProtocolHandler _protocolHandler;
        private readonly ICanAdapter _adapter;

        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _streamingTask;
        private volatile bool _isRunning = false;

        // High-precision timing
        private readonly System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();

        public StreamManager(STM32State state, ProtocolHandler protocolHandler, ICanAdapter adapter)
        {
            _state = state;
            _protocolHandler = protocolHandler;
            _adapter = adapter;
        }

        /// <summary>
        /// Start streaming loop
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _streamingTask = Task.Run(() => StreamingLoopAsync(_cancellationTokenSource.Token));
        }

        /// <summary>
        /// Stop streaming loop
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _cancellationTokenSource?.Cancel();
            _streamingTask?.Wait(1000);
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        /// <summary>
        /// Main streaming loop with high-precision timing (single total weight stream)
        /// </summary>
        private async Task StreamingLoopAsync(CancellationToken token)
        {
            long lastStreamTime = 0;

            while (_isRunning && !token.IsCancellationRequested)
            {
                try
                {
                    long currentTime = _stopwatch.ElapsedMilliseconds;

                    // Check total weight stream
                    if (_state.StreamActive)
                    {
                        int streamInterval = STM32State.GetRateIntervalMs(_state.StreamRate);
                        if (currentTime - lastStreamTime >= streamInterval)
                        {
                            CANMessage totalMessage = _protocolHandler.GenerateTotalDataMessage();
                            _adapter.SendMessage(totalMessage.ID, totalMessage.Data ?? new byte[0]);
                            lastStreamTime = currentTime;
                        }
                    }

                    // Small delay to prevent CPU spinning
                    if (!_state.StreamActive)
                    {
                        await Task.Delay(10, token);
                    }
                    else
                    {
                        // For high-rate streaming, use minimal delay
                        await Task.Delay(0, token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Continue on error
                    await Task.Delay(1, token);
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

