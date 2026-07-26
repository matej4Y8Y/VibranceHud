namespace VibranceHud
{
    /// <summary>
    /// Minimal view of <see cref="VibranceEngine"/> that the auto-apply engine needs.
    /// Lets the tests inject a fake without dragging in the overlay / NVAPI / gamma-ramp
    /// collaborators that the real engine owns.
    /// </summary>
    public interface IVibranceEngine
    {
        int Vibrance { get; set; }
        int Saturation { get; set; }
        int Brightness { get; set; }
        int Gamma { get; set; }
    }
}