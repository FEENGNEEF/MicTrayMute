# Changelog

## Unreleased

- Mute all active recording devices by default so apps using a non-default input device are covered.
- Add direct `DeviceId` targeting for machines where Core Audio endpoint enumeration is unreliable.

## 0.1.0

- Initial public version.
- Windows tray microphone mute indicator.
- Global `Ctrl + Alt + M` hotkey.
- Startup mute behavior.
- Settings stored in `%APPDATA%\MicTrayMute\settings.json`.
