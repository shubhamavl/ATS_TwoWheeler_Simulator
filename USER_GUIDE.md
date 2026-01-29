# ATS Two-Wheeler Simulator User Guide

## 1. Overview
The **Simulator** is like a "Fake Board". It runs on your PC and acts exactly like the real hardware so you can test the UI application without needing to plug anything into a real vehicle.

- **Technical Note**: Fully emulates the latest CAN protocol (v0.2.0.0), including performance metrics, uptime tracking, and multi-byte status payloads.

---

## 2. Setup (Connecting the Fake Board)

To use the simulator, you need a bridge to talk to the UI app.

- **Virtual Pairing (No Wires)**: Use **VSPE (Virtual Serial Ports Emulator)** to create a virtual bridge between two COM ports.
    - **How**: Create a **"Pair"** device in VSPE (e.g., COM1 and COM2). 
    - **Connect**: Set the Simulator to `COM1` and the UI App to `COM2`.
- **PCAN**: Use a real CAN adapter if you want to test the full physical loop with wires.

| Adapter Type | Configuration | Best Used For... |
| :--- | :--- | :--- |
| **USB-CAN (VSPE)** | Select COM Port | **No Wires**: Virtual testing on a single PC. |
| **PCAN-USB** | Select Channel (USB1) | **Real Wires**: Physical hardware testing. |

---

## 3. Creating "Fake" Signals

You can tell the simulator how to behave to test different scenarios.

### 3.1 Patterns (How the numbers move)
- **Static**: The weight stays perfectly still.
- **Sine Wave**: The numbers move up and down like a wave (tests live tracking).
- **Ramp**: The weight slowly increases/decreases (tests capacity limits).

### 3.2 Injecting Jitters (Noise)
Real sensors aren't perfect. Use the **Noise** slider to add "jitter" to the numbers.
- **Why?**: This lets you test if your **Filters (EMA/SMA)** are working correctly to stabilize the display.

---

## 4. System Health & Performance

The simulator now tracks the same metrics as the real firmware.

### 4.1 Live Metrics
- **Uptime**: Tracks how long the board has been "on" since the simulation started.
- **CAN TX Hz**: Shows the actual speed of data being sent to the PC.
- **ADC Sample Hz**: Simulates the internal 1kHz sampling loop of the STM32.

### 4.2 Status Logic (0x300)
The simulator sends a **6-byte packed status** matching the latest firmware:
- **Byte 0**: Status, ADC Mode, and Relay state (packed).
- **Byte 1**: Error Flags.
- **Bytes 2-5**: System Uptime (Seconds).

---

## 5. Testing Errors (Breaking things safely)

You can force the simulator into "Error Mode" to see how the UI reacts.

| Test Case | Trigger Action | What to look for in UI |
| :--- | :--- | :--- |
| **ADC Fail** | Set `Error 0x01` | Dashboard should show "ADC RECOVERY" or Error flag. |
| **Lost Signal** | Click "Disconnect" | Big Weight numbers turn **Red** after 5 seconds. |
| **Brake Test** | Toggle "Relay" | Status Icon turns **Orange** and Peak tracking begins. |

---

## 6. Bootloader Simulation
The simulator fully emulates the Firmware v0.2 update process.
- When you send a file via the UI Bootloader tool, the simulator will show the percentage received, verify the data, and "reboot" its fake firmware.
- This is the safest way to training operators on how to perform updates.
