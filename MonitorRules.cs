using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace VibranceHud
{
    /// <summary>
    /// "When this game launches, put the desktop in this mode."
    ///
    /// Width/height of 0 means "leave it alone" - that is the default for every game, and it
    /// is what removing a rule sets it back to. Storing an explicit zero rather than deleting
    /// the entry keeps the round-trip simple and means a rule the user turned off does not
    /// silently come back if something else writes the list.
    /// </summary>
    public sealed class MonitorRule
    {
        [JsonPropertyName("gameId")] public string GameId { get; set; } = "";
        [JsonPropertyName("width")]  public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }

        [JsonIgnore] public bool IsSet => Width > 0 && Height > 0;
    }

    /// <summary>
    /// The per-game resolution rules, and the pure logic for reading and writing them.
    ///
    /// Separated from the page so the behaviour that matters - a rule survives a round trip,
    /// clearing one does not disturb the others, an unknown game returns "no rule" rather
    /// than throwing - is testable without a monitor attached.
    /// </summary>
    public static class MonitorRules
    {
        /// <summary>The rule for a game, or null when it has none.</summary>
        public static MonitorRule? For(IEnumerable<MonitorRule> rules, string? gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return null;
            var rule = rules?.FirstOrDefault(r =>
                string.Equals(r.GameId, gameId, StringComparison.OrdinalIgnoreCase));
            return rule is { IsSet: true } ? rule : null;
        }

        /// <summary>
        /// Set (or replace) a game's rule. Returns the updated list rather than mutating in
        /// place so the caller decides when it becomes the saved state.
        /// </summary>
        public static List<MonitorRule> Set(IEnumerable<MonitorRule> rules, string gameId,
            int width, int height)
        {
            var list = rules?.ToList() ?? new List<MonitorRule>();
            list.RemoveAll(r => string.Equals(r.GameId, gameId, StringComparison.OrdinalIgnoreCase));

            if (width > 0 && height > 0)
                list.Add(new MonitorRule { GameId = gameId, Width = width, Height = height });

            return list;
        }

        /// <summary>Remove a game's rule. Same as setting it to 0x0.</summary>
        public static List<MonitorRule> Clear(IEnumerable<MonitorRule> rules, string gameId) =>
            Set(rules, gameId, 0, 0);
    }
}
