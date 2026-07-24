using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;

namespace VibranceHud.SystemTweaks
{
    /// <summary>
    /// Ties the catalog to the real machine and handles the admin problem: the app runs
    /// un-elevated (so Vibrance/Games don't demand UAC), and only the specific HKLM tweaks
    /// relaunch a one-off elevated copy of PlexusX to do their write. HKCU tweaks just run
    /// in-process. Either way the user sees a single UAC prompt scoped to that one toggle.
    /// </summary>
    public sealed class SystemTweakService
    {
        private readonly SystemTweakCatalog _catalog;

        public SystemTweakService() : this(new SystemTweakCatalog(new RegistryAccess())) { }
        public SystemTweakService(SystemTweakCatalog catalog) => _catalog = catalog;

        public System.Collections.Generic.IReadOnlyList<ISystemTweak> All => _catalog.All;

        public static bool IsElevated()
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// Turn a tweak on or off. Returns the status line to show, or null if it failed / the
        /// user declined the UAC prompt. Admin-only tweaks are done by an elevated relaunch
        /// unless we're already elevated.
        /// </summary>
        public string? Toggle(string id, bool on)
        {
            var tweak = _catalog.All.FirstOrDefault(t => t.Id == id);
            if (tweak == null) return null;

            if (tweak.RequiresAdmin && !IsElevated())
                return RunElevated(id, on) ? StatusFor(tweak, on) : null;

            return ApplyInProcess(tweak, on);
        }

        private static string StatusFor(ISystemTweak tweak, bool on) =>
            on && tweak is RegistryTweak r ? r.AppliedStatus : on ? "Applied" : "Reverted";

        private static string? ApplyInProcess(ISystemTweak tweak, bool on)
        {
            try
            {
                if (on) return tweak.Apply().StatusText;
                tweak.Revert();
                return "Reverted";
            }
            catch
            {
                return null; // e.g. denied access - surfaced to the user by the caller
            }
        }

        /// <summary>Relaunch PlexusX elevated to run a single headless apply/revert, and wait.</summary>
        private static bool RunElevated(string id, bool on)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath!,
                    Arguments = $"--tweak {(on ? "apply" : "revert")} {id}",
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

        /// <summary>
        /// The headless side of the elevated relaunch. Program.cs calls this when it sees the
        /// <c>--tweak</c> args, before any window is created. Returns a process exit code.
        /// </summary>
        public static int RunHeadless(string[] args)
        {
            // args: --tweak apply|revert <id>
            if (args.Length < 3) return 2;
            bool on = args[1].Equals("apply", StringComparison.OrdinalIgnoreCase);
            var id = args[2];

            var tweak = new SystemTweakCatalog(new RegistryAccess()).All.FirstOrDefault(t => t.Id == id);
            if (tweak == null) return 2;

            try
            {
                if (on) tweak.Apply(); else tweak.Revert();
                return 0;
            }
            catch
            {
                return 1;
            }
        }

        /// <summary>True when the process was started just to run a headless tweak op.</summary>
        public static bool IsHeadlessTweakInvocation(string[] args) =>
            args.Length >= 1 && args[0].Equals("--tweak", StringComparison.OrdinalIgnoreCase);
    }
}
