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
    /// Self-update against GitHub Releases, built for the Inno Setup installer we ship.
    /// On launch the splash screen asks for the newest release and, if it's newer, quietly
    /// downloads it and runs the installer in silent mode - which upgrades in place (same
    /// AppId) and relaunches PlexusX. The user only sees the loading screen, then a
    /// "what's new" note.
    ///
    /// No account or token needed: it only reads the public releases endpoint.
    /// </summary>
    public static class UpdateService
    {
        private const string Repo = "matej4Y8Y/VibranceHud";
        private const string LatestApi = "https://api.github.com/repos/" + Repo + "/releases/latest";
        private const string TagApi = "https://api.github.com/repos/" + Repo + "/releases/tags/";

        /// <summary>The running app's version, normalised to major.minor.build.</summary>
        public static Version CurrentVersion
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
                return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
            }
        }

        private static HttpClient NewClient(TimeSpan? timeout = null)
        {
            var client = new HttpClient { Timeout = timeout ?? TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PlexusX-Updater"); // GitHub requires one
            return client;
        }

        /// <summary>The newest release if it's newer than what's running, else null.</summary>
        public static async Task<ReleaseInfo?> TryGetUpdateAsync()
        {
            try
            {
                using var client = NewClient();
                var release = GitHubReleases.ParseLatest(await client.GetStringAsync(LatestApi));
                if (release == null || !GitHubReleases.IsNewer(release.Version, CurrentVersion)) return null;
                return release;
            }
            catch
            {
                return null; // offline / rate-limited / no releases - just carry on
            }
        }

        /// <summary>Release notes for a given version, for the "what's new" screen.</summary>
        public static async Task<string> GetNotesForVersionAsync(Version version)
        {
            foreach (var tag in new[] { $"v{version}", version.ToString() })
            {
                try
                {
                    using var client = NewClient();
                    var release = GitHubReleases.ParseLatest(await client.GetStringAsync(TagApi + tag));
                    if (release != null) return release.Notes;
                }
                catch { /* try the next tag shape */ }
            }
            return "";
        }

        /// <summary>
        /// Download the installer, reporting 0-100. Returns the file path, or null on failure.
        /// </summary>
        public static async Task<string?> DownloadAsync(ReleaseInfo release, Action<int> onProgress)
        {
            try
            {
                var file = Path.Combine(Path.GetTempPath(), $"PlexusX-Setup-{release.Version}.exe");

                // The installer now bundles the .NET runtime (self-contained build), so it's
                // well over 100MB - the 30s timeout used for the tiny metadata calls above
                // would truncate a download on anything but a fast connection.
                using var client = NewClient(TimeSpan.FromMinutes(15));
                using var response = await client.GetAsync(release.InstallerUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long? total = response.Content.Headers.ContentLength;
                using var source = await response.Content.ReadAsStreamAsync();
                using var target = File.Create(file);

                var buffer = new byte[81920];
                long read = 0;
                int n;
                while ((n = await source.ReadAsync(buffer)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, n));
                    read += n;
                    if (total is > 0) onProgress((int)(read * 100 / total.Value));
                }
                return file;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Run the downloaded installer silently. It closes this app, replaces the files and
        /// relaunches PlexusX, so the user just sees the loading screen and then "what's new".
        ///
        /// Critical: this method MUST NOT block. The Inno Setup installer needs to close
        /// PlexusX to write the new files, and PlexusX is the parent that just launched it.
        /// If we waited on the installer process here, we'd deadlock for ~4 minutes (the
        /// installer waits up to that long for the parent to exit before giving up).
        /// Instead, we launch the installer detached and immediately force-exit the process
        /// so the installer can replace the files without us holding the lock.
        /// </summary>
        public static bool RunInstallerSilently(string installerPath)
        {
            try
            {
                // UseShellExecute=false lets us pass arguments without quoting. The new
                // process is independent of ours - we don't want a job-object inheritance
                // that would kill the installer when we exit.
                var psi = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP-",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetTempPath(),
                };
                Process.Start(psi);

                // Hard-exit so the installer can write the new files. We can't use
                // Application.Exit() (it tries to flush UI state and waits for the form
                // to close, which itself can't close because the installer is writing
                // over our EXE). Process.Kill is also wrong (it's our parent, depends on
                // the UI thread). Environment.Exit terminates the process immediately.
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    // Small delay so the installer has time to spawn its window before
                    // we disappear (some Inno Setup versions refuse to run if their parent
                    // is gone before they init).
                    System.Threading.Thread.Sleep(200);
                    Environment.Exit(0);
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Manual check from Settings / the tray: reports either way.</summary>
        public static async Task CheckManuallyAsync()
        {
            var update = await TryGetUpdateAsync();
            if (update == null)
            {
                MessageBox.Show($"You're on the latest version ({CurrentVersion}).",
                    "PlexusX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var choice = MessageBox.Show(
                $"PlexusX {update.Version} is available (you have {CurrentVersion}).\n\n" +
                "Download and install it now? PlexusX will restart.",
                "PlexusX", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (choice != DialogResult.Yes) return;

            var file = await DownloadAsync(update, _ => { });
            if (file == null || !RunInstallerSilently(file))
            {
                MessageBox.Show("The update couldn't be downloaded. You can grab it from the releases page.",
                    "PlexusX", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Application.Exit();
        }
    }
}
