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
        public int OpacityPercent { get; set; } = 85;
        /// <summary>Legacy light/dark flag. Kept only to migrate old saved settings into
        /// <see cref="ThemeName"/>; new code reads ThemeName.</summary>
        public bool LightTheme { get; set; }

        /// <summary>Selected theme name (e.g. "Violet", "Emerald"). Empty on old/fresh
        /// installs, resolved via <see cref="ThemeCatalog.Resolve"/>.</summary>
        public string ThemeName { get; set; } = "";
        public int BrightnessPercent { get; set; } = 100;
        public int GammaPercent { get; set; } = 100;
        public bool EyeCare { get; set; }

        /// <summary>Which overlay mechanism was actually active last launch (DX11, or the
        /// Magnification-API fallback if DX11 init failed). Surfaced on the Settings page so
        /// a silent fallback - which is invisible to screen-capture tools like OBS/Discord -
        /// isn't hidden from the user. See <see cref="OverlayModeResolver"/>.</summary>
        public OverlayMode OverlayMode { get; set; } = OverlayMode.Dx;

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

        // ---- NVIDIA driver tweaks (Rust) ----

        /// <summary>Ids of the NVIDIA driver tweaks currently applied to Rust's profile.</summary>
        public System.Collections.Generic.HashSet<string> RustNvidiaTweaks { get; set; } = new();

        /// <summary>Target for the steady frame cap, in FPS.</summary>
        public int RustFpsCap { get; set; } = 90;

        /// <summary>Last version that showed its "what's new" notes.</summary>
        public string LastSeenVersion { get; set; } = "";

        /// <summary>False until the first-run onboarding has been completed once.</summary>
        public bool OnboardingComplete { get; set; }

        /// <summary>What the user said they play during onboarding (for light personalization).</summary>
        public string FavoriteGame { get; set; } = "";

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
    }
}
