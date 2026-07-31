using System;
using System.IO;

namespace VibranceHud.Licensing
{
    /// <summary>What the app is entitled to right now.</summary>
    public enum LicenceStatus
    {
        /// <summary>Nothing installed. Trial territory.</summary>
        None,

        /// <summary>Genuine, for this PC, still in date.</summary>
        Valid,

        /// <summary>Genuine, for this PC, ran out.</summary>
        Expired,

        /// <summary>Genuine, but issued for a different machine.</summary>
        WrongMachine,

        /// <summary>Not something this app issued, or damaged beyond reading.</summary>
        Invalid,
    }

    public sealed record LicenceState(LicenceStatus Status, LicenceDocument? Document)
    {
        public bool Unlocked => Status == LicenceStatus.Valid;

        public TimeSpan? Remaining(DateTime nowUtc) =>
            Status == LicenceStatus.Valid && Document != null
                ? Document.ExpiresUtc - nowUtc
                : null;
    }

    /// <summary>
    /// The licence on this PC: putting one there, and answering whether it lets the app run.
    ///
    /// Order matters in both directions. Nothing inside a licence is read or trusted until the
    /// signature has been checked, so a hand-edited file can never talk the app into anything.
    /// And every check that can fail returns a status rather than throwing - a customer who
    /// pastes the wrong thing should see a sentence explaining what happened, not a crash.
    ///
    /// The file is a convenience, not the authority. Copying it to another PC produces
    /// <see cref="LicenceStatus.WrongMachine"/>, because the machine it was issued for is
    /// inside the signed bytes.
    /// </summary>
    public sealed class LicenceStore
    {
        private readonly string _path;
        private readonly byte[] _publicKey;

        public LicenceStore(string path, byte[] publicKey)
        {
            _path = path;
            _publicKey = publicKey;
        }

        /// <summary>The normal location: alongside the app's other per-user data.</summary>
        public static LicenceStore Default() => new(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PlexusX", "licence.dat"),
            LicenceKeys.Verification);

        /// <summary>
        /// Accept a licence the customer pasted in. Refuses anything that would not work
        /// anyway, so problems surface at the moment of pasting rather than silently later.
        /// </summary>
        public bool TryInstall(string? envelopeJson, string hardwareId, DateTime nowUtc, out string error)
        {
            if (!LicenceVerifier.TryVerify(envelopeJson, _publicKey, out var doc) || doc == null)
            {
                error = "That licence could not be verified. Check it was copied in full, " +
                        "including the very first and last characters.";
                return false;
            }

            if (!SameMachine(doc.HardwareId, hardwareId))
            {
                error = "That licence was issued for another PC. Each key works on one machine - " +
                        "if you changed hardware, ask for it to be released and activate again.";
                return false;
            }

            if (doc.IsExpiredAt(nowUtc))
            {
                error = "That licence expired on " + doc.ExpiresUtc.ToLocalTime().ToString("d MMMM yyyy") + ".";
                return false;
            }

            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_path, envelopeJson);
            }
            catch (Exception ex)
            {
                error = "The licence is fine, but it could not be saved: " + ex.Message;
                return false;
            }

            error = "";
            return true;
        }

        public LicenceState Read(string hardwareId, DateTime nowUtc)
        {
            string envelope;
            try
            {
                if (!File.Exists(_path)) return new LicenceState(LicenceStatus.None, null);
                envelope = File.ReadAllText(_path);
            }
            catch
            {
                // Unreadable is not the same as absent: treat it as a broken licence so the
                // customer is told something is wrong instead of being dropped into a trial.
                return new LicenceState(LicenceStatus.Invalid, null);
            }

            if (!LicenceVerifier.TryVerify(envelope, _publicKey, out var doc) || doc == null)
                return new LicenceState(LicenceStatus.Invalid, null);

            if (!SameMachine(doc.HardwareId, hardwareId))
                return new LicenceState(LicenceStatus.WrongMachine, doc);

            return doc.IsExpiredAt(nowUtc)
                ? new LicenceState(LicenceStatus.Expired, doc)
                : new LicenceState(LicenceStatus.Valid, doc);
        }

        public void Clear()
        {
            try { if (File.Exists(_path)) File.Delete(_path); } catch { }
        }

        private static bool SameMachine(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
