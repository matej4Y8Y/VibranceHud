namespace VibranceHud
{
    /// <summary>Why the NVIDIA path is or isn't there.</summary>
    public enum VibranceDriverState
    {
        /// <summary>NVIDIA is driving at least one screen. Everything works.</summary>
        Available,

        /// <summary>No NVIDIA hardware in the machine at all - an AMD or Intel PC.</summary>
        NoNvidiaCard,

        /// <summary>
        /// There is an NVIDIA card, it just isn't driving the screen being looked at.
        ///
        /// Almost always a gaming laptop: the built-in panel is wired to the Intel or AMD chip
        /// and NVIDIA only drives the external ports.
        /// </summary>
        DisplayNotOnNvidia,
    }

    /// <summary>
    /// What the app knows about the driver, and what it tells the user about it.
    ///
    /// These two used to be one thing, and the app said "no NVIDIA GPU" whenever the driver
    /// path was missing. On a laptop that's a lie - the card is right there - and it reads as
    /// the app being broken rather than the screen being wired elsewhere.
    /// </summary>
    public static class VibranceStatus
    {
        /// <summary>
        /// The two facts arrive from separate driver calls, so they can disagree. If there's no
        /// card, nothing else matters - a contradiction must never resolve to "working".
        /// </summary>
        public static VibranceDriverState Determine(bool nvidiaCardPresent, int nvidiaDisplayCount)
        {
            if (!nvidiaCardPresent) return VibranceDriverState.NoNvidiaCard;
            return nvidiaDisplayCount > 0
                ? VibranceDriverState.Available
                : VibranceDriverState.DisplayNotOnNvidia;
        }

        /// <summary>The line under the Vibrance slider.</summary>
        public static string Readout(VibranceDriverState state, int vibrancePercent) => state switch
        {
            VibranceDriverState.Available => $"{vibrancePercent}%",

            // Names the cause and points at the control that does work, because being told
            // what's broken without being told what to do next is still a dead end.
            VibranceDriverState.DisplayNotOnNvidia =>
                "laptop screen - use saturation",

            VibranceDriverState.NoNvidiaCard => "no NVIDIA GPU",

            _ => "unavailable",
        };
    }
}
