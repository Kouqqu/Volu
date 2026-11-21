# Volu

Minimalist volume HUD for Windows 10 and 11, providing discrete 16-step volume control and audio feedback.

## Features

- **macOS-style HUD:** 200x200 centered card with 16 discrete volume segments.
- **Dynamic Speaker Icon:** Automatically adapts to volume level and mute status.
- **Suppression of Default Slider:** Prevents the native Windows volume overlay from popping up on media key presses.
- **Discrete Stepping:** Adjusts volume in exact 6.25% increments (1/16th).
- **Audio Feedback:** Optional click sound on volume adjustments (includes Wood Click, Tactile Click, or Mute).
- **Multi-Monitor Support:** Displays on the monitor currently containing the mouse cursor.
- **Lightweight:** Click-through transparent overlay, minimal memory footprint, and zero CPU usage when idle.
- **System Tray:** Windows 11 style context menu with sound selection and startup toggle.

## Shortcuts

- Volume Up: `Media Volume Up` or `Ctrl + Alt + Up`
- Volume Down: `Media Volume Down` or `Ctrl + Alt + Down`
- Mute: `Media Volume Mute` or `Ctrl + Alt + M`

## Building from Source

Requires .NET 9 SDK:

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```
