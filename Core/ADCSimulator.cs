using System;

namespace ATS_TwoWheeler_Simulator.Core
{
    /// <summary>
    /// ADC Simulator - Generates ADC values from weight values for total weight (all 4 channels summed)
    /// Supports both Internal (12-bit) and ADS1115 (16-bit) modes
    /// </summary>
    public class ADCSimulator
    {
        // Channel configuration - Internal (12-bit) mode
        // Each channel has its own zero and sensitivity, then all are summed
        private ushort _ch0ZeroADC_Internal = 15;
        private ushort _ch1ZeroADC_Internal = 15;
        private ushort _ch2ZeroADC_Internal = 15;
        private ushort _ch3ZeroADC_Internal = 15;
        private double _ch0Sensitivity_Internal = 25.0; // ADC counts per kg per channel
        private double _ch1Sensitivity_Internal = 25.0;
        private double _ch2Sensitivity_Internal = 25.0;
        private double _ch3Sensitivity_Internal = 25.0;

        // Channel configuration - ADS1115 (16-bit) mode (signed)
        private int _ch0ZeroADC_ADS1115 = -15;
        private int _ch1ZeroADC_ADS1115 = -15;
        private int _ch2ZeroADC_ADS1115 = -15;
        private int _ch3ZeroADC_ADS1115 = -15;
        private double _ch0Sensitivity_ADS1115 = 25.0; // ADC counts per kg per channel
        private double _ch1Sensitivity_ADS1115 = 25.0;
        private double _ch2Sensitivity_ADS1115 = 25.0;
        private double _ch3Sensitivity_ADS1115 = 25.0;

        // Current mode
        private byte _currentMode = 1; // Default ADS1115

        // Thread safety
        private readonly object _lock = new object();

        /// <summary>
        /// Calculate total ADC value from total weight (Internal mode)
        /// Simulates all 4 channels and sums them
        /// </summary>
        public ushort CalculateTotalADC(double totalWeightKg, byte adcMode)
        {
            lock (_lock)
            {
                if (adcMode != 0)
                    throw new ArgumentException("CalculateTotalADC is for Internal mode only. Use CalculateTotalADCSigned for ADS1115.");

                // Distribute weight across 4 channels (equal distribution for simplicity)
                double weightPerChannel = totalWeightKg / 4.0;

                // Calculate ADC for each channel
                double ch0ADC = _ch0ZeroADC_Internal + (weightPerChannel * _ch0Sensitivity_Internal);
                double ch1ADC = _ch1ZeroADC_Internal + (weightPerChannel * _ch1Sensitivity_Internal);
                double ch2ADC = _ch2ZeroADC_Internal + (weightPerChannel * _ch2Sensitivity_Internal);
                double ch3ADC = _ch3ZeroADC_Internal + (weightPerChannel * _ch3Sensitivity_Internal);

                // Sum all channels (total weight = Ch0+Ch1+Ch2+Ch3)
                double totalADC = ch0ADC + ch1ADC + ch2ADC + ch3ADC;

                // Clamp to valid range (0-16380 for 4 channels × 4095)
                if (totalADC < 0) totalADC = 0;
                if (totalADC > 16380) totalADC = 16380;

                return (ushort)totalADC;
            }
        }

        /// <summary>
        /// Calculate total signed ADC value from total weight (ADS1115 mode)
        /// Simulates all 4 channels and sums them
        /// </summary>
        public int CalculateTotalADCSigned(double totalWeightKg)
        {
            lock (_lock)
            {
                // Distribute weight across 4 channels (equal distribution for simplicity)
                double weightPerChannel = totalWeightKg / 4.0;

                // Calculate ADC for each channel
                double ch0ADC = _ch0ZeroADC_ADS1115 + (weightPerChannel * _ch0Sensitivity_ADS1115);
                double ch1ADC = _ch1ZeroADC_ADS1115 + (weightPerChannel * _ch1Sensitivity_ADS1115);
                double ch2ADC = _ch2ZeroADC_ADS1115 + (weightPerChannel * _ch2Sensitivity_ADS1115);
                double ch3ADC = _ch3ZeroADC_ADS1115 + (weightPerChannel * _ch3Sensitivity_ADS1115);

                // Sum all channels (total weight = Ch0+Ch1+Ch2+Ch3)
                double totalADC = ch0ADC + ch1ADC + ch2ADC + ch3ADC;

                // Clamp to signed 32-bit range (for 4 channels: -131072 to +131068)
                // Each channel: -32768 to +32767, so total: -131072 to +131068
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

        // Configuration properties - Internal (12-bit) mode
        public ushort Ch0ZeroADC_Internal
        {
            get { lock (_lock) { return _ch0ZeroADC_Internal; } }
            set { lock (_lock) { _ch0ZeroADC_Internal = value; } }
        }

        public ushort Ch1ZeroADC_Internal
        {
            get { lock (_lock) { return _ch1ZeroADC_Internal; } }
            set { lock (_lock) { _ch1ZeroADC_Internal = value; } }
        }

        public ushort Ch2ZeroADC_Internal
        {
            get { lock (_lock) { return _ch2ZeroADC_Internal; } }
            set { lock (_lock) { _ch2ZeroADC_Internal = value; } }
        }

        public ushort Ch3ZeroADC_Internal
        {
            get { lock (_lock) { return _ch3ZeroADC_Internal; } }
            set { lock (_lock) { _ch3ZeroADC_Internal = value; } }
        }

        public double Ch0Sensitivity_Internal
        {
            get { lock (_lock) { return _ch0Sensitivity_Internal; } }
            set { lock (_lock) { _ch0Sensitivity_Internal = value; } }
        }

        public double Ch1Sensitivity_Internal
        {
            get { lock (_lock) { return _ch1Sensitivity_Internal; } }
            set { lock (_lock) { _ch1Sensitivity_Internal = value; } }
        }

        public double Ch2Sensitivity_Internal
        {
            get { lock (_lock) { return _ch2Sensitivity_Internal; } }
            set { lock (_lock) { _ch2Sensitivity_Internal = value; } }
        }

        public double Ch3Sensitivity_Internal
        {
            get { lock (_lock) { return _ch3Sensitivity_Internal; } }
            set { lock (_lock) { _ch3Sensitivity_Internal = value; } }
        }

        // Configuration properties - ADS1115 (16-bit) mode (signed)
        public int Ch0ZeroADC_ADS1115
        {
            get { lock (_lock) { return _ch0ZeroADC_ADS1115; } }
            set { lock (_lock) { _ch0ZeroADC_ADS1115 = value; } }
        }

        public int Ch1ZeroADC_ADS1115
        {
            get { lock (_lock) { return _ch1ZeroADC_ADS1115; } }
            set { lock (_lock) { _ch1ZeroADC_ADS1115 = value; } }
        }

        public int Ch2ZeroADC_ADS1115
        {
            get { lock (_lock) { return _ch2ZeroADC_ADS1115; } }
            set { lock (_lock) { _ch2ZeroADC_ADS1115 = value; } }
        }

        public int Ch3ZeroADC_ADS1115
        {
            get { lock (_lock) { return _ch3ZeroADC_ADS1115; } }
            set { lock (_lock) { _ch3ZeroADC_ADS1115 = value; } }
        }

        public double Ch0Sensitivity_ADS1115
        {
            get { lock (_lock) { return _ch0Sensitivity_ADS1115; } }
            set { lock (_lock) { _ch0Sensitivity_ADS1115 = value; } }
        }

        public double Ch1Sensitivity_ADS1115
        {
            get { lock (_lock) { return _ch1Sensitivity_ADS1115; } }
            set { lock (_lock) { _ch1Sensitivity_ADS1115 = value; } }
        }

        public double Ch2Sensitivity_ADS1115
        {
            get { lock (_lock) { return _ch2Sensitivity_ADS1115; } }
            set { lock (_lock) { _ch2Sensitivity_ADS1115 = value; } }
        }

        public double Ch3Sensitivity_ADS1115
        {
            get { lock (_lock) { return _ch3Sensitivity_ADS1115; } }
            set { lock (_lock) { _ch3Sensitivity_ADS1115 = value; } }
        }
    }
}

