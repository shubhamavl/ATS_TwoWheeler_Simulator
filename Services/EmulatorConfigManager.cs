using System;
using System.IO;
using System.Text.Json;
using ATS_TwoWheeler_Simulator.Core;

namespace ATS_TwoWheeler_Simulator.Services
{
    /// <summary>
    /// Emulator configuration data model - Total weight system (no left/right, no per-channel)
    /// </summary>
    public class EmulatorConfig
    {
        public byte ADCMode { get; set; } = 1; // Default ADS1115
        
        // Internal (12-bit) mode values - Total only
        public ushort TotalZeroADC_Internal { get; set; } = 60; // 4 channels × 15
        public double TotalSensitivity_Internal { get; set; } = 100.0; // 4 channels × 25.0
        
        // ADS1115 (16-bit) mode values (signed) - Total only
        public int TotalZeroADC_ADS1115 { get; set; } = -60; // 4 channels × -15
        public double TotalSensitivity_ADS1115 { get; set; } = 100.0; // 4 channels × 25.0
        
        // Total weight pattern (single pattern, not left/right)
        public double NoiseLevel { get; set; } = 5.0;
        public string TotalPattern { get; set; } = "Static";
        public double TotalBaseline { get; set; } = 0.0;
        public double TotalAmplitude { get; set; } = 200.0;
        public double TotalFrequency { get; set; } = 2.0;
        public double TotalDamping { get; set; } = 0.2;
        public double TotalRampDuration { get; set; } = 5.0;
        public double TotalStaticWeight { get; set; } = 0.0;
        
        public FirmwareVersionConfig FirmwareVersion { get; set; } = new FirmwareVersionConfig();
    }

    /// <summary>
    /// Firmware version configuration
    /// </summary>
    public class FirmwareVersionConfig
    {
        public byte Major { get; set; } = 0;
        public byte Minor { get; set; } = 1;
        public byte Patch { get; set; } = 0;
        public byte Build { get; set; } = 0;
    }

    /// <summary>
    /// Emulator Configuration Manager - Handles save/load of emulator settings
    /// </summary>
    public class EmulatorConfigManager
    {
        private readonly string _configFilePath;

        public EmulatorConfigManager()
        {
            string appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ATS_TwoWheeler_Simulator");
            Directory.CreateDirectory(appDataDir);
            _configFilePath = Path.Combine(appDataDir, "emulator_config.json");
        }

        /// <summary>
        /// Load configuration from file
        /// </summary>
        public EmulatorConfig LoadConfig()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string json = File.ReadAllText(_configFilePath);
                    return JsonSerializer.Deserialize<EmulatorConfig>(json) ?? new EmulatorConfig();
                }
            }
            catch (Exception)
            {
                // Return default config on error
            }
            return new EmulatorConfig();
        }

        /// <summary>
        /// Save configuration to file
        /// </summary>
        public void SaveConfig(EmulatorConfig config)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception)
            {
                // Ignore save errors
            }
        }

        /// <summary>
        /// Apply configuration to emulator components
        /// </summary>
        public void ApplyConfig(EmulatorConfig config, STM32State state, ADCSimulator adcSimulator,
                               WeightPatternGenerator patternGenerator, NoiseGenerator noiseGenerator)
        {
            state.ADCMode = config.ADCMode;
            adcSimulator.CurrentMode = config.ADCMode;
            state.FirmwareVersion = (config.FirmwareVersion.Major, config.FirmwareVersion.Minor,
                                    config.FirmwareVersion.Patch, config.FirmwareVersion.Build);

            // Apply total-only values directly
            adcSimulator.TotalZeroADC_Internal = config.TotalZeroADC_Internal;
            adcSimulator.TotalSensitivity_Internal = config.TotalSensitivity_Internal;
            adcSimulator.TotalZeroADC_ADS1115 = config.TotalZeroADC_ADS1115;
            adcSimulator.TotalSensitivity_ADS1115 = config.TotalSensitivity_ADS1115;

            noiseGenerator.NoiseLevel = config.NoiseLevel;

            // Pattern: Use Total pattern
            string patternStr = config.TotalPattern;
            if (Enum.TryParse<WeightPatternType>(patternStr, out var pattern))
                patternGenerator.Pattern = pattern;
            
            // Use Total properties
            patternGenerator.Baseline = config.TotalBaseline;
            patternGenerator.Amplitude = config.TotalAmplitude;
            patternGenerator.Frequency = config.TotalFrequency;
            patternGenerator.Damping = config.TotalDamping;
            patternGenerator.RampDuration = config.TotalRampDuration;
            patternGenerator.StaticWeight = config.TotalStaticWeight;
        }
        /// <summary>
        /// Export configuration from emulator components
        /// </summary>
        public EmulatorConfig ExportConfig(STM32State state, ADCSimulator adcSimulator,
                                           WeightPatternGenerator patternGenerator, NoiseGenerator noiseGenerator)
        {
            var config = new EmulatorConfig
            {
                ADCMode = state.ADCMode,
                // Export total-only values
                TotalZeroADC_Internal = adcSimulator.TotalZeroADC_Internal,
                TotalSensitivity_Internal = adcSimulator.TotalSensitivity_Internal,
                TotalZeroADC_ADS1115 = adcSimulator.TotalZeroADC_ADS1115,
                TotalSensitivity_ADS1115 = adcSimulator.TotalSensitivity_ADS1115,
                NoiseLevel = noiseGenerator.NoiseLevel,
                // Export total pattern
                TotalPattern = patternGenerator.Pattern.ToString(),
                TotalBaseline = patternGenerator.Baseline,
                TotalAmplitude = patternGenerator.Amplitude,
                TotalFrequency = patternGenerator.Frequency,
                TotalDamping = patternGenerator.Damping,
                TotalRampDuration = patternGenerator.RampDuration,
                TotalStaticWeight = patternGenerator.StaticWeight
            };

            var (major, minor, patch, build) = state.FirmwareVersion;
            config.FirmwareVersion = new FirmwareVersionConfig
            {
                Major = major,
                Minor = minor,
                Patch = patch,
                Build = build
            };

            return config;
        }
    }
}

