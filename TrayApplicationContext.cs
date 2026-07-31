using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VibranceHud.License;

namespace VibranceHud
{
    /// <summary>
    /// Keeps the app alive as a tray icon and owns the main window, opening/focusing it on
    /// the global hotkey (Ctrl+Alt+V), the tray double-click, and startup.
    ///
    /// Two global hotkeys are registered now (post alt-tab fix):
    ///   - HOTKEY_ID      -> the quick vibrance popup (settings.Hotkey* combo)
    ///   - MAIN_HOTKEY_ID -> the full main window (settings.MainHotkey* combo, opt-in)
    /// Both share the same invisible HotkeyWindow so WM_HOTKEY messages from either ID
    /// reach us via the same WndProc.
    /// </summary>
    public sealed class TrayApplicationContext : ApplicationContext
    {
        private const int HOTKEY_ID = 1;
        private const int MAIN_HOTKEY_ID = 2;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private readonly NotifyIcon _trayIcon;
        private BackgroundUpdateChecker? _backgroundChecker;
        private readonly HotkeyWindow _hotkeyWindow;
        private readonly IVibranceController _controller;
        private readonly ISaturationOverlay _overlay;
        private readonly DisplayGammaRamp _gammaRamp;
        private readonly VibranceEngine _engine;
        private readonly SettingsStore _store;
        private readonly Theming.CustomThemeService _customTheme;
        private readonly Crosshair.CrosshairService _crosshair = new();
        private readonly AppSettings _settings;
        private readonly SplashForm _splash;
        private readonly Audio.AudioEdgeService? _audioEdge;
        private ProfileEngineCoordinator? _profileCoordinator;
        private MainWindow _window;
        private readonly LicenseService _license;
        private VibrancePopup? _vibrancePopup;
        private ToolStripItem? _hotkeyMenuItem;
        private ToolStripMenuItem? _mainHotkeyMenuItem;
        private CompositionKeeper? _compositionKeeper;
        private System.Windows.Forms.Timer? _betaGateTimer;

        public TrayApplicationContext(LicenseService? license = null)
        {
            _license = license ?? new LicenseService();
            _controller = CreateVibranceController();
            _overlay = TryCreateOverlay();
            _gammaRamp = new DisplayGammaRamp();
            _engine = new VibranceEngine(_controller, _overlay, _gammaRamp);

            string dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlexusX");
            _store = new SettingsStore(dataDir);
            _settings = _store.Load();

            // Record which overlay mechanism actually ended up active - TryCreateOverlay()
                        // silently falls back to Magnification if DX11 init failed, and that fallback is
                        // invisible to screen-capture tools (OBS/Discord). Persist it so the Settings page
                        // can tell the user instead of hiding it. Also persist the categorised failure
                        // reason + message so the Settings page shows an actionable hint, not just
                        // "Fallback mode" with no context.
            _settings.OverlayMode = OverlayModeResolver.Resolve(_overlay);
            _settings.DxFailure = (_overlay as IDisplayOverlay)?.LastFailure ?? DxInitFailureKind.None;
            _settings.DxFailureMessage = (_overlay as IDisplayOverlay)?.LastFailureMessage ?? "";
            _store.Save(_settings);

            // If we started on the Magnification fallback, keep trying for DX11 in the
            // background rather than leaving the session capture-invisible. Started here,
            // after _store/_settings exist, because a successful upgrade persists the
            // corrected mode so Settings stops warning about it.
            StartOverlayUpgradeWatch();

            // Keep the desktop composited so the colour effect lands in what capture tools
            // read. Without this, whether a user's colours reach their viewers is decided by
            // their GPU and display config - which is why an identical build worked in some
            // people's screen shares and not others.
            if (_settings.KeepDesktopComposited)
                _compositionKeeper = new CompositionKeeper();

            // Rebuild a previously chosen image background + its derived palette before
            // the theme is resolved, so "Custom" is a known name by the time it's applied.
            _customTheme = new Theming.CustomThemeService(dataDir, _settings);
            _customTheme.Restore();

            // Bring back the crosshair the user left on, before the window is built.
            _crosshair.Apply(_settings.ActiveCrosshair);
            if (_settings.CrosshairEnabled) _crosshair.Show();

            // Restore where the user left things last session.
            _engine.Brightness = _settings.BrightnessPercent;
            _engine.Gamma = _settings.GammaPercent;
            _engine.EyeCare = _settings.EyeCare;
            // Resolved properties migrate an old combined "Level" on first run after
            // vibrance and saturation became separate controls.
            _engine.Vibrance = _settings.ResolvedVibrance;
            _engine.Saturation = _settings.ResolvedSaturation;
            _settings.VibrancePercent = _engine.Vibrance;
            _settings.SaturationPercent = _engine.Saturation;

            // Resolve the theme (migrating the old light/dark bool) and pin the name back,
            // persisting once so the migration doesn't re-run every launch.
            var palette = ThemeCatalog.Resolve(_settings.ThemeName, _settings.LightTheme);
            if (_settings.ThemeName != palette.Name)
            {
            _settings.ThemeName = palette.Name;
            _store.Save(_settings);
            }
            Theme.Apply(palette); // before building the window

            // Audio Edge needs a playback device; if there isn't one the feature just hides.
            _audioEdge = CreateAudioEdge();
            if (_audioEdge != null)
            {
            _audioEdge.Threshold = Math.Clamp(_settings.AudioEdgeThresholdPercent, 5, 100) / 100f;
            if (_settings.AudioEdgeEnabled) _audioEdge.Start();
            }

            _window = new MainWindow(_engine, _settings, _store, new SystemTweaks.SystemTweakService(), _audioEdge, ApplyTheme, _customTheme, _crosshair, BuildProfileCoordinator(), _license, ReRegisterHotkey, ReRegisterMainHotkey);

            _hotkeyWindow = new HotkeyWindow();
            _hotkeyWindow.HotkeyPressed += (s, e) =>
            {
            // Distinguish which hotkey fired by the WM_HOTKEY wParam (id field) -
            // HotkeyWindow forwards that as HotkeyEventArgs.HotkeyId.
            if (e is HotkeyEventArgs he && he.HotkeyId == MAIN_HOTKEY_ID) ShowMainWindow();
            else ShowVibrancePopup();
            };

            // The popup hotkey always registers. A registration failure is non-fatal -
            // another app may already own this combo; the tray menu still works.
            if (!RegisterHotKey(_hotkeyWindow.Handle, HOTKEY_ID, _settings.HotkeyModifierMask, _settings.HotkeyVirtualKey))
            {
            MessageBox.Show(
            $"Couldn't register {GetHotkeyDisplay()} (another app may already be using it). " +
            "You can still open the slider from the tray icon.",
            "PlexusX",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
            }

            // The main-window hotkey is opt-in (the picker on the Vibrance page enables
            // it the moment the user picks a combo). Until then we don't try to bind.
            if (_settings.MainHotkeyEnabled)
            TryRegisterMainHotkey();

            var menu = new ContextMenuStrip();
            menu.Items.Add("Open", null, (s, e) => _window.ShowAndFocus());
            _hotkeyMenuItem = menu.Items.Add($"Quick vibrance  ({GetHotkeyDisplay()})", null, (s, e) => ShowVibrancePopup());
            _mainHotkeyMenuItem = (ToolStripMenuItem)menu.Items.Add(
        $"Open main window  ({GetMainHotkeyDisplay()})",
        null,
        (s, e) => ShowMainWindow());
            UpdateMainHotkeyMenuItem();
            menu.Items.Add("Reset vibrance", null, (s, e) => _engine.Reset());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Check for updates", null, async (s, e) => await UpdateService.CheckManuallyAsync());
            menu.Items.Add("Exit", null, (s, e) => ExitThread());

            _trayIcon = new NotifyIcon
            {
            Icon = AppIcon.Value,
            Text = "PlexusX",
            Visible = true,
            ContextMenuStrip = menu
            };
            _trayIcon.DoubleClick += (s, e) => _window.ShowAndFocus();

            // The tray tooltip surfaces the auto-apply state: "PlexusX" when the
            // coordinator isn't built yet (e.g. errors during startup), and
            // "PlexusX — auto-apply running" while the watcher polls.
            UpdateTrayStateText();

            // The splash drives startup: check for updates, install one if there is one,
            // then hand over to the main window.
            _splash = new SplashForm();
            _splash.Shown += async (s, e) => await RunStartupAsync();
            _splash.Show();

            // Background update checker - polls every 6 hours, notifies via systray
            // balloon when a new version appears. Doesn't auto-install (user picks
            // the moment via the tray notification).
            _backgroundChecker = new BackgroundUpdateChecker(_settings, _store, _trayIcon);
            _backgroundChecker.Start();
        }


        /// <summary>
        /// NVIDIA's driver vibrance (NVAPI) only exists on NVIDIA hardware with the driver
        /// installed - on anything else this throws. That must never take down the whole
        /// app: falling back to <see cref="NullVibranceController"/> keeps Games Hub, Rust
        /// tweaks, and the 100-200% software vibrance boost working on every PC.
        /// </summary>
        /// <summary>The limiter, or null when this PC has no usable playback device.</summary>
        private static Audio.AudioEdgeService? CreateAudioEdge()
        {
            try { return new Audio.AudioEdgeService(new Audio.WindowsAudioOutput()); }
            catch { return null; }
        }

        private static IVibranceController CreateVibranceController()
        {
            try
            {
            return new VibranceController();
            }
            catch
            {
            return new NullVibranceController();
            }
        }

        /// <summary>
        /// Re-ask whether this build may still run, now and every few hours.
        ///
        /// The startup check in Program.cs reads only the cached answer so launching never
        /// waits on the network. This is what actually brings the news in. PlexusX normally
        /// runs for days at a time, so without it a user would only find out the beta had
        /// ended at their next reboot.
        ///
        /// When the answer changes, the app closes rather than trying to tear itself down into
        /// some disabled half-state - the next launch shows the ended screen from Program.cs,
        /// which is the one place that decision lives.
        /// </summary>
        private void StartBetaGateWatch()
        {
            _ = CheckBetaGateAsync();

            var timer = new System.Windows.Forms.Timer { Interval = 2 * 60 * 60 * 1000 }; // 2h
            timer.Tick += (s, e) => _ = CheckBetaGateAsync();
            timer.Start();
            _betaGateTimer = timer;
        }

        private async System.Threading.Tasks.Task CheckBetaGateAsync()
        {
            try
            {
                var minimum = await AppStatusService.RefreshAsync();
                if (!VersionGate.IsBlocked(UpdateService.CurrentVersion, minimum)) return;

                // Tell them why the app is closing - otherwise it just vanishes mid-session.
                using (var ended = new BetaEndedWindow(AppStatusService.CachedMessage()))
                    ended.ShowDialog();

                ExitThread();
            }
            catch
            {
                // Never let a status check take the app down. Offline, DNS failure and captive
                // portals are all ordinary and none of them mean the beta has ended.
            }
        }

        /// <summary>One DX11 attempt. Returns null when it isn't available right now, and
        /// reports WHY through the out params - the failed DxOverlay is disposed here, so
        /// this is the only chance to capture its reason before it's gone.</summary>
        private static ISaturationOverlay? TryCreateDxOverlay(
            out DxInitFailureKind failure, out string failureMessage)
        {
            var dx = new DxOverlay();
            if (dx.IsAvailable)
            {
                failure = DxInitFailureKind.None;
                failureMessage = "";
                return dx;
            }
            failure = dx.LastFailure;
            failureMessage = dx.LastFailureMessage;
            dx.Dispose();
            return null;
        }

        /// <summary>Overload for the retry path, which only cares whether it worked.</summary>
        private static ISaturationOverlay? TryCreateDxOverlay() => TryCreateDxOverlay(out _, out _);

        private static ISaturationOverlay TryCreateOverlay()
        {
            var dx = TryCreateDxOverlay(out var failure, out var failureMessage);
            if (dx != null) return dx;

            // DX11 init failed (no DX11 GPU, broken driver, locked session, GPU memory taken
            // by a game/OBS that started first, display not ready yet). Fall back to the
            // Magnification API so the user still sees the effect on their own monitor - but
            // wrap it, because that path is invisible to OBS/Discord/ShadowPlay and most of
            // those causes clear up on their own. UpgradingOverlay moves us to DX11 as soon
            // as it becomes available instead of stranding the session on the fallback.
            return new UpgradingOverlay(new MagOverlay(), TryCreateDxOverlay, failure, failureMessage);
        }

        /// <summary>
        /// Poll for DX11 becoming available after a fallback start, then stop.
        ///
        /// Backs off (2s, 4s, 8s, …) so a machine that genuinely has no DX11 isn't building
        /// throwaway devices forever, and gives up after a handful of tries - by then the
        /// cause is not transient and the Settings hint is the right answer instead.
        /// </summary>
        private void StartOverlayUpgradeWatch()
        {
            if (_overlay is not UpgradingOverlay upgrading || !upgrading.CanUpgrade) return;

            int delayMs = 2000;
            const int maxAttempts = 6;
            int attempts = 0;

            var timer = new System.Windows.Forms.Timer { Interval = delayMs };
            timer.Tick += (s, e) =>
            {
                attempts++;

                if (upgrading.TryUpgrade())
                {
                    timer.Stop();
                    timer.Dispose();
                    // Persist the good news so Settings stops warning about capture
                    // invisibility, and reflect it on the page if it's already open.
                    _settings.OverlayMode = OverlayMode.Dx;
                    _settings.DxFailure = DxInitFailureKind.None;
                    _settings.DxFailureMessage = "";
                    _store.Save(_settings);
                    return;
                }

                if (attempts >= maxAttempts || !upgrading.CanUpgrade)
                {
                    timer.Stop();
                    timer.Dispose();
                    return;
                }

                delayMs = Math.Min(delayMs * 2, 30_000);
                timer.Interval = delayMs;
            };
            timer.Start();
        }

        /// <summary>
        /// Loading sequence: check for an update and, if there is one, download and install
        /// it silently (the installer relaunches us). Otherwise show "what's new" if we just
        /// updated, then open the app.
        /// </summary>
        private async Task RunStartupAsync()
            {
            var startedAt = DateTime.UtcNow;

            // If a previous session downloaded an installer and stashed it in
            // AppSettings.PendingUpdateInstaller, run it now BEFORE doing anything else.
            // The installer will close this new PlexusX, replace the files, and the
            // installer's [Run] section relaunches the new version. This is the only
            // reliable way to self-update - launching the installer from a running
            // PlexusX (the previous design) silently fails on Windows because the OS
            // blocks silent installs from a live parent.
            // Async path: verifies against GitHub releases/latest before launching the
            // installer, so we never silently downgrade the user to an older build that
            // happened to be lying around in %TEMP%.
            // Bin any downloaded installer now at or below the running version. These used to
            // pile up in %TEMP% - ~64MB each, several per user - because the recovery scan only
            // ever looked for NEWER versions and so never removed the old ones. Before the
            // pending check, so a superseded file isn't even considered.
            UpdateService.CleanupObsoleteInstallers();

            // Ask whether this build is still allowed to run. Program.cs already checked the
            // cached answer at startup; this refreshes it, so someone running for days gets
            // locked within hours of the switch being thrown rather than at their next reboot.
            StartBetaGateWatch();

            if (await UpdateService.RunPendingUpdateIfAnyAsync(_settings))
            {
                // Installer is running and will close us shortly. Hand control back to
                // the message loop so it has time to do its work before we shut down.
                Application.Exit();
                return;
            }

            // Pull the latest revocation list while we're already online for the update
            // check. LicenseService.Load() (which ran before this window existed) used the
            // cached copy, so a key revoked since the last launch is only caught here -
            // hence the re-check below rather than waiting for the next start.
            if (await RevocationService.RefreshAsync() && _license.HasValidLicense)
            {
                _license.Load();
                if (_license.State == LicenseState.Revoked)
                {
                    _splash.Close();
                    MessageBox.Show(
                        "This license key has been deactivated by the developer.\n\n" +
                        "If you believe this is a mistake, please get in touch.",
                        "PlexusX", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    ExitThread();
                    return;
                }
            }

            _splash.SetStatus("Checking for updates…");
            var update = await UpdateService.TryGetUpdateAsync();

            if (update != null && await InstallUpdateAsync(update))
            return; // the installer took over and will relaunch PlexusX

            _splash.SetStatus("Starting…");
            var notes = await WhatsNewNotesAsync();

            // Keep the splash up briefly so it never flashes past on a fast machine.
            var shown = DateTime.UtcNow - startedAt;
            var minimum = TimeSpan.FromMilliseconds(1400);
            if (shown < minimum) await Task.Delay(minimum - shown);

            _splash.Close();

            // First run: the cinematic onboarding (which also lets them pick a theme),
            // shown instead of "what's new". A theme change there means we rebuild the
            // already-constructed window so it repaints in the chosen colours.
            if (!_settings.OnboardingComplete)
            {
            var themeBefore = Theme.CurrentName;
            using (var onboarding = new OnboardingForm(_settings, _store))
            onboarding.ShowDialog();

            // Don't also pop "what's new" on a brand-new install.
            _settings.LastSeenVersion = UpdateService.CurrentVersion.ToString();
            _store.Save(_settings);

            if (Theme.CurrentName != themeBefore) RebuildWindow();
            else _window.ShowAndFocus();
            return;
            }

            if (notes != null)
            {
            using var whatsNew = new WhatsNewWindow(UpdateService.CurrentVersion, notes);
            whatsNew.ShowDialog();
            }

            _window.ShowAndFocus();
        }

        /// <summary>Returns true when the installer started and this instance should quit.</summary>
        private async Task<bool> InstallUpdateAsync(ReleaseInfo update)
            {
            string label = $"Downloading update {update.Version}…";
            _splash.SetStatus(label, 0);

            var file = await UpdateService.DownloadAndStageAsync(update, new Progress<int>(p => _splash.SetStatus(label, p)));
            if (file == null) return false; // download failed - carry on into the app

            // Stash the installer path so the NEXT launch picks it up. Running it from
            // here (the previous design) silently failed on this user's machine because
            // Windows blocks silent installs from a live parent process. The next-launch
            // approach is the only reliable way to self-update.
            _settings.PendingUpdateInstaller = file;
            _settings.PendingUpdateVersion = update.Version.ToString();
            _store.Save(_settings);

            // Sanity check before promising the user a clean install.
            if (!UpdateService.RunInstallerSilently(file))
            {
            _settings.PendingUpdateInstaller = "";
            _settings.PendingUpdateVersion = "";
            _store.Save(_settings);
            return false;
            }

            _splash.SetStatus($"Restart PlexusX to finish installing v{update.Version}");
            return false; // don't close the app - user does it manually
            }

        /// <summary>
        /// The notes to show once after an update, or null when there's nothing to show
        /// (same version as last run, or a brand-new install).
        /// </summary>
        private async Task<string?> WhatsNewNotesAsync()
        {
            var current = UpdateService.CurrentVersion.ToString();
            if (_settings.LastSeenVersion == current) return null;

            bool firstEverRun = string.IsNullOrEmpty(_settings.LastSeenVersion);
            _settings.LastSeenVersion = current;
            _store.Save(_settings);
            if (firstEverRun) return null; // a fresh install doesn't need a changelog

            return await UpdateService.GetNotesForVersionAsync(UpdateService.CurrentVersion);
        }

        /// <summary>
        /// Switch the palette and rebuild the window so every control repaints in the new
        /// theme. The rebuild is deferred by a one-shot timer so we don't dispose the window
        /// while it's still handling the toggle's event.
        /// </summary>
        private void ApplyTheme(string themeName)
                {
                    _settings.ThemeName = themeName;
                    // LightTheme was the legacy bool used before theme names existed; it's
                    // kept on AppSettings as a one-way migration aid (TrayApplicationContext
                    // reads it once at startup to seed ThemeCatalog.Resolve). New code
                    // doesn't read the field, so back-writing it here is dead-store churn
                    // on the settings.json file.
                    _store.Save(_settings);
                    Theme.Apply(themeName);

            var deferred = new System.Windows.Forms.Timer { Interval = 1 };
            deferred.Tick += (s, e) =>
            {
            deferred.Stop();
            deferred.Dispose();
            RebuildWindow();
            };
            deferred.Start();
        }

        private void RebuildWindow()
        {
            var old = _window;
            _window = new MainWindow(_engine, _settings, _store, new SystemTweaks.SystemTweakService(), _audioEdge, ApplyTheme, _customTheme, _crosshair, _profileCoordinator, _license, ReRegisterHotkey, ReRegisterMainHotkey);
            _window.ShowAndFocus();
            old.Dispose();
        }

        /// <summary>
        /// The global hotkey's own surface: a small always-on-top popup for a quick tweak
        /// without opening the full window. Reused across hotkey presses (built once, then
        /// just re-shown) so repeated Ctrl+Alt+V doesn't leak a window per press.
        /// </summary>
        private void ShowVibrancePopup()
        {
            if (_vibrancePopup == null || _vibrancePopup.IsDisposed)
            _vibrancePopup = new VibrancePopup(_engine, _settings, _store);
            _vibrancePopup.Show();
            _vibrancePopup.Activate();
        }

        /// <summary>Show or focus the main window. Wired to the second global hotkey
        /// (Ctrl+Shift+M by default) so the user has a one-press path to the full app
        /// distinct from the popup.</summary>
        private void ShowMainWindow()
        {
            _window.ShowAndFocus();
        }

        /// <summary>
        /// Builds (or returns the existing) profile coordinator. Lazy because the
        /// coordinator is created just once per process lifetime — the auto-apply
        /// watcher keeps running across theme-window rebuilds.
        /// </summary>
        private ProfileEngineCoordinator BuildProfileCoordinator()
        {
            if (_profileCoordinator != null) return _profileCoordinator;

            // Static registry of known games → running process name. Independent of
            // whether they're installed on this PC: the per-service hub applier
            // gracefully no-ops when a game is missing, and the watcher just emits
            // launch events for games the user adds later.
            var idToExe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
            ["rust"]     = "RustClient",
            ["cs2"]      = "cs2",
            ["apex"]     = "r5apex",
            ["fortnite"] = "FortniteClient-Win64-Shipping",
            };

            var watcher = new GameProcessWatcher(idToExe);
            var applyEngine = new ProfileApplyEngine(_engine, new GameHubApplier());
            var coordinator = new ProfileEngineCoordinator(watcher, applyEngine, new GameProfileApplyGate(_settings));
            coordinator.Start();

            _profileCoordinator = coordinator;
            return coordinator;
        }

        protected override void ExitThreadCore()
        {
            _crosshair.Dispose(); // never leave an overlay floating on screen
            _vibrancePopup?.Dispose();
            UnregisterHotKey(_hotkeyWindow.Handle, HOTKEY_ID);
            UnregisterHotKey(_hotkeyWindow.Handle, MAIN_HOTKEY_ID);
            _profileCoordinator?.Stop();  // tear down the polling loop before the engine disappears
            // Manual override is a runtime-only flag: cleared on shutdown so a brand-new
            // launch always starts from the saved profile, not from a stale tweak.
            _settings.ManualOverrideActive = false;
            _store.Save(_settings);
            if (_betaGateTimer != null) { _betaGateTimer.Stop(); _betaGateTimer.Dispose(); _betaGateTimer = null; }
            _compositionKeeper?.Dispose();  // drops the 1x1 topmost pixel
            (_overlay as IDisposable)?.Dispose();    // clears any oversaturation and releases the overlay runtime
            _gammaRamp.Dispose();  // gamma ramps persist after exit, so always restore linear

            // Driver Digital Vibrance persists exactly like the gamma ramp above - it's a
            // setting written into the NVIDIA driver, so closing (or uninstalling) PlexusX
            // used to leave the user's monitor permanently altered. Measured on a real
            // machine: DVC sat at 97 with the app shut down, against a driver default of 50.
            // Beyond being untidy, it made diagnosis a mess - a leftover value looks exactly
            // like the app still working, which is what made "it works on his PC" so
            // confusing to chase down.
            try { _controller.SetLevel(_controller.DefaultLevel); }
            catch { /* no driver, or it went away - nothing to restore */ }
            _audioEdge?.Dispose(); // hands the user's volume back - never leave it ducked
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _hotkeyWindow.DestroyHandle();
            base.ExitThreadCore();
        }

        /// <summary>Set the tray tooltip to reflect auto-apply state. Called once on
        /// tray-icon creation; safe to call later if the coordinator ever becomes
        /// available after initial startup.</summary>
        private void UpdateTrayStateText()
        {
            var running = _profileCoordinator?.IsRunning == true;
            // NotifyIcon.Text is capped at 63 chars (defensive truncation).
            _trayIcon.Text = running ? "PlexusX \u2014 auto-apply running" : "PlexusX";
        }

        /// <summary>Drop the live RegisterHotKey, persist the new combo, register it, and
        /// refresh the tray menu so the visible shortcut stays in sync with what's bound.
        /// Called from the picker on the Vibrance page; safe to call at any point after
        /// the hotkey window handle exists.</summary>
        /// <returns>True when the combo actually bound. The picker uses this to tell the user
        /// their new hotkey is in use by another app, instead of showing it as though it were
        /// live while nothing is bound.</returns>
        public bool ReRegisterHotkey(uint mask, uint vk)
                {
                    UnregisterHotKey(_hotkeyWindow.Handle, HOTKEY_ID);
                    _settings.HotkeyModifierMask = mask;
                    _settings.HotkeyVirtualKey = vk;
                    _store.Save(_settings);
                    // Detect a duplicate combo: if the main-window hotkey already owns the
                    // same (mask, vk), don't try to re-register - Windows would silently
                    // drop the new binding and the user would see one hotkey not firing
                    // with no explanation. Disable the popup binding for this combo so
                    // the two never collide.
                    if (_settings.MainHotkeyEnabled
                        && _settings.MainHotkeyVirtualKey == vk
                        && _settings.MainHotkeyModifierMask == mask)
                    {
                        if (_hotkeyMenuItem != null)
                            _hotkeyMenuItem.Text = $"Quick vibrance  (conflicts with main window)";
                        return false;
                    }
                    if (!RegisterHotKey(_hotkeyWindow.Handle, HOTKEY_ID, mask, vk))
                    {
                        if (_hotkeyMenuItem != null)
                            _hotkeyMenuItem.Text = $"Quick vibrance  ({GetHotkeyDisplay()} - unavailable)";
                        return false;
                    }
                    if (_hotkeyMenuItem != null)
                        _hotkeyMenuItem.Text = $"Quick vibrance  ({GetHotkeyDisplay()})";
                    return true;
                }

                /// <summary>Variant for the second (main-window) hotkey. Toggles registration on
                /// or off depending on <see cref="AppSettings.MainHotkeyEnabled"/>: the Vibrance
                /// page's "Main window" picker calls this with <c>enabled=true</c> when the user
                /// picks a combo, and with <c>enabled=false</c> if they unbind it.</summary>
                public void ReRegisterMainHotkey(uint mask, uint vk, bool enabled)
                {
                    UnregisterHotKey(_hotkeyWindow.Handle, MAIN_HOTKEY_ID);
                    _settings.MainHotkeyModifierMask = mask;
                    _settings.MainHotkeyVirtualKey = vk;
                    _settings.MainHotkeyEnabled = enabled;
                    _store.Save(_settings);
                    if (enabled)
                        TryRegisterMainHotkey();
                    UpdateMainHotkeyMenuItem();
                }

                private void TryRegisterMainHotkey()
                {
                    if (_settings.MainHotkeyVirtualKey == 0) return; // nothing to bind yet
                    // Detect collision with the quick hotkey. RegisterHotKey returns false
                    // silently when the OS already owns the combo, but we can detect the
                    // case ourselves and tell the user via the tray menu label so the
                    // "doesn't fire" mystery isn't silent.
                    bool conflict = _settings.HotkeyVirtualKey == _settings.MainHotkeyVirtualKey
                                 && _settings.HotkeyModifierMask == _settings.MainHotkeyModifierMask;
                    if (conflict)
                    {
                        if (_mainHotkeyMenuItem != null)
                            _mainHotkeyMenuItem.Text = $"Open main window  (conflicts with quick vibrance)";
                        return;
                    }
                    if (!RegisterHotKey(_hotkeyWindow.Handle, MAIN_HOTKEY_ID,
                        _settings.MainHotkeyModifierMask, _settings.MainHotkeyVirtualKey))
                    {
                        if (_mainHotkeyMenuItem != null)
                            _mainHotkeyMenuItem.Text = $"Open main window  ({GetMainHotkeyDisplay()} - unavailable)";
                    }
                    else if (_mainHotkeyMenuItem != null)
                        _mainHotkeyMenuItem.Text = $"Open main window  ({GetMainHotkeyDisplay()})";
                }

                private void UpdateMainHotkeyMenuItem()
                {
                    if (_mainHotkeyMenuItem == null) return;
                    _mainHotkeyMenuItem.Text = _settings.MainHotkeyEnabled
                        ? $"Open main window  ({GetMainHotkeyDisplay()})"
                        : "Open main window";
                }

        /// <summary>Render the user's bound combo for the tray menu and the
        /// couldn't-register warning. Routes through the same static helper the picker
        /// uses, so the two displays can never disagree.</summary>
        private string GetHotkeyDisplay() => HotkeyPicker.GetDisplay(
            _settings.HotkeyModifierMask, _settings.HotkeyVirtualKey);

        private string GetMainHotkeyDisplay() => HotkeyPicker.GetDisplay(
            _settings.MainHotkeyModifierMask, _settings.MainHotkeyVirtualKey);
    }

    /// <summary>
    /// Invisible native window that exists purely to receive the WM_HOTKEY message -
    /// RegisterHotKey needs a real window handle to post messages to.
    /// </summary>
    internal sealed class HotkeyWindow : NativeWindow
    {
        private const int WM_HOTKEY = 0x0312;

        public event EventHandler? HotkeyPressed;

        public HotkeyWindow()
        {
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
            // m.WParam carries the hotkey id we passed to RegisterHotKey - propagate it
            // via a typed event so the TrayApplicationContext handler can route to the
            // popup vs the main window.
            var id = m.WParam.ToInt32();
            HotkeyPressed?.Invoke(this, new HotkeyEventArgs(id));
            }

            base.WndProc(ref m);
        }
    }

    /// <summary>Carries the hotkey id (the wParam from WM_HOTKEY) up to the handler so
    /// it knows which combo fired.</summary>
    internal sealed class HotkeyEventArgs : EventArgs
    {
        public int HotkeyId { get; }
        public HotkeyEventArgs(int id) { HotkeyId = id; }
    }
}