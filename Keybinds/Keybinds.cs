using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace VibranceHud.Keybinds
{
    /// <summary>One key pointed at one command, for one game.</summary>
    public sealed class Keybind
    {
        [JsonPropertyName("gameId")]    public string GameId { get; set; } = "";
        /// <summary>The game's own name for the key ("f1", "mouse4"), not a WinForms Keys value.</summary>
        [JsonPropertyName("key")]       public string Key { get; set; } = "";
        [JsonPropertyName("commandId")] public string CommandId { get; set; } = "";
    }

    /// <summary>
    /// The saved binds, and the pure logic over them.
    ///
    /// Kept away from the page so the rules that actually matter - one command per key, a key
    /// can be cleared, one game's binds never touch another's - are testable without a
    /// keyboard on screen.
    /// </summary>
    public static class KeybindSet
    {
        /// <summary>Every bind for a game, in no particular order.</summary>
        public static IReadOnlyList<Keybind> For(IEnumerable<Keybind> binds, string? gameId) =>
            string.IsNullOrWhiteSpace(gameId)
                ? Array.Empty<Keybind>()
                : binds?.Where(b => Same(b.GameId, gameId)).ToList() ?? new List<Keybind>();

        /// <summary>What is on this key, or null.</summary>
        public static Keybind? OnKey(IEnumerable<Keybind> binds, string? gameId, string key) =>
            For(binds, gameId).FirstOrDefault(b => Same(b.Key, key));

        /// <summary>
        /// Put a command on a key.
        ///
        /// One command per key: assigning replaces whatever was there. The alternative -
        /// stacking commands on one key - is how you end up with a config that fires three
        /// things at once and no way to see why.
        /// </summary>
        public static List<Keybind> Assign(IEnumerable<Keybind> binds, string gameId,
            string key, string commandId)
        {
            var list = binds?.ToList() ?? new List<Keybind>();
            list.RemoveAll(b => Same(b.GameId, gameId) && Same(b.Key, key));

            if (!string.IsNullOrWhiteSpace(commandId))
                list.Add(new Keybind { GameId = gameId, Key = key, CommandId = commandId });

            return list;
        }

        /// <summary>Take whatever is on a key off it.</summary>
        public static List<Keybind> Clear(IEnumerable<Keybind> binds, string gameId, string key) =>
            Assign(binds, gameId, key, "");

        /// <summary>Remove every bind for one game, leaving the others alone.</summary>
        public static List<Keybind> ClearGame(IEnumerable<Keybind> binds, string gameId)
        {
            var list = binds?.ToList() ?? new List<Keybind>();
            list.RemoveAll(b => Same(b.GameId, gameId));
            return list;
        }

        private static bool Same(string? a, string? b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Turns saved binds into the lines a game's own config expects.
    ///
    /// Pure text in, pure text out - it never touches a file. The services that own each
    /// game's config do the writing, which keeps the backup-and-restore behaviour they
    /// already have rather than adding a second thing that edits the same file.
    /// </summary>
    public static class KeybindWriter
    {
        /// <summary>Marks the block PlexusX owns, so rewriting it never disturbs anything the
        /// user wrote by hand around it.</summary>
        public const string BeginMarker = "// >>> PlexusX binds - edited by the app";
        public const string EndMarker = "// <<< PlexusX binds";

        /// <summary>The bind lines for one game, in the game's own syntax.</summary>
        public static string Build(IEnumerable<Keybind> binds, string gameId)
        {
            var sb = new StringBuilder();
            sb.AppendLine(BeginMarker);

            foreach (var bind in KeybindSet.For(binds, gameId).OrderBy(b => b.Key))
            {
                var command = GameCommands.ById(gameId, bind.CommandId);
                if (command == null) continue;   // catalogue changed under a saved bind

                // CS2 quotes both halves; Rust's console does not use quotes.
                sb.AppendLine(gameId == "cs2"
                    ? $"bind \"{bind.Key}\" \"{command.Command}\""
                    : $"bind {bind.Key} \"{command.Command}\"");
            }

            sb.AppendLine(EndMarker);
            return sb.ToString();
        }

        /// <summary>
        /// Replace PlexusX's block inside an existing config, or append it if there isn't one.
        ///
        /// Everything outside the markers is preserved exactly. People have binds and settings
        /// they wrote themselves in these files, and silently eating them would be far worse
        /// than the feature is good.
        /// </summary>
        public static string Merge(string? existingConfig, string block)
        {
            var existing = existingConfig ?? "";
            int start = existing.IndexOf(BeginMarker, StringComparison.Ordinal);

            if (start < 0)
            {
                var separator = existing.Length == 0 || existing.EndsWith("\n") ? "" : "\r\n";
                return existing + separator + block;
            }

            int end = existing.IndexOf(EndMarker, start, StringComparison.Ordinal);
            if (end < 0) return existing.Substring(0, start) + block;   // truncated block

            end += EndMarker.Length;
            // Take the line ending with it so repeated writes don't accumulate blank lines.
            while (end < existing.Length && (existing[end] == '\r' || existing[end] == '\n')) end++;

            return existing.Substring(0, start) + block + existing.Substring(end);
        }
    }
}
