namespace VibranceHud.Monitors
{
    /// <summary>
    /// A control on the monitor itself - the things behind its own menu buttons.
    ///
    /// Not every monitor has every one of these, and there is no list we can look them up in:
    /// the only way to know is to ask the monitor and see whether it answers. So the UI is
    /// built from what came back, never from this enum.
    /// </summary>
    public enum MonitorSetting
    {
        /// <summary>The backlight. Unlike the app's own Brightness slider this changes how much
        /// light the panel actually emits, so dark areas genuinely get brighter.</summary>
        Brightness,

        Contrast,

        Red,
        Green,
        Blue,

        Sharpness,

        /// <summary>Speaker volume, on the monitors that have speakers.</summary>
        Volume,

        /// <summary>The monitor's own picture modes - FPS, sRGB, Movie, whatever it shipped
        /// with. The numbers mean different things on different monitors, so this is offered
        /// as "preset 1..n" rather than pretending to know the names.</summary>
        Preset,

        /// <summary>Which cable the monitor is showing. Lets someone switch between their PC
        /// and a console without reaching behind the screen.</summary>
        InputSource,
    }
}
