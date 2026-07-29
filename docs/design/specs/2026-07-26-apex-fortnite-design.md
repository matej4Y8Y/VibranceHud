# 0.5.0 — More Games: Apex Legends + Fortnite

**Date:** 2026-07-26
**Status:** Approved (CEO call: both in one release), in implementation

## Goal

Grow the Games Hub from 2 games (Rust, CS2) to 4 by adding **Apex Legends** and
**Fortnite**, following the established Rust/CS2 provider pattern: safe config-file
edits only, backup before first write, running-game guard, TDD'd pure logic.

## Detection

| Game | Store | Mechanism |
|---|---|---|
| Apex Legends | Steam | Existing detector: appid `1172470`, folder `Apex Legends` |
| Fortnite | Epic Games Store | NEW: `EpicLocator` parses `%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item` (JSON: `AppName` == "Fortnite" → `InstallLocation`). Pure parse function unit-tested; IO wrapper degrades to null |

`SupportedGame` gains an optional `EpicAppName` (default null) — Steam games unchanged.
`GameLibrary.DetectInstalled` merges Steam hits (SteamAppId > 0) and Epic hits
(EpicAppName != null). Never throws; absence of a store just means fewer games.

## Config targets (both per-user, not per-install)

| Game | File | Format |
|---|---|---|
| Apex | `%USERPROFILE%\Saved Games\Respawn\Apex\local\videoconfig.txt` | `"setting.key"  "value"` lines |
| Fortnite | `%LOCALAPPDATA%\FortniteGame\Saved\Config\WindowsClient\GameUserSettings.ini` | INI: `[Section]` + `key=value` |

Both configs are read by the game at launch — **no launch options needed** (unlike CS2's
+exec autoexec). Running-game guard on both: `r5apex.exe`, `FortniteClient-Win64-Shipping.exe`.

## Tweaks (curated, real keys only — an invented key silently does nothing)

**Apex** (`setting.*` keys): Uncapped FPS (`fps_max` 0/144), Disable Shadows
(`csm_enabled`), Low Model Detail (`r_lod_switch_scale` 0.35/1), Disable Anti-Aliasing
(`mat_antialias_mode` 0/12), Disable Adaptive Resolution (`dvs_enable`).

**Fortnite**: Low Scalability (six `sg.*Quality` → 0/2 in `[ScalabilityGroups]`),
Uncapped Frame Rate (`FrameRateLimit` 0/60), V-Sync Off (`bUseVsync`),
Windowed Fullscreen (`FullscreenMode` 1/0 — also what lets the crosshair overlay draw).

Presets per game: **Competitive** (all on) / **Cinematic** (all off), same as CS2.

## Files

```
Games/EpicLocator.cs                    (new, pure parse + IO locator)
Games/SupportedGame.cs                  (+ EpicAppName, + Apex/Fortnite entries)
Games/GameLibrary.cs                    (merge Epic detection)
Apex/ApexConfig.cs ApexTweaks.cs ApexPresets.cs ApexSettingsService.cs
Fortnite/FortniteConfig.cs FortniteTweaks.cs FortnitePresets.cs FortniteSettingsService.cs
Pages/ApexSettingsPage.cs Pages/FortniteSettingsPage.cs   (cloned from Cs2SettingsPage)
MainWindow.cs                           (+ two switch cases)
tests: ApexConfigTests, FortniteConfigTests, EpicLocatorTests
```

## Testing

- ApexConfig: parse/Get/Set, byte-for-byte preservation of untouched lines, append-new-key.
- FortniteConfig: section-aware Get/Set (same key in two sections stays distinct),
  key insertion into existing section, section creation when missing.
- EpicLocator: manifest JSON parse (matching/non-matching AppName, escaped backslashes,
  malformed JSON → null).
- Services stay thin IO (manually verified), same contract as Rust/CS2.

## Out of scope

- Per-game vibrance profiles (user rejected).
- EA App detection for Apex (Steam covers the target audience; can revisit).
- Valorant/LoL (Vanguard risk — standing guardrail).
- Launch-options helpers (not needed — both games read these configs directly).
