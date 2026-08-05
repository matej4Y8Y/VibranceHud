namespace VibranceHud.Capabilities
{
    /// <summary>
    /// Whether the screen's gamma ramp can actually be driven on this machine.
    ///
    /// Three outcomes, not two. <see cref="Clamped"/> is deliberately distinct from
    /// <see cref="Refused"/>: Windows limits how far a ramp may deviate from linear (the
    /// GdiIcmGammaRange policy), so a call can report success and still deliver only part of
    /// the curve. Collapsing the two would tell somebody with a working-but-limited screen
    /// that their hardware is unsupported, which is both wrong and unhelpful.
    /// </summary>
    public enum GammaSupport
    {
        /// <summary>Not measured (probe failed, or hasn't run).</summary>
        Untested,

        /// <summary>The ramp applied as written.</summary>
        Working,

        /// <summary>Accepted, but Windows flattened it. Tone controls will be weaker than the
        /// numbers suggest.</summary>
        Clamped,

        /// <summary>The driver refused it outright. Nothing tonal will have any effect.</summary>
        Refused,
    }

    public enum GpuVendor { Unknown, Nvidia, Amd, Intel, Other }

    /// <summary>
    /// What was measured about this PC, once, at startup.
    ///
    /// Exists because the app used to assert things about the user's machine instead of
    /// checking them - and was repeatedly wrong in the direction that made a working feature
    /// look broken. Anything in here was observed, not assumed.
    ///
    /// Immutable and cheap to copy, so it can be handed to the UI and folded into the
    /// diagnostic report without anyone worrying about who owns it.
    /// </summary>
    public sealed record MachineCapabilities(
        GammaSupport GammaRamp = GammaSupport.Untested,
        bool HdrActive = false,
        GpuVendor Gpu = GpuVendor.Unknown,
        bool DriverVibrance = false,
        int MonitorCount = 1,
        bool MixedDpi = false,
        bool Elevated = false,
        OverlayMode OverlayPath = OverlayMode.Mag)
    {
        /// <summary>Nothing measured yet. Used before the probe runs, and if it fails.</summary>
        public static MachineCapabilities Unknown => new();

        /// <summary>
        /// True when the tonal controls - gamma, highlights, shadows, whites, blacks, fade and
        /// split toning - will do something visible.
        ///
        /// All of advanced colour resolves to the gamma ramp, so this single question decides
        /// whether that whole section is real on this PC.
        /// </summary>
        public bool ToneControlsWork => GammaRamp is GammaSupport.Working or GammaSupport.Clamped;

        /// <summary>Short, plain reason the tonal controls are limited or dead. Empty when
        /// there is nothing to explain.</summary>
        public string ToneLimitation => GammaRamp switch
        {
            GammaSupport.Refused when HdrActive =>
                "HDR is on, and Windows ignores screen-colour changes while it is. "
                + "Turn HDR off to use these.",
            GammaSupport.Refused =>
                "Your graphics driver is refusing screen-colour changes, so these won't do "
                + "anything. Saturation and vibrance above still work.",
            GammaSupport.Clamped when HdrActive =>
                "HDR is on, so Windows is limiting these. They'll work, but weaker than the "
                + "numbers suggest.",
            GammaSupport.Clamped =>
                "Windows is limiting how far these can go on this PC, so they'll be weaker "
                + "than the numbers suggest.",
            GammaSupport.Untested => "",
            _ => "",
        };
    }
}
