using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VibranceHud.Cs2
{
    /// <summary>
    /// Reads and edits a CS2 autoexec.cfg. Like the Rust config it preserves every existing
    /// line verbatim and only rewrites the convars we're asked to. CS2 configs may quote the
    /// value or not (<c>fps_max 0</c> or <c>fps_max "0"</c>), so reads tolerate both; writes
    /// use the quoted form, which CS2 accepts.
    /// </summary>
    public sealed class Cs2Config
    {
        private readonly List<string> _lines;

        private Cs2Config(List<string> lines) => _lines = lines;

        public static Cs2Config Parse(string text) =>
            new(text.Length == 0 ? new List<string>() : new List<string>(text.Split('\n')));

        /// <summary>The convar's value with any surrounding quotes stripped, or null if absent.</summary>
        public string? Get(string convar)
        {
            var idx = FindLine(convar);
            if (idx < 0) return null;

            var m = Regex.Match(_lines[idx], "^\\s*" + Regex.Escape(convar) + "\\s+(.+?)\\s*\\r?$",
                RegexOptions.IgnoreCase);
            if (!m.Success) return null;

            var val = m.Groups[1].Value.Trim();
            if (val.Length >= 2 && val[0] == '"' && val[^1] == '"') val = val[1..^1];
            return val;
        }

        /// <summary>Set (or add) a convar, written as <c>convar "value"</c>.</summary>
        public void Set(string convar, string value)
        {
            var newLine = $"{convar} \"{value}\"";
            var idx = FindLine(convar);
            if (idx >= 0)
            {
                _lines[idx] = _lines[idx].EndsWith("\r") ? newLine + "\r" : newLine;
                return;
            }

            // Add it. If the file ends in a newline (trailing empty element), insert before
            // that so the file keeps ending in one newline rather than gaining a blank line.
            if (_lines.Count > 0 && _lines[^1].Length == 0)
                _lines.Insert(_lines.Count - 1, newLine);
            else
                _lines.Add(newLine);
        }

        public string Serialize() => string.Join("\n", _lines);

        private int FindLine(string convar)
        {
            var pattern = "^\\s*" + Regex.Escape(convar) + "\\s+";
            for (int i = 0; i < _lines.Count; i++)
                if (Regex.IsMatch(_lines[i], pattern, RegexOptions.IgnoreCase))
                    return i;
            return -1;
        }
    }
}
