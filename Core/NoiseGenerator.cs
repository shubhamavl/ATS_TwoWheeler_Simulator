using System;

namespace ATS_TwoWheeler_Simulator.Core
{
    /// <summary>
    /// Noise Generator - Adds Gaussian noise to ADC values
    /// </summary>
    public class NoiseGenerator
    {
        private double _noiseLevel = 5.0; // ±ADC counts
        private readonly Random _random = new Random();
        private readonly object _lock = new object();

        /// <summary>
        /// Add noise to an ADC value (for total weight, noise is applied to sum)
        /// </summary>
        public ushort AddNoise(ushort adcValue, byte adcMode)
        {
            lock (_lock)
            {
                // Generate Gaussian noise using Box-Muller transform
                double u1 = _random.NextDouble();
                double u2 = _random.NextDouble();
                double z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                double noise = z0 * _noiseLevel;

                // Clamp noise to ±noiseLevel to respect the specified range
                if (noise > _noiseLevel) noise = _noiseLevel;
                if (noise < -_noiseLevel) noise = -_noiseLevel;

                double noisyValue = adcValue + noise;

                // Clamp to valid range (0-16380 for 4 channels × 4095)
                if (noisyValue < 0) noisyValue = 0;
                if (noisyValue > 16380) noisyValue = 16380;

                return (ushort)noisyValue;
            }
        }

        /// <summary>
        /// Add noise to a signed ADC value (for ADS1115 total weight)
        /// </summary>
        public int AddNoiseSigned(int adcValue)
        {
            lock (_lock)
            {
                // Generate Gaussian noise
                double u1 = _random.NextDouble();
                double u2 = _random.NextDouble();
                double z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                double noise = z0 * _noiseLevel;

                // Clamp noise to ±noiseLevel to respect the specified range
                if (noise > _noiseLevel) noise = _noiseLevel;
                if (noise < -_noiseLevel) noise = -_noiseLevel;

                double noisyValue = adcValue + noise;

                // Clamp to signed range for 4 channels (-131072 to +131068)
                if (noisyValue < -131072) noisyValue = -131072;
                if (noisyValue > 131068) noisyValue = 131068;

                return (int)noisyValue;
            }
        }

        public double NoiseLevel
        {
            get { lock (_lock) { return _noiseLevel; } }
            set { lock (_lock) { _noiseLevel = Math.Max(0, value); } }
        }
    }
}

