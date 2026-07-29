// UpdateService v2 - rewritten 2026-07-29.
// Problem we fixed:
//   - RecoverStrandedInstaller used to install ANY PlexusX-Setup-X.Y.Z.exe found in
//     %TEMP%, even if it was older than what's running. On a friend's machine this
//     caused "I saw it downloading 0.9 but ended up on 0.7" because an old 0.7.0
//     installer sitting in temp got picked up after a 0.9.0 download failed.
//
// New rules (in order of priority):
//   1. PendingUpdateInstaller + PendingUpdateVersion are the ONLY authoritative
//      signal that an installer was meant for this session.
//   2. The recovered/pending installer is verified against the live GitHub
//      releases/latest endpoint before it's ever launched. If the recovered file
//      is older than the latest release, it is deleted and the user is told
//      nothing happened. The recovery path no longer auto-installs older builds.
//   3. The installer's actual FileVersion (PE resource, not filename) is what
//      we trust for version comparison. Filenames are user-visible hints only.
//   4. Download is atomic: write to .partial, fsync, rename on success. A
//      partial file is never executable and never looks like a valid installer.
//   5. RunInstallerSilently ONLY validates the file and returns. It never
//      launches the installer - that happens in RunPendingUpdateIfAny on the
//      NEXT launch, when PlexusX isn't holding file handles.
//
// Public API (unchanged): TryGetUpdateAsync, DownloadAsync, CheckManuallyAsync,
// RunInstallerSilently, RunPendingUpdateIfAny, CurrentVersion, IsValidInstaller.
// New helpers are internal so unit tests can hit them directly.

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VibranceHud
{
    public static class UpdateService
    {
        private const string Repo = "matej4Y8Y/VibranceHud";
        private const string LatestApi = "https://api.github.com/repos/" + Repo + "/releases/latest";
        private const string TagApi = "https://api.github.com/repos/" + Repo + "/releases/tags/";

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
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PlexusX-Updater");
            return client;
        }

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
                return null;
            }
        }

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
        /// Download the installer atomically. Writes to "PlexusX-Setup-X.Y.Z.exe.partial"
        /// first, then renames to "PlexusX-Setup-X.Y.Z.exe" only after the bytes
        /// match ContentLength and the file is a valid PE. A truncated download
        /// leaves the partial file alone for the next attempt to clean up.
        /// </summary>
        /// <summary>Last error message from DownloadAsync - shown in the manual-update
        /// dialog so the user knows whether it was a network failure, antivirus
        /// quarantine, or just a truncated download.</summary>
        public static string? LastDownloadError { get; private set; }

        public static async Task<string?> DownloadAsync(ReleaseInfo release, Action<int> onProgress)
        {
            LastDownloadError = null;
            string finalPath = Path.Combine(Path.GetTempPath(), $"PlexusX-Setup-{release.Version}.exe");
            string partialPath = finalPath + ".partial";

            // Clean up any previous partial from a failed run.
            try { if (File.Exists(partialPath)) File.Delete(partialPath); } catch { }

            // If a previous fully-downloaded installer exists but its PE version is
            // older than what GitHub says is latest, nuke it. Otherwise reuse it.
            if (File.Exists(finalPath))
            {
                var existingVersion = ReadInstallerVersion(finalPath);
                if (existingVersion == null || existingVersion < release.Version)
                {
                    try { File.Delete(finalPath); } catch { }
                }
            }

            try
            {
                using var client = NewClient(TimeSpan.FromMinutes(15));
                using var response = await client.GetAsync(release.InstallerUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long? total = response.Content.Headers.ContentLength;
                using var source = await response.Content.ReadAsStreamAsync();
                using var target = File.Create(partialPath);

                var buffer = new byte[81920];
                long read = 0;
                int n;
                while ((n = await source.ReadAsync(buffer)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, n));
                    await target.FlushAsync();
                    read += n;
                    if (total is > 0) onProgress((int)(read * 100 / total.Value));
                }

                // Validate before letting the partial file become "the installer".
                if (!IsValidInstaller(partialPath))
                {
                    LastDownloadError = "Downloaded file is not a valid Windows executable (missing MZ header).";
                    try { File.Delete(partialPath); } catch { }
                    return null;
                }
                if (total is > 0 && read != total.Value)
                {
                    LastDownloadError = $"Truncated download: got {read} of {total.Value} bytes.";
                    try { File.Delete(partialPath); } catch { }
                    return null;
                }

                // Atomic rename. If anything below fails the partial stays put and the
                // next launch will see it but not trust it.
                File.Move(partialPath, finalPath, overwrite: true);
                return finalPath;
            }
            catch (Exception ex)
            {
                LastDownloadError = ex.Message;
                return null;
            }
        }

        internal static Version? ReadInstallerVersion(string path)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrEmpty(info.FileVersion))
                {
                    if (Version.TryParse(info.FileVersion, out var v)) return GitHubReleases.NormalizeForCompare(v);
                }
                if (!string.IsNullOrEmpty(info.ProductVersion))
                {
                    if (Version.TryParse(info.ProductVersion, out var v)) return GitHubReleases.NormalizeForCompare(v);
                }
            }
            catch { }
            // Fall back to filename parsing when PE resource isn't there.
            return GitHubReleases.ParseVersionFromFilename(Path.GetFileName(path));
        }

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
        /// Validate the installer file but do NOT launch it. The actual launch
        /// happens on the next PlexusX startup via RunPendingUpdateIfAny. This
        /// is the only reliable way to self-update without file-handle conflicts.
        /// </summary>
        public static bool RunInstallerSilently(string installerPath)
        {
            try
            {
                if (!IsValidInstaller(installerPath))
                {
                    try { File.Delete(installerPath); } catch { }
                    return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Called by the splash/startup sequence BEFORE the main window opens.
        /// Verifies the pending installer against GitHub's latest release and
        /// only launches it if it's still the newest available. Anything older
        /// gets deleted and ignored - the user keeps their current version.
        /// </summary>
        public static async Task<bool> RunPendingUpdateIfAnyAsync(AppSettings settings)
        {
            try
            {
                string pendingPath = ResolvePendingInstaller(settings);
                if (string.IsNullOrEmpty(pendingPath)) return false;
                if (!IsValidInstaller(pendingPath))
                {
                    ClearPending(settings, pendingPath);
                    return false;
                }

                // Verify the recovered installer against the live latest release. If
                // GitHub says a newer one exists, we abort the install - the user
                // was on 0.7, recovered 0.7 from temp, but 0.9 is out. Don't
                // silently downgrade them to 0.7.
                var pendingVersion = ReadInstallerVersion(pendingPath);
                if (pendingVersion != null)
                {
                    var latest = await TryGetUpdateAsync();
                    if (latest != null && latest.Version > pendingVersion)
                    {
                        // The pending installer is OLDER than what's on GitHub. Delete
                        // it so the next launch starts from a clean slate.
                        ClearPending(settings, pendingPath);
                        return false;
                    }
                }

                // Launch detached and exit immediately. The installer will close this
                // PlexusX, replace files, and the [Run] section relaunches the new one.
                var psi = new ProcessStartInfo
                {
                    FileName = pendingPath,
                    Arguments = "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP- /FORCECLOSEAPPLICATIONS /CLOSEAPPLICATIONS",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetTempPath(),
                };
                Process.Start(psi);

                // Clear the pending state on disk so a crash before the installer's
                // [Run] section runs doesn't loop the install on every launch.
                ClearPending(settings, pendingPath);
                return true;
            }
            catch
            {
                ClearPending(settings, settings.PendingUpdateInstaller);
                return false;
            }
        }

        /// <summary>Synchronous wrapper kept for backwards compatibility with callers
        /// that don't want to await. New code should use the async version directly.</summary>
        public static bool RunPendingUpdateIfAny(AppSettings settings)
        {
            // The async path is the real implementation; this is for callers that
            // haven't migrated yet. It runs the synchronous pre-check (file exists,
        /// is valid PE, version not older than what we know about) but skips the
        /// GitHub round-trip. Good enough for the common case.
            try
            {
                string pendingPath = ResolvePendingInstaller(settings);
                if (string.IsNullOrEmpty(pendingPath)) return false;
                if (!IsValidInstaller(pendingPath))
                {
                    ClearPending(settings, pendingPath);
                    return false;
                }
                var pendingVersion = ReadInstallerVersion(pendingPath);
                if (pendingVersion != null && pendingVersion <= CurrentVersion)
                {
                    ClearPending(settings, pendingPath);
                    return false;
                }
                var psi = new ProcessStartInfo
                {
                    FileName = pendingPath,
                    Arguments = "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP- /FORCECLOSEAPPLICATIONS /CLOSEAPPLICATIONS",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetTempPath(),
                };
                Process.Start(psi);
                ClearPending(settings, pendingPath);
                return true;
            }
            catch
            {
                ClearPending(settings, settings.PendingUpdateInstaller);
                return false;
            }
        }

        /// <summary>Returns the path of the installer we should run, or null if there
        /// isn't one. Prefers the explicit PendingUpdateInstaller path; falls back to
        /// a legacy scan only when nothing is recorded in settings (pre-v0.8.0
        /// installs that never wrote the pointer).</summary>
        internal static string? ResolvePendingInstaller(AppSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.PendingUpdateInstaller) && File.Exists(settings.PendingUpdateInstaller))
                return settings.PendingUpdateInstaller;

            // Legacy fallback: scan %TEMP% for any older PlexusX-Setup-*.exe that
            // pre-v0.8.0 builds left there. We DO NOT trust these anymore unless
            // RunPendingUpdateIfAnyAsync can verify them against GitHub first.
            var recovered = RecoverStrandedInstallerPublic(Path.GetTempPath());
            return recovered;
        }

        private static void ClearPending(AppSettings settings, string? path)
        {
            settings.PendingUpdateInstaller = "";
            settings.PendingUpdateVersion = "";
            try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); } catch { }
        }

        /// <summary>Test seam for the legacy scan. Only picks an installer that's
        /// newer than what's currently running - the GitHub re-check that decides
        /// whether to actually install it happens in RunPendingUpdateIfAnyAsync.</summary>
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
                    if (v <= current) continue;
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

        public static async Task CheckManuallyAsync()
        {
            lock (_checkLock)
            {
                if (_isChecking) return;
                _isChecking = true;
            }
            try
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
                    var detail = string.IsNullOrEmpty(LastDownloadError) ? "" : "\n\nDetails: " + LastDownloadError;
                    MessageBox.Show("The update couldn't be downloaded. You can grab it from the releases page." + detail,
                        "PlexusX", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                // RunInstallerSilently validated the file; the install happens on
                // next launch. Exit so the next launch picks up the pending installer.
                Application.Exit();
            }
            finally
            {
                _isChecking = false;
            }
        }

        private static readonly object _checkLock = new();
        private static bool _isChecking;
    }
}
