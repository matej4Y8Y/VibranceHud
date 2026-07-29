namespace VibranceHud.License
{
    /// <summary>
    /// Outcome of a license validation. The UI maps each state to a single Czech/English
    /// sentence so the user always knows what to do next.
    /// </summary>
    public enum LicenseState
    {
        /// <summary>No license file on disk yet (first launch).</summary>
        Invalid,

        /// <summary>Last activation attempt succeeded and the file is intact.</summary>
        Valid,

        /// <summary>Activation key was rejected (wrong format, bad signature, unknown key).</summary>
        InvalidKey,

        /// <summary>License file is signed but the signature doesn't match (file was edited).</summary>
        Tampered,

        /// <summary>License was issued for a different machine (hardware fingerprint mismatch).</summary>
        WrongMachine,

        /// <summary>License file is past its expiry month (free tier = 12 months, etc.).</summary>
        Expired,

        /// <summary>A debugger or known reverse-engineering tool is attached.</summary>
        DebuggerDetected,

        /// <summary>The key's serial appears on the developer-maintained revocation
        /// list (see <see cref="RevocationList"/>). Distinct from Tampered/InvalidKey -
        /// the signature is genuinely valid, the developer chose to cut this specific
        /// key off after the fact.</summary>
        Revoked,
    }
}
