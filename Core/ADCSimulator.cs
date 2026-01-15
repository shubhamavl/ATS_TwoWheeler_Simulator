using System;

namespace ATS_TwoWheeler_Simulator.Core
{
    /// <summary>
    /// ADC Simulator - Generates total ADC values from total weight (simplified for WPF testing)
    /// Supports both Internal (12-bit) and ADS1115 (16-bit) modes
    /// Direct total calculation - no per-channel simulation needed for WPF
    /// </summary>
    public class ADCSimulator
    {
        // Total configuration - Internal (12-bit) mode
        private ushort _totalZeroADC_Internal = 60; // 4 channels × 15
        private double _totalSensitivity_Internal = 100.0; // 4 channels × 25.0
        private ushort _brakeZeroADC_Internal = 15; // Single channel
        private double _brakeSensitivity_Internal = 25.0; // Single channel

        // Total configuration - ADS1115 (16-bit) mode (signed)
        private int _totalZeroADC_ADS1115 = -60; // 4 channels × -15
        private double _totalSensitivity_ADS1115 = 100.0; // 4 channels × 25.0
        private int _brakeZeroADC_ADS1115 = -15; // Single channel
        private double _brakeSensitivity_ADS1115 = 25.0; // Single channel

        // Current mode
        private byte _currentMode = 1; // Default ADS1115

        // Thread safety
        private readonly object _lock = new object();

        /// <summary>
        /// Calculate total ADC value from total weight (Internal mode)
        /// Direct total calculation - no per-channel simulation needed
        /// </summary>
        public ushort CalculateTotalADC(double totalWeightKg, byte adcMode, bool isBrakeMode = false)
        {
            lock (_lock)
            {
                if (adcMode != 0)
                    throw new ArgumentException("CalculateTotalADC is for Internal mode only. Use CalculateTotalADCSigned for ADS1115.");

                double totalADC;
                if (isBrakeMode)
                {
                    // Brake Calculation (Single Channel logic)
                    totalADC = _brakeZeroADC_Internal + (totalWeightKg * _brakeSensitivity_Internal);
                }
                else
                {
                    // Weight Calculation (Total Sum logic)
                    totalADC = _totalZeroADC_Internal + (totalWeightKg * _totalSensitivity_Internal);
                }

                // Clamp to valid range (0-16380 for 4 channels × 4095, or 0-4095 for single)
                if (totalADC < 0) totalADC = 0;
                if (totalADC > 16380) totalADC = 16380;

                return (ushort)totalADC;
            }
        }

        /// <summary>
        /// Calculate total signed ADC value from total weight (ADS1115 mode)
        /// Direct total calculation - no per-channel simulation needed
        /// </summary>
        public int CalculateTotalADCSigned(double totalWeightKg, bool isBrakeMode = false)
        {
            lock (_lock)
            {
                double totalADC;
                if (isBrakeMode)
                {
                    // Brake Calculation (Single Channel logic)
                    totalADC = _brakeZeroADC_ADS1115 + (totalWeightKg * _brakeSensitivity_ADS1115);
                }
                else
                {
                    // Weight Calculation (Total Sum logic)
                    totalADC = _totalZeroADC_ADS1115 + (totalWeightKg * _totalSensitivity_ADS1115);
                }

                // Clamp to signed 32-bit range (for 4 channels: -131072 to +131068)
                // Single channel range is smaller (-32768 to +32767) but fits in int32
                if (totalADC < -131072) totalADC = -131072;
                if (totalADC > 131068) totalADC = 131068;

                return (int)totalADC;
            }
        }

        // Current mode property
        public byte CurrentMode
        {
            get { lock (_lock) { return _currentMode; } }
            set { lock (_lock) { _currentMode = value; } }
        }

        // Configuration properties - Internal (12-bit) mode - Total only
        public ushort TotalZeroADC_Internal
        {
            get { lock (_lock) { return _totalZeroADC_Internal; } }
            set { lock (_lock) { _totalZeroADC_Internal = value; } }
        }

        public double TotalSensitivity_Internal
        {
            get { lock (_lock) { return _totalSensitivity_Internal; } }
            set { lock (_lock) { _totalSensitivity_Internal = value; } }
        }

        // Configuration properties - ADS1115 (16-bit) mode (signed) - Total only
        public int TotalZeroADC_ADS1115
        {
            get { lock (_lock) { return _totalZeroADC_ADS1115; } }
            set { lock (_lock) { _totalZeroADC_ADS1115 = value; } }
        }

        public double TotalSensitivity_ADS1115
        {
            get { lock (_lock) { return _totalSensitivity_ADS1115; } }
            set { lock (_lock) { _totalSensitivity_ADS1115 = value; } }
        }
    }
}

