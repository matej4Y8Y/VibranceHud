using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// Keeps the app alive as a tray icon with no visible main window, and listens
    /// for the global hotkey (Ctrl+Alt+V) to pop the vibrance slider up.
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
        private readonly HudForm _popup;

        public TrayApplicationContext()
        {
            _controller = new VibranceController();
            _overlay = new SaturationOverlay();
            _engine = new VibranceEngine(_controller, _overlay);

            _store = new SettingsStore(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VibranceHud"));
            _settings = _store.Load();

            // Restore where the user left the slider last session.
            _engine.SetLevel(_settings.Level);

            _popup = new HudForm(_engine, _settings, _store);

            _hotkeyWindow = new HotkeyWindow();
            _hotkeyWindow.HotkeyPressed += (s, e) => _popup.ShowNearCursor();

            if (!RegisterHotKey(_hotkeyWindow.Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_V))
            {
                // Not fatal - another app may already own Ctrl+Alt+V. The tray menu
                // still works either way, so just let the user know why the hotkey is quiet.
                MessageBox.Show(
                    "Couldn't register Ctrl+Alt+V (another app may already be using it). " +
                    "You can still open the slider from the tray icon.",
                    "Vibrance HUD",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            var menu = new ContextMenuStrip();
            menu.Items.Add("Open HUD  (Ctrl+Alt+V)", null, (s, e) => _popup.ShowNearCursor());
            menu.Items.Add("Reset to default", null, (s, e) => _engine.Reset());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => ExitThread());

            _trayIcon = new NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application, // swap for a custom .ico whenever you like
                Text = "Vibrance HUD",
                Visible = true,
                ContextMenuStrip = menu
            };
            _trayIcon.DoubleClick += (s, e) => _popup.ShowNearCursor();
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
