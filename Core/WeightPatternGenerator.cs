using System;

namespace ATS_TwoWheeler_Simulator.Core
{
    /// <summary>
    /// Weight pattern types
    /// </summary>
    public enum WeightPatternType
    {
        Static,      // Constant weight
        Sine,        // Oscillating sine wave
        Step,        // Sudden weight change with settling
        Ramp         // Linear increase/decrease
    }

    /// <summary>
    /// Weight Pattern Generator - Generates total weight values based on patterns
    /// </summary>
    public class WeightPatternGenerator
    {
        // Total weight pattern (single pattern, not left/right)
        private WeightPatternType _pattern = WeightPatternType.Static;
        private double _baseline = 0.0;
        private double _amplitude = 200.0;
        private double _frequency = 2.0; // Hz
        private double _damping = 0.2;
        private double _rampDuration = 5.0; // seconds
        private DateTime _patternStartTime = DateTime.Now;

        // Static weight (for Static pattern)
        private double _staticWeight = 0.0;

        // Thread safety
        private readonly object _lock = new object();

        /// <summary>
        /// Calculate current total weight based on pattern
        /// </summary>
        public double CalculateTotalWeight()
        {
            lock (_lock)
            {
                double t = (DateTime.Now - _patternStartTime).TotalSeconds;

                return _pattern switch
                {
                    WeightPatternType.Static => _staticWeight,
                    WeightPatternType.Sine => _baseline + _amplitude * Math.Sin(2 * Math.PI * _frequency * t),
                    WeightPatternType.Step => _baseline + _amplitude * (1 - Math.Exp(-_damping * t)),
                    WeightPatternType.Ramp => _baseline + (_amplitude * t / Math.Max(0.1, _rampDuration)),
                    _ => _baseline
                };
            }
        }

        /// <summary>
        /// Reset pattern start time
        /// </summary>
        public void ResetPattern()
        {
            lock (_lock)
            {
                _patternStartTime = DateTime.Now;
            }
        }

        // Pattern properties
        public WeightPatternType Pattern
        {
            get { lock (_lock) { return _pattern; } }
            set { lock (_lock) { _pattern = value; _patternStartTime = DateTime.Now; } }
        }

        public double Baseline
        {
            get { lock (_lock) { return _baseline; } }
            set { lock (_lock) { _baseline = value; } }
        }

        public double Amplitude
        {
            get { lock (_lock) { return _amplitude; } }
            set { lock (_lock) { _amplitude = value; } }
        }

        public double Frequency
        {
            get { lock (_lock) { return _frequency; } }
            set { lock (_lock) { _frequency = value; } }
        }

        public double Damping
        {
            get { lock (_lock) { return _damping; } }
            set { lock (_lock) { _damping = value; } }
        }

        public double RampDuration
        {
            get { lock (_lock) { return _rampDuration; } }
            set { lock (_lock) { _rampDuration = value; } }
        }

        public double StaticWeight
        {
            get { lock (_lock) { return _staticWeight; } }
            set { lock (_lock) { _staticWeight = value; } }
        }
    }
}

