# Sekce 3 — DX11 reliability (saturation visible everywhere)

**Datum:** 2026-07-28
**Status:** Návrh
**Sekce:** 3 / 14

## Problém

Když DX11 init selže, PlexusX tiše přepne na Magnification API fallback.
User vidí jen "Fallback mode" v Settings, ale neví PROČ to spadlo ani co s
tím má dělat. Pro 100+ userů znamená každý tichý fallback = "PlexusX nezobrazuje
saturaci v mém streamu" bez vysvětlení.

Konkrétně:
- **Settings page varování** je teď: "Display engine: Fallback (hidden from
  OBS Game Capture / Discord)" — říká CO se stalo, ne PROČ.
- **Retry button** restartuje proces, ale neříká userovi CO může zkusit
  mezitím (vypnout fullscreen app, aktualizovat driver, atd.)
- **DxDevice.DxDevice() catch(Exception)** spolkne jakoukoli chybu — ani
  crash log, ani kategorizace selhání.

## Cíl

Každý DX11 init failure = kategorizovaný důvod (driver / GPU / display /
permission) + user-friendly diagnostická zpráva v Settings + akce
kterou user může udělat sám, bez čekání na support.

## Scope

### Co patří do Sekce 3

1. **`DxInitFailureKind` enum** — kategorie selhání: `NoCompatibleAdapter`,
   `DeviceCreationFailed`, `NoOutputs`, `Unknown`
2. **`DxDevice.DxDevice()`** — zachytává `SharpDXException` a `COMException`
   zvlášť, kategorizuje podle HRESULT, **zaloguje do CrashLogu**,
   **uloží do AppSettings** nové pole `DxInitFailureKind` + `DxInitFailureMessage`
3. **`SettingsPage`** — místo "Fallback mode" řádek zobrazí:
   - Kategorie selhání (e.g. "Display driver couldn't initialize DX11")
   - Detailní zpráva (e.g. "DXGI_ERROR_DEVICE_REMOVED — GPU driver restarted")
   - **Akce** kterou může user udělat sám: "Restart PlexusX", "Update GPU driver",
     "Close other 3D apps (game, MSI Afterburner, etc.)"
4. **Auto-retry na splash** — pokud DX11 selhal a `ManualOverrideActive == false`
   (user netweakoval před X minutami), zkusit restart po 5s. Max 3 pokusy.
   Po 3 selháních zobrazit Settings warning s akcí.
5. **Testy**:
   - DxDevice s mock `Factory2` (nebo test seam) — verifikace že kategorizace funguje
   - SettingsPage rendering s různými DxInitFailureKind hodnotami

### Co do Sekce 3 NEpatří

- Cloud reporting (Sentry) — Sekce 14
- GPU driver auto-install — nikdy, nebezpečné
- Sdílení telemetrie — nikdy

## Rozhodnutí

### Jak kategorizovat selhání

`SharpDXException` má vlastnost `HResult` (typ `int`). Mapujeme:

| HRESULT | Kind | Zpráva pro usera |
|---|---|---|
| `DXGI_ERROR_NOT_FOUND` (0x887A0002) | `NoOutputs` | "Couldn't find a connected display." |
| `DXGI_ERROR_UNSUPPORTED` (0x887A0004) | `DriverIssue` | "Your display driver doesn't support DX11. Update it from the GPU maker's website." |
| `DXGI_ERROR_DEVICE_REMOVED` (0x887A0005) | `DriverIssue` | "GPU driver restarted. Restart PlexusX to retry." |
| `DXGI_ERROR_DEVICE_RESET` (0x887A0006) | `DriverIssue` | "GPU recovered from a hang. Restart PlexusX to retry." |
| `DXGI_ERROR_DRIVER_INTERNAL_ERROR` (0x887A0020) | `DriverIssue` | "GPU driver crashed. Update or reinstall the driver." |
| `E_FAIL` (0x80004005) | `Unknown` | "DX11 init failed for an unknown reason." |
| `E_OUTOFMEMORY` (0x8007000E) | `OutOfMemory` | "Not enough GPU memory. Close other 3D apps." |
| `DXGI_ERROR_SDK_COMPONENT_MISSING` | `SdkIssue` | "DX11 runtime missing. Install the DirectX End-User Runtime from Microsoft." |

Fallback pro neznámé HRESULT: `Unknown` + "Restart PlexusX to retry."

### Auto-retry politika

- **Kdy:** jen při splash (startup), ne v průběhu session.
- **Kolikrát:** max 3 pokusy s 5s delay mezi.
- **Kdy NE:** pokud `ManualOverrideActive == true` (user ví co dělá) NEBO
  pokud `DxInitFailureKind == DriverIssue && previousAttempts >= 1` (opakovaný
  driver problém se auto-retry nevyřeší).
- **UI:** Splash status: "Retrying DX11 in 5 seconds (attempt 2/3)..."

### Settings page copy

Místo jednoho řádku se zobrazí blok:
```
Display engine: Fallback
  Why:  Display driver doesn't support DX11
       (HRESULT 0x887A0004: DXGI_ERROR_UNSUPPORTED)
  Try:  Update your GPU driver from nvidia.com / amd.com / intel.com
        Close any 3D overlay apps (MSI Afterburner, RTSS, etc.)
        Then click "Retry display engine" below.
  [ Retry display engine ]   [ Open Windows Display settings ]
```

Akce jsou dva buttons: Retry (stávající — restartuje proces) + Open Windows
Display settings (Win+Ctrl+D nebo `ms-settings:display` URI).

## API

```csharp
public enum DxInitFailureKind
{
    None,             // DX11 succeeded
    NoCompatibleAdapter,  // no GPU supports D3D11
    DeviceCreationFailed, // adapter exists but D3D11 init threw
    NoOutputs,            // no display attached
    DriverIssue,          // driver-side problem (HRESULT mapped)
    SdkIssue,             // DX runtime missing
    OutOfMemory,
    Unknown,
}

// In AppSettings:
public DxInitFailureKind DxFailure { get; set; }
public string DxFailureMessage { get; set; }

// In DxDevice:
public DxInitFailureKind LastFailure { get; }
public string LastFailureMessage { get; }
```

## Změny v existujícím kódu

### `DxDevice.cs`

- `new Device(adapter, BgraSupport)` try/catch: zachytit `SharpDXException`,
  vytáhnout `HResult`, přiřadit `DxInitFailureKind`, uložit do
  `LastFailure` / `LastFailureMessage`
- Venkovní catch: totéž pro `Exception ex` → kind `Unknown` + message z ex
- `Factory2` try/catch: pokud `GetAdapter1(0)` sama vyhodí, kind `NoCompatibleAdapter`
- **Nově**: přidat `public DxInitFailureKind LastFailure` + `LastFailureMessage`

### `TrayApplicationContext.cs`

- Po `TryCreateOverlay()` uložit `_settings.DxFailure = _overlay.DxFailure` +
  `_settings.DxFailureMessage = _overlay.DxFailureMessage`
- **Splash retry**: pokud `_settings.DxFailure != None && !ManualOverrideActive`
  a `previousAttempts < 3`, splash zobrazí "Retrying in 5s..." a zavolá
  `Application.Restart()` (nebo ekvivalent: Process.Start + Exit)
- **NEFUNGUJE napoprvé** — `Application.Restart()` neumožní splash pauzu.
  Jednodušší varianta: Settings page Retry button je dost dobrý pro v0.9,
  splash retry dáme do v0.9.5.

### `Pages/SettingsPage.cs`

- Místo jednoho řádku "Display engine: Fallback (hidden from ...)" vložit
  `FailureCard` — vlastní panel s kategorií, zprávou, akcemi
- **Fallback**: pouze zobrazit zprávu, žádný extra UI když `DxFailure == None`

## Testy

Nové soubory:
- `tests/VibranceHud.Tests/DxInitFailureMappingTests.cs`

Testy:
1. `MapHResult_NoOutputs_0x887A0002_returns_NoOutputs_kind`
2. `MapHResult_DriverUnsupported_0x887A0004_returns_DriverIssue`
3. `MapHResult_Unknown_returns_Unknown_kind`
4. `DxDevice_when_init_fails_sets_LastFailure_to_known_kind`
5. `SettingsPage_with_DxFailure_Unknown_renders_action_text`

## Akceptační kritéria

- [ ] `dotnet test` — 340/340 PASS (337 + 3 nové)
- [ ] `dotnet build -c Release` — čistý
- [ ] Manuální test: vynutit DX11 selhání (odinstalovat GPU driver? těžké
      v testu), ověřit že Settings zobrazí správný `DxInitFailureMessage`
- [ ] Smoke test: nastavit `DxFailure` v `settings.json` ručně na
      `DriverIssue`, spustit PlexusX, ověřit že Settings page zobrazí
      driver-issue text

## Rizika

- **HRESULT mapování není úplné** — DirectX má ~50 různých chybových kódů.
  Mitigation: neznámé HRESULT spadne do `Unknown` + zobrazí hex kód,
  takže support vidí přesnou hodnotu.
- **Settings page může být zahlcená textem** — krátké zprávy, 1-2 řádky
  na položku, ne odstavce.
- **Localization** — zprávy jsou EN. Pro v1.0 internationalizace, teď
  necháme anglicky (uživatelé jsou EN-speaking gamers).

## Co dělá další

Po schválení → implementation plan → kód → testy → release notes.