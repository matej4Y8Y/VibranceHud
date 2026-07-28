# PlexusX 0.8.1 — NVIDIA Tweaks: Admin-Aware Apply

**Released:** 2026-07-28

## What changed

The NVIDIA Tweaks card on the Rust page used to surface every failed apply as the
opaque message "Driver didn't accept this setting." That was misleading: the most
common cause on a standard Windows user account is that NVAPI's per-profile save
target — `C:\ProgramData\NVIDIA Corporation\Drs\nvdrsdb0.bin` — is locked behind
admin, so the DRS session's `Save()` returns access-denied even though the driver
itself is happy to accept every value.

- `NvidiaApplyResult` is now a tri-state: `Success`, `NeedsAdmin`, or `Unsupported`.
  The wrapper catches the `NVIDIAApiException { Status = AccessDenied }` that NVAPI
  raises on the write and surfaces it to the UI instead of collapsing it into a
  boolean false.
- When a non-admin user's save comes back as `NeedsAdmin`, the row now shows a clear
  *"Run PlexusX as administrator to apply this — or click 'Apply as admin'"* hint
  in amber, with the **Apply as admin** button right next to the toggle.
- The button routes through a new `NvidiaTweakElevationService`, which relaunches
  PlexusX once with `runas` to perform exactly the one failing tweak (mirroring the
  HKLM FPS tweaks flow) and exits. One scoped UAC prompt, then the toggle goes
  "Applied."
- Ids that previously failed and still need elevation are now remembered in
  `AppSettings.RustNvidiaTweaksNeedsAdmin`, so the button stays visible on the
  next page open instead of looking broken-and-failed each launch.

## What stayed the same

- The Scan button still runs (and the supported-tweaks filter still hides toggles
  the driver version doesn't recognise).
- Toggles the user has already applied successfully keep their green "✓" state
  across launches — the new admin hint only appears for toggles that *failed*
  in the most recent session.
- No new NVAPI calls, no new dependencies.
- No anti-cheat surface: this is the same NVAPI that ships with the driver; the
  elevated relaunch only writes to NVIDIA's own profile database.

## What it fixes for users

For a standard (non-admin) user account where every NVIDIA tweak previously
reported "Driver didn't accept this setting," the flow now becomes:

1. Click a toggle. NVAPI refuses to save the DRS file → row shows the amber hint +
   **Apply as admin** button.
2. Click the button → UAC prompt.
3. After accept, the elevated helper writes the profile, exits 0, and the toggle
   shows "✓ Frame queue shortened" (or whatever the tweak's AppliedText is).

Without this fix those users see five toggles appear after Scan and every one of
them dead-ends; now at least one click gets them through to a working state.
