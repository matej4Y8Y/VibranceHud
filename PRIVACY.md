# PlexusX — Privacy

Last updated 2026-08-08. Controller: [[LEGAL_ENTITY]].

This describes what PlexusX actually does, written from the code rather than from a
template. If you find something here that does not match the application's behaviour,
that is a bug and we want to hear about it.

## The short version

PlexusX has **no analytics, no telemetry, and no account**. It never uploads your
settings, your usage, or anything about your machine. Every network request it makes is
a download, and there are only three of them.

## What is stored, and where

All of it stays on your PC.

| What | Where |
|---|---|
| Your colour settings, theme, hotkeys, crosshair, window position | `%APPDATA%\PlexusX\settings.json` |
| Your licence | `%LOCALAPPDATA%\PlexusX\license.json` |
| Per-game colour profiles | `%LOCALAPPDATA%\PlexusX\profiles.json` |
| Cached update / status / revocation checks | `%LOCALAPPDATA%\PlexusX\*.json` |
| Crash reports | `%LOCALAPPDATA%\PlexusX\crashes\` |

Uninstalling removes the application. To remove the data as well, delete the two
`PlexusX` folders above.

## What leaves your machine

Three outbound requests. All are plain downloads of public files — none of them send
anything about you, and none of them carry an identifier.

1. **Update check** — `api.github.com`, for the latest release of the PlexusX
   repository. GitHub will see your IP address, as it would for any download.
2. **Status check** — a public `app-status.json` on `raw.githubusercontent.com`, used to
   tell you when a beta build has expired.
3. **Licence revocation list** — a public `license-revocations.json` on
   `raw.githubusercontent.com`, so a licence that has been withdrawn stops working.

That is the complete list. There is no request that uploads anything.

## Your PC id

Your licence is tied to one computer. To do that, PlexusX computes a **one-way SHA-256
hash** of four things:

- your CPU's identifier,
- your first disk's serial number,
- your Windows computer name,
- your Windows user name.

The hash is truncated to 16 characters and shown to you as your **PC id**. The four
inputs are never written to disk and never sent anywhere — only the hash exists, and a
hash cannot be reversed back into your user name or your hardware.

**The PC id leaves your machine only when you send it.** To get a licence you copy it and
give it to us yourself; nothing transmits it automatically. It is then embedded in the
signed licence file so the application can check it is running on the right computer.

Because the computer name and user name are part of the input, renaming your PC or your
Windows account changes your PC id, as does changing your CPU or boot drive. If that
happens, ask us to release the licence from the old machine.

## Crash reports

When PlexusX crashes it writes a file locally containing the error, the stack trace, the
application version and your Windows version. **It is not uploaded.** If you choose to
send one, you are sending it yourself, and you can read it first — it is a plain text
file.

Old crash logs are deleted automatically.

## What is never collected

No browsing history. No keystrokes. No screenshots or screen contents. No information
about which games you own or play. No advertising identifiers. No profiling, and no
automated decision-making.

## Children

PlexusX is not directed at children under 13 and does not knowingly collect anything
from them. Since it collects nothing from anyone, this is a statement of intent rather
than a data practice.

## Your rights under the GDPR

Because everything is stored locally on your own computer and nothing is transmitted, we
hold no personal data about you and there is nothing for us to export or erase on your
behalf. You have complete control: the files listed above are yours, on your disk, and
deleting them deletes everything.

The one exception is the licence record. If you have bought a licence, we hold the
licence key, the plan, the hashed PC id it is bound to, and whatever note you gave us
when you asked for it (usually a Discord name). That is kept for as long as the licence
is valid, plus the period we are required to retain records of a sale.

To ask what we hold, correct it, or have it erased, contact [[LEGAL_ENTITY]]. You also
have the right to complain to your national data protection authority.

## Changes

If this policy changes, the date at the top changes with it, and the current version
ships inside the application under **Settings → Legal & licences**.
