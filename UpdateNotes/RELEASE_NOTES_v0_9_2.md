# Release Notes - PlexusX v0.9.2

**Datum:** 2026-07-29
**Typ:** Patch - Robust auto-update pipeline rewrite

## Co je nového

### Robust auto-update pipeline

`UpdateService` kompletně přepsán. Update by měl fungovat **vždy**:

- **Retry s exponential backoff** - 3 pokusy na kaŽý zdroj, 5s/10s/20s
- **Multi-source fallback** - GitHub Releases → Gist mirror → Raw mirror. Jeden zdroj selže, druhý náhradí.
- **SHA256 verification** - corrupted downloady se odhalí před spuštěním
- **PE header + file version check** - trojitá kontrola před spuštěním
- **GitHub re-check před launch** - odmítne spustit starší installer i kdyby nějak proklouzl
- **Background update checker** - běží kaŽých 6 hodin, ETag-cached HEAD request (lehký)
- **Systray notification** - user dostane upozornění když je update k dispozici
- **LastDownloadError diagnostika** - MessageBox ukáže **proč** selhalo ("SHA256 mismatch", "Truncated download", atd.)
- **Embedded release notes** - "What's new" dialog nikdy neukáže prázdný text, fallback na notes v resources

### Opravené bugy

- Update selhal uživatelům na 0.7.x/0.8.x bez zjevného důvodu (rate limit, antivirus, CDN redirect)
- "What's new" dialog ukazoval staré/empty notes
- Update se vrátil na starou verzi bez varování (downgrade bug)

## Statistiky

- 345 testů pass (8 nových pro update pipeline v3)
- 0 warnings, 0 failures
- Build clean

## Kompatibilita

Všechny existující nastavení (AppSettings.json, license.json, profily) zůstávají kompatibilní.
