// LicenseService — the gatekeeper. Loads license.json from %LocalAppData%\PlexusX,
// verifies the HMAC signature, checks the hardware fingerprint and expiry, and
// reports a LicenseState. Apply() takes a raw key string the user typed and writes
// a fresh license file.
//
// Failure modes are deliberately loud: every state other than Valid shows a clear
// reason. The user never sees "license failed" - they see "wrong machine" or
// "expired" or "tampered" so they know what to do.

using System;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Text.Json;

namespace VibranceHud.License
{
    public sealed class LicenseService
    {
        private static string DefaultLicenseDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PlexusX");

        // Per-instance rather than static so a test can point at a temp folder.
        //
        // These used to be static readonly paths to the one real licence file, so every test
        // that activated or deactivated a licence wrote to - and Deactivate() outright
        // DELETED - the developer's own licence. Running the suite silently signed you out of
        // your own app, and left the next launch sitting on the activation dialog. That cost
        // real debugging time more than once, because a stale settings.json then looked like
        // a display-engine bug.
        private readonly string _licenseDir;
        private readonly string _licensePath;

        private static readonly string[] ForbiddenProcesses = new[]
        {
            "dnSpy", "dnSpy-x86", "x64dbg", "x32dbg", "fiddler", "charles",
            "wireshark", "httpdebuggerpro", "megadumper", "ilspy",
        };

        public LicenseState State { get; private set; } = LicenseState.Invalid;
        public LicensePayload? Current { get; private set; }
        public string KeyText { get; private set; } = "";
        public bool HasValidLicense => State == LicenseState.Valid;

        /// <summary>When the current license stops being valid, or null when there is no
        /// current license. Exposed so the Account page can show it without duplicating
        /// the tier duration table that <see cref="IsExpiredAt"/> uses - two copies of that
        /// table would eventually drift and disagree about what "expired" means.</summary>
        public DateTime? ExpiresAt
        {
            get
            {
                if (Current == null) return null;
                var issued = ParseIssued(Current.Issued);
                return issued == null ? null : issued.Value + DurationForTier(Current.Tier);
            }
        }

        public LicenseService() : this(null) { }

        /// <param name="licenseDir">Where license.json lives. Null uses the real
        /// %LocalAppData%\PlexusX. Tests pass a temp directory so they can't clobber the
        /// developer's own licence.</param>
        public LicenseService(string? licenseDir)
        {
            _licenseDir = licenseDir ?? DefaultLicenseDir;
            _licensePath = Path.Combine(_licenseDir, "license.json");
            Load();
        }

        /// <summary>
        /// Read license.json, verify signature, check machine fingerprint, check expiry.
        /// Failures get logged to %LocalAppData%\PlexusX\crashes\license-fail.log so the
        /// support team can debug "my key stopped working" tickets.
        /// </summary>
        public void Load()
        {
            State = LicenseState.Invalid;
            Current = null;

            if (Debugger.IsAttached)
            {
                LogFailure("debugger attached");
                State = LicenseState.DebuggerDetected;
                return;
            }

            foreach (var p in ForbiddenProcesses)
            {
                try
                {
                    foreach (var proc in Process.GetProcessesByName(p))
                    {
                        proc.Dispose();
                        LogFailure($"forbidden process running: {p}");
                        State = LicenseState.DebuggerDetected;
                        return;
                    }
                }
                catch { /* ignore - we don't want to crash the app for a missing process */ }
            }

            if (!File.Exists(_licensePath))
            {
                return;
            }

            LicenseRecord? record;
            try
            {
                var json = File.ReadAllText(_licensePath);
                record = JsonSerializer.Deserialize<LicenseRecord>(json);
            }
            catch (Exception ex)
            {
                LogFailure($"file read/parse failed: {ex.Message}");
                State = LicenseState.Tampered;
                return;
            }

            if (record == null || string.IsNullOrEmpty(record.Payload) || string.IsNullOrEmpty(record.Signature))
            {
                LogFailure("record has empty payload or signature");
                State = LicenseState.Tampered;
                return;
            }

            byte[] masterKey = LicenseKeyDerivation.DeriveMasterKey();
            if (!LicenseKeyDerivation.VerifySignature(record.Payload, record.Signature, masterKey))
            {
                LogFailure($"signature mismatch for payload '{record.Payload}'");
                State = LicenseState.Tampered;
                return;
            }

            LicensePayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<LicensePayload>(record.Payload);
            }
            catch (Exception ex)
            {
                LogFailure($"payload parse failed: {ex.Message}");
                State = LicenseState.Tampered;
                return;
            }

            if (payload == null || string.IsNullOrEmpty(payload.Serial))
            {
                LogFailure("payload missing serial");
                State = LicenseState.Tampered;
                return;
            }

            var currentHw = LicenseKeyDerivation.GetHardwareFingerprintHash();
            if (currentHw != null && !string.IsNullOrEmpty(payload.HardwareId))
            {
                if (!string.Equals(currentHw, payload.HardwareId, StringComparison.Ordinal))
                {
                    LogFailure($"hardware mismatch: stored={payload.HardwareId} current={currentHw}");
                    State = LicenseState.WrongMachine;
                    return;
                }
            }

            if (IsExpired(payload))
            {
                LogFailure($"license expired: issued={payload.Issued}");
                State = LicenseState.Expired;
                return;
            }

            if (RevocationList.IsRevoked(payload.Serial, RevocationService.LoadCached()))
            {
                LogFailure($"serial is on the revocation list: {payload.Serial}");
                State = LicenseState.Revoked;
                return;
            }

            Current = payload;
            KeyText = record.KeyText;
            State = LicenseState.Valid;
        }

        public LicenseState TryActivate(string keyString)
        {
            var key = LicenseKey.Parse(keyString);
            if (key == null) return LicenseState.InvalidKey;

            byte[] masterKey = LicenseKeyDerivation.DeriveMasterKey();
            // Sign the 4-group payload (without the trailing checksum), matching what
            // KeyGenerator.cs signed when it produced this key. SignedPayload exists
            // exactly so both sides agree on the byte sequence that gets HMAC'd.
            if (!LicenseKeyDerivation.VerifySignature(key.SignedPayload, key.Checksum, masterKey))
            {
                return LicenseState.InvalidKey;
            }

            // Block a revoked key at activation too, not just on load - otherwise a
            // revoked key still "works" once on a fresh machine (it would write a valid
            // license file, and only get caught on the NEXT launch).
            if (RevocationList.IsRevoked(key.Serial, RevocationService.LoadCached()))
            {
                LogFailure($"activation refused, serial revoked: {key.Serial}");
                return LicenseState.Revoked;
            }

            var hw = LicenseKeyDerivation.GetHardwareFingerprintHash();
            var payload = new LicensePayload
            {
                Serial = key.Serial,
                Tier = key.GetKind().ToString().ToLowerInvariant(),
                Issued = FormatIssued(DateTime.UtcNow),
                HardwareId = hw ?? "",
            };

            // Re-encode the payload deterministically so the signature we generate
            // matches the signature we just verified. System.Text.Json by default
            // preserves property order but we go through the same minimal writer
            // so the format is locked.
            var payloadJson = SerializePayload(payload);
            var sig = LicenseKeyDerivation.SignPayload(payloadJson, masterKey);

            try
            {
                Directory.CreateDirectory(_licenseDir);
                var record = new LicenseRecord
                {
                    Payload = payloadJson,
                    Signature = sig,
                    KeyText = key.Serial,
                };
                var json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_licensePath, json);
            }
            catch (Exception ex)
            {
                LogFailure($"write failed: {ex.Message}");
                return LicenseState.Invalid;
            }

            Current = payload;
            KeyText = key.Serial;
            State = LicenseState.Valid;
            return LicenseState.Valid;
        }

        public void Deactivate()
        {
            try
            {
                if (File.Exists(_licensePath)) File.Delete(_licensePath);
            }
            catch { /* ignore */ }
            Current = null;
            KeyText = "";
            State = LicenseState.Invalid;
        }

        private static string SerializePayload(LicensePayload p)
        {
            // Deterministic - hardcoded field order so the signature stays valid
            // across process restarts.
            return JsonSerializer.Serialize(p);
        }

        private static bool IsExpired(LicensePayload p) =>
            IsExpiredAt(p.Tier, ParseIssued(p.Issued), DateTime.UtcNow);

        /// <summary>
        /// Expiry as a pure function of tier, issue instant and "now", so the rules are
        /// unit-testable without writing a licence file or waiting six hours.
        /// Fails closed: an issue date we can't read counts as expired.
        /// </summary>
        public static bool IsExpiredAt(string tier, DateTime? issuedUtc, DateTime nowUtc)
        {
            if (issuedUtc == null) return true;
            return nowUtc >= issuedUtc.Value + DurationForTier(tier);
        }

        /// <summary>
        /// How long each tier lasts. The single source of truth for
        /// <see cref="IsExpiredAt"/> and <see cref="ExpiresAt"/>.
        ///
        /// A TimeSpan rather than a month count, because "yyyy-MM" + AddMonths made one
        /// calendar month the shortest expressible licence - a short demo key was impossible.
        /// Unknown tiers deliberately get the shortest of the long windows rather than
        /// unlimited access, so a typo can't mint a forever key.
        /// </summary>
        public static TimeSpan DurationForTier(string tier) => tier switch
        {
            "temp" => TimeSpan.FromHours(6),
            "week" => TimeSpan.FromDays(7),
            "trial" => TimeSpan.FromDays(30),
            "paid" => TimeSpan.FromDays(730),
            _ => TimeSpan.FromDays(365), // free, and anything unrecognised
        };

        /// <summary>Round-trip format for the issue instant: full precision UTC.</summary>
        public static string FormatIssued(DateTime utc) =>
            utc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        /// <summary>
        /// Read an issue date, accepting both the current full-precision form and the legacy
        /// "yyyy-MM" written before short-lived keys existed. Dropping legacy support would
        /// make every already-activated install read as expired the moment it updated.
        /// </summary>
        public static DateTime? ParseIssued(string? issued)
        {
            if (string.IsNullOrWhiteSpace(issued)) return null;
            issued = issued.Trim();

            // Legacy "yyyy-MM" - treat as the first instant of that month. Checked by shape
            // first so it can't be mistaken for a partial ISO timestamp.
            if (issued.Length == 7 && issued[4] == '-')
            {
                return DateTime.TryParse(issued + "-01", CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var month)
                    ? month
                    : null;
            }

            return DateTime.TryParse(issued, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var exact)
                ? exact
                : null;
        }


        private void LogFailure(string reason)
        {
            try
            {
                var dir = Path.Combine(_licenseDir, "crashes");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "license-fail.log");
                File.AppendAllText(path,
                    $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC  state=PENDING  reason={reason}\n");
            }
            catch { /* never crash the app for a log failure */ }
        }
    }
}
