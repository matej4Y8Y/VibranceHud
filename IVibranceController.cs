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

        /// <summary>False when there's no NVIDIA driver to talk to (e.g. AMD/Intel GPU) -
        /// the 0-100 driver range is then a no-op, but the 100-200 software range still works.</summary>
        bool IsAvailable { get; }

        void SetLevel(int level);
    }
}
