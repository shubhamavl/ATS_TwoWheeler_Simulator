using System;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ATS_TwoWheeler_Simulator.Adapters;
using ATS_TwoWheeler_Simulator.Core;
using ATS_TwoWheeler_Simulator.Models;
using ATS_TwoWheeler_Simulator.Services;

namespace ATS_TwoWheeler_Simulator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Core components
        private STM32State? _state;
        private ADCSimulator? _adcSimulator;
        private WeightPatternGenerator? _patternGenerator;
        private NoiseGenerator? _noiseGenerator;
        private ProtocolHandler? _protocolHandler;
        private StreamManager? _streamManager;
        private ErrorInjector? _errorInjector;

        // Services
        private ICanAdapter? _adapter;
        private EmulatorConfigManager? _configManager;
        private UpdateService? _updateService;

        // UI update timer
        private DispatcherTimer? _uiUpdateTimer;
        private long _txMessageCount = 0;
        private long _rxMessageCount = 0;

        public MainWindow()
        {
            InitializeComponent();
            InitializeComponents();
            LoadAvailableComPorts();
            LoadConfiguration();
            InitializeVersionDisplay();
            StartUIUpdateTimer();
            // Debug controls are initialized via LoadConfiguration() -> UpdateSystemStatus() -> UpdateDebugControls()
        }

        private void InitializeComponents()
        {
            // Initialize core components
            _state = new STM32State();
            _adcSimulator = new ADCSimulator();
            _adcSimulator.CurrentMode = _state.ADCMode; // Sync mode to state
            _patternGenerator = new WeightPatternGenerator();
            _noiseGenerator = new NoiseGenerator();
            _protocolHandler = new ProtocolHandler(_state, _adcSimulator, _patternGenerator, _noiseGenerator);
            _errorInjector = new ErrorInjector();

            // Initialize config manager
            _configManager = new EmulatorConfigManager();

            // Initialize update service
            _updateService = new UpdateService();

            // Setup protocol handler response event
            _protocolHandler.ResponseReady += ProtocolHandler_ResponseReady;
        }

        private void InitializeVersionDisplay()
        {
            try
            {
                var versionInfo = VersionInfo.Instance;
                if (VersionText != null)
                {
                    VersionText.Text = $"v{versionInfo.Version}";
                    
                    // Update tooltip if it exists
                    if (VersionText.ToolTip is TextBlock tooltip)
                    {
                        var run = tooltip.Inlines.OfType<System.Windows.Documents.Run>()
                            .FirstOrDefault(r => r.Name == "VersionTooltipText");
                        if (run != null)
                        {
                            run.Text = versionInfo.DisplayVersion;
                        }
                    }
                }
            }
            catch
            {
                // Silently fail - version display is optional
            }
        }

        private void LoadAvailableComPorts()
        {
            if (ComPortCombo == null) return;
            
            ComPortCombo.Items.Clear();
            foreach (string port in SerialPort.GetPortNames().OrderBy(p => p))
            {
                ComPortCombo.Items.Add(port);
            }
            if (ComPortCombo.Items.Count > 0)
            {
                ComPortCombo.SelectedIndex = 0;
            }
        }

        private void StartUIUpdateTimer()
        {
            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // Update 10 times per second
            };
            _uiUpdateTimer.Tick += UIUpdateTimer_Tick;
            _uiUpdateTimer.Start();
        }

        private void UIUpdateTimer_Tick(object? sender, EventArgs e)
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_state == null || _adcSimulator == null || _patternGenerator == null) return;

            // Update status indicators
            UpdateConnectionStatus();
            UpdateADCValues();
            UpdateSystemStatus();
        }

        private void UpdateConnectionStatus()
        {
            bool adapterConnected = _adapter?.IsConnected ?? false;

            ConnectionStatusIndicator.Fill = new SolidColorBrush(adapterConnected ? Colors.Green : Colors.Red);
            ConnectionStatusText.Text = adapterConnected ? "CAN: Connected" : "CAN: Disconnected";
        }

        private void UpdateADCValues()
        {
            if (_state == null || _adcSimulator == null || _patternGenerator == null) return;

            double totalWeight = _patternGenerator.CalculateTotalWeight();

            byte adcMode = _state.ADCMode;
            if (adcMode == 0) // Internal
            {
                ushort totalADC = _adcSimulator.CalculateTotalADC(totalWeight, adcMode);
                TotalCurrentADCText.Text = totalADC.ToString();
            }
            else // ADS1115
            {
                int totalADC = _adcSimulator.CalculateTotalADCSigned(totalWeight);
                TotalCurrentADCText.Text = totalADC.ToString();
            }
        }

        private void UpdateSystemStatus()
        {
            if (_state == null) return;

            StatusADCModeText.Text = _state.ADCMode == 0 ? "Internal (12-bit)" : "ADS1115 (16-bit)";
            StatusStreamText.Text = _state.StreamActive ? $"Active ({GetRateText(_state.StreamRate)})" : "Stopped";
            StatusStreamText.Foreground = new SolidColorBrush(_state.StreamActive ? Colors.Green : Colors.Red);

            var (major, minor, patch, build) = _state.FirmwareVersion;
            StatusFirmwareText.Text = $"{major}.{minor}.{patch}.{build}";
            StatusTxCountText.Text = _txMessageCount.ToString();
            StatusRxCountText.Text = _rxMessageCount.ToString();

            // Update debug UI controls
            UpdateDebugControls();
        }

        private void UpdateDebugControls()
        {
            if (_state == null) return;

            // Update System Status combo (without triggering event)
            if (SystemStatusCombo != null)
            {
                SystemStatusCombo.SelectionChanged -= SystemStatusCombo_SelectionChanged;
                foreach (System.Windows.Controls.ComboBoxItem item in SystemStatusCombo.Items)
                {
                    if (item.Tag?.ToString() == _state.SystemStatus.ToString())
                    {
                        SystemStatusCombo.SelectedItem = item;
                        break;
                    }
                }
                SystemStatusCombo.SelectionChanged += SystemStatusCombo_SelectionChanged;
            }

            // Update Error Flags
            if (ErrorFlagsTextBox != null)
            {
                ErrorFlagsTextBox.TextChanged -= ErrorFlagsTextBox_TextChanged;
                ErrorFlagsTextBox.Text = $"0x{_state.ErrorFlags:X2}";
                ErrorFlagsTextBox.TextChanged += ErrorFlagsTextBox_TextChanged;
            }

            // Update Firmware Version
            var (major, minor, patch, build) = _state.FirmwareVersion;
            if (FirmwareMajorTextBox != null)
            {
                FirmwareMajorTextBox.TextChanged -= FirmwareVersion_TextChanged;
                FirmwareMajorTextBox.Text = major.ToString();
                FirmwareMajorTextBox.TextChanged += FirmwareVersion_TextChanged;
            }
            if (FirmwareMinorTextBox != null)
            {
                FirmwareMinorTextBox.TextChanged -= FirmwareVersion_TextChanged;
                FirmwareMinorTextBox.Text = minor.ToString();
                FirmwareMinorTextBox.TextChanged += FirmwareVersion_TextChanged;
            }
            if (FirmwarePatchTextBox != null)
            {
                FirmwarePatchTextBox.TextChanged -= FirmwareVersion_TextChanged;
                FirmwarePatchTextBox.Text = patch.ToString();
                FirmwarePatchTextBox.TextChanged += FirmwareVersion_TextChanged;
            }
            if (FirmwareBuildTextBox != null)
            {
                FirmwareBuildTextBox.TextChanged -= FirmwareVersion_TextChanged;
                FirmwareBuildTextBox.Text = build.ToString();
                FirmwareBuildTextBox.TextChanged += FirmwareVersion_TextChanged;
            }
        }

        // Debug control event handlers
        private void SystemStatusCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_state == null || SystemStatusCombo?.SelectedItem is not System.Windows.Controls.ComboBoxItem selectedItem)
                return;

            if (byte.TryParse(selectedItem.Tag?.ToString() ?? "0", out byte status))
            {
                _state.SystemStatus = status;
            }
        }

        private void ErrorFlagsTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_state == null || ErrorFlagsTextBox == null) return;

            string text = ErrorFlagsTextBox.Text.Trim();
            byte value = 0;

            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                // Hex format
                if (byte.TryParse(text.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out value))
                {
                    _state.ErrorFlags = value;
                }
            }
            else
            {
                // Decimal format
                if (byte.TryParse(text, out value))
                {
                    _state.ErrorFlags = value;
                }
            }
        }

        private void FirmwareVersion_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_state == null) return;

            if (byte.TryParse(FirmwareMajorTextBox?.Text ?? "0", out byte major) &&
                byte.TryParse(FirmwareMinorTextBox?.Text ?? "0", out byte minor) &&
                byte.TryParse(FirmwarePatchTextBox?.Text ?? "0", out byte patch) &&
                byte.TryParse(FirmwareBuildTextBox?.Text ?? "0", out byte build))
            {
                _state.FirmwareVersion = (major, minor, patch, build);
            }
        }

        private string GetRateText(byte rate)
        {
            return rate switch
            {
                0x01 => "1Hz",
                0x02 => "100Hz",
                0x03 => "500Hz",
                0x04 => "1kHz",
                _ => "Unknown"
            };
        }

        // Adapter Selection Events
        private void AdapterTypeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (AdapterTypeCombo?.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
            {
                string adapterType = selectedItem.Tag?.ToString() ?? "USB";
                
                if (adapterType == "PCAN")
                {
                    if (PcanChannelCombo != null)
                    {
                        PcanChannelCombo.Visibility = Visibility.Visible;
                        RefreshPcanChannels();
                    }
                    if (ComPortCombo != null)
                        ComPortCombo.Visibility = Visibility.Collapsed;
                    if (AdapterHintTxt != null)
                        AdapterHintTxt.Text = "PCAN adapter selected. Make sure PCANBasic.dll is available and PCAN driver is installed.";
                }
                else // USB-CAN-A
                {
                    if (PcanChannelCombo != null)
                        PcanChannelCombo.Visibility = Visibility.Collapsed;
                    if (ComPortCombo != null)
                    {
                        ComPortCombo.Visibility = Visibility.Visible;
                        LoadAvailableComPorts();
                    }
                    if (BaudRateCombo != null)
                        BaudRateCombo.Visibility = Visibility.Visible;
                    if (AdapterHintTxt != null)
                        AdapterHintTxt.Text = "USB-CAN-A Serial adapter selected. Uses COM port communication.";
                }
            }
        }

        private void PcanChannelCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Channel selection changed
        }

        private void ComPortCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // COM port selection changed
        }

        private void BaudRateCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Baud rate selection changed
        }

        // Connection Events
        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            CanAdapterConfig? config = GetAdapterConfig();
            if (config == null)
            {
                MessageBox.Show("Invalid adapter configuration", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Create adapter instance based on type
            if (AdapterTypeCombo?.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem2)
            {
                string adapterType = selectedItem2.Tag?.ToString() ?? "USB";
                
                if (adapterType == "PCAN")
                {
                    _adapter = new PcanCanAdapter();
                }
                else // USB-CAN-A
                {
                    _adapter = new UsbSerialCanAdapter();
                }
            }
            else
            {
                _adapter = new UsbSerialCanAdapter(); // Default
            }

            _adapter.MessageReceived += Adapter_MessageReceived;
            _adapter.ConnectionStatusChanged += Adapter_ConnectionStatusChanged;

            if (_adapter.Connect(config, out string errorMessage))
            {
                AdapterStatusText.Text = $"Connected via {_adapter.AdapterType}";
                ConnectButton.IsEnabled = false;
                DisconnectButton.IsEnabled = true;

                // Initialize stream manager
                if (_state != null && _protocolHandler != null && _adapter != null)
                {
                    _streamManager = new StreamManager(_state, _protocolHandler, _adapter);
                    _streamManager.Start();
                }
            }
            else
            {
                MessageBox.Show($"Failed to connect: {errorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _adapter = null;
            }
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            _streamManager?.Stop();
            _streamManager = null;
            _adapter?.Disconnect();
            _adapter = null;
            
            ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = false;
            AdapterStatusText.Text = "Disconnected";
        }

        private CanAdapterConfig? GetAdapterConfig()
        {
            if (AdapterTypeCombo?.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
            {
                string adapterType = selectedItem.Tag?.ToString() ?? "USB";
                
                if (adapterType == "PCAN")
                {
                    ushort channel = PcanCanAdapter.PCAN_USBBUS1;
                    if (PcanChannelCombo?.SelectedItem is System.Windows.Controls.ComboBoxItem channelItem)
                    {
                        string channelTag = channelItem.Tag?.ToString() ?? "0x51";
                        channel = Convert.ToUInt16(channelTag, 16);
                    }

                    ushort bitrate = GetPcanBitrate();
                    return new PcanCanAdapterConfig
                    {
                        Channel = channel,
                        PcanBitrate = bitrate,
                        BitrateKbps = GetBaudRateValue()
                    };
                }
                else // USB-CAN-A
                {
                    string portName = string.Empty;
                    if (ComPortCombo?.SelectedItem != null)
                    {
                        portName = ComPortCombo.SelectedItem.ToString() ?? string.Empty;
                    }
                    return new UsbSerialCanAdapterConfig
                    {
                        PortName = portName,
                        SerialBaudRate = 2000000,
                        BitrateKbps = GetBaudRateValue()
                    };
                }
            }
            return null;
        }

        private ushort GetBaudRateValue()
        {
            if (BaudRateCombo?.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                string content = item.Content?.ToString() ?? "250 kbps";
                return content switch
                {
                    "125 kbps" => 125,
                    "250 kbps" => 250,
                    "500 kbps" => 500,
                    "1 Mbps" => 1000,
                    _ => 250
                };
            }
            return 250;
        }

        private ushort GetPcanBitrate()
        {
            return GetBaudRateValue() switch
            {
                1000 => PcanCanAdapter.PCAN_BAUD_1M,
                500 => PcanCanAdapter.PCAN_BAUD_500K,
                250 => PcanCanAdapter.PCAN_BAUD_250K,
                125 => PcanCanAdapter.PCAN_BAUD_125K,
                _ => PcanCanAdapter.PCAN_BAUD_500K
            };
        }

        private void RefreshPcanChannels()
        {
            try
            {
                var adapter = new PcanCanAdapter();
                string[] availableChannels = adapter.GetAvailableOptions();
                
                PcanChannelCombo.Items.Clear();
                foreach (string channel in availableChannels)
                {
                    ushort channelValue = channel switch
                    {
                        "USB1" => PcanCanAdapter.PCAN_USBBUS1,
                        "USB2" => PcanCanAdapter.PCAN_USBBUS2,
                        "USB3" => PcanCanAdapter.PCAN_USBBUS3,
                        "USB4" => PcanCanAdapter.PCAN_USBBUS4,
                        "USB5" => PcanCanAdapter.PCAN_USBBUS5,
                        "USB6" => PcanCanAdapter.PCAN_USBBUS6,
                        "USB7" => PcanCanAdapter.PCAN_USBBUS7,
                        "USB8" => PcanCanAdapter.PCAN_USBBUS8,
                        _ => PcanCanAdapter.PCAN_USBBUS1
                    };
                    
                    var item = new System.Windows.Controls.ComboBoxItem
                    {
                        Content = channel,
                        Tag = $"0x{channelValue:X2}"
                    };
                    PcanChannelCombo.Items.Add(item);
                }
                
                if (PcanChannelCombo.Items.Count > 0)
                {
                    PcanChannelCombo.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing PCAN channels: {ex.Message}");
            }
        }

        private void Adapter_MessageReceived(CANMessage message)
        {
            _rxMessageCount++;
            System.Diagnostics.Debug.WriteLine($"Simulator: Adapter_MessageReceived - ID=0x{message.ID:X3}, Data={BitConverter.ToString(message.Data ?? new byte[0])}");
            Dispatcher.Invoke(() =>
            {
                if (_protocolHandler != null)
                {
                    _protocolHandler.ProcessMessage(message);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Simulator: _protocolHandler is null!");
                }
            });
        }

        private void Adapter_ConnectionStatusChanged(object? sender, bool connected)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateConnectionStatus();
            });
        }

        private void ProtocolHandler_ResponseReady(CANMessage message)
        {
            _adapter?.SendMessage(message.ID, message.Data ?? new byte[0]);
        }

        // ADC Configuration Events (removed - ADC config is now simplified for total weight)

        private void ADCMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_state == null || _adcSimulator == null) return;

            byte newMode = 1; // Default ADS1115
            if (InternalADCRadio.IsChecked == true)
            {
                newMode = 0;
                ADCModeText.Text = "Current Mode: Internal (12-bit)";
            }
            else if (ADS1115Radio.IsChecked == true)
            {
                newMode = 1;
                ADCModeText.Text = "Current Mode: ADS1115 (16-bit)";
            }

            _state.ADCMode = newMode;
            _adcSimulator.CurrentMode = newMode;

            // Update UI to show values for the new mode
            UpdateADCValuesForMode(newMode);
        }

        private void UpdateADCValuesForMode(byte adcMode)
        {
            // ADC mode changed - no UI update needed for simplified total weight display
        }

        // Pattern Configuration Events (Total Weight)
        private void TotalPattern_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (TotalPatternCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item && _patternGenerator != null)
            {
                string pattern = item.Tag?.ToString() ?? "Static";
                if (Enum.TryParse<WeightPatternType>(pattern, out var patternType))
                {
                    _patternGenerator.Pattern = patternType;
                    UpdateTotalPatternUI();
                }
            }
        }

        private void UpdateTotalPatternUI()
        {
            if (_patternGenerator == null) return;

            WeightPatternType pattern = _patternGenerator.Pattern;

            TotalAmplitudePanel.Visibility = pattern != WeightPatternType.Static ? Visibility.Visible : Visibility.Collapsed;
            TotalFrequencyPanel.Visibility = pattern == WeightPatternType.Sine ? Visibility.Visible : Visibility.Collapsed;
            TotalDampingPanel.Visibility = pattern == WeightPatternType.Step ? Visibility.Visible : Visibility.Collapsed;
            TotalRampPanel.Visibility = pattern == WeightPatternType.Ramp ? Visibility.Visible : Visibility.Collapsed;
            TotalStaticPanel.Visibility = pattern == WeightPatternType.Static ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TotalBaseline_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (double.TryParse(TotalBaselineTextBox.Text, out double value) && _patternGenerator != null)
            {
                _patternGenerator.Baseline = value;
            }
        }

        private void TotalAmplitude_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (double.TryParse(TotalAmplitudeTextBox.Text, out double value) && _patternGenerator != null)
            {
                _patternGenerator.Amplitude = value;
            }
        }

        private void TotalFrequency_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (double.TryParse(TotalFrequencyTextBox.Text, out double value) && _patternGenerator != null)
            {
                _patternGenerator.Frequency = value;
            }
        }

        private void TotalDamping_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (double.TryParse(TotalDampingTextBox.Text, out double value) && _patternGenerator != null)
            {
                _patternGenerator.Damping = value;
            }
        }

        private void TotalRampDuration_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (double.TryParse(TotalRampDurationTextBox.Text, out double value) && _patternGenerator != null)
            {
                _patternGenerator.RampDuration = value;
            }
        }

        private void TotalStaticWeight_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (double.TryParse(TotalStaticWeightTextBox.Text, out double value) && _patternGenerator != null)
            {
                _patternGenerator.StaticWeight = value;
            }
        }

        private void TotalRestartPattern_Click(object sender, RoutedEventArgs e)
        {
            _patternGenerator?.ResetPattern();
        }

        // Noise Configuration
        private void NoiseLevel_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_noiseGenerator != null)
            {
                _noiseGenerator.NoiseLevel = e.NewValue;
                NoiseLevelText.Text = e.NewValue.ToString("F1");
            }
        }

        // Error Injection
        private void ErrorInjection_Changed(object sender, RoutedEventArgs e)
        {
            bool enabled = ErrorInjectionEnabled.IsChecked == true;
            ErrorTypeCombo.IsEnabled = enabled;
            ErrorRateSlider.IsEnabled = enabled;
            if (_errorInjector != null)
            {
                _errorInjector.Enabled = enabled;
            }
        }

        private void ErrorType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ErrorTypeCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item && _errorInjector != null)
            {
                string errorType = item.Tag?.ToString() ?? "None";
                if (Enum.TryParse<ErrorInjectionType>(errorType, out var type))
                {
                    _errorInjector.ErrorType = type;
                }
            }
        }

        private void ErrorRate_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_errorInjector != null)
            {
                _errorInjector.ErrorRate = e.NewValue / 100.0; // Convert percentage to 0-1
                ErrorRateText.Text = $"{(int)e.NewValue}%";
            }
        }

        // Configuration Management
        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_configManager == null || _state == null || _adcSimulator == null || 
                _patternGenerator == null || _noiseGenerator == null) return;

            var config = _configManager.ExportConfig(_state, _adcSimulator, _patternGenerator, _noiseGenerator);
            _configManager.SaveConfig(config);
            MessageBox.Show("Configuration saved successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_configManager == null || _state == null || _adcSimulator == null || 
                _patternGenerator == null || _noiseGenerator == null) return;

            var config = _configManager.LoadConfig();
            _configManager.ApplyConfig(config, _state, _adcSimulator, _patternGenerator, _noiseGenerator);
            ApplyConfigToUI(config);
            MessageBox.Show("Configuration loaded successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            var defaultConfig = new EmulatorConfig();
            if (_configManager != null && _state != null && _adcSimulator != null && 
                _patternGenerator != null && _noiseGenerator != null)
            {
                _configManager.ApplyConfig(defaultConfig, _state, _adcSimulator, _patternGenerator, _noiseGenerator);
                ApplyConfigToUI(defaultConfig);
            }
        }

        private void LoadConfiguration()
        {
            if (_configManager == null || _state == null || _adcSimulator == null || 
                _patternGenerator == null || _noiseGenerator == null) return;

            var config = _configManager.LoadConfig();
            _configManager.ApplyConfig(config, _state, _adcSimulator, _patternGenerator, _noiseGenerator);
            ApplyConfigToUI(config);
        }

        private void ApplyConfigToUI(EmulatorConfig config)
        {
            // Set ADC mode first
            if (config.ADCMode == 0)
                InternalADCRadio.IsChecked = true;
            else
                ADS1115Radio.IsChecked = true;

            // Update ADC simulator mode
            if (_adcSimulator != null)
                _adcSimulator.CurrentMode = config.ADCMode;

            // ADC configuration removed (simplified for total weight)

            NoiseLevelSlider.Value = config.NoiseLevel;
            NoiseLevelText.Text = config.NoiseLevel.ToString("F1");

            // Total weight pattern settings (using left values as defaults for migration)
            TotalBaselineTextBox.Text = config.LeftBaseline.ToString("F1");
            TotalAmplitudeTextBox.Text = config.LeftAmplitude.ToString("F1");
            TotalFrequencyTextBox.Text = config.LeftFrequency.ToString("F1");
            TotalDampingTextBox.Text = config.LeftDamping.ToString("F1");
            TotalRampDurationTextBox.Text = config.LeftRampDuration.ToString("F1");
            TotalStaticWeightTextBox.Text = config.LeftStaticWeight.ToString("F1");

            // Pattern type selection (using left pattern as default for migration)
            foreach (System.Windows.Controls.ComboBoxItem item in TotalPatternCombo.Items)
            {
                if (item.Tag?.ToString() == config.LeftPattern)
                {
                    TotalPatternCombo.SelectedItem = item;
                    break;
                }
            }
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Try multiple locations for the user guide
                string[] possiblePaths = new[]
                {
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "USER_GUIDE.md"),
                    System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "USER_GUIDE.md"),
                    System.IO.Path.Combine(AppContext.BaseDirectory, "USER_GUIDE.md")
                };

                string? foundPath = null;
                foreach (string path in possiblePaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        foundPath = path;
                        break;
                    }
                }

                if (foundPath != null)
                {
                    // Open with default markdown viewer or text editor
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = foundPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    // Show helpful message with guide content summary
                    string message = "📖 User Guide Information\n\n" +
                                   "The USER_GUIDE.md file contains:\n" +
                                   "• Quick Start Guide\n" +
                                   "• Detailed Feature Explanations\n" +
                                   "• Step-by-Step Tutorials\n" +
                                   "• Troubleshooting Tips\n\n" +
                                   "💡 Tip: Hover over any UI element to see tooltips with help!\n\n" +
                                   "The guide should be in the project repository or application directory.";

                    MessageBox.Show(
                        message,
                        "User Guide",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not open User Guide: {ex.Message}\n\n" +
                    "You can find the USER_GUIDE.md file in the project repository.\n" +
                    "Hover over UI elements for tooltips with help!",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async void CheckUpdatesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_updateService == null) return;

            try
            {
                CheckUpdatesBtn.IsEnabled = false;
                CheckUpdatesBtn.Content = "Checking...";

                var result = await _updateService.CheckForUpdateAsync();

                CheckUpdatesBtn.IsEnabled = true;
                CheckUpdatesBtn.Content = "Check Updates";

                if (!result.IsSuccess)
                {
                    if (result.IsNetworkError)
                    {
                        MessageBox.Show(
                            $"Network error: {result.ErrorMessage}\n\nPlease check your internet connection and try again.",
                            "Update Check Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                    }
                    else
                    {
                        MessageBox.Show(
                            $"Update check failed: {result.ErrorMessage}",
                            "Update Check Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        );
                    }
                    return;
                }

                if (result.Info == null)
                {
                    MessageBox.Show(
                        "No update information available.",
                        "Update Check",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                    return;
                }

                var info = result.Info;

                if (!info.IsUpdateAvailable)
                {
                    MessageBox.Show(
                        $"You are already running the latest version.\n\nCurrent version: {info.CurrentVersion}",
                        "No Updates Available",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                    return;
                }

                // Update available
                var updateResult = MessageBox.Show(
                    $"Update available!\n\n" +
                    $"Current version: {info.CurrentVersion}\n" +
                    $"Latest version: {info.LatestVersion}\n\n" +
                    $"Would you like to download and install the update?",
                    "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (updateResult == MessageBoxResult.Yes)
                {
                    await DownloadAndInstallUpdateAsync(info);
                }
            }
            catch (Exception ex)
            {
                CheckUpdatesBtn.IsEnabled = true;
                CheckUpdatesBtn.Content = "Check Updates";
                MessageBox.Show(
                    $"Error checking for updates: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async Task DownloadAndInstallUpdateAsync(UpdateService.UpdateInfo info)
        {
            try
            {
                var progress = new Progress<double>(p =>
                {
                    CheckUpdatesBtn.Content = $"Downloading... {p:0}%";
                });

                var downloadResult = await _updateService!.DownloadUpdateAsync(info, progress);

                CheckUpdatesBtn.Content = "Check Updates";

                if (!downloadResult.IsSuccess)
                {
                    string errorMessage = downloadResult.ErrorMessage ?? "Failed to download update package.";
                    
                    if (downloadResult.IsNetworkError)
                    {
                        errorMessage += "\n\nPlease check your internet connection and try again.";
                    }
                    else if (downloadResult.IsHashMismatch)
                    {
                        errorMessage += "\n\nThe downloaded file may be corrupted. Please try again.";
                    }
                    
                    MessageBox.Show(errorMessage, "Update Download Failed",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Show success message
                MessageBox.Show(
                    $"Update downloaded successfully!\n\n" +
                    $"File location: {downloadResult.FilePath}\n\n" +
                    $"Please extract the ZIP file and replace the application files manually, or use the updater if available.",
                    "Download Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error downloading update: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _streamManager?.Stop();
            _adapter?.Disconnect();
            _uiUpdateTimer?.Stop();
            base.OnClosed(e);
        }
    }
}

