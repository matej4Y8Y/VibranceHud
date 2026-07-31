using VibranceHud.License;

namespace VibranceHud.Licensing
{
    /// <summary>
    /// Reads this PC's id. Split out from <see cref="MachineId"/> so the formatting rules can be
    /// shared with PlexusX Keys, which has no business touching WMI - it only ever handles ids
    /// that customers send it.
    /// </summary>
    public static class ThisMachine
    {
        /// <summary>This PC's id, or empty if the hardware could not be read. A machine that
        /// stripped down cannot be bound to, and the caller has to decide what that means.</summary>
        public static string Id() => MachineId.Format(LicenseKeyDerivation.GetHardwareFingerprintHash());
    }
}
