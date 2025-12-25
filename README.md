# ATS Two-Wheeler System Simulator

A comprehensive software simulator for testing STM32-based ATS Two-Wheeler systems without physical hardware.

## 📚 Documentation

**👉 [USER_GUIDE.md](USER_GUIDE.md) - Complete beginner-friendly guide with step-by-step instructions**

## Quick Start

1. **Launch** `ATS_TwoWheeler_Simulator.exe`
2. **Select Adapter:**
   - USB-CAN-A Serial: Choose COM port
   - PCAN: Choose channel
3. **Click "Connect"** (green button)
4. **Configure Total Weight Pattern**
5. **Start Testing** - Simulator responds to CAN commands automatically

## Features

- ✅ **Multiple CAN Adapters:** USB-CAN-A Serial, PCAN
- ✅ **Total Weight Patterns:** Static, Sine Wave, Step Response, Ramp
- ✅ **ADC Simulation:** Internal (12-bit) and ADS1115 (16-bit) modes
- ✅ **4-Channel Summing:** Simulates all 4 load cells summed for total weight
- ✅ **Noise Simulation:** Realistic sensor noise
- ✅ **Error Injection:** Test error handling (advanced)
- ✅ **Configuration Management:** Save/load settings
- ✅ **Real-time Status:** Monitor streams and message counts
- ✅ **Protocol v0.1:** Supports ATS Two-Wheeler CAN protocol

## Getting Help

### In the Application
- **Hover over any UI element** to see tooltips with explanations
- **Click "❓ Help" button** to open the User Guide
- **Check System Status** section for connection and stream status

### Documentation
- **USER_GUIDE.md** - Complete user guide with tutorials
- **Tooltips** - Hover over buttons, fields, and controls for instant help

## Common Tasks

### Connect to Your Application
1. Use **VSPE** (Virtual Serial Port Emulator) to create virtual COM port pair
2. Simulator connects to one port, your application to the other
3. Both communicate through VSPE bridge

### Test with Static Total Weight
1. Set Pattern Type to "Static"
2. Enter total weight value (e.g., 200.0 kg)
3. Start stream in your application
4. Verify total weight readings match

### Test with Dynamic Patterns
1. Select pattern (Sine Wave, Step, Ramp)
2. Configure parameters (Baseline, Amplitude, Frequency, etc.)
3. Start stream and watch total weight change over time
4. Click "🔄 Restart Pattern" to replay

## System Requirements

- Windows 7/8/10/11
- .NET 8.0 Runtime
- CAN Adapter (USB-CAN-A or PCAN) OR VSPE for virtual testing

## Protocol Support

- **CAN Protocol v0.1:** Total weight measurement system
- **Message ID 0x200:** Total raw ADC data (all 4 channels summed)
- **Stream Control 0x040:** Single stream start command
- **ADC Modes:** Internal (12-bit) and ADS1115 (16-bit)

## Support

For detailed instructions, troubleshooting, and tutorials, see **[USER_GUIDE.md](USER_GUIDE.md)**

---

**Version:** 0.1.0  
**Last Updated:** 25 December 2025

