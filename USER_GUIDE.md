# ATS Two-Wheeler System Simulator - User Guide

## 📖 Table of Contents
1. [Introduction](#introduction)
2. [Quick Start Guide](#quick-start-guide)
3. [Detailed Feature Guide](#detailed-feature-guide)
4. [Step-by-Step Tutorials](#step-by-step-tutorials)
5. [Troubleshooting](#troubleshooting)

---

## Introduction

### What is the Simulator?
The **ATS Two-Wheeler System Simulator** is a software tool that mimics the behavior of a real STM32-based two-wheeler weight measurement system. It generates realistic total weight data (sum of all 4 load cells) and communicates via CAN bus, allowing you to test your two-wheeler system software without needing physical hardware.

### Why Use the Simulator?
- ✅ **No Hardware Required** - Test your software immediately
- ✅ **Controlled Testing** - Create repeatable test scenarios
- ✅ **Safe Environment** - Test without risk to physical equipment
- ✅ **Development Speed** - Faster iteration and debugging
- ✅ **Total Weight Simulation** - Simulates all 4 channels summed

---

## Quick Start Guide

### Step 1: Launch the Application
1. Double-click `ATS_TwoWheeler_Simulator.exe`
2. The main window will open showing all configuration options

### Step 2: Connect to CAN Bus
1. **Select Adapter Type:**
   - **USB-CAN-A Serial**: For USB-to-CAN adapters (most common)
   - **PCAN**: For Peak CAN adapters

2. **Configure Connection:**
   - **USB-CAN-A**: Select COM port from dropdown
   - **PCAN**: Select channel (USB1, USB2, etc.)
   - Select CAN bitrate (usually 250 kbps)

3. **Click "Connect" Button**
   - Status indicator turns green when connected
   - Status text shows "CAN: Connected"

### Step 3: Configure Total Weight Pattern
1. **Total Weight Pattern:**
   - Select pattern type (Static, Sine Wave, Step, Ramp)
   - Set weight values (total weight across all 4 channels)
   - Click "🔄 Restart Pattern" to apply

### Step 4: Start Testing
- The simulator automatically responds to CAN commands from your application
- Total weight data streams at the configured rate (0x200 message)
- Monitor status in the "System Status" section

---

## Detailed Feature Guide

### 🔌 CAN Adapter Configuration

#### Adapter Type
**What it does:** Selects how the simulator connects to the CAN bus.

**Options:**
- **USB-CAN-A Serial**: Uses a USB-to-CAN adapter connected via COM port
  - Requires: USB-CAN-A adapter and COM port
  - Best for: Most users, simple setup
  
- **PCAN**: Uses Peak CAN adapter
  - Requires: PCAN hardware and drivers
  - Best for: Professional testing environments

#### COM Port (USB-CAN-A only)
**What it does:** Selects which COM port your USB-CAN-A adapter is using.

**How to find your COM port:**
1. Open Device Manager (Windows)
2. Expand "Ports (COM & LPT)"
3. Look for your USB-CAN-A device
4. Note the COM port number (e.g., COM3, COM10)

**Tip:** If using VSPE (Virtual Serial Port Emulator), select the first port of your virtual pair.

#### CAN Bitrate
**What it does:** Sets the communication speed on the CAN bus.

**Options:**
- **125 kbps**: Slow, reliable, long-distance
- **250 kbps**: Standard, most common ⭐ (Recommended)
- **500 kbps**: Fast, shorter distance
- **1 Mbps**: Very fast, short distance only

**Recommendation:** Use 250 kbps unless you have specific requirements.

#### Connect/Disconnect Buttons
**What it does:** Establishes or closes the CAN bus connection.

**When to use:**
- **Connect**: After configuring adapter settings
- **Disconnect**: Before changing adapter settings or closing application

---

### ⚖️ Total Weight Pattern Configuration

The simulator generates total weight values by summing all 4 load cell channels (Ch0+Ch1+Ch2+Ch3).

#### Pattern Type
**What it does:** Selects how the total weight changes over time.

**Options:**
- **Static**: Constant total weight (no change)
- **Sine Wave**: Oscillating weight (smooth up/down)
- **Step**: Sudden weight change with settling
- **Ramp**: Linear increase or decrease

#### Static Pattern
**Use case:** Testing with fixed total weight.

**Configuration:**
- **Static Weight**: Enter total weight in kg (e.g., 200.0)
- All 4 channels contribute equally to the total

#### Sine Wave Pattern
**Use case:** Simulating dynamic loading (vibration, oscillation).

**Configuration:**
- **Baseline**: Center weight value (kg)
- **Amplitude**: Peak variation from baseline (kg)
- **Frequency**: Oscillation rate (Hz)

**Example:** Baseline=100kg, Amplitude=50kg, Frequency=2Hz
- Weight oscillates between 50kg and 150kg at 2 cycles per second

#### Step Pattern
**Use case:** Testing sudden weight changes (loading/unloading).

**Configuration:**
- **Baseline**: Starting weight (kg)
- **Amplitude**: Final weight change (kg)
- **Damping**: Settling speed (higher = faster)

**Example:** Baseline=0kg, Amplitude=200kg, Damping=0.5
- Weight increases from 0kg to 200kg with exponential settling

#### Ramp Pattern
**Use case:** Testing gradual weight changes.

**Configuration:**
- **Baseline**: Starting weight (kg)
- **Amplitude**: Total change (kg)
- **Ramp Duration**: Time to complete change (seconds)

**Example:** Baseline=0kg, Amplitude=300kg, Duration=10s
- Weight increases linearly from 0kg to 300kg over 10 seconds

---

### 🔧 ADC Configuration

#### ADC Mode
**What it does:** Selects which ADC the simulator emulates.

**Options:**
- **Internal ADC (12-bit)**: STM32 internal ADC
  - Range: 0-16380 (4 channels × 4095)
  - Message size: 2 bytes
  - Resolution: ~1.075 kg/count
  
- **ADS1115 (16-bit)**: External ADS1115 ADC
  - Range: -131072 to +131068 (4 channels × signed range)
  - Message size: 4 bytes
  - Resolution: ~0.167 kg/count

**How to switch:**
- Use "Switch to Internal ADC" or "Switch to ADS1115" buttons
- Or send CAN command 0x030 (Internal) or 0x031 (ADS1115)

#### Channel Configuration
**What it does:** Configures individual channel parameters (for advanced users).

**Note:** The simulator distributes total weight equally across all 4 channels, then sums them. Individual channel settings affect the simulation accuracy.

---

### 📊 System Status

#### Connection Status
- **🔴 Red**: Disconnected
- **🟢 Green**: Connected

#### Stream Status
- **Active**: Stream is running (receiving CAN commands)
- **Inactive**: No active stream

#### Message Counters
- **TX Messages**: Total messages sent by simulator
- **RX Messages**: Total messages received by simulator

---

## Step-by-Step Tutorials

### Tutorial 1: Basic Connection Test

**Goal:** Verify simulator can communicate with your application.

**Steps:**
1. Launch simulator
2. Select "USB-CAN-A Serial" adapter
3. Select COM port
4. Set bitrate to 250 kbps
5. Click "Connect"
6. Verify status shows "CAN: Connected"
7. In your application, send status request (0x032)
8. Verify simulator responds with status message (0x300)

### Tutorial 2: Static Total Weight Test

**Goal:** Test with fixed total weight.

**Steps:**
1. Connect to CAN bus
2. Set Pattern Type to "Static"
3. Set Static Weight to 150.0 kg
4. Click "🔄 Restart Pattern"
5. In your application, start stream (0x040 with rate 0x02 = 100Hz)
6. Verify your application receives total weight ≈ 150.0 kg
7. Check message counter increases

### Tutorial 3: Dynamic Pattern Test

**Goal:** Test with oscillating total weight.

**Steps:**
1. Connect to CAN bus
2. Set Pattern Type to "Sine Wave"
3. Set Baseline to 100.0 kg
4. Set Amplitude to 50.0 kg
5. Set Frequency to 1.0 Hz
6. Click "🔄 Restart Pattern"
7. Start stream in your application
8. Watch total weight oscillate between 50kg and 150kg
9. Verify smooth sine wave pattern

---

## Troubleshooting

### Connection Issues

**Problem:** Cannot connect to CAN bus
- **Check:** COM port is correct and not in use by another application
- **Check:** USB-CAN-A adapter is properly connected
- **Check:** Drivers are installed correctly
- **Solution:** Try different COM port or restart adapter

**Problem:** Status shows "Connected" but no messages
- **Check:** CAN bitrate matches your application (usually 250 kbps)
- **Check:** Your application is sending commands
- **Check:** Message filters are not blocking messages
- **Solution:** Verify CAN bus configuration in both applications

### Weight Data Issues

**Problem:** Total weight values are incorrect
- **Check:** ADC mode matches your application's mode
- **Check:** Pattern configuration is correct
- **Check:** Channel sensitivity settings
- **Solution:** Verify ADC mode and recalibrate if needed

**Problem:** No weight data received
- **Check:** Stream is started (0x040 command sent)
- **Check:** Stream rate is configured correctly
- **Check:** Message ID 0x200 is not filtered
- **Solution:** Restart stream and verify CAN communication

### Performance Issues

**Problem:** High CPU usage
- **Check:** Stream rate is not too high (1kHz max recommended)
- **Check:** Error injection is disabled
- **Solution:** Reduce stream rate or disable unnecessary features

---

**Version:** 0.1.0  
**Last Updated:** 25 December 2025

