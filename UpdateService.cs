using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// Self-update against GitHub Releases, built for the Inno Setup installer we ship:
    /// ask GitHub for the newest release, compare versions, and - if there's a newer one -
    /// download that installer and run it. Because the installer keeps the same AppId it
    /// upgrades the existing install in place; we exit so it can replace our files.
    ///
    /// No account or token needed: it only reads the public releases endpoint.
    /// </summary>
    public static class UpdateService
    {
        private const string LatestApi =
            "https://api.github.com/repos/matej4Y8Y/VibranceHud/releases/latest";

        /// <summary>The running app's version, normalised to major.minor.build.</summary>
        public static Version CurrentVersion
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
                return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
            }
        }

        private static HttpClient NewClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            // GitHub rejects requests without a User-Agent.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PlexusX-Updater");
            return client;
        }

        private static async Task<ReleaseInfo?> FetchLatestAsync()
        {
            using var client = NewClient();
            var json = await client.GetStringAsync(LatestApi);
            return GitHubReleases.ParseLatest(json);
        }

        /// <summary>
        /// Runs on every launch. Stays silent unless there's genuinely a newer release,
        /// and never interrupts startup if the machine is offline.
        /// </summary>
        public static async Task CheckOnStartupAsync()
        {
            try
            {
                var latest = await FetchLatestAsync();
                if (latest == null || !GitHubReleases.IsNewer(latest.Version, CurrentVersion)) return;
                PromptAndInstall(latest);
            }
            catch
            {
                // Offline, rate-limited, or no releases yet - just skip quietly.
            }
        }

        /// <summary>Manual check from Settings / the tray: reports either way.</summary>
        public static async Task CheckManuallyAsync()
        {
            try
            {
                var latest = await FetchLatestAsync();
                if (latest == null)
                {
                    Info("Couldn't read the release list. Try again later.");
                    return;
                }
                if (!GitHubReleases.IsNewer(latest.Version, CurrentVersion))
                {
                    Info($"You're on the latest version ({CurrentVersion}).");
                    return;
                }
                PromptAndInstall(latest);
            }
            catch (Exception ex)
            {
                Info("Couldn't check for updates:\n\n" + ex.Message);
            }
        }

        private static void PromptAndInstall(ReleaseInfo latest)
        {
            var choice = MessageBox.Show(
                $"PlexusX {latest.Version} is available (you have {CurrentVersion}).\n\n" +
                "Download and install it now? PlexusX will close while it updates.",
                "PlexusX", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (choice != DialogResult.Yes) return;

            _ = DownloadAndRunAsync(latest);
        }

        private static async Task DownloadAndRunAsync(ReleaseInfo latest)
        {
            try
            {
                var file = Path.Combine(Path.GetTempPath(),
                    $"PlexusX-Setup-{latest.Version}.exe");

                using (var client = NewClient())
                using (var stream = await client.GetStreamAsync(latest.InstallerUrl))
                using (var target = File.Create(file))
                {
                    await stream.CopyToAsync(target);
                }

                Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
                Application.Exit(); // let the installer replace our files
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The update couldn't be downloaded:\n\n" + ex.Message +
                    "\n\nYou can grab it manually from the releases page.",
                    "PlexusX", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void Info(string text) =>
            MessageBox.Show(text, "PlexusX", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
