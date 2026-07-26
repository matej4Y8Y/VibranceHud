# QA checklist

Run before every release build. No need to run for small changes unless they touch rendering, settings, detection, or the updater.

## Before shipping

- [ ] `dotnet build -c Release` is clean
- [ ] `dotnet test` is green (record the count)
- [ ] Version in csproj matches `AppVersion` in .iss
- [ ] No undisposed `new SolidBrush` / `new Pen` / `new Bitmap` in OnPaint paths (leak trap)
- [ ] `bin\Release\net8.0-windows\win-x64\PlexusX.exe` is newer than the last commit

## Visual sweep on the dev build

Launch the dev build, visit every page, check:

- [ ] **Vibrance** — separate vibrance + saturation sliders; brightness / gamma / eye-care; presets; readouts aligned; nothing clipped
- [ ] **Games Hub** — cards clean, no ghosting or double frames; grid scrolls if more than 6 games
- [ ] **Rust / CS2 / Apex / Fortnite** — toggles reflect real config; running-game warning shows when the game is open; Apply writes the config and a backup
- [ ] **FPS Tweaks** — Apply completes without crashing; status text is honest, not a fake score
- [ ] **Crosshair** — preview on checkerboard; **no opaque square artifact anywhere**; shape chips switch; saved chips reachable
- [ ] **Settings** — theme swatches including the Custom image option; image pick recolours accent + particles; dim / blur settings work
- [ ] **Account** — page renders; trial state shown
- [ ] **Window chrome** — rounded corners; glass panels; particles animate on every page; no stale frames after resize

Watch for bug classes that bit the project before: opaque overlay squares, nested-transparency ghosting, bottom rows clipped by parent bounds, stale repaint after theme switch, misaligned custom-drawn text.

## Functional spot checks

- [ ] Visit every nav page, then visit again — second visit shouldn't crash with ObjectDisposedException
- [ ] Drag vibrance / saturation — screen visibly changes
- [ ] Crosshair on — cross at screen centre, mouse clicks pass through to the window beneath
- [ ] Theme switch — accent changes everywhere at once
- [ ] Settings save / load — restart preserves values; corrupt settings.json triggers recovery from .bak
- [ ] Per-game Apply — config file contains the change, `.vibrancebak` exists, Restore Backup reverts

## Performance budgets

- Cold start to main window: under 3 seconds
- Idle CPU with particles running: under 3 percent
- RAM after 60 s idle: under 250 MB
- Slider drag: no visible stutter, no CPU spike

## Safety

- Config providers write only inside the game's own config path or `%AppData%\PlexusX` — any `Path.Combine` fed by user input (`..`, `:`, `/`) needs validation
- Backup / restore targets only `config path + ".vibrancebak"`, never user-supplied paths
- Malformed `settings.json`, deleted theme image, corrupt config: app degrades, never crashes
- Updater reads version from the release tag with a filename cross-check — no silent downgrade
- Crosshair window keeps `WS_EX_TRANSPARENT | WS_EX_NOACTIVATE` so it's click-through and doesn't steal focus
- `Process.Start` call sites: no unsanitized user input as the target
- No secrets in the repo (scan added lines for API keys etc.)
