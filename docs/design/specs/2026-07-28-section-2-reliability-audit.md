# Sekce 2 — Reliability audit

**Datum:** 2026-07-28
**Status:** Průběžná (ne finished — nekonečný proces)
**Sekce:** 2 / 14

## Co to je

Audit každého interaktivního prvku v PlexusX: dělá to, co jeho popisek říká?
Kde ne, opravit. Kde to spadne tiše, opravit. Kde to visí na špatném předpokladu,
opravit.

## Co bylo opraveno tuto session (2026-07-28)

### 1. Check-for-updates double-launch guard

`UpdateService.CheckManuallyAsync` — dva rychlé kliknutí spouštěly dva paralelní
Tasky, což způsobovalo dva downloady a dva MessageBoxy. Přidán `lock(_checkLock)`
guard s `bool _isChecking` flagem.

### 2. Hotkey collision detection

`TrayApplicationContext.ReRegisterHotkey` a `TryRegisterMainHotkey` — pokud user
nastaví quick hotkey a main hotkey na stejnou kombinaci, Windows tiše odmítne
druhou registraci. Přidána detekce kolize + viditelné upozornění v tray menu
("Quick vibrance (conflicts with main window)"). Přidáno i hlášení, když
RegisterHotKey vrátí false z jiných důvodů (jiná app vlastní tu kombinaci).

## Co zbývá (další iterace)

Tyto nebyly tuto session opraveny — jsou v backlogu:

- **Crosshair Save as... flow** — `Pages/CrosshairPage.cs` má vlastní input
  dialog, nekonzistentní s ostatními dialogy v app
- **FPS Tweaks Apply** — co když je potřeba admin? Vidí user co se stalo?
- **Custom theme extraction edge cases** — `Theming/ImagePalette.cs` — co
  když image nemá dominantní barvu?
- **Onboarding save/cancel** — `OnboardingForm.cs` — co když user zavře
  formulář během step přechodu?
- **WhatsNewWindow modal** — blokuje startup? Testováno?

## Postup

Section 2 je průběžný audit. Každá další session by měla projít další kategorii
prvků, najít nové bugy, opravit. Není to "jednou hotovo" — je to disciplína.

Acceptance pro tuto session: 337/337 unit testů PASS, build clean, dva
identifikované bugy opraveny.