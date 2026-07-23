using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VibranceHud.Games;
using VibranceHud.Pages;

namespace VibranceHud
{
    /// <summary>
    /// The main application window: a large matte-black panel with a custom title bar, a
    /// left navigation column, and a content area that swaps pages. Replaces the old
    /// cursor popup. Closing hides it to the tray; the app keeps running.
    /// </summary>
    public sealed class MainWindow : Form
    {
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private const int TitleH = 52;
        private const int NavW = 210;

        private readonly VibranceEngine _engine;
        private readonly AppSettings _settings;
        private readonly SettingsStore _store;

        private readonly Panel _contentHost;
        private readonly VibrancePage _vibrancePage;
        private readonly SettingsPage _settingsPage;
        private readonly AccountPage _accountPage;
        private readonly NavButton _navVibrance, _navGames, _navSettings, _navAccount;

        public MainWindow(VibranceEngine engine, AppSettings settings, SettingsStore store)
        {
            _engine = engine;
            _settings = settings;
            _store = store;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Vibrance HUD";
            BackColor = Theme.Background;
            ClientSize = new Size(1040, 680);
            MinimumSize = new Size(900, 600);
            Opacity = Math.Clamp(settings.OpacityPercent, 50, 100) / 100.0;
            Font = new Font(Theme.FontFamily, 9f);
            DoubleBuffered = true;

            // ---- Title bar ----
            var titleBar = new Panel { Location = new Point(0, 0), Size = new Size(ClientSize.Width, TitleH), BackColor = Theme.Background, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            titleBar.MouseDown += DragWindow;

            var brand = new Label { Text = "VIBRANCE", ForeColor = Theme.Accent, Font = new Font(Theme.FontFamily, 11f, FontStyle.Bold), Location = new Point(20, 16), AutoSize = true, BackColor = Color.Transparent };
            brand.MouseDown += DragWindow;
            var brand2 = new Label { Text = "HUD", ForeColor = Theme.TextDim, Font = new Font(Theme.FontFamily, 11f, FontStyle.Bold), Location = new Point(107, 16), AutoSize = true, BackColor = Color.Transparent };
            brand2.MouseDown += DragWindow;
            titleBar.Controls.Add(brand);
            titleBar.Controls.Add(brand2);

            var close = TitleGlyph("✕", ClientSize.Width - 42);
            close.Click += (s, e) => Hide();
            var min = TitleGlyph("─", ClientSize.Width - 78);
            min.Click += (s, e) => WindowState = FormWindowState.Minimized;
            titleBar.Controls.Add(close);
            titleBar.Controls.Add(min);
            Controls.Add(titleBar);

            // Pages are created before the nav wires its click handlers to them.
            _vibrancePage = new VibrancePage(_engine, _settings, _store);
            _settingsPage = new SettingsPage(_settings, _store, SetWindowOpacity);
            _accountPage = new AccountPage();

            // ---- Left nav ----
            var nav = new Panel { Location = new Point(0, TitleH), Size = new Size(NavW, ClientSize.Height - TitleH), BackColor = Theme.Background, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom };

            _navVibrance = MakeNav("", "Vibrance", 0);
            _navGames = MakeNav("", "Games", 1);
            _navSettings = MakeNav("", "Settings", 2);
            _navAccount = MakeNav("", "Account", 3);
            _navVibrance.Click += (s, e) => ShowVibrance();
            _navGames.Click += (s, e) => ShowGames();
            _navSettings.Click += (s, e) => Select(_navSettings, _settingsPage);
            _navAccount.Click += (s, e) => Select(_navAccount, _accountPage);
            nav.Controls.AddRange(new Control[] { _navVibrance, _navGames, _navSettings, _navAccount });
            Controls.Add(nav);

            // ---- Content host ----
            _contentHost = new Panel { Location = new Point(NavW, TitleH), Size = new Size(ClientSize.Width - NavW, ClientSize.Height - TitleH), BackColor = Theme.Background, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
            Controls.Add(_contentHost);

            ShowVibrance();
        }

        private NavButton MakeNav(string glyph, string label, int index)
        {
            // Segoe MDL2 Assets glyphs by nav slot: brightness, game, gear, contact.
            string[] glyphs = { "", "", "", "" };
            return new NavButton
            {
                IconKind = index,
                Text = label,
                Location = new Point(0, 16 + index * 48),
                Size = new Size(NavW, 46)
            };
        }

        private Label TitleGlyph(string text, int x) => new()
        {
            Text = text,
            ForeColor = Theme.TextDim,
            Font = new Font(Theme.FontFamily, 10f),
            Size = new Size(32, TitleH),
            Location = new Point(x, 0),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        private void SetWindowOpacity(int percent) => Opacity = Math.Clamp(percent, 50, 100) / 100.0;

        private void ShowVibrance()
        {
            Select(_navVibrance, _vibrancePage);
            _vibrancePage.Refresh(_engine.CurrentLevel);
        }

        private void ShowGames()
        {
            var page = new GamesHubPage(OnConfigureGame);
            Select(_navGames, page);
        }

        private void OnConfigureGame(DetectedGame game)
        {
            var page = new RustSettingsPage(game, onBack: ShowGames);
            SetContent(page);
            SetActive(_navGames); // stay under the Games section
        }

        private void Select(NavButton button, Control page)
        {
            SetActive(button);
            SetContent(page);
        }

        private void SetActive(NavButton active)
        {
            foreach (var b in new[] { _navVibrance, _navGames, _navSettings, _navAccount })
                b.Active = ReferenceEquals(b, active);
        }

        private void SetContent(Control page)
        {
            _contentHost.SuspendLayout();
            _contentHost.Controls.Clear();
            page.Dock = DockStyle.Fill;
            _contentHost.Controls.Add(page);
            _contentHost.ResumeLayout();
        }

        /// <summary>Bring the window to the front (used by the tray/hotkey).</summary>
        public void ShowAndFocus()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            BringToFront();
            Activate();
            _vibrancePage.Refresh(_engine.CurrentLevel);
        }

        private void DragWindow(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Subtle grey, semi-transparent frame + dividers under the title bar and beside
            // the nav - the whole window is already translucent via Opacity.
            using var pen = new Pen(Theme.GlassEdge, 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            using var divider = new Pen(Theme.Border, 1f);
            e.Graphics.DrawLine(divider, 0, TitleH, Width, TitleH);
            e.Graphics.DrawLine(divider, NavW, TitleH, NavW, Height);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // The window hides to tray instead of closing while the app runs.
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                _store.Save(_settings);
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
