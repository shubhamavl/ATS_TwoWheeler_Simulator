using System;
using System.IO;
using System.Text.Json;
using ATS_TwoWheeler_Simulator.Core;

namespace ATS_TwoWheeler_Simulator.Services
{
    /// <summary>
    /// Emulator configuration data model
    /// </summary>
    public class EmulatorConfig
    {
        public byte ADCMode { get; set; } = 1; // Default ADS1115
        
        // Internal (12-bit) mode values
        public ushort LeftZeroADC_Internal { get; set; } = 15;
        public ushort RightZeroADC_Internal { get; set; } = 15;
        public double LeftSensitivity_Internal { get; set; } = 100.0;
        public double RightSensitivity_Internal { get; set; } = 100.0;
        
        // ADS1115 (16-bit) mode values (signed)
        public int LeftZeroADC_ADS1115 { get; set; } = -15; // Signed, direct value
        public int RightZeroADC_ADS1115 { get; set; } = -15;
        public double LeftSensitivity_ADS1115 { get; set; } = 100.0;
        public double RightSensitivity_ADS1115 { get; set; } = 100.0;
        
        // Legacy fields for backward compatibility (will be migrated on load)
        public ushort LeftZeroADC { get; set; } = 15;
        public ushort RightZeroADC { get; set; } = 15;
        public double LeftSensitivity { get; set; } = 100.0;
        public double RightSensitivity { get; set; } = 100.0;
        public double NoiseLevel { get; set; } = 5.0;
        public string LeftPattern { get; set; } = "Static";
        public string RightPattern { get; set; } = "Static";
        public double LeftBaseline { get; set; } = 0.0;
        public double RightBaseline { get; set; } = 0.0;
        public double LeftAmplitude { get; set; } = 200.0;
        public double RightAmplitude { get; set; } = 200.0;
        public double LeftFrequency { get; set; } = 2.0;
        public double RightFrequency { get; set; } = 2.0;
        public double LeftDamping { get; set; } = 0.2;
        public double RightDamping { get; set; } = 0.2;
        public double LeftRampDuration { get; set; } = 5.0;
        public double RightRampDuration { get; set; } = 5.0;
        public double LeftStaticWeight { get; set; } = 0.0;
        public double RightStaticWeight { get; set; } = 0.0;
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

            // Apply mode-specific values (with backward compatibility)
            // Internal mode: use provided value or legacy
            adcSimulator.LeftZeroADC_Internal = config.LeftZeroADC_Internal != 0 ? config.LeftZeroADC_Internal : config.LeftZeroADC;
            adcSimulator.RightZeroADC_Internal = config.RightZeroADC_Internal != 0 ? config.RightZeroADC_Internal : config.RightZeroADC;
            adcSimulator.LeftSensitivity_Internal = config.LeftSensitivity_Internal != 0 ? config.LeftSensitivity_Internal : config.LeftSensitivity;
            adcSimulator.RightSensitivity_Internal = config.RightSensitivity_Internal != 0 ? config.RightSensitivity_Internal : config.RightSensitivity;
            
            // ADS1115 mode: use provided signed value or migrate from legacy
            // Check if new signed format is being used (negative values or non-zero)
            if (config.LeftZeroADC_ADS1115 != 0 || config.LeftZeroADC_ADS1115 < 0)
            {
                // New format: signed value provided directly
                adcSimulator.LeftZeroADC_ADS1115 = config.LeftZeroADC_ADS1115;
                adcSimulator.RightZeroADC_ADS1115 = config.RightZeroADC_ADS1115;
            }
            else
            {
                // Legacy format: migrate from unsigned
                // Old system used 32768 to represent 0, or 2048 to represent -30720
                // New system: use 0 for center, or convert old values
                if (config.LeftZeroADC == 2048)
                {
                    // Old default: convert to new default
                    adcSimulator.LeftZeroADC_ADS1115 = -15;
                    adcSimulator.RightZeroADC_ADS1115 = -15;
                }
                else if (config.LeftZeroADC == 32768)
                {
                    // Old center-zero: convert to 0
                    adcSimulator.LeftZeroADC_ADS1115 = 0;
                    adcSimulator.RightZeroADC_ADS1115 = 0;
                }
                else
                {
                    // Other legacy value: try to convert (assume it was meant to be signed)
                    adcSimulator.LeftZeroADC_ADS1115 = (int)config.LeftZeroADC - 32768;
                    adcSimulator.RightZeroADC_ADS1115 = (int)config.RightZeroADC - 32768;
                }
            }
            adcSimulator.LeftSensitivity_ADS1115 = config.LeftSensitivity_ADS1115 != 0 ? config.LeftSensitivity_ADS1115 : config.LeftSensitivity;
            adcSimulator.RightSensitivity_ADS1115 = config.RightSensitivity_ADS1115 != 0 ? config.RightSensitivity_ADS1115 : config.RightSensitivity;

            noiseGenerator.NoiseLevel = config.NoiseLevel;

            if (Enum.TryParse<WeightPatternType>(config.LeftPattern, out var leftPattern))
                patternGenerator.LeftPattern = leftPattern;
            patternGenerator.LeftBaseline = config.LeftBaseline;
            patternGenerator.LeftAmplitude = config.LeftAmplitude;
            patternGenerator.LeftFrequency = config.LeftFrequency;
            patternGenerator.LeftDamping = config.LeftDamping;
            patternGenerator.LeftRampDuration = config.LeftRampDuration;
            patternGenerator.LeftStaticWeight = config.LeftStaticWeight;

            if (Enum.TryParse<WeightPatternType>(config.RightPattern, out var rightPattern))
                patternGenerator.RightPattern = rightPattern;
            patternGenerator.RightBaseline = config.RightBaseline;
            patternGenerator.RightAmplitude = config.RightAmplitude;
            patternGenerator.RightFrequency = config.RightFrequency;
            patternGenerator.RightDamping = config.RightDamping;
            patternGenerator.RightRampDuration = config.RightRampDuration;
            patternGenerator.RightStaticWeight = config.RightStaticWeight;
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
                // Mode-specific values
                LeftZeroADC_Internal = adcSimulator.LeftZeroADC_Internal,
                RightZeroADC_Internal = adcSimulator.RightZeroADC_Internal,
                LeftSensitivity_Internal = adcSimulator.LeftSensitivity_Internal,
                RightSensitivity_Internal = adcSimulator.RightSensitivity_Internal,
                LeftZeroADC_ADS1115 = adcSimulator.LeftZeroADC_ADS1115,
                RightZeroADC_ADS1115 = adcSimulator.RightZeroADC_ADS1115,
                LeftSensitivity_ADS1115 = adcSimulator.LeftSensitivity_ADS1115,
                RightSensitivity_ADS1115 = adcSimulator.RightSensitivity_ADS1115,
                // Legacy values (for backward compatibility)
                LeftZeroADC = adcSimulator.LeftZeroADC,
                RightZeroADC = adcSimulator.RightZeroADC,
                LeftSensitivity = adcSimulator.LeftSensitivity,
                RightSensitivity = adcSimulator.RightSensitivity,
                NoiseLevel = noiseGenerator.NoiseLevel,
                LeftPattern = patternGenerator.LeftPattern.ToString(),
                RightPattern = patternGenerator.RightPattern.ToString(),
                LeftBaseline = patternGenerator.LeftBaseline,
                RightBaseline = patternGenerator.RightBaseline,
                LeftAmplitude = patternGenerator.LeftAmplitude,
                RightAmplitude = patternGenerator.RightAmplitude,
                LeftFrequency = patternGenerator.LeftFrequency,
                RightFrequency = patternGenerator.RightFrequency,
                LeftDamping = patternGenerator.LeftDamping,
                RightDamping = patternGenerator.RightDamping,
                LeftRampDuration = patternGenerator.LeftRampDuration,
                RightRampDuration = patternGenerator.RightRampDuration,
                LeftStaticWeight = patternGenerator.LeftStaticWeight,
                RightStaticWeight = patternGenerator.RightStaticWeight
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

