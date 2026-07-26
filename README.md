# PlexusX

Windows tool for boosting colors past what NVIDIA's control panel allows.

NVIDIA's driver caps digital vibrance at 100%. PlexusX pushes past that - up to 200% - on your primary display. The first 0-100% goes through the driver directly so games see real hardware vibrance. The 100-200% range runs as a screen-wide color effect through Windows, so the colors keep saturating.

## What it does

- Lives in the system tray
- **Ctrl+Alt+V** brings up a small slider
- Slider goes 0 to 200 percent
- Right-click the tray icon for the menu

## What it doesn't do

- Touch exclusive-fullscreen games or DRM video (Netflix etc). Those bypass the screen color effect.
- Run on AMD or Intel-only GPUs without compromise. NVIDIA-only driver acceleration for 0-100%; everything else uses the software path.

The 100-200% effect shares Windows' color pipeline with Night Light and Color Filters. If it looks wrong, turn Windows Color Filters off in Settings > Accessibility > Color filters.

## Install

Download `PlexusX-Setup-0.7.2.exe` from the releases page, run it. Old versions upgrade in place. No uninstall needed.

## Notes

The app talks directly to your NVIDIA driver, so it needs an actual GPU with the driver installed to work. It's not something you can compile and run on a machine without hardware - if `dotnet build` runs without a real GPU, you'll get test failures for the hardware-bound code paths but the rest builds fine.

## What's in here

- `bin\Release\net8.0-windows\win-x64\PlexusX.exe` - the app
- `installer\PlexusX-Setup-0.7.2.exe` - the installer
- `docs\` - design notes, releases, roadmap
- `docs\superpowers\` - internal stuff I wrote for myself, ignore it
