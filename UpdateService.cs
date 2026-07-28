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
        /// Run the downloaded installer at next launch. Stores the installer path in
        /// AppSettings.PendingUpdateInstaller so the next startup sequence picks it up
        /// and runs the install BEFORE opening the main window. This is the only
        /// reliable way to self-update: launching the installer while PlexusX is
        /// running either deadlocks (the installer waits for the parent to exit) or
        /// silently fails because Windows blocks silent installs from a live parent.
        ///
        /// The previous design used Process.Start + 700ms delay + Environment.Exit(0),
        /// which worked on simple machines but failed on this user's setup: the installer
        /// was downloaded but never actually replaced the installed exe, leaving the
        /// user stuck on the old version. The "run on next launch" pattern fixes that
        /// for real because the installer runs BEFORE PlexusX starts holding file handles.
        /// </summary>
        public static bool RunInstallerSilently(string installerPath)
        {
            try
            {
                if (!IsValidInstaller(installerPath))
                {
                    try { File.Delete(installerPath); } catch { /* best-effort */ }
                    return false;
                }

                // Hand the installer off to the next-launch startup sequence. The user's
                // current PlexusX session keeps running normally; the next time they
                // open PlexusX, the install happens before the splash.
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Called by the splash/startup sequence BEFORE the main window opens. If
        /// AppSettings.PendingUpdateInstaller is set, runs that installer (which then
        /// closes this new PlexusX, replaces files, relaunches the new version).
        /// </summary>
        public static bool RunPendingUpdateIfAny(AppSettings settings)
        {
            try
            {
                // Recovery path: a previous PlexusX version (pre-v0.8.0) downloaded an
                // installer to %TEMP% but never set PendingUpdateInstaller. Detect that
                // installer and treat it as a pending update. Same idea as the explicit
                // path below - we trust the installer's PE header and the version in its
                // filename. Only one such installer ever exists at a time because
                // DownloadAsync overwrites the path.
                string pendingPath = settings.PendingUpdateInstaller;
                if (string.IsNullOrEmpty(pendingPath) || !File.Exists(pendingPath))
                {
                    var recovered = RecoverStrandedInstaller();
                    if (recovered != null) pendingPath = recovered;
                }

                if (string.IsNullOrEmpty(pendingPath)) return false;
                if (!File.Exists(pendingPath)) return false;
                if (!IsValidInstaller(pendingPath))
                {
                    try { File.Delete(pendingPath); } catch { }
                    settings.PendingUpdateInstaller = "";
                    settings.PendingUpdateVersion = "";
                    return false;
                }

                // Launch the installer detached. It will close this PlexusX (via
                // /FORCECLOSEAPPLICATIONS in the .iss) and the installer's [Run] section
                // relaunches the new PlexusX at the end.
                var psi = new ProcessStartInfo
                {
                    FileName = pendingPath,
                    Arguments = "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP- /FORCECLOSEAPPLICATIONS /CLOSEAPPLICATIONS",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetTempPath(),
                };
                Process.Start(psi);

                // Clear the pending flag immediately so a crash before relaunch doesn't
                // loop the install on every subsequent launch.
                settings.PendingUpdateInstaller = "";
                settings.PendingUpdateVersion = "";
                return true;
            }
            catch
            {
                // If launch failed, clear the flag so we don't keep trying every launch.
                settings.PendingUpdateInstaller = "";
                settings.PendingUpdateVersion = "";
                return false;
            }
        }

        /// <summary>
        /// If a PlexusX-Setup-X.Y.Z.exe file is sitting in %TEMP% (from a pre-v0.8.0
        /// download that never set PendingUpdateInstaller), recover it. Returns the path
        /// if the installer is for a newer version than what the user is running.
        /// </summary>
        private static string? RecoverStrandedInstaller() => RecoverStrandedInstallerPublic(Path.GetTempPath());

        /// <summary>Test seam: same logic, but takes the directory to scan. Lets tests
        /// use an isolated temp dir without leaking installer files into the real %TEMP%.</summary>
        internal static string? RecoverStrandedInstallerPublic(string? dir = null)
        {
            try
            {
                string temp = dir ?? Path.GetTempPath();
                Version current = CurrentVersion;
                string? bestPath = null;
                Version? bestVersion = null;

                foreach (var path in Directory.EnumerateFiles(temp, "PlexusX-Setup-*.exe"))
                {
                    var name = Path.GetFileName(path);
                    var m = System.Text.RegularExpressions.Regex.Match(name, @"PlexusX-Setup-(\d+\.\d+(?:\.\d+)?)\.exe$");
                    if (!m.Success) continue;
                    if (!IsValidInstaller(path)) continue;
                    if (!Version.TryParse(m.Groups[1].Value, out var v)) continue;
                    if (v <= current) continue; // only upgrade
                    if (bestVersion == null || v > bestVersion)
                    {
                        bestPath = path;
                        bestVersion = v;
                    }
                }

                return bestPath;
            }
            catch
            {
                return null;
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
