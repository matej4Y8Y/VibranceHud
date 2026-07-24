namespace VibranceHud
{
    /// <summary>
    /// Everything the app remembers between runs. Persisted as JSON by
    /// <see cref="SettingsStore"/>.
    /// </summary>
    public sealed class AppSettings
    {
        public int Level { get; set; } = 100;
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

        /// <summary>Last version that showed its "what's new" notes.</summary>
        public string LastSeenVersion { get; set; } = "";

        // Rust launch boosts
        public bool RustHighPriority { get; set; } = true;
        public bool RustTrimLauncher { get; set; }

        /// <summary>Desktop resolution to switch to when launching Rust (0 = leave it alone).</summary>
        public int RustResolutionWidth { get; set; }
        public int RustResolutionHeight { get; set; }
    }
}
