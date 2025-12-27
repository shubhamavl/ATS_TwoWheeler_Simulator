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
        
        // Legacy fields for migration (deprecated, will be removed in future)
        [System.Obsolete("Use TotalZeroADC_Internal instead")]
        public ushort LeftZeroADC_Internal { get; set; } = 15;
        [System.Obsolete("Use TotalZeroADC_Internal instead")]
        public ushort RightZeroADC_Internal { get; set; } = 15;
        [System.Obsolete("Use TotalSensitivity_Internal instead")]
        public double LeftSensitivity_Internal { get; set; } = 100.0;
        [System.Obsolete("Use TotalSensitivity_Internal instead")]
        public double RightSensitivity_Internal { get; set; } = 100.0;
        [System.Obsolete("Use TotalZeroADC_ADS1115 instead")]
        public int LeftZeroADC_ADS1115 { get; set; } = -15;
        [System.Obsolete("Use TotalZeroADC_ADS1115 instead")]
        public int RightZeroADC_ADS1115 { get; set; } = -15;
        [System.Obsolete("Use TotalSensitivity_ADS1115 instead")]
        public double LeftSensitivity_ADS1115 { get; set; } = 100.0;
        [System.Obsolete("Use TotalSensitivity_ADS1115 instead")]
        public double RightSensitivity_ADS1115 { get; set; } = 100.0;
        [System.Obsolete("Use TotalPattern instead")]
        public string LeftPattern { get; set; } = "Static";
        [System.Obsolete("Use TotalPattern instead")]
        public string RightPattern { get; set; } = "Static";
        [System.Obsolete("Use TotalBaseline instead")]
        public double LeftBaseline { get; set; } = 0.0;
        [System.Obsolete("Use TotalBaseline instead")]
        public double RightBaseline { get; set; } = 0.0;
        [System.Obsolete("Use TotalAmplitude instead")]
        public double LeftAmplitude { get; set; } = 200.0;
        [System.Obsolete("Use TotalAmplitude instead")]
        public double RightAmplitude { get; set; } = 200.0;
        [System.Obsolete("Use TotalFrequency instead")]
        public double LeftFrequency { get; set; } = 2.0;
        [System.Obsolete("Use TotalFrequency instead")]
        public double RightFrequency { get; set; } = 2.0;
        [System.Obsolete("Use TotalDamping instead")]
        public double LeftDamping { get; set; } = 0.2;
        [System.Obsolete("Use TotalDamping instead")]
        public double RightDamping { get; set; } = 0.2;
        [System.Obsolete("Use TotalRampDuration instead")]
        public double LeftRampDuration { get; set; } = 5.0;
        [System.Obsolete("Use TotalRampDuration instead")]
        public double RightRampDuration { get; set; } = 5.0;
        [System.Obsolete("Use TotalStaticWeight instead")]
        public double LeftStaticWeight { get; set; } = 0.0;
        [System.Obsolete("Use TotalStaticWeight instead")]
        public double RightStaticWeight { get; set; } = 0.0;
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

            // Migrate from old Left/Right config if Total properties are not set
            ushort totalZeroInternal = config.TotalZeroADC_Internal;
            double totalSensInternal = config.TotalSensitivity_Internal;
            int totalZeroADS1115 = config.TotalZeroADC_ADS1115;
            double totalSensADS1115 = config.TotalSensitivity_ADS1115;
            
            // Migration: If Total properties are default (0 or 60/-60), try to migrate from Left/Right
            if (totalZeroInternal == 60 && (config.LeftZeroADC_Internal != 15 || config.RightZeroADC_Internal != 15))
            {
                // Migrate from Left/Right: Total = Left + Right (for 2-channel legacy)
                totalZeroInternal = (ushort)(config.LeftZeroADC_Internal + config.RightZeroADC_Internal);
                totalSensInternal = config.LeftSensitivity_Internal + config.RightSensitivity_Internal;
            }
            
            if (totalZeroADS1115 == -60 && (config.LeftZeroADC_ADS1115 != -15 || config.RightZeroADC_ADS1115 != -15))
            {
                // Migrate from Left/Right: Total = Left + Right (for 2-channel legacy)
                totalZeroADS1115 = config.LeftZeroADC_ADS1115 + config.RightZeroADC_ADS1115;
                totalSensADS1115 = config.LeftSensitivity_ADS1115 + config.RightSensitivity_ADS1115;
            }
            
            // Apply total-only values directly
            adcSimulator.TotalZeroADC_Internal = totalZeroInternal;
            adcSimulator.TotalSensitivity_Internal = totalSensInternal;
            adcSimulator.TotalZeroADC_ADS1115 = totalZeroADS1115;
            adcSimulator.TotalSensitivity_ADS1115 = totalSensADS1115;

            noiseGenerator.NoiseLevel = config.NoiseLevel;

            // Pattern: Use Total pattern, migrate from Left if needed
            string patternStr = config.TotalPattern;
            if (string.IsNullOrEmpty(patternStr) || patternStr == "Static")
            {
                // Try to migrate from Left pattern
                if (!string.IsNullOrEmpty(config.LeftPattern))
                    patternStr = config.LeftPattern;
            }
            
            if (Enum.TryParse<WeightPatternType>(patternStr, out var pattern))
                patternGenerator.Pattern = pattern;
            
            // Use Total properties, migrate from Left if needed
            patternGenerator.Baseline = config.TotalBaseline != 0.0 ? config.TotalBaseline : config.LeftBaseline;
            patternGenerator.Amplitude = config.TotalAmplitude != 200.0 ? config.TotalAmplitude : config.LeftAmplitude;
            patternGenerator.Frequency = config.TotalFrequency != 2.0 ? config.TotalFrequency : config.LeftFrequency;
            patternGenerator.Damping = config.TotalDamping != 0.2 ? config.TotalDamping : config.LeftDamping;
            patternGenerator.RampDuration = config.TotalRampDuration != 5.0 ? config.TotalRampDuration : config.LeftRampDuration;
            patternGenerator.StaticWeight = config.TotalStaticWeight != 0.0 ? config.TotalStaticWeight : config.LeftStaticWeight;
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

