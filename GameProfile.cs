using System;
using System.Text.Json.Serialization;

namespace VibranceHud
{
    /// <summary>
    /// A per-game profile saved by the user: the four visual slider values PlexusX
    /// applies when the game launches, plus the Game-Hub options that should be
    /// written to the game's own config (graphics quality, FPS cap, toggles, tools).
    /// Persisted to %LOCALAPPDATA%\PlexusX\profiles.json by <see cref="GameProfileStore"/>.
    /// </summary>
    public sealed class GameProfile
    {
        [JsonPropertyName("gameId")]      public string GameId { get; set; } = "";
        [JsonPropertyName("displayName")] public string DisplayName { get; set; } = "";

        // Visual sliders
        [JsonPropertyName("vibrance")]    public int Vibrance { get; set; } = 100;
        [JsonPropertyName("saturation")]  public int Saturation { get; set; } = 100;
        [JsonPropertyName("brightness")]  public int Brightness { get; set; } = 100;
        [JsonPropertyName("gamma")]       public int Gamma { get; set; } = 100;

        // Game-Hub options (per-game; games with no hub options get an empty object)
        [JsonPropertyName("gameHub")] public GameHubOptions GameHub { get; set; } = new();

        [JsonPropertyName("lastUpdated")] public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>The Game-Hub options a profile can carry. Stored as a sub-object
    /// inside <see cref="GameProfile"/> so a profile for a game with no hub options
    /// still round-trips cleanly (just an empty object).</summary>
    public sealed class GameHubOptions
    {
        [JsonPropertyName("graphicsQuality")] public string GraphicsQuality { get; set; } = "";
        [JsonPropertyName("fpsCap")]          public int FpsCap { get; set; } = 0;
        [JsonPropertyName("effectToggles")]   public string[] EffectToggles { get; set; } = Array.Empty<string>();
        [JsonPropertyName("tools")]            public string[] Tools { get; set; } = Array.Empty<string>();
    }
}