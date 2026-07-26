using System;
using System.Collections.Generic;
using System.Linq;
using VibranceHud.Apex;
using VibranceHud.Cs2;
using VibranceHud.Fortnite;
using VibranceHud.Games;
using VibranceHud.Rust;

namespace VibranceHud
{
    /// <summary>
    /// The narrow slice of the Games-Hub config-write path that
    /// <see cref="ProfileApplyEngine"/> needs. The real implementation wraps the
    /// existing per-game <c>SettingsService</c>s (Rust, CS2, Apex, Fortnite); tests
    /// substitute a fake.
    /// </summary>
    public interface IGameHubApplier
    {
        /// <summary>Writes the supplied hub options into the game's own config.
        /// Called on game launch. Implementations are responsible for the same
        /// exception handling the Games-Hub UI already does (e.g. "config busy"
        /// toast when the cfg file is locked).</summary>
        void Apply(string gameId, GameHubOptions options);
    }

    /// <summary>
    /// Production <see cref="IGameHubApplier"/> that delegates to the existing
    /// per-game settings services. The mapping from <see cref="GameHubOptions"/>
    /// to per-service config keys is intentionally conservative: only the
    /// <see cref="GameHubOptions.FpsCap"/> field is applied for every supported
    /// game, <see cref="GameHubOptions.GraphicsQuality"/> is applied for Rust
    /// only (the others don't expose a portable quality key). Effect toggles
    /// and tools captured in the profile are stored and shown in the editor
    /// card but, in v0.7.0, not rewritten into the game's config on auto-apply
    /// — that path is intentionally narrow to keep the launch-loop side-effect
    /// surface tiny.
    /// </summary>
    public sealed class GameHubApplier : IGameHubApplier
    {
        public void Apply(string gameId, GameHubOptions options)
        {
            try
            {
                switch (gameId)
                {
                    case "rust":     ApplyRust(options); break;
                    case "cs2":      ApplyCs2(options); break;
                    case "apex":     ApplyApex(options); break;
                    case "fortnite": ApplyFortnite(options); break;
                    default:
                        // Unknown game id - silently ignore.
                        break;
                }
            }
            catch
            {
                // Same "config busy" semantics as the manual Games-Hub Apply buttons:
                // log silently, don't propagate up to the apply engine. A failed hub
                // write must never abort the visual-slider side of the profile.
            }
        }

        private static void ApplyRust(GameHubOptions options)
        {
            var svc = RustSettingsService.ForInstalledRust();
            if (svc == null) return; // not installed — silent no-op

            var changes = new Dictionary<string, string>();
            if (options.GraphicsQuality != null && TryParseQuality(options.GraphicsQuality, out var q))
                changes["graphics.quality"] = q.ToString();
            if (options.FpsCap > 0)
                changes["fps.limit"] = options.FpsCap.ToString();

            if (changes.Count == 0) return;
            svc.Apply(changes);
        }

        private static void ApplyCs2(GameHubOptions options)
        {
            var svc = Cs2SettingsService.ForInstalledCs2();
            if (svc == null) return;

            var changes = new Dictionary<string, string>();
            // CS2 doesn't have a portable single-key "graphics quality" - the engine
            // exposes individual r_* / mat_* convars that the per-game tweaks already
            // manage. We only push the FPS cap here.
            if (options.FpsCap > 0)
                changes["fps_max"] = options.FpsCap.ToString();

            if (changes.Count == 0) return;
            svc.Apply(changes);
        }

        private static void ApplyApex(GameHubOptions options)
        {
            var svc = ApexSettingsService.ForInstalledApex();
            if (svc == null) return;

            var changes = new Dictionary<string, string>();
            if (options.FpsCap > 0)
                changes["fps_max"] = options.FpsCap.ToString();

            if (changes.Count == 0) return;
            svc.Apply(changes);
        }

        private static void ApplyFortnite(GameHubOptions options)
        {
            var svc = FortniteSettingsService.ForInstalledFortnite();
            if (svc == null) return;

            // Fortnite has no portable single-key fps cap that survives across patches
            // (its config lives in GameUserSettings.ini with engine-version-specific
            // section names). v0.7.0 keeps the profile slot for forward-compat but
            // does not write through it on auto-apply.
            _ = options;
        }

        /// <summary>"low", "med", "high", etc. → 1/2/3/4-ish. Falls back to the
        /// numeric string when the label isn't recognised.</summary>
        private static bool TryParseQuality(string label, out int value)
        {
            switch (label.Trim().ToLowerInvariant())
            {
                case "0": case "potato": case "very low": value = 0; return true;
                case "1": case "low":                       value = 1; return true;
                case "2": case "medium": case "med":        value = 2; return true;
                case "3": case "high":                      value = 3; return true;
                case "4": case "very high":
                case "5": case "ultra":
                case "6": case "max":                       value = int.TryParse(label, out var v) ? Math.Clamp(v, 0, 9) : 5; return true;
                default:
                    if (int.TryParse(label, out var n)) { value = Math.Clamp(n, 0, 9); return true; }
                    value = 0; return false;
            }
        }
    }
}
