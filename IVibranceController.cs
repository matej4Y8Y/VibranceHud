namespace VibranceHud
{
    /// <summary>
    /// The driver-level vibrance control (NVAPI), 0-100. Abstracted so the coordinating
    /// <see cref="VibranceEngine"/> can be unit-tested without a real GPU.
    /// </summary>
    public interface IVibranceController
    {
        int CurrentLevel { get; }
        int DefaultLevel { get; }
        void SetLevel(int level);
    }
}
