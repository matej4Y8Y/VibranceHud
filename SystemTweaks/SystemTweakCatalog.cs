using System.Collections.Generic;

namespace VibranceHud.SystemTweaks
{
    /// <summary>
    /// The curated set of system-wide FPS/latency tweaks, grouped for the UI. Curated by
    /// *actual* effect - every entry does something measurable to how the machine runs, not
    /// the "disable a random service to free 4MB" padding common in optimizer apps.
    ///
    /// Each entry names the exact registry values it writes, verified against documented
    /// Windows behaviour. Reversible: every setting carries its stock value (or null = the
    /// value simply isn't there by default, so reverting deletes it).
    /// </summary>
    public sealed class SystemTweakCatalog
    {
        private readonly IRegistryAccess _reg;

        public SystemTweakCatalog(IRegistryAccess reg) => _reg = reg;

        private const string GameConfig = @"System\GameConfigStore";
        private const string SystemProfile =
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        private const string GamesTask = SystemProfile + @"\Tasks\Games";

        public IReadOnlyList<ISystemTweak> All => new ISystemTweak[]
        {
            // ---- Safe: clean, reversible, real ----
            new RegistryTweak(_reg, "game-dvr", "Disable Game DVR",
                "Turns off Windows' background game recording, which quietly steals GPU time.",
                "Windows", TweakTier.Safe, "Game DVR turned off",
                new RegistrySetting(RegistryRoot.CurrentUser, GameConfig, "GameDVR_Enabled", "0", "1")),

            new RegistryTweak(_reg, "network-throttling", "Remove Network Throttling",
                "Lifts Windows' 10-packet-per-ms cap so online games get the full connection.",
                "Network", TweakTier.Safe, "Network throttling removed",
                new RegistrySetting(RegistryRoot.LocalMachine, SystemProfile, "NetworkThrottlingIndex",
                    "4294967295", "10")),

            new RegistryTweak(_reg, "system-responsiveness", "Prioritise Foreground Game",
                "Lets your game use more CPU by shrinking the slice Windows reserves for background tasks.",
                "System", TweakTier.Safe, "Foreground priority raised",
                new RegistrySetting(RegistryRoot.LocalMachine, SystemProfile, "SystemResponsiveness", "0", "20")),

            new RegistryTweak(_reg, "games-task-priority", "Boost Games Scheduling",
                "Raises the CPU priority Windows gives programs it recognises as games (default 2 -> 6).",
                "System", TweakTier.Safe, "Game scheduling boosted",
                new RegistrySetting(RegistryRoot.LocalMachine, GamesTask, "Priority", "6", "2")),

            // ---- Advanced: real but situational (off by default, flagged in the UI) ----
            new RegistryTweak(_reg, "game-mode", "Disable Windows Game Mode",
                "Game Mode helps on some PCs and hurts on others. Turn it off if you see stutter with it on.",
                "Windows", TweakTier.Advanced, "Game Mode turned off",
                new RegistrySetting(RegistryRoot.CurrentUser, @"Software\Microsoft\GameBar",
                    "AllowAutoGameMode", "0", "1")),
        };
    }
}
