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
        private readonly VibranceController _controller;
        private readonly SaturationOverlay _overlay;
        private readonly VibranceEngine _engine;
        private readonly SettingsStore _store;
        private readonly AppSettings _settings;
        private MainWindow _window;

        public TrayApplicationContext()
        {
            _controller = new VibranceController();
            _overlay = new SaturationOverlay();
            _engine = new VibranceEngine(_controller, _overlay);

            _store = new SettingsStore(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlexusX"));
            _settings = _store.Load();

            // Restore where the user left the slider last session.
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

            // Show the window on launch so the app opens to something visible.
            _window.ShowAndFocus();

            // Silently look for a newer release in the background, so an update is ready
            // to install the next time the app launches. Fire-and-forget by design.
            _ = UpdateService.CheckInBackgroundAsync();
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
            _overlay.Dispose(); // clears any oversaturation and releases the Magnification runtime
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
