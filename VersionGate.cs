using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VibranceHud
{
    /// <summary>What the published status file says. Missing pieces are simply absent.</summary>
    public sealed class AppStatus
    {
        /// <summary>Oldest version allowed to run, or null when nothing has been published /
        /// the content couldn't be read. Null always means "no restriction".</summary>
        public Version? MinimumVersion { get; init; }

        /// <summary>Optional line shown to a blocked user, e.g. "PlexusX 1.0 is out".</summary>
        public string Message { get; init; } = "";
    }

    /// <summary>
    /// Ends the beta on demand.
    ///
    /// A small file published alongside the app names the minimum version allowed to run.
    /// While it names the current beta, everything works normally; the day the full version
    /// ships it's changed and every beta install locks itself on the next check. Chosen over a
    /// hardcoded expiry date because a date strands the entire userbase if the release slips -
    /// this way the switch is thrown when the replacement genuinely exists.
    ///
    /// Same shape as the revocation list: fetch a small file, cache it, decide locally. No
    /// server to run and nothing to keep alive.
    ///
    /// All the logic that matters is here and pure - <see cref="AppStatusService"/> is only
    /// the network and disk around it.
    /// </summary>
    public static class VersionGate
    {
        /// <summary>Whether this build is no longer allowed to run.</summary>
        public static bool IsBlocked(Version current, Version? minimum)
        {
            // Never lock on the absence of information. A user who has never reached the
            // status file must keep working.
            if (minimum == null) return false;
            return Normalize(current) < Normalize(minimum);
        }

        /// <summary>
        /// Which requirement actually applies, given what was just fetched and what was already
        /// known. The highest ever seen wins.
        ///
        /// That matters in both directions: a failed fetch must not undo a lockout that already
        /// happened (pull the network cable and carry on), and neither must an older file being
        /// served again - by a stale CDN copy, or by someone replaying one deliberately.
        /// </summary>
        public static Version? Resolve(Version? fetched, Version? cached)
        {
            if (fetched == null) return cached;
            if (cached == null) return fetched;
            return Normalize(fetched) >= Normalize(cached) ? fetched : cached;
        }

        /// <summary>
        /// Read the published status. Anything unusable - malformed JSON, a captive-portal
        /// login page, a truncated download, an unparseable version - yields no minimum, i.e.
        /// no restriction. Read strictly so a half-written file can never be interpreted as
        /// "block everyone".
        /// </summary>
        public static AppStatus Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new AppStatus();

            try
            {
                var raw = JsonSerializer.Deserialize<RawStatus>(json);
                if (raw == null) return new AppStatus();

                Version? min = null;
                if (!string.IsNullOrWhiteSpace(raw.MinimumVersion)
                    && Version.TryParse(raw.MinimumVersion.Trim(), out var parsed))
                {
                    min = parsed;
                }

                return new AppStatus { MinimumVersion = min, Message = raw.Message ?? "" };
            }
            catch
            {
                return new AppStatus();
            }
        }

        public static string Serialize(Version minimum, string message) =>
            JsonSerializer.Serialize(
                new RawStatus { MinimumVersion = minimum.ToString(), Message = message },
                new JsonSerializerOptions { WriteIndented = true });

        private static Version Normalize(Version v) =>
            new(v.Major, v.Minor, Math.Max(v.Build, 0));

        private sealed class RawStatus
        {
            [JsonPropertyName("minimumVersion")]
            public string? MinimumVersion { get; set; }

            [JsonPropertyName("message")]
            public string? Message { get; set; }
        }
    }
}
