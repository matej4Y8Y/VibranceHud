using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VibranceHud.Games
{
    /// <summary>
    /// Pure detection logic: given the Steam library paths and the supported-games catalog,
    /// work out which games are installed by checking for their appmanifest. The filesystem
    /// check is injected (<paramref name="fileExists"/>) so this is unit-testable with fakes.
    /// </summary>
    public static class GameDetection
    {
        public static IReadOnlyList<DetectedGame> DetectInstalled(
            IEnumerable<string> libraryPaths,
            IEnumerable<SupportedGame> games,
            Func<string, bool> fileExists)
        {
            var libs = libraryPaths.ToList();
            var result = new List<DetectedGame>();

            foreach (var game in games)
            {
                foreach (var lib in libs)
                {
                    var manifest = Path.Combine(lib, "steamapps", $"appmanifest_{game.SteamAppId}.acf");
                    if (!fileExists(manifest)) continue;

                    var installDir = Path.Combine(lib, "steamapps", "common", game.InstallFolder);
                    result.Add(new DetectedGame(game, installDir));
                    break; // found this game; stop scanning further libraries
                }
            }
            return result;
        }
    }
}
