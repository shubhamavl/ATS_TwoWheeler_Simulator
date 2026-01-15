using System;

namespace ATS_TwoWheeler_Simulator.Core
{
    /// <summary>
    /// STM32 state management - tracks current system state for ATS Two-Wheeler
    /// </summary>
    public class STM32State
    {
        // ADC Mode: 0 = Internal (12-bit), 1 = ADS1115 (16-bit)
        private byte _adcMode = 1; // Default to ADS1115

        // Stream state (single stream for total weight)
        private bool _streamActive = false;
        private byte _streamRate = 0x04; // Default 1kHz (0x04 for v0.1)
        private bool _isBrakeMode = false; // False=Weight, True=Brake

        // System status
        private byte _systemStatus = 0; // 0=OK, 1=Warning, 2=Error, 3=Critical
        private byte _errorFlags = 0;

        // Firmware version
        private byte _firmwareMajor = 0;
        private byte _firmwareMinor = 1;
        private byte _firmwarePatch = 0;
        private byte _firmwareBuild = 0;

        // Bootloader state
        private bool _bootloaderActive = false;
        private bool _updateInProgress = false;
        private uint _updateSize = 0;
        private uint _flashWriteOffset = 0;
        private byte _expectedSequence = 0;

        // 8KB RAM buffer for firmware update simulation
        private const int RAM_BUFFER_SIZE = 8192; // 8KB
        private byte[] _ramBuffer = new byte[RAM_BUFFER_SIZE];
        private int _ramBufferOffset = 0;
        private uint _updateCrc = 0xFFFFFFFF;

        // Thread safety
        private readonly object _lock = new object();

        public byte ADCMode
        {
            get { lock (_lock) { return _adcMode; } }
            set { lock (_lock) { _adcMode = value; } }
        }

        public bool StreamActive
        {
            get { lock (_lock) { return _streamActive; } }
            set { lock (_lock) { _streamActive = value; } }
        }

        public byte StreamRate
        {
            get { lock (_lock) { return _streamRate; } }
            set { lock (_lock) { _streamRate = value; } }
        }

        public bool IsBrakeMode
        {
            get { lock (_lock) { return _isBrakeMode; } }
            set { lock (_lock) { _isBrakeMode = value; } }
        }

        public byte SystemStatus
        {
            get { lock (_lock) { return _systemStatus; } }
            set { lock (_lock) { _systemStatus = value; } }
        }

        public byte ErrorFlags
        {
            get { lock (_lock) { return _errorFlags; } }
            set { lock (_lock) { _errorFlags = value; } }
        }

        public (byte major, byte minor, byte patch, byte build) FirmwareVersion
        {
            get
            {
                lock (_lock)
                {
                    return (_firmwareMajor, _firmwareMinor, _firmwarePatch, _firmwareBuild);
                }
            }
            set
            {
                lock (_lock)
                {
                    _firmwareMajor = value.major;
                    _firmwareMinor = value.minor;
                    _firmwarePatch = value.patch;
                    _firmwareBuild = value.build;
                }
            }
        }

        /// <summary>
        /// Stop all active streams
        /// </summary>
        public void StopAllStreams()
        {
            lock (_lock)
            {
                _streamActive = false;
            }
        }

        /// <summary>
        /// Get transmission interval in milliseconds for a rate code
        /// </summary>
        public static int GetRateIntervalMs(byte rate)
        {
            return rate switch
            {
                0x01 => 1000,  // 1Hz = 1000ms
                0x02 => 10,    // 100Hz = 10ms
                0x03 => 2,     // 500Hz = 2ms
                0x04 => 1,     // 1kHz = 1ms
                _ => 1         // Default 1kHz
            };
        }

        // Bootloader state properties
        public bool BootloaderActive
        {
            get { lock (_lock) { return _bootloaderActive; } }
            set { lock (_lock) { _bootloaderActive = value; } }
        }

        public bool UpdateInProgress
        {
            get { lock (_lock) { return _updateInProgress; } }
            set { lock (_lock) { _updateInProgress = value; } }
        }

        public uint UpdateSize
        {
            get { lock (_lock) { return _updateSize; } }
            set { lock (_lock) { _updateSize = value; } }
        }

        public uint FlashWriteOffset
        {
            get { lock (_lock) { return _flashWriteOffset; } }
            set { lock (_lock) { _flashWriteOffset = value; } }
        }

        public byte ExpectedSequence
        {
            get { lock (_lock) { return _expectedSequence; } }
            set { lock (_lock) { _expectedSequence = value; } }
        }

        public uint UpdateCrc
        {
            get { lock (_lock) { return _updateCrc; } }
            set { lock (_lock) { _updateCrc = value; } }
        }

        /// <summary>
        /// Reset all bootloader state variables
        /// </summary>
        public void ResetBootloaderState()
        {
            lock (_lock)
            {
                _updateInProgress = false;
                _updateSize = 0;
                _flashWriteOffset = 0;
                _ramBufferOffset = 0;
                _expectedSequence = 0;
                _updateCrc = 0xFFFFFFFF;
                Array.Fill(_ramBuffer, (byte)0xFF);
            }
        }

        /// <summary>
        /// Flush RAM buffer to flash (simulate flash write)
        /// </summary>
        public void FlushRamBufferToFlash()
        {
            lock (_lock)
            {
                if (_ramBufferOffset > 0)
                {
                    // Simulate flash write: update flash write offset
                    _flashWriteOffset += (uint)_ramBufferOffset;
                    // Clear buffer
                    _ramBufferOffset = 0;
                    Array.Fill(_ramBuffer, (byte)0xFF);
                }
            }
        }

        /// <summary>
        /// Store data in RAM buffer
        /// </summary>
        public void StoreInRamBuffer(byte[] data, int length)
        {
            lock (_lock)
            {
                if (_ramBufferOffset + length <= RAM_BUFFER_SIZE)
                {
                    Array.Copy(data, 0, _ramBuffer, _ramBufferOffset, length);
                    _ramBufferOffset += length;
                }
            }
        }

        /// <summary>
        /// Check if RAM buffer would overflow with additional bytes
        /// </summary>
        public bool IsRamBufferFull(int additionalBytes)
        {
            lock (_lock)
            {
                return _ramBufferOffset + additionalBytes > RAM_BUFFER_SIZE;
            }
        }

        /// <summary>
        /// Get total data received (flash offset + RAM buffer offset)
        /// </summary>
        public uint GetTotalReceived()
        {
            lock (_lock)
            {
                return _flashWriteOffset + (uint)_ramBufferOffset;
            }
        }

        /// <summary>
        /// Get current RAM buffer offset
        /// </summary>
        public int RamBufferOffset
        {
            get { lock (_lock) { return _ramBufferOffset; } }
        }
    }
}

