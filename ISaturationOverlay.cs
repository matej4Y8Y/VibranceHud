namespace VibranceHud
{
    /// <summary>
    /// A system-wide saturation color effect (Windows Magnification API). Abstracted so
    /// the coordinating <see cref="VibranceEngine"/> can be unit-tested without touching
    /// the real OS effect.
    /// </summary>
    public interface ISaturationOverlay
    {
        /// <summary>Apply an oversaturation factor (1.0 = no change, 2.0 = double).</summary>
        void SetSaturation(float factor);

        /// <summary>Remove any saturation effect, returning the screen to normal.</summary>
        void Clear();
    }
}
