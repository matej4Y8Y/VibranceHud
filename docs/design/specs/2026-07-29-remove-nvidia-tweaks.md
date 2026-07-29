# Sekce X — Remove NVIDIA Tweaks (feature doesn't work on user's hardware)

**Datum:** 2026-07-29
**Status:** Návrh
**Sekce:** X (cleanup)

## Problém

NVIDIA Tweaks card na Rust Settings Page nefunguje na userově NVIDIA kartě. Každý
toggle hlásí "Driver didn't accept this setting" i s v0.8.2 tri-state fixem. Mezera
mezi NVAPI expected environment a userovým driver version je nepřeklenutelná,
a DRS writes vyžadují admin.

Viz `docs/design/specs/2026-07-28-cleanup-star-points-design.md` řádek 12-13:
> "The NVIDIA Tweaks card on the Rust Settings page doesn't work on the user's
> NVIDIA card. Every toggle reports 'Driver didn't accept this setting' even
> with the v0.8.2 tri-state fix"

User tuhle sekci backlogoval na odstranění. Tenhle dokument je implementační plán.

## Cíl

Odstranit NVIDIA Tweaks kompletně — code, UI, settings. Žádný broken feature
v app. Migrations: settings.json klíče `RustNvidiaTweaks`, `NvAppSupportedTweaks`,
`RustNvidiaTweaksNeedsAdmin` se stanou no-ops (read, ignore, don't write).

## Scope

### Co odstranit (production code)

- `Nvidia/NvidiaTweakCatalog.cs` (4 KB) — definice tweaks
- `Nvidia/NvidiaDriverSettings.cs` (13 KB) — NVAPI wrapper, IsSupported, Apply
- `Nvidia/NvidiaTweakElevationService.cs` (4 KB) — headless elevated relaunch
- `Nvidia/NvAppRustProfileTweak.cs` (10 KB) — NVIDIA Experience tweak (také broken per design doc)

### Co odstranit (UI)

- `Pages/RustSettingsPage.cs` řádky 308-635 (Nvidia Tweaks card builder) + `_nvCardY`
  field + `BuildNvidiaCard` call site + `Scan` button click handler
- `Program.cs` řádky 17-21 (NvidiaTweakElevationService.IsHeadlessInvocation check)
- `TrayApplicationContext.cs` — `NvidiaDriverSettings` constructor call + Scan
  button integration if present

### Co odstranit (tests)

- `tests/.../NvidiaTweakScanTests.cs` (6 KB)
- `tests/.../NvAppRustProfileTweakTests.cs` (7 KB)

### Co zachovat (settings.json schema)

`AppSettings.RustNvidiaTweaks`, `NvAppSupportedTweaks`, `RustNvidiaTweaksNeedsAdmin` —
zachovat jako `IReadOnlySet<string>` vlastnosti (aby se settings.json z
předchozí verze nerozbil), ale **přestat je číst a zapisovat**. Staré
settings.json soubory s těmito klíči se tiše načtou a budou ignorovány.

### Co NEodstranit

- `Nvidia/NvAppRustProfileTweak.cs` — **odstranit**, viz výše
- `Nvidia/GpuCapability.cs` — **zachovat**, používá se jinde (vibrance detection)
- `Pages/FpsTweaksPage.cs` — **zachovat**, je to jiná sekce (registry tweaks, ne NVAPI)

## Rozhodnutí

### Proč úplné odstranění (ne skrytí za feature flag)

Feature flag by přidal:
- `SettingsStore` schema migraci (odstranit klíče z serializace)
- UI kód s `if (NvidiaTweaksEnabled)` guardy
- Admin elevation path, který nikdo nepoužije
- Testy pro feature flag

Za to máme user-facing výhodu "user může zase zapnout když NVAPI bude fungovat" —
ale ten use case nikdy nenastane (driver mismatch je trvalý).

**Levnější:** úplné odstranění. Kdyby NVAPI někdy zase fungoval, přidáme
feature zpátky z gitu.

### Settings.json migrace

`SettingsStore.Load` používá `JsonSerializer.Deserialize<AppSettings>` s
default konvertorem — neznámé vlastnosti jsou **ignorovány**, nevyhazují
výjimku. Takže settings.json z v0.9.0-rc1 s `RustNvidiaTweaks` array
zůstane validní po odstranění fieldů.

### Co dělá Scan button (pokud existuje v tray)

Tray nemá NVIDIA Tweaks Scan button — `TrayApplicationContext` nemá takový
handler. Scan button je **pouze v RustSettingsPage** (řádek ~308).

## API

Žádná API změna. Jen odstranění.

## Změny v existujícím kódu

### `AppSettings.cs`

Odstranit fieldy:
- `public HashSet<string> RustNvidiaTweaks { get; set; } = new();`
- `public HashSet<string> NvAppSupportedTweaks { get; set; } = new();`
- `public HashSet<string> RustNvidiaTweaksNeedsAdmin { get; set; } = new();`

### `Pages/RustSettingsPage.cs`

- Smazat field `_nvCardY`
- Smazat field `_nvCard`
- Smazat field `_scanLastLabel`
- Smazat field `_settings.NvAppSupportedTweaks` reference
- Smazat metodu `BuildNvidiaCard`
- Smazat Scan button click handler
- Upravit `BuildLayout` — odebrat `_nvCard` allocation + Y tracking

### `Program.cs`

Smazat:
```csharp
if (NvidiaTweakElevationService.IsHeadlessInvocation(args))
    return NvidiaTweakElevationService.RunHeadless(args);
```

### `TrayApplicationContext.cs`

Smazat:
- `_nvidia = CreateNvidia();` field
- `CreateNvidia()` method
- `NvidiaDriverSettings` constructor call

## Testy

Po odstranění:
- `dotnet test` — 332 PASS (348 - 16 NVIDIA testů)
- `dotnet build -c Release` — čistý
- Manuální test: nastartuj PlexusX, jdi na Rust, ověř že NVIDIA Tweaks card
  je pryč

## Akceptační kritéria

- [ ] Všech 348 testů - 16 NVIDIA testů = 332 PASS
- [ ] `dotnet build -c Release` — čistý
- [ ] `Nvidia/` adresář smazán (kromě `GpuCapability.cs`)
- [ ] RustSettingsPage.cs zkráceno o ~330 řádků
- [ ] Settings.json s `RustNvidiaTweaks` se stále načte (žádný výjimka)

## Rizika

- **Settings.json schema drift** — pokud user má z v0.9.0 settings s těmito
  klíči, po upgradu se ty klíče ztratí. Mitigation: dokumentovat v release
  notes.
- **Lost work** — kdokoliv měl uložené NVIDIA Tweaky v Rust profilu, přijde
  o ně. Toto je inherent cost odstranění.
- **Regresion v zobrazení RustSettingsPage** — stránka se zmenší o
  ~330 řádků layout kódu. Musím ověřit, že ostatní karty (Launch boosts,
  NVIDIA Tweaks status, Rust config) sedí správně.

## Co dělá další

Schválení od tebe → implementation plan → kód → testy → release notes.

Pokud řekneš NE, alternativy:
1. Skrýt NVIDIA Tweaks card za "Disabled in this build" label (1 den)
2. Zachovat NVIDIA Tweaks ale vylepšit error message (1 den)
3. Plné odstranění (tento plán, 2 dny včetně testů)

Doporučuji plné odstranění protože useri nikdy NVIDIA Tweaky nepoužijí
(driver mismatch na tvém HW, pravděpodobně i na většině jiných).
