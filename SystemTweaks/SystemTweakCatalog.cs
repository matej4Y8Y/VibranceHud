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
    ///
    /// Previously also accepted an NvAppRustProfileTweak for NVIDIA Experience per-game
    /// profile tweaks, but that path was removed in v0.9.0 (the tweak didn't work on
    /// the user's machine - see docs/design/specs/2026-07-29-remove-nvidia-tweaks.md).
    /// </summary>
    public sealed class SystemTweakCatalog
    {
        private readonly IRegistryAccess _reg;

        public SystemTweakCatalog(IRegistryAccess reg)
        {
            _reg = reg;
        }

        private const string GameConfig = @"System\GameConfigStore";
        private const string SystemProfile =
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        private const string GamesTask = SystemProfile + @"\Tasks\Games";
        private const string PowerThrottling = @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling";
        private const string GraphicsDrivers = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
        private const string MouseKey = @"Control Panel\Mouse";
        private const string GameDvrKey = @"Software\Microsoft\Windows\CurrentVersion\GameDVR";

        public IReadOnlyList<ISystemTweak> All
        {
            get
            {
                return new List<ISystemTweak>
                {
                    // ---- Safe: clean, reversible, real ----
                    // Two values, not one. GameDVR_Enabled is the app-level switch; the thing
                    // that actually keeps a recorder running behind every game is
                    // AppCaptureEnabled. Setting only the first left the capture pipeline alive,
                    // so the tweak reported success and changed nothing measurable.
                    new RegistryTweak(_reg, "game-dvr", "Disable Game DVR",
                        "Turns off the recorder Windows leaves running behind every game.",
                        "Windows", TweakTier.Safe, "Game DVR and background capture turned off",
                        new RegistrySetting(RegistryRoot.CurrentUser, GameConfig, "GameDVR_Enabled", "0", "1"),
                        new RegistrySetting(RegistryRoot.CurrentUser, GameDvrKey, "AppCaptureEnabled", "0", "1")),

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

                    // The Games MMCSS task also carries a scheduling category and a GPU
                    // priority. Raising the category is what actually moves the game's threads
                    // ahead of background work - the Priority above only orders tasks within
                    // the same category, so on its own it does less than people assume.
                    new RegistryTweak(_reg, "games-scheduling-category", "Give Games Top Scheduling",
                        "Moves games into Windows' highest scheduling class, ahead of background work.",
                        "System", TweakTier.Safe, "Games moved to top scheduling class",
                        // Only the category. GPU Priority is already 8 by default, so writing 8
                        // would be a no-op that still leaves our fingerprint on the machine.
                        new RegistrySetting(RegistryRoot.LocalMachine, GamesTask, "Scheduling Category",
                            "High", "Medium", RegistryKind.String)),

                    // REMOVED: "Favour the Active Window" (Win32PrioritySeparation = 38).
                    //
                    // It was doing the opposite of what it claimed. The value is three 2-bit
                    // fields - foreground bias, variable/fixed quantum, short/long quantum:
                    //
                    //    38 = 0b10_01_10 -> 3:1 bias, variable, LONG quantums
                    //     2 = 0b00_00_10 -> 3:1 bias, variable, SHORT quantums (desktop default)
                    //
                    // So Windows already gives a desktop the 3:1 foreground bias out of the box.
                    // The only thing writing 38 changed was short -> long quantums, which is the
                    // *server* configuration: threads run longer before being preempted, which
                    // costs a game latency rather than gaining it anything.
                    //
                    // There is no value here that beats the default, so the honest move is to
                    // ship no toggle at all. See PriorityControlIsLeftAlone in the tests.

                    // Windows throttles background processes to save power. On a desktop that
                    // only costs performance, and it can clip a game's own helper threads.
                    new RegistryTweak(_reg, "power-throttling", "Stop CPU Power Throttling",
                        "Stops Windows slowing processes down to save power - pointless on a desktop.",
                        "System", TweakTier.Safe, "Power throttling disabled",
                        new RegistrySetting(RegistryRoot.LocalMachine, PowerThrottling,
                            "PowerThrottlingOff", "1", null)),

                    // Mouse acceleration: not FPS, but it's the single most common thing wrong
                    // with an aim setup, and it's on by default in Windows.
                    new RegistryTweak(_reg, "mouse-accel", "Turn Off Mouse Acceleration",
                        "Makes aim 1:1 with your hand - Windows speeds the pointer up when you move fast.",
                        "Input", TweakTier.Safe, "Mouse acceleration off - aim is now 1:1",
                        new RegistrySetting(RegistryRoot.CurrentUser, MouseKey, "MouseSpeed", "0", "1", RegistryKind.String),
                        new RegistrySetting(RegistryRoot.CurrentUser, MouseKey, "MouseThreshold1", "0", "6", RegistryKind.String),
                        new RegistrySetting(RegistryRoot.CurrentUser, MouseKey, "MouseThreshold2", "0", "10", RegistryKind.String)),

                    // REMOVED: "Disable Windows Game Mode".
                    //
                    // Shipped as "helps some PCs, hurts others", which is another way of saying
                    // we didn't know. On Windows 11 Game Mode is neutral to mildly positive on
                    // most machines, so the toggle's honest description was "probably does
                    // nothing, might cost you frames". That is exactly the padding this catalog
                    // exists to avoid.

                    // ---- Advanced: real but situational (off by default, flagged in the UI) ----
                    // Hardware-accelerated GPU scheduling. Genuinely helps latency on most
                    // modern cards and genuinely hurts on some older ones, and unlike everything
                    // else here it needs a reboot to take effect - hence Advanced.
                    new RegistryTweak(_reg, "hags", "Hardware GPU Scheduling",
                        "Lets the GPU manage its own work queue. Usually lowers latency - needs a restart, and helps less on older cards.",
                        "Windows", TweakTier.Advanced, "GPU scheduling on - restart to apply",
                        new RegistrySetting(RegistryRoot.LocalMachine, GraphicsDrivers, "HwSchMode", "2", "1")),

                    // REMOVED: "Disable Fullscreen Optimisations".
                    //
                    // Fullscreen Optimisations is what lets a borderless game present like
                    // exclusive fullscreen. Disabling it sends borderless back through the
                    // desktop compositor, which costs frames.
                    //
                    // The guides that recommend it assume exclusive fullscreen. Our users don't
                    // play that way - in Rust essentially everyone is borderless - so for this
                    // audience the toggle is a straight downgrade, and its old description
                    // ("lower latency in most games") was backwards for them.
                    //
                    // Measured: 77 fps with everything off, 73 with everything on.
};
            }
        }
    }
}