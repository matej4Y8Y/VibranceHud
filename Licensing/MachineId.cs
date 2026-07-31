using System;
using System.Linq;

namespace VibranceHud.Licensing
{
    /// <summary>
    /// The id of this PC, in a form a customer can actually send you.
    ///
    /// The underlying fingerprint is a 64-character hash. Nobody is retyping that into Discord
    /// correctly, so what gets shown - and what goes inside the signed licence - is the first
    /// 16 characters in groups of four. That is still 64 bits: for two customers to collide,
    /// and for that to matter, one would need the other's key as well.
    ///
    /// It stays stable across app updates and Windows reinstalls, and changes when the CPU or
    /// the boot drive changes. That last part is why "release from PC" exists in PlexusX Keys.
    /// </summary>
    public static class MachineId
    {
        /// <summary>Turn a raw fingerprint hash into the form customers see and licences bind
        /// to. Separate from <see cref="Current"/> so it can be tested without a real PC.</summary>
        public static string Format(string? rawHash)
        {
            if (string.IsNullOrWhiteSpace(rawHash)) return "";

            var clean = new string(rawHash.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
            if (clean.Length < 16) return "";

            var head = clean[..16];
            return $"{head[..4]}-{head[4..8]}-{head[8..12]}-{head[12..]}";
        }

        /// <summary>True if the text looks like a machine id, so a customer who pastes their
        /// key code into the wrong box is told so immediately.</summary>
        public static bool LooksValid(string? id) =>
            !string.IsNullOrWhiteSpace(id) &&
            Format(id.Replace("-", "")) == id.Trim().ToUpperInvariant();
    }
}
