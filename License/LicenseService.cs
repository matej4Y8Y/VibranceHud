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
using System.Text.Json;

namespace VibranceHud.License
{
    public sealed class LicenseService
    {
        private static readonly string LicenseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PlexusX");

        private static readonly string LicensePath = Path.Combine(LicenseDir, "license.json");

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
        /// the tier-to-month table that <see cref="IsExpired"/> uses - two copies of that
        /// table would eventually drift and disagree about what "expired" means.</summary>
        public DateTime? ExpiresAt =>
            Current != null && DateTime.TryParse(Current.Issued + "-01", out var issued)
                ? issued.AddMonths(MonthsForTier(Current.Tier))
                : null;

        public LicenseService()
        {
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

            if (!File.Exists(LicensePath))
            {
                return;
            }

            LicenseRecord? record;
            try
            {
                var json = File.ReadAllText(LicensePath);
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
                Issued = DateTime.UtcNow.ToString("yyyy-MM"),
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
                Directory.CreateDirectory(LicenseDir);
                var record = new LicenseRecord
                {
                    Payload = payloadJson,
                    Signature = sig,
                    KeyText = key.Serial,
                };
                var json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(LicensePath, json);
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
                if (File.Exists(LicensePath)) File.Delete(LicensePath);
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

        private static bool IsExpired(LicensePayload p)
        {
            if (string.IsNullOrEmpty(p.Issued)) return true;
            if (!DateTime.TryParse(p.Issued + "-01", out var issued)) return true;
            var expiresAt = issued.AddMonths(MonthsForTier(p.Tier));
            return DateTime.UtcNow > expiresAt;
        }

        /// <summary>The single source of truth for how long each tier lasts. Shared by
        /// <see cref="IsExpired"/> and <see cref="ExpiresAt"/> so there is exactly one
        /// place that can get the free/trial/paid durations wrong.</summary>
        private static int MonthsForTier(string tier) => tier switch
        {
            "trial" => 1,
            "paid" => 24,
            _ => 12, // free
        };

        private static void LogFailure(string reason)
        {
            try
            {
                var dir = Path.Combine(LicenseDir, "crashes");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "license-fail.log");
                File.AppendAllText(path,
                    $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC  state=PENDING  reason={reason}\n");
            }
            catch { /* never crash the app for a log failure */ }
        }
    }
}
