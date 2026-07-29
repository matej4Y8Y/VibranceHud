using System.Text.Json.Serialization;

namespace VibranceHud.License
{
    /// <summary>
    /// What gets serialised to disk in %LocalAppData%\PlexusX\license.json. Kept tiny
    /// on purpose - anything that needs to be signed lives in the inner JSON payload,
    /// which is base32-encoded and signed with HMAC. The outer record is just the
    /// two signed fields, so a bit-flip on either side fails verification.
    /// </summary>
    public sealed class LicenseRecord
    {
        [JsonPropertyName("payload")]
        public string Payload { get; set; } = "";

        [JsonPropertyName("sig")]
        public string Signature { get; set; } = "";

        /// <summary>
        /// What the user typed in the activation dialog before it was signed. Cached
        /// so the Account tab can show the full key without rebuilding it from the
        /// inner payload. Read-only on disk; tampered values are rejected at load.
        /// </summary>
        [JsonPropertyName("keyText")]
        public string KeyText { get; set; } = "";
    }

    /// <summary>
    /// Inner signed payload. Stored as a JSON string inside the outer record's
    /// `Payload` field. The KDF master key + HMAC over this JSON is what
    /// `Signature` checks against.
    /// </summary>
    public sealed class LicensePayload
    {
        [JsonPropertyName("serial")]
        public string Serial { get; set; } = "";

        [JsonPropertyName("tier")]
        public string Tier { get; set; } = "free";

        [JsonPropertyName("issued")]
        public string Issued { get; set; } = ""; // "YYYY-MM"

        [JsonPropertyName("hw")]
        public string HardwareId { get; set; } = "";
    }
}
