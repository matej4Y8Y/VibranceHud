namespace VibranceHud
{
    /// <summary>Whether what PlexusX draws can reach recording software right now.</summary>
    public enum CaptureState
    {
        /// <summary>Everything the user set is already in the captured picture.</summary>
        Visible,

        /// <summary>It would be, if Streaming Mode were switched on. Their vibrance is
        /// currently sitting in the driver, which capture never sees.</summary>
        NeedsStreamingMode,

        /// <summary>
        /// Shows in most recording software, but not in every screen-share path.
        ///
        /// Replaces the old <c>Impossible</c>. That state asserted nothing could ever reach
        /// capture on the Magnification fallback, and two users disproved it on different
        /// hardware: an NVIDIA machine where driver vibrance records, and an AMD machine with
        /// no driver path at all - so purely the software colour matrix - where OBS captures
        /// it in full. What actually varies is the capture API, not the PC.
        /// </summary>
        DependsOnCaptureMethod,
    }

    /// <summary>
    /// One place that decides what recordings will actually show, and what to say about it.
    ///
    /// This existed in three places before, each with its own idea of the truth, and then the
    /// single place was wrong too. It claimed the Magnification path could never reach
    /// capture. Measured evidence says otherwise:
    ///
    ///   - AMD RX 6800, Windows 11, no driver vibrance at all (so every bit of the colour is
    ///     the software matrix on the Magnification path): OBS Display Capture records it in
    ///     full. Discord screen share records none of it.
    ///   - NVIDIA GTX 1660, Windows 10, vibrance 106 - which is 100 driver plus a 1.06x
    ///     software nudge, so effectively all driver: OBS records it.
    ///   - The note in CaptureDiagnostic that 8 of 20 testers saw their colours in a screen
    ///     share, which was treated as a mystery rather than as evidence.
    ///
    /// The likely mechanism is that OBS defaults to Windows Graphics Capture, which reads
    /// from DWM composition, while DXGI Desktop Duplication - what the in-app probe uses, and
    /// what Discord appears to use - does not see the effect. That inference is not needed to
    /// know the old claim was false, so the wording here states what has been observed and
    /// stops short of explaining it.
    ///
    /// Pure, so the rules are unit-tested rather than argued about.
    /// </summary>
    public static class CaptureStatus
    {
        /// <summary>
        /// What recordings show right now.
        ///
        /// The colour matrix is applied while the desktop is composited, which is where
        /// Windows Graphics Capture - and so OBS Display Capture on its default settings -
        /// reads. The gamma ramp is applied after that, on the way to the cable, so no
        /// capture path sees it.
        /// </summary>
        public static CaptureState Resolve(OverlayMode overlay, bool driverVibranceAvailable, bool streamingMode)
        {
            // The Magnification path reaches OBS but not every screen-share path, so this is
            // a caveat rather than a dead end. It used to return Impossible, which told AMD
            // users - who have no driver path at all - that the thing they could plainly see
            // working did not work.
            if (overlay == OverlayMode.Mag) return CaptureState.DependsOnCaptureMethod;

            // No driver means software already carries the whole range, so there is nothing
            // left in the uncapturable path to move.
            if (!driverVibranceAvailable) return CaptureState.Visible;
            return streamingMode ? CaptureState.Visible : CaptureState.NeedsStreamingMode;
        }

        /// <summary>Whether the Streaming Mode switch can change anything at all. When it
        /// can't, it should be shown disabled with a reason rather than offered as a fix -
        /// an inert control that silently costs image quality is worse than no control.</summary>
        public static bool ToggleCanHelp(OverlayMode overlay, bool driverVibranceAvailable) =>
            overlay == OverlayMode.Dx && driverVibranceAvailable;

        /// <summary>The one-line verdict shown under the switch.</summary>
        public static string Headline(CaptureState state) => state switch
        {
            CaptureState.Visible => "Recordings show your colours.",
            CaptureState.NeedsStreamingMode => "Recordings are missing your vibrance right now.",
            _ => "Recordings show your colours — screen share may not.",
        };

        /// <summary>Why, and what to do about it. Empty when there's nothing to explain.</summary>
        public static string Reason(CaptureState state, bool driverVibranceAvailable) => state switch
        {
            CaptureState.NeedsStreamingMode =>
                "Your vibrance is being applied by the graphics driver, after recording software "
                + "has already read the screen. Turn this on to move it somewhere they can see it.",

            // Says what has been observed on real machines, and gives the one instruction that
            // actually changes the outcome. The previous wording here told everyone on this
            // path that recording their colours was impossible on any PC - which was wrong,
            // and was talking people out of a feature that works.
            CaptureState.DependsOnCaptureMethod =>
                "OBS records your colours with its default Display Capture settings — confirmed "
                + "on both NVIDIA and AMD. If they don't appear, open your Display Capture "
                + "source and set Capture Method to \"Windows 10 (1903 and up)\".\n\n"
                + "Discord screen share reads the screen a different way and won't show them.",

            // Visible, but for two quite different reasons worth distinguishing.
            _ => driverVibranceAvailable
                ? "Your vibrance is being applied early enough for capture to pick it up."
                : "Your colours are already applied in software, which capture reads - so there "
                  + "was never anything to move. This switch is for NVIDIA driver vibrance.",
        };

        /// <summary>The limitation that holds in every state, so it's stated once and always.
        /// Gamma is non-linear and can't fold into the colour matrix, so it never reaches
        /// capture no matter what - and OBS Game Capture hooks the game directly, below the
        /// point any of this happens.</summary>
        public const string AlwaysTrue =
            "In OBS use Display Capture, not Game Capture - Game Capture reads the game directly "
            + "and never sees this. Gamma is never captured either way.";
    }
}
