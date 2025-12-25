using System;

namespace ATS_TwoWheeler_Simulator.Models
{
    /// <summary>
    /// CAN Message data structure
    /// </summary>
    public class CANMessage
    {
        public uint ID { get; set; }
        public byte[] Data { get; set; }
        public DateTime Timestamp { get; set; }
        public string Direction { get; set; } = "RX";
        public int Length => Data?.Length ?? 0;

        public CANMessage()
        {
            ID = 0;
            Data = new byte[0];
            Timestamp = DateTime.Now;
            Direction = "RX";
        }

        public CANMessage(uint id, byte[] data, string direction = "RX")
        {
            ID = id;
            Data = data ?? new byte[0];
            Timestamp = DateTime.Now;
            Direction = direction;
        }

        public CANMessage(uint id, byte[] data, DateTime timestamp, string direction = "RX")
        {
            ID = id;
            Data = data ?? new byte[0];
            Timestamp = timestamp;
            Direction = direction;
        }

        /// <summary>
        /// Get hex string representation of data
        /// </summary>
        public string GetDataHexString()
        {
            if (Data == null || Data.Length == 0)
                return "No Data";

            return BitConverter.ToString(Data).Replace("-", " ");
        }

        /// <summary>
        /// Get ID as hex string
        /// </summary>
        public string GetIDHexString()
        {
            return $"0x{ID:X3}";
        }

        /// <summary>
        /// Get protocol description for semantic IDs (Protocol v0.1)
        /// </summary>
        public string GetProtocolDescription()
        {
            return ID switch
            {
                0x200 => "TOTAL_RAW_DATA",
                0x040 => "START_STREAM",
                0x044 => "STOP_ALL_STREAMS",
                0x300 => "SYSTEM_STATUS",
                0x301 => "VERSION_RESPONSE",
                0x032 => "STATUS_REQUEST",
                0x033 => "VERSION_REQUEST",
                0x030 => "MODE_INTERNAL",
                0x031 => "MODE_ADS1115",
                _ => $"UNKNOWN_0x{ID:X3}"
            };
        }
    }
}

