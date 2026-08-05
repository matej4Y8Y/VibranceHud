namespace VibranceHud.Capabilities
{
    /// <summary>
    /// What this PC was measured to be able to do, for the lifetime of the process.
    ///
    /// A static because it is exactly that: measured once at startup, immutable afterwards,
    /// and needed by several unrelated pages that have no other reason to know about each
    /// other. Threading it through half a dozen constructors would buy nothing - the same
    /// reasoning that makes <see cref="Theme"/> a static here.
    ///
    /// Deliberately NOT re-probed on demand. The probe writes a curve to the screen to
    /// measure it, so running it while the user is looking at their colours would flicker
    /// them for no reason.
    /// </summary>
    public static class Machine
    {
        /// <summary>Unknown until <see cref="Measure"/> runs, so nothing can accidentally
        /// treat "not measured yet" as "not supported".</summary>
        public static MachineCapabilities Current { get; private set; } = MachineCapabilities.Unknown;

        /// <summary>Run the probe and remember the answer. Called once, during startup,
        /// before the main window is built - so the window styles capability-dependent
        /// controls correctly the first time instead of correcting itself.</summary>
        public static void Measure(bool driverVibrance, OverlayMode overlayPath)
        {
            Current = CapabilityProbe.Run(driverVibrance, overlayPath);
        }

        /// <summary>Test seam: set a known machine without touching the display.</summary>
        internal static void OverrideForTests(MachineCapabilities caps) => Current = caps;
    }
}
