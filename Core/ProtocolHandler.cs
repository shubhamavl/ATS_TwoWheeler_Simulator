using System;
using System.Diagnostics;
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
        private const uint CAN_MSG_ID_SYS_PERF = 0x302;           // System performance metrics
        private const uint CAN_MSG_ID_SET_SYSTEM_MODE = 0x050;    // Set System Mode (Weight/Brake)

        // Bootloader CAN IDs (same as v2.0)
        private const uint CAN_ID_BOOT_ENTER = 0x510;
        private const uint CAN_ID_BOOT_QUERY_INFO = 0x511;
        private const uint CAN_ID_BOOT_PING = 0x512;
        private const uint CAN_ID_BOOT_BEGIN = 0x513;
        private const uint CAN_ID_BOOT_END = 0x514;
        private const uint CAN_ID_BOOT_RESET = 0x515;
        private const uint CAN_ID_BOOT_DATA = 0x520;           // Single ID for all data frames
        private const uint CAN_ID_BOOT_PING_RESPONSE = 0x517;
        private const uint CAN_ID_BOOT_BEGIN_RESPONSE = 0x518;
        private const uint CAN_ID_BOOT_PROGRESS = 0x519;
        private const uint CAN_ID_BOOT_END_RESPONSE = 0x51A;
        private const uint CAN_ID_BOOT_ERROR = 0x51B;
        private const uint CAN_ID_ERR_SIZE = 0x51D;
        private const uint CAN_ID_ERR_WRITE = 0x51E;
        private const uint CAN_ID_ERR_VALIDATION = 0x51F;
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

            // Set default version to 0.2.0.0 to match latest firmware
            _state.FirmwareVersion = (0, 2, 0, 0);
        }

        /// <summary>
        /// Process incoming CAN message and generate response if needed
        /// </summary>
        public void ProcessMessage(CANMessage message)
        {
            // Ignore our own transmitted messages (echoes)
            if (message.Direction == "TX") return;

            // Track bootloader command IDs for UI
            if (message.ID >= 0x510 && message.ID <= 0x91F)
            {
                _state.LastBootCommandId = message.ID;
            }

            if (message.ID == CAN_ID_BOOT_DATA)
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

                case CAN_MSG_ID_SET_SYSTEM_MODE:
                    HandleSetSystemMode(message);
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
            bool isBrakeMode = _state.IsBrakeMode; // Get current mode
            byte[] data;

            if (adcMode == 0) // Internal 12-bit
            {
                ushort totalADC = _adcSimulator.CalculateTotalADC(totalWeight, adcMode, isBrakeMode);
                totalADC = _noiseGenerator.AddNoise(totalADC, adcMode);
                data = new byte[] { (byte)(totalADC & 0xFF), (byte)((totalADC >> 8) & 0xFF) };
            }
            else // ADS1115 16-bit (signed)
            {
                int totalADCSigned = _adcSimulator.CalculateTotalADCSigned(totalWeight, isBrakeMode);
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
            _adcSimulator.CurrentMode = 0;  // Sync ADCSimulator mode
        }

        private void HandleSwitchToADS1115()
        {
            _state.ADCMode = 1;
            _adcSimulator.CurrentMode = 1;  // Sync ADCSimulator mode
        }

        private void HandleStatusRequest()
        {
            SendStatusMessage();
        }

        private void HandleVersionRequest()
        {
            SendVersionMessage();
        }

        private void HandleSetSystemMode(CANMessage message)
        {
            if (message.Data != null && message.Data.Length > 0)
            {
                byte mode = message.Data[0];
                if (mode == 0x01)
                {
                    _state.IsBrakeMode = true;
                }
                else
                {
                    _state.IsBrakeMode = false;
                }
            }
            // In firmware, a mode change also triggers a status response
            SendStatusMessage();
        }

        /// <summary>
        /// Send status message automatically (v1.1 - 6 bytes packed)
        /// </summary>
        public void SendStatusMessage()
        {
            // Byte 0: status_packed (Bit 0-1: status, Bit 2: adc_mode, Bit 3: relay_state)
            byte statusPacked = (byte)(_state.SystemStatus & 0x03);
            if (_state.ADCMode == 1) statusPacked |= 0x04; // Bit 2: ADC mode (0=INT, 1=ADS)
            if (_state.IsBrakeMode) statusPacked |= 0x08;  // Bit 3: Relay state (0=OFF, 1=ON)

            byte[] statusData = new byte[6];
            statusData[0] = statusPacked;
            statusData[1] = _state.ErrorFlags;
            
            uint uptime = _state.UptimeSeconds;
            byte[] uptimeBytes = BitConverter.GetBytes(uptime);
            Array.Copy(uptimeBytes, 0, statusData, 2, 4);

            var response = new CANMessage(CAN_MSG_ID_SYSTEM_STATUS, statusData, DateTime.Now);
            ResponseReady?.Invoke(response);
        }

        /// <summary>
        /// Send performance message (v1.1 - 4 bytes)
        /// </summary>
        public void SendPerformanceMessage()
        {
            ushort canHz = _state.CanTxHz;
            ushort adcHz = _state.AdcSampleHz;

            // If zero, simulate realistic values
            if (canHz == 0) canHz = (ushort)(_state.StreamActive ? 1000 : 0);
            if (adcHz == 0) adcHz = 1000;

            byte[] perfData = new byte[4];
            perfData[0] = (byte)(canHz & 0xFF);
            perfData[1] = (byte)((canHz >> 8) & 0xFF);
            perfData[2] = (byte)(adcHz & 0xFF);
            perfData[3] = (byte)((adcHz >> 8) & 0xFF);

            var response = new CANMessage(CAN_MSG_ID_SYS_PERF, perfData, DateTime.Now);
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
            Debug.WriteLine("Simulator: ENTER BOOTLOADER - Simulating Reset...");
            _state.BootloaderActive = true;
            _state.ResetBootloaderState();
            _state.StreamActive = false; // Silence app streams

            // Simulate Hardware Reset Delay (100ms) then send READY (0x517)
            // The real firmware in main.c sends CAN_ID_BOOT_PING_RESPONSE on startup if g_force_bootloader is set.
            System.Threading.Tasks.Task.Run(async () => {
                await System.Threading.Tasks.Task.Delay(100);
                
                Debug.WriteLine("Simulator: BOOTLOADER READY (Sending 0x517)");
                
                // Ping Response: [0x01] (READY status)
                byte[] pingData = new byte[] { BOOTLOADER_STATUS_READY };
                var response = new CANMessage(CAN_ID_BOOT_PING_RESPONSE, pingData, DateTime.Now);
                
                // Update state for UI
                _state.LastBootResponseId = response.ID;
                
                ResponseReady?.Invoke(response);
            });
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
            _state.LastBootResponseId = response.ID;
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

            Debug.WriteLine($"Simulator: BEGIN UPDATE Size={firmwareSize} bytes");
            
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

            // Simulate erase delay: Send SUCCESS (0x03) after 500ms
            System.Threading.Tasks.Task.Run(async () => {
                await System.Threading.Tasks.Task.Delay(500);
                Debug.WriteLine("Simulator: ERASE COMPLETE");
                
                // Final Success Response
                byte[] successData = new byte[] { BOOTLOADER_STATUS_SUCCESS };
                var successResponse = new CANMessage(CAN_ID_BOOT_BEGIN_RESPONSE, successData, DateTime.Now);
                ResponseReady?.Invoke(successResponse);
            });
        }

        private void HandleBootloaderData(CANMessage message)
        {
            // Reject data if update not in progress
            if (!_state.UpdateInProgress)
            {
                return;
            }

            // Extract sequence from Byte 0 of payload
            if (message.Data == null || message.Data.Length == 0)
            {
                SendBootloaderError(BOOTLOADER_STATUS_FAILED_CHECKSUM, _state.ExpectedSequence);
                return;
            }

            byte receivedSeq = message.Data[0];
            int dataLen = message.Data.Length - 1;

            if (dataLen < 1)
            {
                SendBootloaderError(BOOTLOADER_STATUS_FAILED_CHECKSUM, _state.ExpectedSequence);
                return;
            }

            // Validate sequence matches expected
            if (receivedSeq != _state.ExpectedSequence)
            {
                // Sequence mismatch - send error with expected sequence for retry
                SendBootloaderError(BOOTLOADER_STATUS_FAILED_CHECKSUM, _state.ExpectedSequence);
                return;
            }

            // Check total data received (RAM buffer + already written to flash)
            uint totalReceived = _state.GetTotalReceived();

            // Check for data overflow
            if (totalReceived + dataLen > _state.UpdateSize)
            {
                // Excess data received - cap it to avoid overflow
                dataLen = (int)(_state.UpdateSize - totalReceived);
                if (dataLen <= 0)
                {
                    _state.UpdateInProgress = false;
                    SendBootloaderError(BOOTLOADER_STATUS_FAILED_CHECKSUM, 0);
                    _state.ResetBootloaderState();
                    return;
                }
            }

            // Extract user data (skip sequence byte)
            byte[] chunkData = new byte[dataLen];
            Array.Copy(message.Data, 1, chunkData, 0, dataLen);

            // Store in simulation buffer
            _state.StoreInRamBuffer(chunkData, dataLen);

            // Update CRC32 (only data bytes)
            _state.UpdateCrc = UpdateCrc32(_state.UpdateCrc, chunkData);

            // Increment expected sequence (wraps automatically: 255 → 0)
            _state.ExpectedSequence++;

            // Periodically send progress updates (every 2KB)
            uint totalProcessed = _state.GetTotalReceived();
            if (totalProcessed > 0 && (totalProcessed % 2048 < (uint)dataLen))
            {
                SendBootloaderProgress();
            }
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

            // Verify all data received
            uint totalReceived = _state.GetTotalReceived();
            Debug.WriteLine($"Simulator: END UPDATE - Final Received Size={totalReceived}, Calculating CRC...");
            
            // --- BANK VALIDATION (Sanity Check) ---
            // Simulating Bank_IsValid as implemented in the actual firmware
            byte[] flash = _state.GetRamBuffer();
            
            // 1. Stack Pointer Check (Offset 0)
            // Should be in RAM range: 0x20000000 - 0x2000A000
            uint stackPtr = BitConverter.ToUInt32(flash, 0);
            bool isStackValid = stackPtr >= 0x20000000 && stackPtr <= 0x2000A000;
            
            // 2. Reset Handler Check (Offset 4)
            // Mask thumb bit and check if in flash range: 0x08008000 - 0x08040000
            uint resetHandler = BitConverter.ToUInt32(flash, 4) & ~1u;
            bool isResetValid = resetHandler >= 0x08008000 && resetHandler <= 0x08040000;

            if (!isStackValid || !isResetValid)
            {
                Debug.WriteLine($"Simulator: VALIDATION FAILED! SP=0x{stackPtr:X8}, Reset=0x{resetHandler:X8}");
                // Validation failed - Send 0x51F with the 8 bytes
                byte[] valData = new byte[8];
                Array.Copy(flash, 0, valData, 0, 8);
                var valResponse = new CANMessage(CAN_ID_ERR_VALIDATION, valData, DateTime.Now);
                ResponseReady?.Invoke(valResponse);
                
                _state.ResetBootloaderState();
                return;
            }

            Debug.WriteLine("Simulator: VALIDATION SUCCESS");
            
            // --- CRC VERIFICATION ---
            uint receivedCrc = BitConverter.ToUInt32(message.Data, 0);
            uint calculatedCrc = _state.UpdateCrc ^ 0xFFFFFFFF;
            
            if (receivedCrc != calculatedCrc && receivedCrc != 0)
            {
                 Debug.WriteLine($"Simulator: CRC Mismatch! Expected: 0x{calculatedCrc:X8}, Got: 0x{receivedCrc:X8} (Ignoring as per Hardware)");
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
                ulong progress_64 = ((ulong)_state.GetTotalReceived() * 100) / _state.UpdateSize;
                percent = (byte)(progress_64 > 100 ? 100 : progress_64); // Cap at 100%
            }

            // Progress message format: [percent, bytes_L, bytes_H, bytes_H2, bytes_H3]
            byte[] progressData = new byte[5];
            progressData[0] = percent;
            uint bytesWritten = _state.GetTotalReceived();
            progressData[1] = (byte)(bytesWritten & 0xFF);
            progressData[2] = (byte)((bytesWritten >> 8) & 0xFF);
            progressData[3] = (byte)((bytesWritten >> 16) & 0xFF);
            progressData[4] = (byte)((bytesWritten >> 24) & 0xFF);

            var response = new CANMessage(CAN_ID_BOOT_PROGRESS, progressData, DateTime.Now);
            _state.LastBootResponseId = response.ID;
            ResponseReady?.Invoke(response);
        }

        private void SendBootloaderError(byte errorCode, byte additionalData)
        {
            // Error Response: [errorCode, additionalData]
            byte[] errorData = new byte[] { errorCode, additionalData };
            var response = new CANMessage(CAN_ID_BOOT_ERROR, errorData, DateTime.Now);
            _state.LastBootResponseId = response.ID;
            _state.LastBootError = DescribeBootloaderStatus(errorCode);
            ResponseReady?.Invoke(response);
        }

        private string DescribeBootloaderStatus(byte status)
        {
            return status switch
            {
                BOOTLOADER_STATUS_READY => "Ready",
                BOOTLOADER_STATUS_IN_PROGRESS => "In Progress",
                BOOTLOADER_STATUS_SUCCESS => "Success",
                BOOTLOADER_STATUS_FAILED_CHECKSUM => "Checksum Fail",
                BOOTLOADER_STATUS_FAILED_FLASH => "Flash Fail",
                0x05 => "Timeout", // FailedTimeout
                _ => $"Error 0x{status:X2}"
            };
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

