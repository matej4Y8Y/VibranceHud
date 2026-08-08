using System.Collections.Generic;

namespace VibranceHud
{
    /// <summary>
    /// Everything the app remembers between runs. Persisted as JSON by
    /// <see cref="SettingsStore"/>.
    /// </summary>
    public sealed class AppSettings
    {
        /// <summary>Legacy combined 0-200 vibrance slider. Kept only so old saved settings
        /// migrate into <see cref="VibrancePercent"/> + <see cref="SaturationPercent"/>;
        /// new code reads the resolved properties below.</summary>
        public int Level { get; set; } = 100;

        /// <summary>Driver Digital Vibrance, 0-100. Null on settings written before
        /// vibrance and saturation became separate controls.</summary>
        public int? VibrancePercent { get; set; }

        /// <summary>Software colour-matrix saturation, 0-200 (100 = untouched). Null on
        /// settings written before the split.</summary>
        public int? SaturationPercent { get; set; }

        /// <summary>Vibrance to actually use, migrating a legacy <see cref="Level"/> the
        /// same way the old engine split it internally - so upgrading looks identical.</summary>
        public int ResolvedVibrance => VibrancePercent ?? System.Math.Min(Level, 100);

        /// <summary>Saturation to actually use. A legacy level only drove the software
        /// matrix above 100; below that the matrix was neutral.</summary>
        public int ResolvedSaturation => SaturationPercent ?? (Level > 100 ? Level : 100);

        public bool StartWithWindows { get; set; }

        /// <summary>
        /// Hold a 1x1 topmost window so Windows keeps compositing the desktop, which is what
        /// makes the colour effect appear in screen capture (OBS, Discord share, Medal). See
        /// <see cref="CompositionKeeper"/> for why this is needed and why it varies by machine.
        ///
        /// Defaults OFF. It was shipped on, on the theory that forcing composition would put
        /// the colour effect into what capture tools read - but the effect currently comes from
        /// the Magnification API, which applies *after* composition, so there was nothing in the
        /// composed frame for this to help with. Confirmed by a user on a 3060 Ti: no change.
        ///
        /// Kept rather than deleted because it becomes necessary the moment the DX11 overlay
        /// actually renders - that path draws into the composed frame, and would then be
        /// bypassed by Independent Flip without this. Until then it's an always-topmost window
        /// for no benefit, which isn't worth the risk of upsetting a fullscreen game.
        /// </summary>
        public bool KeepDesktopComposited { get; set; } = false;
        public int OpacityPercent { get; set; } = 85;
        /// <summary>Legacy light/dark flag. Kept only to migrate old saved settings into
        /// <see cref="ThemeName"/>; new code reads ThemeName.</summary>
        public bool LightTheme { get; set; }

        /// <summary>Selected theme name (e.g. "Violet", "Emerald"). Empty on old/fresh
        /// installs, resolved via <see cref="ThemeCatalog.Resolve"/>.</summary>
        public string ThemeName { get; set; } = "";
        public int BrightnessPercent { get; set; } = 100;
        public int GammaPercent { get; set; } = 100;
        public int ContrastPercent { get; set; } = 100;

        /// <summary>-100 (cool) to +100 (warm). Nullable so a settings file saved before
        /// this control existed migrates from the old <see cref="EyeCare"/> switch instead
        /// of silently resetting everyone's screen to neutral on upgrade.</summary>
        public int? Temperature { get; set; }

        /// <summary>Legacy on/off eye-care switch. New code reads
        /// <see cref="ResolvedTemperature"/>; kept so an old settings file still migrates.</summary>
        public bool EyeCare { get; set; }

        /// <summary>Temperature to actually use: the saved value if this settings file has
        /// ever been through the new control, otherwise the old switch mapped onto the same
        /// scale it always produced.</summary>
        public int ResolvedTemperature =>
            Temperature ?? (EyeCare ? VibranceEngine.EyeCareTemperature : 0);

        /// <summary>Which overlay mechanism was actually active last launch (DX11, or the
        /// Magnification-API fallback if DX11 init failed). Surfaced on the Settings page so
        /// a silent fallback - which is invisible to screen-capture tools like OBS/Discord -
        /// isn't hidden from the user. See <see cref="OverlayModeResolver"/>.</summary>
        public OverlayMode OverlayMode { get; set; } = OverlayMode.Dx;

        /// <summary>Categorised reason DX11 init failed last launch. Used by the
        /// Settings page to show an actionable message instead of "Fallback
        /// mode" with no context. Persisted so the warning survives a restart
        /// (the user may not look at Settings until after the failure).</summary>
        public DxInitFailureKind DxFailure { get; set; } = DxInitFailureKind.None;

        /// <summary>Short, human-readable label for the last DX11 failure
        /// ("Display driver doesn't support DX11" etc.). Kept short because
        /// it lives in a single-line Settings card alongside the kind.</summary>
        public string DxFailureMessage { get; set; } = "";

        /// <summary>Raw HRESULT behind <see cref="DxFailure"/>, 0 when there wasn't one.
        /// Only the category used to be kept, so every failure the mapper didn't recognise
        /// became "Unknown" with nothing left to investigate - while the hint told the user
        /// to report the code.</summary>
        public int DxFailureCode { get; set; }

        // ---- Custom image theme ----

        /// <summary>File name of the background image inside the app's own data folder.
        /// The picked file is copied there, so moving or deleting the original can't
        /// break the theme. Empty = no custom background.</summary>
        public string CustomBackgroundFile { get; set; } = "";

        /// <summary>How far the image is darkened (0-80) so the UI stays readable.
        /// Auto-set from the image's brightness on upload, then user-adjustable.</summary>
        public int CustomBackgroundDim { get; set; } = 40;

        /// <summary>How far the background image is softened (0-100). Blur makes busy
        /// wallpapers sit behind the UI without fighting it.</summary>
        public int CustomBackgroundBlur { get; set; }

        /// <summary>The accent extracted from the image, cached so startup doesn't have to
        /// re-scan it. 0 = not derived yet.</summary>
        public int CustomAccentArgb { get; set; }

        // ---- Crosshair overlay ----

        public bool CrosshairEnabled { get; set; }

        /// <summary>The crosshair currently being used / edited.</summary>
        public Crosshair.CrosshairConfig ActiveCrosshair { get; set; } = new();

        /// <summary>The user's named crosshairs, switched manually.</summary>
        public System.Collections.Generic.List<Crosshair.CrosshairConfig> SavedCrosshairs { get; set; } = new();

        /// <summary>Gallery entries the user hearted, by id. They sort to the top of the grid,
        /// so somebody who has found their two or three does not scroll past thirty every
        /// time.</summary>
        public System.Collections.Generic.List<string> FavouriteCrosshairs { get; set; } = new();

        // ---- NVIDIA driver tweaks (Rust) ----

        /// <summary>Ids of the NVIDIA driver tweaks currently applied to Rust's profile.</summary>
        public System.Collections.Generic.HashSet<string> RustNvidiaTweaks { get; set; } = new();

        /// <summary>
        /// Ids of NVIDIA driver tweaks the user has confirmed the driver actually accepts,
        /// captured by the Scan button on the Rust page. Empty on a fresh install means
        /// "no scan yet" - the card then shows every tweak the tier allows, so Scan is
        /// opt-in, not a prerequisite for the card to be useful. Always non-null so the
        /// UI can iterate without null-checks every page load.
        /// </summary>
        public System.Collections.Generic.HashSet<string> NvAppSupportedTweaks { get; set; } = new();

        /// <summary>
        /// Ids of NVIDIA driver tweaks whose DRS save failed with access-denied on a
        /// standard user account. Recorded so the Rust page surfaces the "Apply as admin"
        /// button on the very first render after the failure, instead of waiting for the
        /// user to retry and only then discovering the elevation is needed. Cleared
        /// automatically when the elevated re-apply succeeds.
        /// </summary>
        public System.Collections.Generic.HashSet<string> RustNvidiaTweaksNeedsAdmin { get; set; } = new();

        /// <summary>Path to a downloaded installer that's waiting to be run. The PlexusX
        /// startup sequence checks this BEFORE the main window opens and runs the
        /// installer (which then closes PlexusX, replaces files, relaunches). This is
        /// the only reliable way to self-update - launching the installer while PlexusX
        /// is running either deadlocks or fails silently because Windows blocks silent
        /// installs from a live parent process.</summary>
        public string PendingUpdateInstaller { get; set; } = "";

        /// <summary>The version the <see cref="PendingUpdateInstaller"/> is for, so we can
        /// show "update to vX.Y.Z" on the splash when the install kicks off.</summary>
        public string PendingUpdateVersion { get; set; } = "";

        /// <summary>Target for the steady frame cap, in FPS.</summary>
        public int RustFpsCap { get; set; } = 90;

        /// <summary>Last version that showed its "what's new" notes.</summary>
        public string LastSeenVersion { get; set; } = "";

        /// <summary>False until the first-run onboarding has been completed once.</summary>
        public bool OnboardingComplete { get; set; }

        /// <summary>What the user said they play during onboarding (for light personalization).</summary>
        public string FavoriteGame { get; set; } = "";

        /// <summary>
        /// The game the app is currently pointed at; empty means Desktop (no game).
        ///
        /// A UI selection only - it decides what the Game tab and Profile Editor show, never
        /// what auto-apply does. See <see cref="Games.GameSelection"/>.
        /// </summary>
        public string CurrentGameId { get; set; } = "";

        /// <summary>
        /// Per-game desktop resolution rules, applied on launch and undone on exit.
        ///
        /// Lives here rather than in the game profile because it is a display setting, not a
        /// colour one - it belongs with the Monitor tab that owns it, and it has to work for
        /// people who never touch profiles.
        /// </summary>
        public List<MonitorRule> MonitorRules { get; set; } = new();

        /// <summary>
        /// Key → command bindings, per game.
        ///
        /// Held here rather than only in the game's config so the app can show you what you
        /// set up without parsing a file it does not own, and so the bindings survive a game
        /// reinstall wiping its cfg folder.
        /// </summary>
        public List<Keybinds.Keybind> Keybinds { get; set; } = new();

        // Rust launch boosts
        public bool RustHighPriority { get; set; } = true;
        public bool RustTrimLauncher { get; set; }

        /// <summary>Desktop resolution to switch to when launching Rust (0 = leave it alone).</summary>
        public int RustResolutionWidth { get; set; }
        public int RustResolutionHeight { get; set; }

        // Audio Edge (peak limiter)
        public bool AudioEdgeEnabled { get; set; }

        /// <summary>The loudness ceiling, 5-100 (%). Quiet sounds are untouched; anything
        /// louder is pulled down to this, so footsteps and gun shots end up level.</summary>
        public int AudioEdgeThresholdPercent { get; set; } = 30;

        // ---- Quick vibrance hotkey ----

        /// <summary>Win32 RegisterHotKey modifier mask (MOD_ALT|MOD_CONTROL|MOD_SHIFT|MOD_WIN
        /// bits; see HotkeyPicker.Modifiers for the constants). Default: Ctrl+Alt.</summary>
        public uint HotkeyModifierMask { get; set; } = HotkeyModifiers.Control | HotkeyModifiers.Alt;

        /// <summary>The non-modifier key the user picks (V = 0x56 by default, so the
        /// out-of-the-box behaviour matches the hardcoded Ctrl+Alt+V the app shipped with).
        /// Range: 0x30..0x5A (top-row 0-9, A-Z), 0x60..0x87 (numpad 0-9), 0x70..0x7B (F1-F12),
        /// plus a handful of named keys.</summary>
        public uint HotkeyVirtualKey { get; set; } = HotkeyKeys.V;

        // ---- Main window hotkey (separate from the quick-popup hotkey) ----

        /// <summary>Whether the user opted into a second global hotkey that opens the full
        /// main window. Off by default so existing users don't get a surprise binding on
        /// first launch after upgrade. The Vibrance page's "Main window" hotkey picker
        /// sets this to true the moment the user picks a combo.</summary>
        /// <summary>
        /// Bumped when a saved value changes meaning. 1 = vibrance on the software path was
        /// value/100, so its neutral sat at 100 instead of 50.
        /// </summary>
        public int VibranceScaleVersion { get; set; }

        /// <summary>Streaming Mode: put the whole effect through the colour matrix so
        /// capture can see it. Off by default - it costs a little image quality, and only
        /// someone who records has a reason to pay that.</summary>
        public bool StreamingMode { get; set; }

        public bool MainHotkeyEnabled { get; set; }

        /// <summary>Modifier mask for the main-window hotkey. Default: Ctrl+Shift (distinct
        /// from the popup's Ctrl+Alt+V default so the two never collide out of the box).</summary>
        public uint MainHotkeyModifierMask { get; set; } = HotkeyModifiers.Control | HotkeyModifiers.Shift;

        /// <summary>Virtual key for the main-window hotkey. Default: M (Ctrl+Shift+M).
        /// Zero means "no binding" until the user picks one.</summary>
        public uint MainHotkeyVirtualKey { get; set; } = HotkeyKeys.M;

        /// <summary>True when the user has manually tweaked values from the popup while a
        /// game profile is currently applied. The auto-apply coordinator consults this
        /// flag so a fresh launch of the same game doesn't immediately clobber the user's
        /// last manual override - the user has to opt back into the saved profile (or the
        /// flag has to expire). Cleared on PlexusX shutdown so it never persists across
        /// reboots.</summary>
        public bool ManualOverrideActive { get; set; }

        /// <summary>
        /// Where the user last dragged the quick-vibrance popup, in screen coordinates.
        ///
        /// <see cref="int.MinValue"/> means "never moved" - the popup opens centred, which is
        /// the behaviour it always had. Once someone moves it, it's because the middle of the
        /// screen was covering something they wanted to see, so reopening it back in the
        /// middle every time undoes the move they just made.
        ///
        /// Validated against the live monitor layout on load, so a saved spot on a monitor
        /// that has since been unplugged can't strand the popup off-screen.
        /// </summary>
        public int PopupX { get; set; } = int.MinValue;
        public int PopupY { get; set; } = int.MinValue;

        // ---- Main window placement ----

        /// <summary>
        /// Where the main window was last left, in screen coordinates.
        ///
        /// Zero width means "never saved", and the window then opens centred exactly as it
        /// always did. Validated against the live monitor layout on load - see
        /// <see cref="WindowBounds"/> - so a position on a monitor that has since been
        /// unplugged cannot strand the window somewhere unreachable.
        /// </summary>
        public int WindowX { get; set; }
        public int WindowY { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }

        /// <summary>True when the window was last closed maximized, so it returns that way.</summary>
        public bool WindowMaximized { get; set; }

        // ---- Advanced colour ----

        /// <summary>
        /// The advanced colour grade — highlights, shadows, whites, blacks, fade and split
        /// toning.
        ///
        /// Nullable so a settings file written before advanced colour existed resolves to
        /// neutral rather than to a zero-initialised grade, which would mean gamma 0. See
        /// <see cref="ToneSettings.ResolvedGamma"/> for why that distinction matters.
        /// </summary>
        public ToneSettings? Tone { get; set; }

        // ---- physical monitor (DDC/CI) ----
        //
        // -1 means "never set", which is different from 0. Zero brightness is a legitimate
        // value somebody could choose; not having chosen one means the page should read the
        // panel instead of assuming.

        /// <summary>
        /// What the user set on one physical panel, and where that panel was before we
        /// touched it.
        ///
        /// Per monitor, because a two-screen desk gets two cards and they must not share one
        /// value. The Original* fields are in the panel's OWN units, not percent - they are
        /// what gets written back verbatim by "Put it back", and a percentage would be
        /// re-derived through a range that may have been read differently.
        /// </summary>
        public sealed class PanelSettings
        {
            public int Index { get; set; }

            // -1 means never set, which is different from 0.
            public int Brightness { get; set; } = -1;
            public int Contrast { get; set; } = -1;
            public int LowBlue { get; set; } = -1;

            public bool HasOriginals { get; set; }
            public int OriginalBrightness { get; set; }
            public int OriginalContrast { get; set; }
            public int OriginalBlueGain { get; set; }
        }

        public List<PanelSettings> Panels { get; set; } = new();

        /// <summary>The record for one panel, created on first use so callers never null-check.</summary>
        public PanelSettings PanelFor(int index)
        {
            Panels ??= new List<PanelSettings>();

            var found = Panels.FirstOrDefault(p => p.Index == index);
            if (found != null) return found;

            found = new PanelSettings { Index = index };
            Panels.Add(found);
            return found;
        }

        /// <summary>The grade to actually apply, with gamma taken from the existing
        /// standalone setting so the two can never disagree. Not serialized - it is derived,
        /// and writing it would duplicate state that already has one home.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public ToneSettings ResolvedTone =>
            (Tone ?? ToneSettings.Neutral) with { Gamma = GammaPercent };
    }
}