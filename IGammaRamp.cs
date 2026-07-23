namespace VibranceHud
{
    /// <summary>
    /// The display's gamma ramp. Abstracted so <see cref="VibranceEngine"/> can be
    /// unit-tested without touching a real monitor.
    /// </summary>
    public interface IGammaRamp
    {
        /// <summary>Apply a 3 x 256 16-bit ramp (768 entries: R, G, B).</summary>
        void Apply(ushort[] ramp);

        /// <summary>Restore the untouched linear ramp.</summary>
        void Reset();
    }
}
