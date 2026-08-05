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

        // The rest of the look.
        //
        // A profile is now created by capturing whatever is on the Display page, so it has to
        // carry everything that page can set. It previously held only the four above, which
        // meant "save my look" would silently drop contrast, warmth and the whole advanced
        // grade - the user would apply their own profile and get a different screen.
        //
        // Nullable so a profile saved before these existed reads as "not set" rather than as
        // a deliberate zero, which for contrast would mean a grey screen.
        [JsonPropertyName("contrast")]    public int? Contrast { get; set; }
        [JsonPropertyName("temperature")] public int? Temperature { get; set; }
        [JsonPropertyName("tone")]        public ToneSettings? Tone { get; set; }

        [JsonIgnore] public int ResolvedContrast => Contrast ?? 100;
        [JsonIgnore] public int ResolvedTemperature => Temperature ?? 0;

        /// <summary>The grade to apply, with gamma folded in from the standalone field so the
        /// two can never disagree.</summary>
        [JsonIgnore]
        public ToneSettings ResolvedTone =>
            (Tone ?? ToneSettings.Neutral) with { Gamma = Gamma };

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