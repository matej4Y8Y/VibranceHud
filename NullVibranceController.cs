namespace VibranceHud
{
    /// <summary>
    /// Stand-in used when there's no NVIDIA driver to talk to (AMD/Intel GPU, or an NVIDIA
    /// card with no driver installed). The driver-level 0-100 vibrance range has nothing to
    /// apply to, so it's a no-op here - but <see cref="VibranceEngine"/>'s 100-200 software
    /// saturation overlay works on any GPU, so the app stays usable instead of refusing to
    /// start at all.
    /// </summary>
    public sealed class NullVibranceController : IVibranceController
    {
        /// <summary>
        /// Why there's no driver. This stand-in covers two very different situations - a PC
        /// with no NVIDIA card, and a laptop whose built-in screen runs off the integrated
        /// chip - and the user has to be told which. Defaults to "no card" so anything that
        /// forgets to say can't accidentally claim the driver is fine.
        /// </summary>
        public NullVibranceController(
            VibranceDriverState state = VibranceDriverState.NoNvidiaCard) => DriverState = state;

        public int CurrentLevel => 100;
        public int DefaultLevel => 100;
        public bool IsAvailable => false;
        public VibranceDriverState DriverState { get; }
        public void SetLevel(int level) { /* no driver to talk to */ }
    }
}
