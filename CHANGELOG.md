# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-01-29

### Added
- **Protocol v1.1 Support**: Implemented new 6-byte packed status messages (0x300) and performance metrics (0x301).
- **Live Performance Metrics**: Simulator now reports real-time CAN TX frequency and simulated 1kHz ADC sampling rates.
- **System Uptime Tracking**: Added system-wide uptime counter in seconds, mirrored in the status protocol and UI.
- **Pattern Jitter Simulation**: Introduced noise/jitter controls to simulate real-world sensor fluctuations for filter testing.
- **Expanded User Documentation**: Completely rewritten User Guide with advanced testing tutorials and error injection scenarios.

### Changed
- **Rate Mapping Alignment**: Updated transmission rate IDs to match Firmware v0.3.0 standards (0x01=100Hz, 0x02=500Hz, 0x03=1kHz, 0x05=1Hz).
- **UI Diagnostics Panel**: Added dedicated fields for Uptime and Perf statistics in the main window.
- **Heartbeat System**: Introduced a background heartbeat timer for accurate performance metric calculation.

### Fixed
- **ADC Mode Handshaking**: Fixed issue where mode-switch commands didn't immediately trigger a status response.
- **Timing Accuracy**: Improved high-frequency stream timing for more realistic simulation of 1kHz data rates.
- **Thread Safety**: Resolved potential race conditions when accessing system state from the CAN protocol handler.

## [0.1.0] - 2026-01-25

### Added
- **Initial Release**: Core simulator functionality for ATS Two-Wheeler system.
- **ADC Emulation**: Support for Internal (12-bit) and ADS1115 (16-bit) ADC simulation.
- **Weight Patterns**: Basic Static and Sine Wave patterns for total weight simulation.
- **CAN Protocol Core**: Fundamental support for system status, version, and data streaming commands.
- **Adapter Support**: Support for USB-CAN-A Serial and basic CAN connectivity.
- **Bootloader Emulation**: Basic framework for simulating firmware update processes.
