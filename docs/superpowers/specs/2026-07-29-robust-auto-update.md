# Robust auto-update pipeline

**Datum:** 2026-07-29
**Status:** Brief for code subagent
**Scope:** Update + download pipeline rewrite, end-to-end

## Goal

The PlexusX auto-update must work reliably across:

1. **Brand new installs** (no previous version, no settings)
2. **Old versions (0.7.x, 0.8.x)** — first-time upgrade to 0.9+
3. **Skipped versions** (0.8.2 → 0.9.2, never had 0.9.1)
4. **Network problems** (rate limit, slow connection, antivirus quarantine)
5. **GitHub outage** (CDN redirect, S3 down, api.github.com rate limited)
6. **Anti-tamper** (debugger attached, license tampered, installation interrupted)

The update must never silently fail, never silently downgrade, never install an older version, never lose state.

## User decisions (locked)

- **No telemetry, no cloud crash reporting** — already decided
- **Activation key gate** — already decided, do not change
- **Plexus-style black & white UI** — already decided, do not change
- **Self-contained single-file exe** — already decided, do not change
- **No installer signing (EV cert deferred)** — already decided

## Problem we are solving

Reported symptoms (from real users, not theoretical):

1. User on 0.7.7 clicks "Check for updates", gets generic "couldn't be downloaded" — no idea why
2. User updates successfully to 0.9.1, gets "What's new in 0.8.2" with empty notes — wrong release
3. User on 0.8.2 auto-updates to 0.9.1, installer fails silently, app stays on 0.8.2
4. User has old PlexusX from a previous attempt, hits GitHub API rate limit, never sees the update
5. Anti-virus deletes the `.partial` download, app silently retries nothing

Root causes:

- `DownloadAsync` returns `null` on any failure with no diagnostic detail (now: `LastDownloadError`)
- No retry logic — one failure = done
- Single source (GitHub Releases API + CDN) — single point of failure
- No SHA256 verification — corrupted downloads look valid
- Background check only happens once at startup — if rate-limited, user never knows
- No fallback mirror — GitHub outage = no update

## Architecture

### Components

```
UpdateService (orchestrator)
├── UpdateSource (strategy)
│   ├── GitHubReleasesSource (primary)
│   ├── GitHubGistMirrorSource (fallback, higher rate limit)
│   └── GitHubRawSource (last-resort, hardcoded URLs)
├── DownloadPipeline
│   ├── Atomic download (.partial → rename)
│   ├── SHA256 verify
│   ├── PE header verify
│   └── Retry with backoff (3 attempts: 5s, 30s, 2min)
├── InstallPipeline
│   ├── Pending installer storage
│   ├── GitHub re-check before launch
│   └── Rollback on failure
└── BackgroundUpdateChecker
    ├── 6-hour polling timer (systray service)
    ├── HEAD request (lightweight, ETag-cached)
    └── Notification on available update

UpdateNotesService
├── Primary: fetch from GitHub release body
├── Fallback: embedded `RELEASE_NOTES` resource (compiled in)
└── Cache: %LocalAppData%\PlexusX\notes-cache\<version>.md
```

### Update flow (new)

```
User launches PlexusX
   ↓
TrayApplicationContext.RunStartupAsync()
   ↓
[1] UpdateService.RunPendingUpdateIfAnyAsync(settings)
   ↓
   Resolve pending installer (settings + temp scan)
   ↓
   Verify GitHub re-check (refuse if older than latest)
   ↓
   Launch installer + clear pending
   ↓
[2] BackgroundUpdateChecker.Start() (systray service, 6h timer)
   ↓
[3] MainWindow opens
   ↓
   User sees normal app
```

```
BackgroundUpdateChecker tick (every 6h)
   ↓
HEAD request to GitHub (lightweight, ETag cached)
   ↓
If ETag changed (new version):
   ↓
   UpdateService.TryGetUpdateAsync()
   ↓
   If newer than running:
     UpdateService.DownloadAndStageAsync(release)
       (background task, retry x3, SHA256, PE verify)
   ↓
   On success: store in PendingUpdateInstaller
   ↓
   Notify user via systray balloon
```

```
User manually clicks "Check for updates" (Settings/tray)
   ↓
UpdateService.CheckManuallyAsync()
   ↓
Same flow as background, but with UI progress + dialog
```

### Files

```
UpdateService.cs                          — orchestrator + retry (rewrite)
GitHubReleases.cs                         — ParseLatest (existing)
GitHubGistMirror.cs                       — NEW: fallback source
GitHubRawMirror.cs                        — NEW: last-resort source
BackgroundUpdateChecker.cs                — NEW: systray polling service
UpdateNotesService.cs                     — NEW: notes fetcher + cache
UpdateNotes/RELEASE_NOTES_v0_9_1.md       — embedded resource (fallback)
UpdateNotes/RELEASE_NOTES_v0_9_0.md
UpdateNotes/...
```

## UpdateService public API

```csharp
public static class UpdateService
{
    public static Version CurrentVersion { get; }
    public static string? LastDownloadError { get; }

    // Orchestration
    public static Task<ReleaseInfo?> TryGetUpdateAsync();
    public static Task<string?> DownloadAndStageAsync(ReleaseInfo release, IProgress<int>? progress = null);
    public static Task<bool> RunPendingUpdateIfAnyAsync(AppSettings settings);

    // Manual check from UI (existing API, signature preserved)
    public static Task CheckManuallyAsync();

    // Internal helpers (used by tests)
    internal static bool IsValidInstaller(string path);
    internal static Version? ReadInstallerVersion(string path);
    internal static string? ResolvePendingInstaller(AppSettings settings);
    internal static string? RecoverStrandedInstallerPublic(string? dir = null);
}
```

## Download pipeline (with retry + SHA256)

```csharp
public static async Task<string?> DownloadAndStageAsync(
    ReleaseInfo release, IProgress<int>? progress = null)
{
    foreach (var source in ResolveDownloadSources(release))
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var tempPath = await source.DownloadAsync(progress);
                if (tempPath == null) continue;

                if (!await VerifyAsync(tempPath, release))
                {
                    File.Delete(tempPath);
                    continue;
                }

                return StageInstaller(tempPath, release.Version);
            }
            catch (Exception ex)
            {
                LastDownloadError = $"Source {source.Name}, attempt {attempt}: {ex.Message}";
                // backoff
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt) * 5));
            }
        }
    }

    LastDownloadError = "All sources and retries exhausted";
    return null;
}
```

## SHA256 verification

```csharp
private static async Task<bool> VerifyAsync(string path, ReleaseInfo release)
{
    // 1. PE header check (cheap)
    if (!IsValidInstaller(path)) return false;

    // 2. SHA256 (fetched from release, optional but recommended)
    if (!string.IsNullOrEmpty(release.Sha256))
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(await sha.ComputeHashAsync(stream));
        if (!string.Equals(hash, release.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            LastDownloadError = $"SHA256 mismatch: expected {release.Sha256}, got {hash}";
            return false;
        }
    }

    // 3. File version check
    var fileVersion = ReadInstallerVersion(path);
    if (fileVersion == null || fileVersion < release.Version)
    {
        LastDownloadError = $"Installer version {fileVersion} != release version {release.Version}";
        return false;
    }

    return true;
}
```

## Multi-source download

```csharp
private static IEnumerable<IDownloadSource> ResolveDownloadSources(ReleaseInfo release)
{
    // Primary
    yield return new GitHubReleasesSource(release);
    // Fallback 1: Gist mirror (you control, hard to rate-limit)
    yield return new GitHubGistMirrorSource();
    // Fallback 2: Raw mirror (last resort)
    yield return new GitHubRawMirror();
}
```

## Background update checker

Runs as systray service. Ticks every 6 hours. HEAD request only (lightweight, 1KB response). ETag-cached — only re-fetches body when ETag changes.

```csharp
public sealed class BackgroundUpdateChecker : IDisposable
{
    private readonly Timer _timer;
    private readonly AppSettings _settings;
    private string? _lastEtag;

    public BackgroundUpdateChecker(AppSettings settings)
    {
        _settings = settings;
        _timer = new Timer(OnTick, null, TimeSpan.Zero, TimeSpan.FromHours(6));
    }

    private async void OnTick(object? state)
    {
        try
        {
            var head = await UpdateService.HeadLatestReleaseAsync();
            if (head.Etag == _lastEtag) return;
            _lastEtag = head.Etag;

            var release = await UpdateService.TryGetUpdateAsync();
            if (release != null) NotifyUser(release);
        }
        catch { /* offline, ignore */ }
    }
}
```

## Notes service

```csharp
public static class UpdateNotesService
{
    public static async Task<string> GetNotesForVersionAsync(Version version)
    {
        // 1. Try GitHub release body
        var fromGitHub = await TryFetchFromGitHubAsync(version);
        if (!string.IsNullOrWhiteSpace(fromGitHub)) return fromGitHub;

        // 2. Fall back to embedded resource
        var embedded = LoadEmbeddedNotes(version);
        if (!string.IsNullOrWhiteSpace(embedded)) return embedded;

        // 3. Cache locally (so we don't hit GitHub twice)
        return $"v{version} - no release notes yet. Check GitHub for details.";
    }
}
```

## Tests

1. `DownloadAndStageAsync_RetriesOnTransientFailure` — first source fails, second succeeds
2. `DownloadAndStageAsync_RefusesCorruptPeHeader` — `IsValidInstaller` returns false
3. `DownloadAndStageAsync_RefusesSha256Mismatch` — SHA256 doesn't match
4. `DownloadAndStageAsync_AllSourcesFail_ReturnsNull` — null + LastDownloadError set
5. `BackgroundUpdateChecker_HeadRequestCachesETag` — second call within 6h is a no-op
6. `UpdateNotesService_GitHubEmpty_FallsBackToEmbedded` — empty GitHub body returns embedded
7. `UpdateNotesService_GitHubEmpty_FallsBackToCache` — empty body returns cached notes
8. `ReadInstallerVersion_ReadsPeResource` — returns the FileVersion from PE

## File changes summary

- `UpdateService.cs` — full rewrite (orchestrator, retry, SHA256)
- `UpdateService.cs:ResolveDownloadSources` — multi-source enumeration
- `UpdateService.cs:BackgroundUpdateChecker` — systray polling
- `UpdateService.cs:NotesService` — notes fetcher
- `UpdateNotes/RELEASE_NOTES_v0_9_1.md` — embedded resource (fallback)
- `UpdateNotes/RELEASE_NOTES_v0_9_0.md` — embedded resource (fallback)

## Acceptance criteria

1. User on 0.7.x auto-updates to 0.9.2 successfully (even with antivirus deletion of partial file)
2. SHA256 mismatch downloads from fallback mirror
3. Empty release body shows embedded notes
4. Network outage retries 3 times with backoff
5. GitHub API rate limit falls back to Gist mirror
6. Background checker silently polls every 6 hours
7. All existing 337 tests still pass
8. Build clean, no warnings
9. Publish produces single-file exe with all updates baked in

## Out of scope (deferred)

- Online activation server (LemonSqueezy / Paddle)
- Subscription / auto-renewal
- Delta updates (only full installers)
- Resume interrupted downloads (already partial files survive but app must re-download)
- Multi-language release notes

## Verification

1. `dotnet build -c Release` → 0 errors, 0 warnings
2. `dotnet test` → 337+ pass (10+ new tests for the rewrite)
3. `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish` → exit 0
4. Manual flow test:
   - Generate fake installer with wrong SHA256 → DownloadAsync refuses, retries from next source
   - Generate fake installer with correct SHA256 → DownloadAsync succeeds
   - Empty release body → UpdateNotesService returns embedded notes
   - BackgroundUpdateChecker tick fires twice → second tick is a no-op (ETag cached)
