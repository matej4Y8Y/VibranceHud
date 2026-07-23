using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Velopack;
using Velopack.Sources;

namespace VibranceHud
{
    /// <summary>
    /// Self-update against GitHub Releases. Every installed copy checks the repo for a
    /// newer release; if one exists it downloads and applies it, then restarts into the
    /// new version. Only works for copies installed via the Velopack Setup.exe (a plain
    /// unzipped exe has nowhere to update into) - which is exactly why we ship an
    /// installer.
    /// </summary>
    public static class UpdateService
    {
        private const string RepoUrl = "https://github.com/matej4Y8Y/VibranceHud";

        private static UpdateManager NewManager() =>
            new(new GithubSource(RepoUrl, accessToken: null, prerelease: false));

        /// <summary>
        /// Silent background check used on startup: if an update exists, fetch and stage
        /// it so it installs on next launch. Never interrupts the user, never throws.
        /// </summary>
        public static async Task CheckInBackgroundAsync()
        {
            try
            {
                var mgr = NewManager();
                if (!mgr.IsInstalled) return;

                var update = await mgr.CheckForUpdatesAsync();
                if (update == null) return;

                await mgr.DownloadUpdatesAsync(update);
                mgr.ApplyUpdatesAndRestart(update);
            }
            catch
            {
                // Offline, rate-limited, or no releases yet - just skip quietly.
            }
        }

        /// <summary>
        /// Manual check from the tray menu: tells the user the result either way, and
        /// asks before restarting to apply.
        /// </summary>
        public static async Task CheckManuallyAsync()
        {
            try
            {
                var mgr = NewManager();
                if (!mgr.IsInstalled)
                {
                    MessageBox.Show(
                        "Updates are only available in the installed version. This looks " +
                        "like a portable copy - grab the installer to get automatic updates.",
                        "Vibrance HUD", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var update = await mgr.CheckForUpdatesAsync();
                if (update == null)
                {
                    MessageBox.Show("You're on the latest version.",
                        "Vibrance HUD", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var choice = MessageBox.Show(
                    $"Version {update.TargetFullRelease.Version} is available. " +
                    "Download and restart to update now?",
                    "Vibrance HUD", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (choice != DialogResult.Yes) return;

                await mgr.DownloadUpdatesAsync(update);
                mgr.ApplyUpdatesAndRestart(update);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Couldn't check for updates:\n\n{ex.Message}",
                    "Vibrance HUD", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
