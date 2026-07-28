using System;
using System.Diagnostics;

namespace VibranceHud.Nvidia
{
    /// <summary>
    /// Bridges the un-elevated PlexusX process and the admin-required NVAPI save path.
    ///
    /// NVAPI's profile save writes to <c>C:\ProgramData\NVIDIA Corporation\Drs\nvdrsdb0.bin</c>
    /// which needs admin write access. PlexusX intentionally runs un-elevated (no UAC for
    /// vibrance/games), so when a standard user's DRS save fails with "Access denied", this
    /// service relaunches PlexusX once with the <c>runas</c> verb to perform exactly one
    /// tweak op (apply or revert), then exits with the success code. The user sees a single
    /// scoped UAC prompt, and the toggle goes "Applied".
    ///
    /// Modeled on <see cref="VibranceHud.SystemTweaks.SystemTweakService"/> which already
    /// does the same dance for HKLM registry tweaks. NVAPI is just another admin-only
    /// tweakable surface.
    /// </summary>
    public static class NvidiaTweakElevationService
    {
        /// <summary>Launch an elevated helper that applies <paramref name="on"/> for
        /// <paramref name="tweakId"/> with <paramref name="fpsCap"/> (only meaningful
        /// for the fps-cap id; ignored otherwise). Returns true only when the helper
        /// exited 0 - i.e. the UAC prompt was accepted AND the DRS save succeeded.
        /// False covers both "user declined UAC" and "save still failed elevated".</summary>
        public static bool RunElevated(string tweakId, bool on, int fpsCap)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath!,
                    // --nvtweak apply <id> <on=0|1> <fps>  -- args parsed by Program.cs.
                    // Using the same --tweak channel naming is intentional: it keeps the
                    // headless invocation contract discoverable from a single pattern.
                    Arguments = $"--nvtweak apply {tweakId} {(on ? 1 : 0)} {fpsCap}",
                    UseShellExecute = true,
                    Verb = "runas", // triggers the UAC prompt
                };
                var proc = Process.Start(psi);
                if (proc == null) return false;
                proc.WaitForExit();
                return proc.ExitCode == 0;
            }
            catch
            {
                return false; // user clicked "No" on UAC, or it couldn't start
            }
        }

        /// <summary>True when the process was started just to run a headless NVAPI tweak op.</summary>
        public static bool IsHeadlessInvocation(string[] args) =>
            args.Length >= 1 && args[0].Equals("--nvtweak", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The headless side of the elevated relaunch. Program.cs calls this when it
        /// sees the <c>--nvtweak</c> args, before any window is created. Returns a
        /// process exit code: 0 = success, 1 = tweak reported NeedsAdmin / Unsupported,
        /// 2 = bad args.
        /// </summary>
        public static int RunHeadless(string[] args)
        {
            // args: --nvtweak apply <id> <on> <fpsCap>
            if (args.Length < 5) return 2;
            var op = args[1];
            if (!op.Equals("apply", StringComparison.OrdinalIgnoreCase) &&
                !op.Equals("revert", StringComparison.OrdinalIgnoreCase))
                return 2;

            var tweakId = args[2];
            // "apply" carries the user's intent (on/off in the UI == on here);
            // "revert" is treated as off so the stock value gets written back.
            bool on =
                op.Equals("apply", StringComparison.OrdinalIgnoreCase)
                && args[3] == "1";

            if (!int.TryParse(args[4], out var fpsCap)) fpsCap = 0;

            var result = NvidiaDriverSettings.ApplyHeadless(tweakId, on, fpsCap);
            return result == NvidiaApplyResult.Success ? 0 : 1;
        }
    }
}
