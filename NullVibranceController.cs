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
        public int CurrentLevel => 100;
        public int DefaultLevel => 100;
        public bool IsAvailable => false;
        public void SetLevel(int level) { /* no driver to talk to */ }
    }
}
