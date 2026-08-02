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

        /// <summary>
        /// Why the driver path is or isn't there. <see cref="IsAvailable"/> only says whether
        /// it works; this says why not, which is what the user needs to be told.
        ///
        /// Defaults to deriving from IsAvailable so existing implementations keep working. The
        /// real ones override it - only they know whether an NVIDIA card exists at all.
        /// </summary>
        VibranceDriverState DriverState => IsAvailable
            ? VibranceDriverState.Available
            : VibranceDriverState.NoNvidiaCard;

        void SetLevel(int level);
    }
}
