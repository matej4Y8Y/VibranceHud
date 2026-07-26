using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// Keeps the app alive as a tray icon and owns the main window, opening/focusing it on
    /// the global hotkey (Ctrl+Alt+V), the tray double-click, and startup.
    /// </summary>
    public sealed class TrayApplicationContext : ApplicationContext
    {
        private const int HOTKEY_ID = 1;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_V = 0x56;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private readonly NotifyIcon _trayIcon;
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

        public TrayApplicationContext()
        {
            _controller = CreateVibranceController();
            _overlay = TryCreateOverlay();
            _gammaRamp = new DisplayGammaRamp();
            _engine = new VibranceEngine(_controller, _overlay, _gammaRamp);

            string dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlexusX");
            _store = new SettingsStore(dataDir);
            _settings = _store.Load();

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

            _window = new MainWindow(_engine, _settings, _store, new SystemTweaks.SystemTweakService(), _audioEdge, ApplyTheme, _customTheme, _crosshair, BuildProfileCoordinator());

            _hotkeyWindow = new HotkeyWindow();
            _hotkeyWindow.HotkeyPressed += (s, e) => _window.ShowAndFocus();

            if (!RegisterHotKey(_hotkeyWindow.Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_V))
            {
                // Not fatal - another app may already own Ctrl+Alt+V. The tray menu
                // still works either way, so just let the user know why the hotkey is quiet.
                MessageBox.Show(
                    "Couldn't register Ctrl+Alt+V (another app may already be using it). " +
                    "You can still open the slider from the tray icon.",
                    "PlexusX",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            var menu = new ContextMenuStrip();
            menu.Items.Add("Open  (Ctrl+Alt+V)", null, (s, e) => _window.ShowAndFocus());
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

        private static ISaturationOverlay TryCreateOverlay()
        {
            var dx = new DxOverlay();
            if (dx.IsAvailable) return dx;
            dx.Dispose();
            // DX11 init failed (no DX11 GPU, broken driver, session locked, etc.) -
            // fall back to Magnification API. The user sees saturated colors on the
            // monitor but the effect is not visible in capture tools.
            return new MagOverlay();
        }

        /// <summary>
        /// Loading sequence: check for an update and, if there is one, download and install
        /// it silently (the installer relaunches us). Otherwise show "what's new" if we just
        /// updated, then open the app.
        /// </summary>
        private async Task RunStartupAsync()
        {
            var startedAt = DateTime.UtcNow;

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

            var file = await UpdateService.DownloadAsync(update, p => _splash.SetStatus(label, p));
            if (file == null) return false; // download failed - carry on into the app

            _splash.SetStatus("Installing update…");
            if (!UpdateService.RunInstallerSilently(file)) return false;

            ExitThread();
            return true;
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
            _settings.LightTheme = ThemeCatalog.ByName(themeName).IsLight; // keep legacy flag consistent
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
            _window = new MainWindow(_engine, _settings, _store, new SystemTweaks.SystemTweakService(), _audioEdge, ApplyTheme, _customTheme, _crosshair, _profileCoordinator);
            _window.ShowAndFocus();
            old.Dispose();
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
            var coordinator = new ProfileEngineCoordinator(watcher, applyEngine, new GameProfileApplyGate());
            coordinator.Start();

            _profileCoordinator = coordinator;
            return coordinator;
        }

        protected override void ExitThreadCore()
        {
            _crosshair.Dispose(); // never leave an overlay floating on screen
            UnregisterHotKey(_hotkeyWindow.Handle, HOTKEY_ID);
            _profileCoordinator?.Stop();  // tear down the polling loop before the engine disappears
            _store.Save(_settings);
            (_overlay as IDisposable)?.Dispose();    // clears any oversaturation and releases the overlay runtime
            _gammaRamp.Dispose();  // gamma ramps persist after exit, so always restore linear
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
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
            }

            base.WndProc(ref m);
        }
    }
}
