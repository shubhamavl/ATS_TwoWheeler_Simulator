using System;
using System.Collections.Generic;
using ATS_TwoWheeler_Simulator.Models;

namespace ATS_TwoWheeler_Simulator.Core
{
    /// <summary>
    /// Error injection types
    /// </summary>
    public enum ErrorInjectionType
    {
        None,
        MessageDrop,      // Randomly drop messages
        DataCorruption,   // Corrupt data bytes
        Timeout,          // Simulate timeout (delay)
        InvalidCANID      // Send with invalid CAN ID
    }

    /// <summary>
    /// Error Injector - Injects errors for testing purposes
    /// </summary>
    public class ErrorInjector
    {
        private bool _enabled = false;
        private ErrorInjectionType _errorType = ErrorInjectionType.None;
        private double _errorRate = 0.01; // 1% error rate
        private readonly Random _random = new Random();
        private readonly object _lock = new object();

        // Per-message-type error control
        private readonly HashSet<uint> _affectedMessageIds = new HashSet<uint>();

        /// <summary>
        /// Check if message should be dropped
        /// </summary>
        public bool ShouldDropMessage(uint messageId)
        {
            lock (_lock)
            {
                if (!_enabled || _errorType != ErrorInjectionType.MessageDrop)
                    return false;

                if (_affectedMessageIds.Count > 0 && !_affectedMessageIds.Contains(messageId))
                    return false;

                return _random.NextDouble() < _errorRate;
            }
        }

        /// <summary>
        /// Inject error into message if needed
        /// </summary>
        public CANMessage? InjectError(CANMessage message)
        {
            lock (_lock)
            {
                if (!_enabled || _errorType == ErrorInjectionType.None)
                    return message;

                if (_affectedMessageIds.Count > 0 && !_affectedMessageIds.Contains(message.ID))
                    return message;

                if (_random.NextDouble() >= _errorRate)
                    return message;

                return _errorType switch
                {
                    ErrorInjectionType.MessageDrop => null, // Drop message
                    ErrorInjectionType.DataCorruption => CorruptData(message),
                    ErrorInjectionType.InvalidCANID => CorruptID(message),
                    ErrorInjectionType.Timeout => message, // Timeout handled separately
                    _ => message
                };
            }
        }

        /// <summary>
        /// Corrupt data bytes
        /// </summary>
        private CANMessage CorruptData(CANMessage message)
        {
            if (message.Data == null || message.Data.Length == 0)
                return message;

            byte[] corrupted = new byte[message.Data.Length];
            Array.Copy(message.Data, corrupted, message.Data.Length);

            // Corrupt random byte
            int corruptIndex = _random.Next(corrupted.Length);
            corrupted[corruptIndex] = (byte)_random.Next(256);

            return new CANMessage(message.ID, corrupted, message.Timestamp, message.Direction);
        }

        /// <summary>
        /// Corrupt CAN ID (make it invalid)
        /// </summary>
        private CANMessage CorruptID(CANMessage message)
        {
            // Set ID to invalid value (> 0x7FF for 11-bit standard)
            uint invalidId = 0x800 + (uint)_random.Next(0x7FF);
            return new CANMessage(invalidId, message.Data ?? new byte[0], message.Timestamp, message.Direction);
        }

        /// <summary>
        /// Get timeout delay if timeout error is active
        /// </summary>
        public int GetTimeoutDelay()
        {
            lock (_lock)
            {
                if (!_enabled || _errorType != ErrorInjectionType.Timeout)
                    return 0;

                if (_random.NextDouble() < _errorRate)
                    return _random.Next(100, 1000); // 100-1000ms delay

                return 0;
            }
        }

        // Properties
        public bool Enabled
        {
            get { lock (_lock) { return _enabled; } }
            set { lock (_lock) { _enabled = value; } }
        }

        public ErrorInjectionType ErrorType
        {
            get { lock (_lock) { return _errorType; } }
            set { lock (_lock) { _errorType = value; } }
        }

        public double ErrorRate
        {
            get { lock (_lock) { return _errorRate; } }
            set { lock (_lock) { _errorRate = Math.Max(0, Math.Min(1, value)); } }
        }

        /// <summary>
        /// Add message ID to affected list
        /// </summary>
        public void AddAffectedMessageId(uint messageId)
        {
            lock (_lock)
            {
                _affectedMessageIds.Add(messageId);
            }
        }

        /// <summary>
        /// Remove message ID from affected list
        /// </summary>
        public void RemoveAffectedMessageId(uint messageId)
        {
            lock (_lock)
            {
                _affectedMessageIds.Remove(messageId);
            }
        }

        /// <summary>
        /// Clear all affected message IDs
        /// </summary>
        public void ClearAffectedMessageIds()
        {
            lock (_lock)
            {
                _affectedMessageIds.Clear();
            }
        }
    }
}

