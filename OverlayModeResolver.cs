namespace VibranceHud
{
    /// <summary>
    /// Pure lookup that turns whichever <see cref="ISaturationOverlay"/> actually got kept
    /// at startup into the <see cref="OverlayMode"/> to persist. Split out from
    /// <see cref="TrayApplicationContext"/> so the fallback-detection logic can be unit
    /// tested with a fake overlay instead of constructing real DX11/Magnification resources.
    /// </summary>
    public static class OverlayModeResolver
    {
        /// <summary>Defaults to Dx when the overlay doesn't report a mode (shouldn't happen
        /// for the two real implementations, but keeps old callers/fakes safe).</summary>
        public static OverlayMode Resolve(ISaturationOverlay overlay) =>
            (overlay as IDisplayOverlay)?.ActiveMode ?? OverlayMode.Dx;
    }
}
