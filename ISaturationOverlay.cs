namespace VibranceHud
{
    /// <summary>
    /// A system-wide screen color effect (Windows Magnification API). Takes the full 5x5
    /// color matrix so one pass covers saturation, brightness and eye-care warmth together.
    /// Abstracted so the coordinating <see cref="VibranceEngine"/> can be unit-tested
    /// without touching the real OS effect.
    /// </summary>
    public interface ISaturationOverlay
    {
        /// <summary>Apply a 5x5 color matrix (row-major, 25 floats).</summary>
        void Apply(float[] matrix);

        /// <summary>Remove any effect, returning the screen to normal.</summary>
        void Clear();
    }
}
