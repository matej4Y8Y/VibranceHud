# PlexusX

A lightweight Windows tray app that puts NVIDIA's Digital Vibrance control behind a
global hotkey, instead of three menus deep in NVIDIA Control Panel - and lets you push
saturation **past** the driver's 100% ceiling, system-wide.

- **0-100%** uses NVIDIA's Digital Vibrance directly - the same driver feature (and hard
  cap) as the NVIDIA Control Panel slider.
- **100-200%** pins the driver at max and applies a saturation color matrix to the whole
  screen via the Windows Magnification API, so colors keep going past what the driver
  alone allows.

## What it does

- Sits quietly in the system tray, no visible window by default
- **Ctrl+Alt+V** pops a small slider near your cursor
- Dragging it live-updates vibrance, 0-200%, on your primary NVIDIA display
- Right-click the tray icon for a menu: open slider, reset to default, exit

## Good to know about the 100-200% range

- It's applied to the **whole desktop** and any **borderless-windowed** game.
- It does **not** affect true **exclusive-fullscreen** games or **DRM-protected video**
  (e.g. Netflix) - those bypass the screen color effect.
- It shares Windows' color pipeline with **Night Light** and **Color Filters**; if the
  oversaturation looks off, turn Windows Color Filters off (Settings > Accessibility >
  Color filters).
- The effect is cleared automatically when you exit or reset, and Windows removes it on
  its own if the app is ever force-closed.

## Requirements

- Windows 10/11
- An NVIDIA GPU with the driver installed
- [.NET 8 SDK](https://dotnet.microsoft.com/download) or newer, to build it

## Build & run

```
dotnet build
dotnet run
```

Or open the folder in Visual Studio and press F5. `dotnet restore` will pull down
the one dependency (NvAPIWrapper.Net) automatically on first build.

## A heads-up

This talks directly to your NVIDIA driver through
[NvAPIWrapper](https://github.com/falahati/NvAPIWrapper) (LGPL-licensed, fine for
this kind of use). That means it genuinely needs your real GPU, driver, and a
connected monitor to run - it's not something that can be compiled or tested from
where I'm working, so this hasn't been built and run end-to-end.

The method calls in `VibranceController.cs` are grounded directly in the library's
real source (not guessed), so it should build cleanly. If `dotnet build` does throw
an error, it's most likely a small property-name mismatch in that one file - an easy
fix once you can see the actual compiler error and IntelliSense's suggestions.

## Next up

The system-wide 100-200% boost above is done. A possible future tier: a custom
saturation shader injected into a *specific* game through ReShade, which would also
reach exclusive-fullscreen titles the screen-wide effect can't - at the cost of
per-game setup and anti-cheat risk in competitive games.

## Tests

The color-matrix math and the 0-200 threshold logic are covered by unit tests
(`tests/VibranceHud.Tests`, xUnit) that run without a GPU:

```
dotnet test
```

The driver (NVAPI) and screen-effect (Magnification) calls need real hardware, so
those are verified by running the app.
