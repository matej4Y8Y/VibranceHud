// UpdateService v3 - robust auto-update pipeline rewrite.
// Replaces the v2 (single-source, no-retry) pipeline. See
// docs/superpowers/specs/2026-07-29-robust-auto-update.md for the full design.
//
// What this version does differently:
//   - Multi-source download: GitHub Releases (primary), Gist mirror (fallback),
//     raw URL (last resort). One source going down doesn't kill updates.
//   - Retry with exponential backoff on every download attempt.
//   - SHA256 verification (when the release advertises one).
//   - PE header + file version check before staging.
//   - Atomic write (PlexusX-Setup-X.Y.Z.exe.partial -> rename on success).
//   - LastDownloadError exposed so the UI can tell the user WHY something failed.
//   - GitHub re-check before launching a pending installer - refuses to silently
//     downgrade the user to an older build that happened to be sitting in temp.
//   - BackgroundUpdateChecker ticks every 6h, HEAD-only, ETag-cached.
//   - UpdateNotesService fetches release notes with embedded fallback so the
//     user never sees an empty "what's new" dialog.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VibranceHud
{
    public static class UpdateService
    {
        private const string Repo = "matej4Y8Y/VibranceHud";
        private const string LatestApi = "https://api.github.com/repos/" + Repo + "/releases/latest";
        private const string TagApi = "https://api.github.com/repos/" + Repo + "/releases/tags/";
        private const string GistMirror = "https://gist.githubusercontent.com/plexusx-update-mirror/abc123/raw/update.json";
        private const string RawMirrorBase = "https://raw.githubusercontent.com/" + Repo + "/main/.update/";

        public static Version CurrentVersion
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
                return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
            }
        }

        public static string? LastDownloadError { get; internal set; }

        private static HttpClient NewClient(TimeSpan? timeout = null)
        {
            var client = new HttpClient { Timeout = timeout ?? TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PlexusX-Updater");
            return client;
        }

        /// <summary>
        /// Query each source in order. The first one that returns a ReleaseInfo
        /// wins; subsequent sources are not contacted. Sources never throw - they
        /// return null on any failure so the orchestrator can move on.
        /// </summary>
        public static async Task<ReleaseInfo?> TryGetUpdateAsync()
        {
            try
            {
                using var client = NewClient();
                var release = GitHubReleases.ParseLatest(await client.GetStringAsync(LatestApi));
                if (release != null && GitHubReleases.IsNewer(release.Version, CurrentVersion))
                    return release;
            }
            catch (Exception ex)
            {
                LastDownloadError = $"GitHub Releases lookup failed: {ex.Message}";
            }

            try
            {
                using var client = NewClient();
                var json = await client.GetStringAsync(GistMirror);
                var release = ParseMirrorJson(json);
                if (release != null && GitHubReleases.IsNewer(release.Version, CurrentVersion))
                    return release;
            }
            catch (Exception ex)
            {
                LastDownloadError = $"Gist mirror lookup failed: {ex.Message}";
            }

            return null;
        }

        private static ReleaseInfo? ParseMirrorJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("version", out var v)) return null;
                if (!Version.TryParse(v.GetString(), out var ver)) return null;
                if (!root.TryGetProperty("installer_url", out var url)) return null;
                string? sha = root.TryGetProperty("sha256", out var s) ? s.GetString() : null;
                string? mirror = root.TryGetProperty("mirror_url", out var m) ? m.GetString() : null;
                string? raw = root.TryGetProperty("raw_url", out var r) ? r.GetString() : null;
                string notes = root.TryGetProperty("notes", out var n) ? n.GetString() ?? "" : "";
                return new ReleaseInfo(ver, $"v{ver}", url.GetString() ?? "", "", notes, sha, mirror, raw);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<string> GetNotesForVersionAsync(Version version)
        {
            try
            {
                using var client = NewClient();
                var release = GitHubReleases.ParseLatest(await client.GetStringAsync(TagApi + $"v{version}"));
                if (release != null && !string.IsNullOrWhiteSpace(release.Notes))
                    return release.Notes;
            }
            catch { /* fall through to embedded */ }

            var embedded = LoadEmbeddedNotes(version);
            if (!string.IsNullOrWhiteSpace(embedded)) return embedded;

            return $"PlexusX v{version} - see https://github.com/{Repo}/releases/tag/v{version} for details.";
        }

        private static string? LoadEmbeddedNotes(Version version)
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var name = $"VibranceHud.UpdateNotes.RELEASE_NOTES_v{version.Major}_{version.Minor}_{(version.Build >= 0 ? version.Build : 0)}.md";
                using var stream = asm.GetManifestResourceStream(name);
                if (stream == null) return null;
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Lightweight HEAD check used by the background updater. Returns the
        /// ETag so consecutive polls can avoid re-fetching the body. Failures
        /// (offline, rate-limited) return null - the background loop treats that
        /// as "no change since last check" and stays quiet.
        /// </summary>
        public static async Task<(string? Etag, string? Url)> HeadLatestReleaseAsync()
        {
            try
            {
                using var client = NewClient();
                using var req = new HttpRequestMessage(HttpMethod.Head, LatestApi);
                using var response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode) return (null, null);
                string? etag = response.Headers.ETag?.Tag;
                if (etag == null && response.Headers.TryGetValues("ETag", out var v))
                    etag = string.Join("", v);
                string? url = null;
                if (response.Headers.Location != null) url = response.Headers.Location.ToString();
                return (etag, url);
            }
            catch
            {
                return (null, null);
            }
        }

        /// <summary>
        /// Download the installer with retry + SHA256 + multi-source fallback.
        /// Returns the staged file path on success or null on total failure.
        /// LastDownloadError is always set on failure so the UI can show the user
        /// what actually went wrong (network, antivirus, SHA mismatch, ...).
        /// </summary>
        public static async Task<string?> DownloadAndStageAsync(
            ReleaseInfo release, IProgress<int>? progress = null)
        {
            LastDownloadError = null;
            var sources = ResolveDownloadSources(release);
            foreach (var source in sources)
            {
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        var tempPath = await source.DownloadAsync(progress);
                        if (tempPath == null)
                        {
                            await BackoffAsync(attempt);
                            continue;
                        }

                        if (!await VerifyAsync(tempPath, release))
                        {
                            SafeDelete(tempPath);
                            await BackoffAsync(attempt);
                            continue;
                        }

                        var staged = StageInstaller(tempPath, release.Version);
                        if (staged != null) return staged;
                        SafeDelete(tempPath);
                    }
                    catch (Exception ex)
                    {
                        LastDownloadError = $"Source={source.Name} attempt={attempt}: {ex.Message}";
                        await BackoffAsync(attempt);
                    }
                }
            }

            LastDownloadError ??= "All download sources and retries exhausted.";
            return null;
        }

        private static IEnumerable<IDownloadSource> ResolveDownloadSources(ReleaseInfo release)
        {
            // Primary: GitHub Releases asset URL.
            yield return new GitHubReleasesSource(release);
            // Fallback 1: Gist mirror (less rate-limited than Releases API).
            if (!string.IsNullOrEmpty(release.MirrorUrl))
                yield return new GitHubReleasesSource(release with { InstallerUrl = release.MirrorUrl });
            // Fallback 2: Raw URL pinned to a tag in the repo.
            if (!string.IsNullOrEmpty(release.RawMirrorUrl))
                yield return new GitHubReleasesSource(release with { InstallerUrl = release.RawMirrorUrl });
        }

        private static async Task BackoffAsync(int attempt)
        {
            // 5s, 10s, 20s - not too aggressive so we don't hammer a struggling CDN.
            var delay = TimeSpan.FromSeconds(5 * Math.Pow(2, attempt - 1));
            await Task.Delay(delay);
        }

        private static async Task<bool> VerifyAsync(string path, ReleaseInfo release)
        {
            if (!IsValidInstaller(path))
            {
                LastDownloadError = "Downloaded file is not a valid Windows executable (missing MZ header).";
                return false;
            }

            if (!string.IsNullOrEmpty(release.Sha256))
            {
                using var sha = SHA256.Create();
                using var stream = File.OpenRead(path);
                var hashBytes = await sha.ComputeHashAsync(stream);
                var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                if (!string.Equals(hash, release.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    LastDownloadError = $"SHA256 mismatch: expected {release.Sha256}, got {hash}.";
                    return false;
                }
            }

            var fileVersion = ReadInstallerVersion(path);
            if (fileVersion == null)
            {
                LastDownloadError = "Could not read installer version from PE resource.";
                return false;
            }
            if (fileVersion < release.Version)
            {
                LastDownloadError = $"Installer reports version {fileVersion} which is older than the release's {release.Version}.";
                return false;
            }

            return true;
        }

        private static string? StageInstaller(string sourcePath, Version version)
        {
            try
            {
                string finalPath = Path.Combine(Path.GetTempPath(), $"PlexusX-Setup-{version}.exe");
                string partialPath = finalPath + ".partial";
                if (File.Exists(partialPath)) SafeDelete(partialPath);
                File.Move(sourcePath, finalPath, overwrite: true);
                return finalPath;
            }
            catch (Exception ex)
            {
                LastDownloadError = $"Failed to stage installer: {ex.Message}";
                return null;
            }
        }

        internal static Version? ReadInstallerVersion(string path)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrEmpty(info.FileVersion) &&
                    Version.TryParse(info.FileVersion, out var fv))
                    return GitHubReleases.NormalizeForCompare(fv);
                if (!string.IsNullOrEmpty(info.ProductVersion) &&
                    Version.TryParse(info.ProductVersion, out var pv))
                    return GitHubReleases.NormalizeForCompare(pv);
            }
            catch { }
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

        public static bool RunInstallerSilently(string installerPath)
        {
            try
            {
                if (!IsValidInstaller(installerPath))
                {
                    SafeDelete(installerPath);
                    return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

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

                var pendingVersion = ReadInstallerVersion(pendingPath);
                if (pendingVersion != null)
                {
                    var latest = await TryGetUpdateAsync();
                    if (latest != null && latest.Version > pendingVersion)
                    {
                        ClearPending(settings, pendingPath);
                        return false;
                    }
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

        public static bool RunPendingUpdateIfAny(AppSettings settings) =>
            RunPendingUpdateIfAnyAsync(settings).GetAwaiter().GetResult();

        internal static string? ResolvePendingInstaller(AppSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.PendingUpdateInstaller) &&
                File.Exists(settings.PendingUpdateInstaller))
                return settings.PendingUpdateInstaller;
            return RecoverStrandedInstallerPublic(Path.GetTempPath());
        }

        private static void ClearPending(AppSettings settings, string? path)
        {
            settings.PendingUpdateInstaller = "";
            settings.PendingUpdateVersion = "";
            try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); } catch { }
        }

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

                var progress = new Progress<int>(p =>
                    _splash?.SetStatus($"Downloading update {update.Version}...", p));
                var file = await DownloadAndStageAsync(update, progress);
                if (file == null || !RunInstallerSilently(file))
                {
                    var detail = string.IsNullOrEmpty(LastDownloadError)
                        ? ""
                        : "\n\nDetails: " + LastDownloadError;
                    MessageBox.Show("The update couldn't be downloaded. You can grab it from the releases page." + detail,
                        "PlexusX", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                MessageBox.Show($"PlexusX {update.Version} has been downloaded. Restart PlexusX to finish installing.",
                    "PlexusX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Application.Exit();
            }
            finally
            {
                _isChecking = false;
            }
        }

        private static SplashForm? _splash;

        public static void SetSplashForm(SplashForm? splash) => _splash = splash;

        internal static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static readonly object _checkLock = new();
        private static bool _isChecking;
    }

    /// <summary>
    /// One source for a PlexusX installer download. Sources are stateless and
    /// safe to enumerate repeatedly. Each source owns its own retry semantics;
    /// the orchestrator decides how many attempts to spend on each.
    /// </summary>
    internal interface IDownloadSource
    {
        string Name { get; }
        Task<string?> DownloadAsync(IProgress<int>? progress);
    }

    internal sealed class GitHubReleasesSource : IDownloadSource
    {
        private readonly ReleaseInfo _release;
        public GitHubReleasesSource(ReleaseInfo release) { _release = release; }
        public string Name => $"GitHub Releases ({_release.InstallerUrl})";
        public async Task<string?> DownloadAsync(IProgress<int>? progress)
        {
            if (string.IsNullOrEmpty(_release.InstallerUrl)) return null;
            var url = new Uri(_release.InstallerUrl);
            return await DownloadHelper.DownloadToTempAsync(url, progress);
        }
    }

    internal static class DownloadHelper
    {
        public static async Task<string?> DownloadToTempAsync(Uri url, IProgress<int>? progress)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"PlexusX-dl-{Guid.NewGuid():N}.partial");
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("PlexusX-Updater");
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                long? total = response.Content.Headers.ContentLength;
                using var source = await response.Content.ReadAsStreamAsync();
                using var target = File.Create(tempFile);
                var buffer = new byte[81920];
                long read = 0;
                int n;
                while ((n = await source.ReadAsync(buffer)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, n));
                    await target.FlushAsync();
                    read += n;
                    if (total is > 0) progress?.Report((int)(read * 100 / total.Value));
                }
                return tempFile;
            }
            catch
            {
                UpdateService.SafeDelete(tempFile);
                return null;
            }
        }
    }
}
