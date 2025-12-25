using System;
using ATS_TwoWheeler_Simulator.Models;

namespace ATS_TwoWheeler_Simulator.Core
{
    /// <summary>
    /// Protocol Handler - Handles Protocol v0.1 messages and generates responses for ATS Two-Wheeler
    /// </summary>
    public class ProtocolHandler
    {
        private readonly STM32State _state;
        private readonly ADCSimulator _adcSimulator;
        private readonly WeightPatternGenerator _patternGenerator;
        private readonly NoiseGenerator _noiseGenerator;

        // Protocol constants - v0.1 (Total Weight System)
        private const uint CAN_MSG_ID_TOTAL_RAW_DATA = 0x200;      // Total raw ADC data (Ch0+Ch1+Ch2+Ch3)
        private const uint CAN_MSG_ID_START_STREAM = 0x040;        // Start streaming (single stream)
        private const uint CAN_MSG_ID_STOP_ALL_STREAMS = 0x044;   // Stop all streams
        private const uint CAN_MSG_ID_MODE_INTERNAL = 0x030;      // Switch to Internal ADC mode
        private const uint CAN_MSG_ID_MODE_ADS1115 = 0x031;       // Switch to ADS1115 mode
        private const uint CAN_MSG_ID_STATUS_REQUEST = 0x032;      // Request system status
        private const uint CAN_MSG_ID_VERSION_REQUEST = 0x033;    // Request firmware version
        private const uint CAN_MSG_ID_SYSTEM_STATUS = 0x300;      // System status response
        private const uint CAN_MSG_ID_VERSION_RESPONSE = 0x301;    // Firmware version response

        // Bootloader CAN IDs (same as v2.0)
        private const uint CAN_ID_BOOT_ENTER = 0x510;
        private const uint CAN_ID_BOOT_QUERY_INFO = 0x511;
        private const uint CAN_ID_BOOT_PING = 0x512;
        private const uint CAN_ID_BOOT_BEGIN = 0x513;
        private const uint CAN_ID_BOOT_END = 0x514;
        private const uint CAN_ID_BOOT_RESET = 0x515;
        private const uint CAN_ID_BOOT_DATA_BASE = 0x520;
        private const uint CAN_ID_BOOT_DATA_MAX = 0x91F;
        private const uint CAN_ID_BOOT_PING_RESPONSE = 0x517;
        private const uint CAN_ID_BOOT_BEGIN_RESPONSE = 0x518;
        private const uint CAN_ID_BOOT_PROGRESS = 0x519;
        private const uint CAN_ID_BOOT_END_RESPONSE = 0x51A;
        private const uint CAN_ID_BOOT_ERROR = 0x51B;
        private const uint CAN_ID_BOOT_QUERY_RESPONSE = 0x51C;

        // Bootloader status codes
        private const byte BOOTLOADER_STATUS_READY = 0x01;
        private const byte BOOTLOADER_STATUS_IN_PROGRESS = 0x02;
        private const byte BOOTLOADER_STATUS_SUCCESS = 0x03;
        private const byte BOOTLOADER_STATUS_FAILED_CHECKSUM = 0x04;
        private const byte BOOTLOADER_STATUS_FAILED_FLASH = 0x06;

        public event Action<CANMessage>? ResponseReady;

        public ProtocolHandler(STM32State state, ADCSimulator adcSimulator, 
                              WeightPatternGenerator patternGenerator, NoiseGenerator noiseGenerator)
        {
            _state = state;
            _adcSimulator = adcSimulator;
            _patternGenerator = patternGenerator;
            _noiseGenerator = noiseGenerator;
        }

        /// <summary>
        /// Process incoming CAN message and generate response if needed
        /// </summary>
        public void ProcessMessage(CANMessage message)
        {
            // Check for bootloader data frames first (range 0x520-0x91F)
            if (message.ID >= CAN_ID_BOOT_DATA_BASE && message.ID <= CAN_ID_BOOT_DATA_MAX)
            {
                HandleBootloaderData(message);
                return;
            }

            switch (message.ID)
            {
                // Bootloader commands
                case CAN_ID_BOOT_ENTER:
                    HandleBootloaderEnter();
                    break;

                case CAN_ID_BOOT_QUERY_INFO:
                    HandleBootloaderQuery();
                    break;

                case CAN_ID_BOOT_PING:
                    HandleBootloaderPing();
                    break;

                case CAN_ID_BOOT_BEGIN:
                    HandleBootloaderBegin(message);
                    break;

                case CAN_ID_BOOT_END:
                    HandleBootloaderEnd(message);
                    break;

                case CAN_ID_BOOT_RESET:
                    HandleBootloaderReset();
                    break;

                // Normal two-wheeler messages (v0.1)
                case CAN_MSG_ID_START_STREAM:
                    HandleStartStream(message);
                    break;

                case CAN_MSG_ID_STOP_ALL_STREAMS:
                    HandleStopAllStreams();
                    break;

                case CAN_MSG_ID_MODE_INTERNAL:
                    HandleSwitchToInternal();
                    break;

                case CAN_MSG_ID_MODE_ADS1115:
                    HandleSwitchToADS1115();
                    break;

                case CAN_MSG_ID_STATUS_REQUEST:
                    HandleStatusRequest();
                    break;

                case CAN_MSG_ID_VERSION_REQUEST:
                    HandleVersionRequest();
                    break;
            }
        }

        /// <summary>
        /// Generate total weight data message (all 4 channels summed)
        /// </summary>
        public CANMessage GenerateTotalDataMessage()
        {
            double totalWeight = _patternGenerator.CalculateTotalWeight();
            byte adcMode = _state.ADCMode;
            byte[] data;

            if (adcMode == 0) // Internal 12-bit
            {
                ushort totalADC = _adcSimulator.CalculateTotalADC(totalWeight, adcMode);
                totalADC = _noiseGenerator.AddNoise(totalADC, adcMode);
                data = new byte[] { (byte)(totalADC & 0xFF), (byte)((totalADC >> 8) & 0xFF) };
            }
            else // ADS1115 16-bit (signed)
            {
                int totalADCSigned = _adcSimulator.CalculateTotalADCSigned(totalWeight);
                totalADCSigned = _noiseGenerator.AddNoiseSigned(totalADCSigned);
                data = BitConverter.GetBytes(totalADCSigned);
            }

            return new CANMessage(CAN_MSG_ID_TOTAL_RAW_DATA, data, DateTime.Now);
        }

        private void HandleStartStream(CANMessage message)
        {
            if (message.Data != null && message.Data.Length > 0)
            {
                byte rate = message.Data[0];
                _state.StreamRate = rate;
                _state.StreamActive = true;
            }
        }

        private void HandleStopAllStreams()
        {
            _state.StopAllStreams();
        }

        private void HandleSwitchToInternal()
        {
            _state.ADCMode = 0;
        }

        private void HandleSwitchToADS1115()
        {
            _state.ADCMode = 1;
        }

        private void HandleStatusRequest()
        {
            byte[] statusData = new byte[]
            {
                _state.SystemStatus,
                _state.ErrorFlags,
                _state.ADCMode
            };
            var response = new CANMessage(CAN_MSG_ID_SYSTEM_STATUS, statusData, DateTime.Now);
            ResponseReady?.Invoke(response);
        }

        private void HandleVersionRequest()
        {
            SendVersionMessage();
        }

        /// <summary>
        /// Send status message automatically (for periodic updates)
        /// </summary>
        public void SendStatusMessage()
        {
            byte[] statusData = new byte[]
            {
                _state.SystemStatus,
                _state.ErrorFlags,
                _state.ADCMode
            };
            var response = new CANMessage(CAN_MSG_ID_SYSTEM_STATUS, statusData, DateTime.Now);
            ResponseReady?.Invoke(response);
        }

        /// <summary>
        /// Send version message automatically (for periodic updates)
        /// </summary>
        public void SendVersionMessage()
        {
            var (major, minor, patch, build) = _state.FirmwareVersion;
            byte[] versionData = new byte[] { major, minor, patch, build };
            var response = new CANMessage(CAN_MSG_ID_VERSION_RESPONSE, versionData, DateTime.Now);
            ResponseReady?.Invoke(response);
        }

        // Bootloader handlers (same as v2.0)
        private void HandleBootloaderEnter()
        {
            _state.BootloaderActive = true;
            _state.ResetBootloaderState();
            // Note: Real STM32 resets, simulator just enters bootloader mode
        }

        private void HandleBootloaderQuery()
        {
            var (major, minor, patch, build) = _state.FirmwareVersion;
            // Query Response: [0x01, major, minor, patch]
            // 0x01 = Bootloader present indicator
            byte[] queryData = new byte[] { 0x01, major, minor, patch };
            var response = new CANMessage(CAN_ID_BOOT_QUERY_RESPONSE, queryData, DateTime.Now);
            ResponseReady?.Invoke(response);
        }

        private void HandleBootloaderPing()
        {
            // Ping Response: [0x01] (READY status)
            byte[] pingData = new byte[] { BOOTLOADER_STATUS_READY };
            var response = new CANMessage(CAN_ID_BOOT_PING_RESPONSE, pingData, DateTime.Now);
            ResponseReady?.Invoke(response);
        }

        private void HandleBootloaderBegin(CANMessage message)
        {
            // Begin command: 4 bytes (firmware size, little-endian uint32)
            if (message.Data == null || message.Data.Length < 4)
            {
                SendBootloaderError(BOOTLOADER_STATUS_FAILED_FLASH, 0);
                return;
            }

            // Read firmware size (little-endian uint32)
            uint firmwareSize = BitConverter.ToUInt32(message.Data, 0);

            // Validate size: 4KB minimum (0x1000), 120KB maximum (0x1E000)
            const uint MIN_SIZE = 0x1000;  // 4KB
            const uint MAX_SIZE = 0x1E000; // 120KB

            if (firmwareSize < MIN_SIZE || firmwareSize > MAX_SIZE)
            {
                SendBootloaderError(BOOTLOADER_STATUS_FAILED_FLASH, 0);
                return;
            }

            // Initialize update state
            _state.ResetBootloaderState(); // Reset everything first
            _state.UpdateInProgress = true;
            _state.UpdateSize = firmwareSize;
            _state.FlashWriteOffset = 0;
            _state.ExpectedSequence = 0;
            _state.UpdateCrc = 0xFFFFFFFF;

            // Send Begin Response: [0x02] (IN_PROGRESS)
            byte[] beginData = new byte[] { BOOTLOADER_STATUS_IN_PROGRESS };
            var response = new CANMessage(CAN_ID_BOOT_BEGIN_RESPONSE, beginData, DateTime.Now);
            ResponseReady?.Invoke(response);
        }

        private void HandleBootloaderData(CANMessage message)
        {
            // Extract sequence from CAN ID
            byte receivedSeq = (byte)(message.ID - CAN_ID_BOOT_DATA_BASE);

            // Validate data length (should be 8 bytes)
            if (message.Data == null || message.Data.Length < 8)
            {
                SendBootloaderError(BOOTLOADER_STATUS_FAILED_CHECKSUM, _state.ExpectedSequence);
                return;
            }

            // Reject data if update not in progress
            if (!_state.UpdateInProgress)
            {
                // Update not in progress - ignore data
                return;
            }

            // Check total data received (RAM buffer + already written to flash)
            uint totalReceived = _state.GetTotalReceived();

            // Check for data overflow
            if (totalReceived >= _state.UpdateSize)
            {
                // Excess data received - send error
                _state.UpdateInProgress = false;
                SendBootloaderError(BOOTLOADER_STATUS_FAILED_CHECKSUM, 0);
                _state.ResetBootloaderState();
                return;
            }

            // Validate sequence matches expected
            if (receivedSeq != _state.ExpectedSequence)
            {
                // Sequence mismatch - send error with expected sequence for retry
                SendBootloaderError(BOOTLOADER_STATUS_FAILED_CHECKSUM, _state.ExpectedSequence);
                return;
            }

            // Calculate chunk size (remaining bytes or 8, whichever is smaller)
            uint remaining = _state.UpdateSize - totalReceived;
            int chunkSize = (int)(remaining > 8 ? 8 : remaining);

            // Check if RAM buffer would overflow
            if (_state.IsRamBufferFull(chunkSize))
            {
                // Buffer full (8KB) - flush to flash first
                _state.FlushRamBufferToFlash();
                SendBootloaderProgress();
                // Note: FlushRamBufferToFlash() already clears the buffer offset
            }

            // Store in RAM buffer
            byte[] chunkData = new byte[chunkSize];
            Array.Copy(message.Data, 0, chunkData, 0, chunkSize);
            _state.StoreInRamBuffer(chunkData, chunkSize);

            // Update CRC32 (all 8 bytes are data, no sequence byte to skip)
            _state.UpdateCrc = UpdateCrc32(_state.UpdateCrc, chunkData);

            // Increment expected sequence (wraps automatically: 255 → 0)
            _state.ExpectedSequence++;
        }

        private void HandleBootloaderEnd(CANMessage message)
        {
            // End command: 4 bytes (CRC32, little-endian uint32)
            if (message.Data == null || message.Data.Length < 4)
            {
                SendBootloaderError(BOOTLOADER_STATUS_FAILED_CHECKSUM, 0);
                _state.ResetBootloaderState();
                return;
            }

            // Flush remaining RAM buffer to flash
            _state.FlushRamBufferToFlash();

            // Verify all data received
            if (_state.FlashWriteOffset != _state.UpdateSize)
            {
                SendBootloaderError(BOOTLOADER_STATUS_FAILED_CHECKSUM, 0);
                _state.ResetBootloaderState();
                return;
            }

            // Read received CRC32 from message
            uint receivedCrc = BitConverter.ToUInt32(message.Data, 0);

            // Calculate final CRC32 (running CRC ^ 0xFFFFFFFF)
            uint calculatedCrc = _state.UpdateCrc ^ 0xFFFFFFFF;

            // Compare CRCs
            if (receivedCrc != calculatedCrc)
            {
                SendBootloaderError(BOOTLOADER_STATUS_FAILED_CHECKSUM, 0);
                _state.ResetBootloaderState();
                return;
            }

            // Send End Response: [0x03] (SUCCESS)
            byte[] endData = new byte[] { BOOTLOADER_STATUS_SUCCESS };
            var response = new CANMessage(CAN_ID_BOOT_END_RESPONSE, endData, DateTime.Now);
            ResponseReady?.Invoke(response);

            // Send final progress update (100%)
            SendBootloaderProgress();

            // Reset bootloader state
            _state.UpdateInProgress = false;
            _state.ResetBootloaderState();
        }

        private void HandleBootloaderReset()
        {
            _state.BootloaderActive = false;
            _state.ResetBootloaderState();
            // Note: Real STM32 resets, simulator just exits bootloader mode
        }

        // Helper methods
        private void SendBootloaderProgress()
        {
            // Calculate percentage: (FlashWriteOffset * 100) / UpdateSize
            byte percent = 0;
            if (_state.UpdateSize > 0)
            {
                // Use 64-bit calculation to prevent overflow
                ulong progress_64 = ((ulong)_state.FlashWriteOffset * 100) / _state.UpdateSize;
                percent = (byte)(progress_64 > 100 ? 100 : progress_64); // Cap at 100%
            }

            // Progress message format: [percent, bytes_L, bytes_H, bytes_H2, bytes_H3]
            byte[] progressData = new byte[5];
            progressData[0] = percent;
            uint bytesWritten = _state.FlashWriteOffset;
            progressData[1] = (byte)(bytesWritten & 0xFF);
            progressData[2] = (byte)((bytesWritten >> 8) & 0xFF);
            progressData[3] = (byte)((bytesWritten >> 16) & 0xFF);
            progressData[4] = (byte)((bytesWritten >> 24) & 0xFF);

            var response = new CANMessage(CAN_ID_BOOT_PROGRESS, progressData, DateTime.Now);
            ResponseReady?.Invoke(response);
        }

        private void SendBootloaderError(byte errorCode, byte additionalData)
        {
            // Error Response: [errorCode, additionalData]
            byte[] errorData = new byte[] { errorCode, additionalData };
            var response = new CANMessage(CAN_ID_BOOT_ERROR, errorData, DateTime.Now);
            ResponseReady?.Invoke(response);
        }

        /// <summary>
        /// Update CRC32 calculation (matching PC application algorithm)
        /// </summary>
        private uint UpdateCrc32(uint running, byte[] data)
        {
            const uint polynomial = 0x04C11DB7u;
            uint crc = running;

            foreach (byte b in data)
            {
                crc ^= b;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x00000001) != 0)
                    {
                        crc = (crc >> 1) ^ polynomial;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }

            return crc;
        }
    }
}

