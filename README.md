# TimeularAudioSwitcher

A small Windows tray app that switches the system’s default audio input/output devices based on the active side of a Timeular (or similar) BLE tracker.

## Main features
- Map up to 8 sides to specific audio devices (separate input and output per side)
- When the tracker is flipped, automatically switches Windows default playback and recording devices
- Tray icon shows the active side (number) and connection status (color dot)
- Use the Tray menu to manually trigger any side
- Options to autostart on boot and start minimized to tray
- Robust BLE handling: service/characteristic discovery, indication subscription, auto-reconnect with backoff, and resume after sleep