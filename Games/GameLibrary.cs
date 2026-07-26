using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VibranceHud.Games
{
    /// <summary>
    /// Top-level entry point for the Games Hub: finds Steam and Epic, and returns the
    /// supported games actually installed on this PC across both stores. Returns empty
    /// (never throws) when a store or its games are absent.
    /// </summary>
    public static class GameLibrary
    {
        public static IReadOnlyList<DetectedGame> DetectInstalled()
        {
            var result = new List<DetectedGame>();
            result.AddRange(DetectSteam());
            result.AddRange(DetectEpic());
            return result;
        }

        private static IReadOnlyList<DetectedGame> DetectSteam()
        {
            try
            {
                var steam = SteamLocator.FindSteamPath();
                if (steam == null) return Array.Empty<DetectedGame>();

                var libraries = SteamLocator.GetLibraries(steam);
                var steamGames = SupportedGames.All.Where(g => g.SteamAppId > 0);
                return GameDetection.DetectInstalled(libraries, steamGames, File.Exists);
            }
            catch
            {
                return Array.Empty<DetectedGame>();
            }
        }

        private static IReadOnlyList<DetectedGame> DetectEpic()
        {
            try
            {
                var result = new List<DetectedGame>();
                foreach (var game in SupportedGames.All.Where(g => g.EpicAppName != null))
                {
                    var loc = EpicLocator.FindGameInstall(game.EpicAppName!);
                    if (loc != null) result.Add(new DetectedGame(game, loc));
                }
                return result;
            }
            catch
            {
                return Array.Empty<DetectedGame>();
            }
        }
    }
}
