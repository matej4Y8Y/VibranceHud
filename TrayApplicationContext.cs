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
        private readonly SaturationOverlay _overlay;
        private readonly DisplayGammaRamp _gammaRamp;
        private readonly VibranceEngine _engine;
        private readonly SettingsStore _store;
        private readonly AppSettings _settings;
        private readonly SplashForm _splash;
        private MainWindow _window;

        public TrayApplicationContext()
        {
            _controller = CreateVibranceController();
            _overlay = new SaturationOverlay();
            _gammaRamp = new DisplayGammaRamp();
            _engine = new VibranceEngine(_controller, _overlay, _gammaRamp);

            _store = new SettingsStore(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlexusX"));
            _settings = _store.Load();

            // Restore where the user left things last session.
            _engine.Brightness = _settings.BrightnessPercent;
            _engine.Gamma = _settings.GammaPercent;
            _engine.EyeCare = _settings.EyeCare;
            _engine.SetLevel(_settings.Level);

            Theme.Apply(_settings.LightTheme); // before building the window

            _window = new MainWindow(_engine, _settings, _store, ApplyTheme);

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
        private void ApplyTheme(bool light)
        {
            _settings.LightTheme = light;
            _store.Save(_settings);
            Theme.Apply(light);

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
            _window = new MainWindow(_engine, _settings, _store, ApplyTheme);
            _window.ShowAndFocus();
            old.Dispose();
        }

        protected override void ExitThreadCore()
        {
            UnregisterHotKey(_hotkeyWindow.Handle, HOTKEY_ID);
            _store.Save(_settings);
            _overlay.Dispose();    // clears any oversaturation and releases the Magnification runtime
            _gammaRamp.Dispose();  // gamma ramps persist after exit, so always restore linear
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _hotkeyWindow.DestroyHandle();
            base.ExitThreadCore();
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
