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
        /// Re-downloads if the target file already exists with a corrupt PE header (catches
        /// partial downloads or a previous failed update that left a 0-byte / junk file
        /// in %TEMP%).
        /// </summary>
        public static async Task<string?> DownloadAsync(ReleaseInfo release, Action<int> onProgress)
        {
            try
            {
                var file = Path.Combine(Path.GetTempPath(), $"PlexusX-Setup-{release.Version}.exe");

                // If a corrupt installer from a prior failed update is sitting in %TEMP%,
                // wipe it so the download below writes a fresh file. The corrupt file has
                // the same name the downloader would write, so without this we'd reuse
                // it and the new Process.Start would fail with WinError 216 ("not a valid
                // Win32 application") exactly as the user reported.
                if (File.Exists(file) && !IsValidInstaller(file))
                {
                    try { File.Delete(file); } catch { /* best-effort */ }
                }

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

                // Final sanity check: did we get a valid PE? A truncated download passes
                // the size check but Process.Start will fail with WinError 216, leaving
                // the user on "Installing update..." forever. Reject it here instead.
                if (!IsValidInstaller(file))
                {
                    try { File.Delete(file); } catch { /* best-effort */ }
                    return null;
                }
                return file;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>True when the file starts with the PE magic "MZ" (0x4D5A). Used to
        /// detect a corrupt / partially-downloaded installer before we try to run it.
        /// Internal so tests can verify the validator directly.</summary>
        internal static bool IsValidInstaller(string path)
        {
            try
            {
                using var s = File.OpenRead(path);
                if (s.Length < 2) return false;
                Span<byte> head = stackalloc byte[2];
                int n = s.Read(head);
                return n == 2 && head[0] == 0x4D && head[1] == 0x5A;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Run the downloaded installer silently. It closes this app, replaces the files and
        /// relaunches PlexusX, so the user just sees the loading screen and then "what's new".
        ///
        /// Implementation notes - the auto-update flow has multiple failure modes and this
        /// version hardens each one:
        ///
        /// 1. The Inno Setup installer needs to close PlexusX to write the new files, and
        ///    PlexusX is the parent that just launched it. If we waited on the installer
        ///    here (the original UseShellExecute=true approach), we'd deadlock for ~4 min
        ///    (the installer waits that long for the parent to exit before giving up).
        /// 2. Anti-virus + Windows file permissions sometimes block silent installers started
        ///    with UseShellExecute=true from a different process. UseShellExecute=false +
        ///    explicit arguments + CreateNoWindow=true avoids that path.
        /// 3. Inno Setup refuses to run if its parent process is gone before the installer's
        ///    own window initializes. We sleep ~700ms before Environment.Exit so the
        ///    installer has time to init.
        /// 4. On Windows the installer also needs an interactive desktop (not session 0)
        ///    to display any error dialogs; we run from the user's desktop session by
        ///    using their %TEMP% as WorkingDirectory.
        /// </summary>
        public static bool RunInstallerSilently(string installerPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = installerPath,
                    // /SP- = "don't show the 'do you want to install' prompt"
                    // /CLOSEAPPLICATIONS = polite close (CurStepChanged does the F kill)
                    // /FORCECLOSEAPPLICATIONS = bypass graceful-shutdown prompt
                    // /RESTARTAPPLICATIONS handled by [Run] section in the .iss
                    Arguments = "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP- /FORCECLOSEAPPLICATIONS /CLOSEAPPLICATIONS",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetTempPath(),
                };
                var proc = Process.Start(psi);
                if (proc == null)
                {
                    // Anti-virus or Windows policy blocked the launch. Don't try to exit;
                    // the user still has a working PlexusX.
                    return false;
                }

                // Hard-exit so the installer can write the new files. Application.Exit()
                // can't be used here - it tries to flush UI state and waits for the form
                // to close, which itself can't close because the installer is writing
                // over our EXE. Process.Kill on self is also wrong (it depends on the UI
                // thread we are on). Environment.Exit terminates the process immediately
                // from any thread.
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    // Wait long enough for the installer to spawn its process tree and
                    // initialize its window. 700ms also matches the sleep in CurStepChanged
                    // that follows the taskkill, so the installer's taskkill has time to
                    // complete before our process disappears (Inno Setup can fail if the
                    // parent exits mid-taskkill).
                    System.Threading.Thread.Sleep(700);
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
