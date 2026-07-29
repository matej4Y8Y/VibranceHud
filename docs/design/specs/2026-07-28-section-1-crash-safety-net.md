# Sekce 1 — Crash safety net

**Datum:** 2026-07-28
**Status:** Návrh
**Sekce:** 1 / 14 (z `docs/ROADMAP-v1.0.md`)

## Problém

Když PlexusX spadne (cokoli od NVAPI chyby po chybu v UI handleru), proces tiše
zmizí. Žádný log, žádný dialog. Pro 100+ lidí znamená každý pád = "ten program je
rozbitý, mažu ho".

## Cíl

Každá nezachycená výjimka = dialog s textem + crash log na disk. Staré logy se
automaticky mažou po 30 dnech.

## Scope

### Co patří do Sekce 1

1. **`CrashLog.cs`** — nová třída, zapíše výjimku + stack trace do souboru
2. **Integrace s `Program.cs`** — `ShowFatal()` nejdřív zapíše log, pak ukáže dialog
   s cestou k logu
3. **Cleanup** — logy starší 30 dní smazat při startu aplikace
4. **Testy**:
   - `CrashLog.Write()` vytvoří soubor ve správném adresáři, obsahuje stack trace
   - `CrashLog.Cleanup()` smaže logy starší 30 dní, nechá novější
   - Unit test na `CrashLog.Cleanup()` s mock časem (aby test nezávisel na reálném
     datu)

### Co do Sekce 1 NEpatří

- Cloud crash reporting (Sentry apod.) — Sekce 14
- Auto-restart po pádu — user si restartuje sám
- Telemetrie — nikdy
- Crash log upload do GitHub Issues — Sekce 14

## Rozhodnutí

### Kde ukládat logy

`%LocalAppData%\PlexusX\crashes\`

Důvod: `%AppData%` = uživatelská data (settings, themes). `%LocalAppData%` =
strojově generované (cache, logy, profiles). Crash logy do druhé skupiny —
při uninstallu jdou pryč s aplikací, necestují přes reinstall/update.

### Formát logu

Plain text, jeden soubor na pád. Jméno: `crash-YYYY-MM-DD-HHMMSS.txt`.

Obsah:
```
PlexusX crash report
Generated: 2026-07-28 22:35:12 UTC
Version: 0.9.0-rc1
OS: Microsoft Windows 11 Pro 10.0.22631
.NET: 8.0.x

Exception type: System.InvalidOperationException
Message: Couldn't initialize DX11 overlay
Stack trace:
   at VibranceHud.DxOverlay..ctor()
   at VibranceHud.TrayApplicationContext..ctor()
   ...
```

### Žádný PII

Stack trace NESMÍ obsahovat:
- Cestu k `C:\Users\<name>\...` — nahradit `<user>`
- Cestu k Steam/Epic hrám — nahradit `<game-path>`
- Registry klíče s user SID — nahradit `<sid>`

### Cleanup politika

- Spustí se při startu aplikace (v `Program.Main` po `EnableVisualStyles`)
- Maže soubory starší 30 dní podle data v názvu
- Při startu nikdy nevyhodí výjimku — celé v `try/catch`

## API

```csharp
public static class CrashLog
{
    // Cesta ke složce s crash logy. Vytvoří ji, pokud neexistuje.
    public static string CrashDirectory { get; }

    // Zapíše crash log, vrátí cestu k souboru. Nikdy nevyhodí výjimku.
    public static string Write(Exception ex);

    // Smaže logy starší 30 dní. Nikdy nevyhodí výjimku.
    public static void Cleanup();
}
```

## Změny v existujícím kódu

### `Program.cs`

- `ShowFatal()` přidá volání `CrashLog.Write(ex)` PŘED `MessageBox.Show()`
- Do textu dialogu přidá cestu k logu (uživatel ji může zkopírovat do bug reportu)
- Přidá `CrashLog.Cleanup()` na začátek `Main()` (hned po `STAThread` kontrole
  hlavy, před `Application.SetHighDpiMode`)

## Testy

Nové soubory:
- `tests/VibranceHud.Tests/CrashLogTests.cs`

Testy:
1. `Write_createsFile_withStackTraceInContent` — vyhoď výjimku, ověř že soubor
   existuje a obsahuje stack trace
2. `Write_redactsUserPath` — výjimka s cestou `C:\Users\Jmeno\...`, ověř že log
   obsahuje `<user>` ne `Jmeno`
3. `Cleanup_deletesOldLogs_keepsRecent` — mock čas, vytvoří 5 souborů s různým
   datem, ověř že staré smazal a nové nechal

## Akceptační kritéria

- [ ] `dotnet test` — 335/335 PASS (332 + 3 nové)
- [ ] `dotnet build -c Release` — čistý
- [ ] Manuální test v dev buildu: vyvolej výjimku → objeví se dialog s cestou k
      logu, log existuje na `%LocalAppData%\PlexusX\crashes\`
- [ ] Cleanup: simuluj staré logy, ověř že jsou smazány

## Rizika

- **Win32 file locks**: Pokud antivir drží log otevřený, `File.WriteAllText` může
  selhat. Mitigation: zabalit do `try/catch` a tiše pokračovat.
- **Disková kvóta**: Teoreticky může userům zaplnit `%LocalAppData%`. Mitigation:
  cleanup při startu + max 50 souborů (když jich je > 50, smazat nejstarší
  bez ohledu na věk).
- **PII únik přes env variables**: Stack trace může obsahovat env vars jako
  `%USERNAME%`. Mitigation: regex scan + redact známých env var jmen.

## Co dělá další

Po schválení tohoto specu → napíšu implementation plan (krok po kroku) →
implementuju → testy → commit.