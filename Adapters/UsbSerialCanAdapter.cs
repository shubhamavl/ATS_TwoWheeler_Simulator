using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using ATS_TwoWheeler_Simulator.Models;

namespace ATS_TwoWheeler_Simulator.Adapters
{
    /// <summary>
    /// USB-CAN-A Serial adapter implementation using SerialPort
    /// </summary>
    public class UsbSerialCanAdapter : ICanAdapter
    {
        public string AdapterType => "USB-CAN-A Serial";

        private SerialPort? _serialPort;
        private readonly ConcurrentQueue<byte> _frameBuffer = new();
        private volatile bool _connected;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly object _sendLock = new object();
        private DateTime _lastMessageTime = DateTime.MinValue;
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);
        private bool _timeoutNotified = false;

        // Protocol constants
        private const byte FRAME_HEADER = 0xAA;
        private const byte FRAME_FOOTER = 0x55;
        private const uint MAX_CAN_ID = 0x7FF; // 11-bit CAN ID limit

        public bool IsConnected => _connected;

        public event Action<CANMessage>? MessageReceived;
        public event EventHandler<string>? DataTimeout;
        public event EventHandler<bool>? ConnectionStatusChanged;

        public bool Connect(CanAdapterConfig config, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (config is not UsbSerialCanAdapterConfig usbConfig)
            {
                errorMessage = "Invalid configuration type for USB-CAN-A Serial adapter";
                return false;
            }

            try
            {
                if (string.IsNullOrEmpty(usbConfig.PortName))
                {
                    // Auto-detect COM port
                    string[] availablePorts = SerialPort.GetPortNames();
                    if (availablePorts.Length == 0)
                    {
                        errorMessage = "No COM ports found. Please check:\n• USB-CAN-A is connected\n• CH341 driver is installed\n• Device appears in Device Manager";
                        return false;
                    }
                    usbConfig.PortName = availablePorts[availablePorts.Length - 1];
                }

                _serialPort = new SerialPort(usbConfig.PortName, usbConfig.SerialBaudRate, Parity.None, 8, StopBits.One);
                _serialPort.Open();
                _connected = true;

                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(() => ReadMessagesAsync(_cancellationTokenSource.Token));

                ConnectionStatusChanged?.Invoke(this, true);
                System.Diagnostics.Debug.WriteLine($"USB-CAN-A Connected on {usbConfig.PortName}");
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                System.Diagnostics.Debug.WriteLine($"USB-CAN-A connection error: {ex.Message}");
                _connected = false;
                ConnectionStatusChanged?.Invoke(this, false);
                return false;
            }
        }

        public void Disconnect()
        {
            _connected = false;
            _cancellationTokenSource?.Cancel();
            if (_serialPort?.IsOpen == true)
                _serialPort.Close();

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            ConnectionStatusChanged?.Invoke(this, false);
            System.Diagnostics.Debug.WriteLine("USB-CAN-A Disconnected");
        }

        public bool SendMessage(uint id, byte[] data)
        {
            if (!_connected || _serialPort == null) return false;

            try
            {
                // Validate CAN ID (11-bit max for standard frame)
                if (id > MAX_CAN_ID)
                {
                    System.Diagnostics.Debug.WriteLine($"Invalid CAN ID: 0x{id:X3} (max 0x{MAX_CAN_ID:X3} for standard frame)");
                    return false;
                }

                // Validate data length
                if (data != null && data.Length > 8)
                {
                    System.Diagnostics.Debug.WriteLine($"Invalid data length: {data.Length} (max 8 bytes)");
                    return false;
                }

                var frame = CreateFrame(id, data ?? new byte[0]);

                lock (_sendLock)
                {
                    _serialPort.Write(frame, 0, frame.Length);
                }

                // Fire event for TX messages
                var txMessage = new CANMessage(id, data ?? new byte[0], DateTime.Now, "TX");
                MessageReceived?.Invoke(txMessage);

                System.Diagnostics.Debug.WriteLine($"USB-CAN-A: Sent CAN frame ID=0x{id:X3}, Data={BitConverter.ToString(data ?? new byte[0])}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Send message error: {ex.Message}");
                return false;
            }
        }

        public string[] GetAvailableOptions()
        {
            return SerialPort.GetPortNames();
        }

        private async Task ReadMessagesAsync(CancellationToken token)
        {
            var buffer = new byte[256];
            _lastMessageTime = DateTime.UtcNow;

            while (_connected && !token.IsCancellationRequested)
            {
                try
                {
                    if (_serialPort is { IsOpen: true } && _serialPort.BytesToRead > 0)
                    {
                        int count = _serialPort.Read(buffer, 0, buffer.Length);

                        for (int i = 0; i < count; i++)
                            _frameBuffer.Enqueue(buffer[i]);

                        ProcessFrames();

                        // Update last received time
                        _lastMessageTime = DateTime.UtcNow;
                        _timeoutNotified = false;
                    }
                }
                catch
                {
                    // ignore read errors for now
                }

                // Check for timeout
                if (!_timeoutNotified && DateTime.UtcNow - _lastMessageTime > _timeout)
                {
                    _timeoutNotified = true;
                    DataTimeout?.Invoke(this, "Timeout");
                }

                await Task.Delay(5, token);
            }
        }

        private void ProcessFrames()
        {
            // Variable-length protocol: [0xAA] [Type] [ID_LOW] [ID_HIGH] [DATA...] [0x55]
            while (_frameBuffer.Count >= 5)
            {
                if (!_frameBuffer.TryPeek(out byte first) || first != FRAME_HEADER)
                {
                    _frameBuffer.TryDequeue(out _);
                    continue;
                }

                if (_frameBuffer.Count < 2) break;

                if (!_frameBuffer.TryDequeue(out byte header) || !_frameBuffer.TryDequeue(out byte typeByte))
                    continue;

                if (header != FRAME_HEADER) continue;

                byte dlc = (byte)(typeByte & 0x0F);
                int frameLength = 5 + dlc;
                int remainingBytes = 2 + dlc + 1;

                if (_frameBuffer.Count < remainingBytes)
                {
                    _frameBuffer.Enqueue(header);
                    _frameBuffer.Enqueue(typeByte);
                    break;
                }

                var frame = new byte[frameLength];
                frame[0] = header;
                frame[1] = typeByte;

                for (int i = 2; i < frameLength; i++)
                {
                    if (!_frameBuffer.TryDequeue(out frame[i]))
                        return;
                }

                DecodeFrame(frame);
            }
        }

        private void DecodeFrame(byte[] frame)
        {
            if (frame.Length < 5 || frame[0] != FRAME_HEADER)
                return;

            try
            {
                if (frame[frame.Length - 1] != FRAME_FOOTER)
                {
                    System.Diagnostics.Debug.WriteLine($"Invalid frame footer: expected 0x55, got 0x{frame[frame.Length - 1]:X2}");
                    return;
                }

                byte typeByte = frame[1];
                byte dlc = (byte)(typeByte & 0x0F);
                
                int expectedLength = 5 + dlc;
                if (frame.Length != expectedLength)
                {
                    System.Diagnostics.Debug.WriteLine($"Frame length mismatch: expected {expectedLength}, got {frame.Length}");
                    return;
                }

                uint canId = (uint)(frame[2] | (frame[3] << 8));

                byte[] canData = new byte[dlc];
                if (dlc > 0)
                {
                    Array.Copy(frame, 4, canData, 0, dlc);
                }

                // Accept all messages for simulator (no filtering)
                var canMessage = new CANMessage(canId, canData, DateTime.Now);
                MessageReceived?.Invoke(canMessage);

                System.Diagnostics.Debug.WriteLine($"Processed: ID=0x{canId:X3}, DLC={dlc}, Data={BitConverter.ToString(canData)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Decode error: {ex.Message}");
            }
        }

        private static byte[] CreateFrame(uint id, byte[] data)
        {
            byte dlc = (byte)Math.Min(data.Length, 8);
            
            var frame = new List<byte>
            {
                FRAME_HEADER,
                (byte)(0xC0 | dlc),
                (byte)(id & 0xFF),
                (byte)((id >> 8) & 0xFF)
            };

            if (data != null && dlc > 0)
            {
                frame.AddRange(data.Take(dlc));
            }

            frame.Add(FRAME_FOOTER);
            
            return frame.ToArray();
        }

        public void Dispose()
        {
            Disconnect();
            _serialPort?.Dispose();
        }
    }
}

