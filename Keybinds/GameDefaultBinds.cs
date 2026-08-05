using System;
using System.Collections.Generic;

namespace VibranceHud.Keybinds
{
    /// <summary>
    /// The keys each supported game ships already using.
    ///
    /// Exists so the keyboard can warn before somebody binds a PlexusX command over their
    /// reload or their sprint. Without it the board shows sixty free-looking keys, most of
    /// which are not free at all, and the first thing a user learns about the feature is that
    /// it broke their movement.
    ///
    /// These are the GAME'S DEFAULTS, not a reading of the player's own config. Somebody who
    /// has rebound their keys will see marks that do not match their setup, so every place
    /// this is surfaced has to say "default" rather than "in use" - claiming to know
    /// something we have not measured is the mistake this codebase has already made once,
    /// with screen capture.
    ///
    /// Deliberately conservative. Only bindings that are unambiguous defaults are listed; a
    /// half-remembered one is worse than a blank key, because a wrong warning trains people
    /// to ignore the right ones.
    /// </summary>
    public static class GameDefaultBinds
    {
        /// <summary>Keys shared by essentially every shooter, so they are listed once.</summary>
        private static readonly (string Key, string Purpose)[] Common =
        {
            ("w", "Forward"), ("a", "Left"), ("s", "Back"), ("d", "Right"),
            ("space", "Jump"), ("leftcontrol", "Crouch"), ("leftshift", "Sprint"),
            ("r", "Reload"), ("e", "Use"), ("tab", "Scoreboard"),
            ("mouse1", "Fire"), ("mouse2", "Aim"),
            ("escape", "Menu"),
        };

        private static readonly (string Key, string Purpose)[] RustOnly =
        {
            ("1", "Hotbar 1"), ("2", "Hotbar 2"), ("3", "Hotbar 3"),
            ("4", "Hotbar 4"), ("5", "Hotbar 5"), ("6", "Hotbar 6"),
            ("tab", "Inventory"), ("f1", "Console"), ("leftalt", "Free look"),
            ("q", "Gestures"),
        };

        private static readonly (string Key, string Purpose)[] Cs2Only =
        {
            ("1", "Primary"), ("2", "Secondary"), ("3", "Knife"),
            ("4", "Grenades"), ("5", "Bomb"),
            ("q", "Last weapon"), ("g", "Drop"), ("b", "Buy menu"),
        };

        /// <summary>keyId → what the game uses it for by default. Empty for a game we have
        /// not catalogued, which is treated as "we don't know" rather than "nothing is
        /// bound".</summary>
        public static IReadOnlyDictionary<string, string> For(string? gameId)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var specific = gameId?.ToLowerInvariant() switch
            {
                "rust" => RustOnly,
                "cs2" => Cs2Only,
                _ => null,
            };

            // An uncatalogued game gets nothing at all. Showing the common shooter keys for a
            // game we know nothing about would be guessing, and a guess presented as fact is
            // exactly what this class is here to avoid.
            if (specific == null) return map;

            foreach (var (key, purpose) in Common) map[key] = purpose;

            // Game-specific entries win: Rust's Tab is Inventory, not Scoreboard.
            foreach (var (key, purpose) in specific) map[key] = purpose;

            return map;
        }

        /// <summary>Whether we have anything to say about this game at all.</summary>
        public static bool Knows(string? gameId) => For(gameId).Count > 0;
    }
}
