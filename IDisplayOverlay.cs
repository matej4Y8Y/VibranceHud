namespace VibranceHud
{
    /// <summary>Which concrete <see cref="ISaturationOverlay"/> implementation is actually
    /// running. DX11 is the preferred path (visible in screen capture); Mag is the silent
    /// fallback used when DX11 init fails (see <see cref="DxOverlay"/>).</summary>
    public enum OverlayMode
    {
        Dx,
        Mag,
    }

    /// <summary>
    /// Reports which underlying overlay mechanism is behind an <see cref="ISaturationOverlay"/>
    /// right now. <see cref="TrayApplicationContext"/> used to fall back from DX11 to the
    /// Magnification API silently - the user had no way to know their screen capture tool
    /// (OBS, Discord) would no longer show the saturation effect. Exposing this lets the
    /// Settings page surface it and lets the choice be persisted to <see cref="AppSettings"/>.
    /// </summary>
    public interface IDisplayOverlay
    {
        OverlayMode ActiveMode { get; }

        /// <summary>Categorised reason DX11 init failed (None if it succeeded
        /// or if this isn't the DX11 path). Read by TrayApplicationContext after
        /// construction and persisted to <see cref="AppSettings.DxFailure"/> so
        /// the Settings page can show an actionable reason.</summary>
        DxInitFailureKind LastFailure { get; }

        /// <summary>Short, user-facing label for the last failure (empty when
        /// DX11 succeeded).</summary>
        string LastFailureMessage { get; }
    }
}
