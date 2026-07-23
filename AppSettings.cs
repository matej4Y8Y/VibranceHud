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
        public bool LightTheme { get; set; }
    }
}
