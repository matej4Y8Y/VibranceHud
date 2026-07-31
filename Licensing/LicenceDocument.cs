using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VibranceHud.Licensing
{
    /// <summary>
    /// What a redeemed key actually grants. This is the thing that gets signed.
    ///
    /// Expiry is an explicit date rather than a plan the app has to look up. That is what ends
    /// the version-compatibility problem the beta had: a licence issued years from now, under a
    /// plan this build has never heard of, still expires on exactly the right day. The app
    /// never needs teaching what a plan means.
    /// </summary>
    public sealed record LicenceDocument(
        string Serial,
        string Plan,
        DateTime IssuedUtc,
        DateTime ExpiresUtc,
        string HardwareId)
    {
        private const string TimeFormat = "yyyy-MM-ddTHH:mm:ssZ";

        public bool IsExpiredAt(DateTime nowUtc) => nowUtc >= ExpiresUtc;

        /// <summary>
        /// The exact bytes the signature covers. Property order is fixed and written by hand
        /// rather than left to a serialiser's defaults - if this output ever shifts by a single
        /// byte, every licence already issued stops verifying.
        /// </summary>
        public string ToCanonicalJson()
        {
            var raw = new RawDocument
            {
                Serial = Serial,
                Plan = Plan,
                Issued = IssuedUtc.ToUniversalTime().ToString(TimeFormat, CultureInfo.InvariantCulture),
                Expires = ExpiresUtc.ToUniversalTime().ToString(TimeFormat, CultureInfo.InvariantCulture),
                Hardware = HardwareId,
            };
            return JsonSerializer.Serialize(raw);
        }

        public static bool TryFromJson(string? json, out LicenceDocument? doc)
        {
            doc = null;
            if (string.IsNullOrWhiteSpace(json)) return false;

            try
            {
                var raw = JsonSerializer.Deserialize<RawDocument>(json);
                if (raw == null) return false;
                if (string.IsNullOrEmpty(raw.Serial) || string.IsNullOrEmpty(raw.Plan)) return false;
                if (string.IsNullOrEmpty(raw.Issued) || string.IsNullOrEmpty(raw.Expires)) return false;

                if (!TryParseUtc(raw.Issued, out var issued)) return false;
                if (!TryParseUtc(raw.Expires, out var expires)) return false;

                doc = new LicenceDocument(raw.Serial, raw.Plan, issued, expires, raw.Hardware ?? "");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseUtc(string value, out DateTime utc) =>
            DateTime.TryParseExact(value, TimeFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);

        private sealed class RawDocument
        {
            [JsonPropertyName("serial")] public string? Serial { get; set; }
            [JsonPropertyName("plan")] public string? Plan { get; set; }
            [JsonPropertyName("issued")] public string? Issued { get; set; }
            [JsonPropertyName("expires")] public string? Expires { get; set; }
            [JsonPropertyName("hardware")] public string? Hardware { get; set; }
        }
    }
}
