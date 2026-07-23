using System;
using System.Collections.Generic;
using System.IO;

namespace VibranceHud.Games
{
    /// <summary>
    /// Top-level entry point for the Games Hub: finds Steam, enumerates its libraries, and
    /// returns the supported games actually installed on this PC. Returns empty (never
    /// throws) when Steam or the games are absent.
    /// </summary>
    public static class GameLibrary
    {
        public static IReadOnlyList<DetectedGame> DetectInstalled()
        {
            try
            {
                var steam = SteamLocator.FindSteamPath();
                if (steam == null) return Array.Empty<DetectedGame>();

                var libraries = SteamLocator.GetLibraries(steam);
                return GameDetection.DetectInstalled(libraries, SupportedGames.All, File.Exists);
            }
            catch
            {
                return Array.Empty<DetectedGame>();
            }
        }
    }
}
