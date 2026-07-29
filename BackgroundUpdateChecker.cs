// BackgroundUpdateChecker - polls GitHub for new releases in the background.
// Runs once when PlexusX starts, then every 6 hours. Uses HEAD requests
// (lightweight, ETag-cached) so it doesn't count against the GitHub API
// rate limit unless a new version actually appears.
//
// When TryGetUpdateAsync returns a non-null ReleaseInfo (newer than what's
// running), the checker writes the pending installer + version to AppSettings
// and notifies the user via the systray balloon. The actual install happens
// on next PlexusX restart, the same flow as a manual "Check for updates".
//
// This service replaces the "CheckManuallyAsync is the only update path"
// behaviour. It does NOT auto-install - that's an explicit user choice the
// user makes via the tray notification.

using System;
using System.Diagnostics;
using System.Windows.Forms;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace VibranceHud
{
    public sealed class BackgroundUpdateChecker : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly SettingsStore _store;
        private readonly NotifyIcon _trayIcon;
        private readonly System.Windows.Forms.Timer _timer;
        private string? _lastEtag;
        private bool _firstRun = true;

        public BackgroundUpdateChecker(AppSettings settings, SettingsStore store, NotifyIcon trayIcon)
        {
            _settings = settings;
            _store = store;
            _trayIcon = trayIcon;
            _timer = new WinFormsTimer { Interval = 6 * 60 * 60 * 1000 }; // 6 hours
            _timer.Tick += async (s, e) => await TickAsync();
        }

        public void Start()
        {
            _timer.Start();
            // Fire one immediately so the first check isn't delayed by 6 hours.
            _ = FirstTickAsync();
        }

        private async System.Threading.Tasks.Task FirstTickAsync()
        {
            // Give the splash screen 3 seconds to settle before we hit GitHub.
            await System.Threading.Tasks.Task.Delay(3000);
            await TickAsync();
        }

        private async System.Threading.Tasks.Task TickAsync()
        {
            if (_isRunning) return;
            _isRunning = true;
            try
            {
                var head = await UpdateService.HeadLatestReleaseAsync();
                if (head.Etag == null) return; // offline - skip
                if (head.Etag == _lastEtag && !_firstRun) return; // no change
                _lastEtag = head.Etag;
                _firstRun = false;

                var release = await UpdateService.TryGetUpdateAsync();
                if (release == null) return;

                // Don't notify if the user is already on the latest pending update.
                if (_settings.PendingUpdateVersion == release.Version.ToString()) return;

                // Background-checker doesn't download (that's the user's choice).
                // Just tell them an update is available.
                _trayIcon.BalloonTipTitle = "PlexusX update available";
                _trayIcon.BalloonTipText = $"Version {release.Version} is available. Click here to install.";
                _trayIcon.BalloonTipClicked -= OnBalloonClicked;
                _trayIcon.BalloonTipClicked += OnBalloonClicked;
                _trayIcon.ShowBalloonTip(10_000);
            }
            catch
            {
                // Network error / rate limit - silent retry on next tick.
            }
            finally
            {
                _isRunning = false;
            }
        }

        private void OnBalloonClicked(object? sender, EventArgs e)
        {
            // Trigger the manual check flow which handles download + restart.
            _ = UpdateService.CheckManuallyAsync();
        }

        private bool _isRunning;

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
