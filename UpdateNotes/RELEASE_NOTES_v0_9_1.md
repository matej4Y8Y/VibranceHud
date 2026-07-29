# Release Notes — PlexusX v0.9.1

**Datum:** 2026-07-29
**Typ:** Patch release (auto-update fix + activation key gate)

## Co je nového

### 🔧 Update pipeline rewrite

`UpdateService` přepsán kompletně. Opravuje bug kdy přítel updatoval z 0.7.x na 0.9.0 a zůstal na staré verzi.

**Příčina:** `RecoverStrandedInstaller` v recovery scanu bral **jakýkoliv** `PlexusX-Setup-*.exe` z `%TEMP%` který měl vyšší verzi než current, **bez ověření** že odpovídá nejnovějšímu GitHub release. Pokud selhalo stažení nového installeru, recovery mohl najít a spustit starý/stale soubor.

**Oprava v 0.9.1:**
- ✅ **Atomic download** — `.partial` + rename. Truncated download neskončí jako validní installer.
- ✅ **GitHub re-check** — `RunPendingUpdateIfAnyAsync` ověří verzi proti `releases/latest` **před** spuštěním. Starší instalátor se **smaže**, ne nainstaluje.
- ✅ **PE resource version** — verze se čte z FileVersionInfo (PE resource), ne z filename (který může být zastaralý).
- ✅ **Detailed error** — `LastDownloadError` zobrazí v MessageBoxu proč selhal download ("Truncated download: got X of Y bytes", "Not a valid Windows executable", atd.)

### 🔑 Activation key gate (nový)

- App se **nespustí** bez platného klíče (LICENSE GATE).
- Pokud licence chybí → **Account tab** se ukáže, ostatní taby (Vibrance, Games, FPS Tweaks, Crosshair, Settings, Set Profile) jsou **skryté**.
- **Modalní Activation dialog** se zobrazí s 23-char klíčem (formát `YYYY-R-T-BODY-XXXX-XXXX`).
- Anti-tamper: HMAC-SHA256 s 100k KDF iterací, hardware-bound (CPU + disk + machine name), debugger detection.
- Deactivate tlačítko v Account tab → vrátí do blocked stavu.

### 🎨 Plexus-style UI restyle

- Default theme = **Light** (černobílý, pure white/black/grey).
- Activation dialog restyled: žádné fialové akcenty, jen Theme.* barvy.
- Settings page "Retry display engine" tlačítko: AutoSize fix (useknutý text opraven).

### 🛠 Set Profile redesign

- Dřív side-panel slide-in, teď **normální tab** jako ostatní.
- Nový nav button "Profile Editor" v sidebaru.
- AutoScroll reset při přepnutí zpět na tab (jinak GAME sekce mimo viewport).

## Statistiky

- **337 testů pass** (4 nové regression testy pro update pipeline, 1 pro license round-trip)
- **0 warnings, 0 failures**
- Build clean

## Známé limitace

- **Bez EV certifikátu** — SmartScreen bude varovat při prvním spuštění. Přátelům řekni: "More info → Run anyway"
- **Bez GDPR/EULA** — komerční launch to bude potřebovat
- **License keys jsou per-PC** — každý klíč je vázaný na jeden hardware fingerprint. Pokud někdo mění PC, potřebuje nový klíč.

## Pro vývojáře

Pokud instaluješ z Gitu a měl jsi `PlexusX-Setup-0.9.0.exe` v `%TEMP%`, **smaž ho** — nová verze ho stejně odmítne.
