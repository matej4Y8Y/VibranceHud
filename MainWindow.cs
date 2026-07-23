using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VibranceHud.Games;
using VibranceHud.Pages;

namespace VibranceHud
{
    /// <summary>
    /// The main application window: a large matte panel with a custom title bar, a left
    /// navigation column, and a content area that swaps pages - all sharing one animated
    /// purple particle field that emanates from the window centre and fades to the edges.
    /// The field animates only while this window is the foreground window, so it costs no
    /// CPU when hidden, minimized, or while you're in a game.
    /// </summary>
    public sealed class MainWindow : Form
    {
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private const int TitleH = 52;
        private const int NavW = 210;

        private readonly VibranceEngine _engine;
        private readonly AppSettings _settings;
        private readonly SettingsStore _store;
        private readonly Action<bool> _onThemeChanged;

        private readonly ParticleField _field = new(65);
        private readonly System.Windows.Forms.Timer _timer;
        private DateTime _last = DateTime.UtcNow;

        private readonly GlowPanel _titleBar;
        private readonly GlowPanel _nav;
        private readonly Panel _contentHost;
        private Control? _currentPage;

        private readonly VibrancePage _vibrancePage;
        private readonly SettingsPage _settingsPage;
        private readonly AccountPage _accountPage;
        private readonly NavButton _navVibrance, _navGames, _navSettings, _navAccount;

        public MainWindow(VibranceEngine engine, AppSettings settings, SettingsStore store,
            Action<bool> onThemeChanged)
        {
            _engine = engine;
            _settings = settings;
            _store = store;
            _onThemeChanged = onThemeChanged;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Vibrance HUD";
            BackColor = Theme.Background;
            ClientSize = new Size(1040, 680);
            MinimumSize = new Size(900, 600);
            Opacity = Math.Clamp(settings.OpacityPercent, 50, 100) / 100.0;
            Font = new Font(Theme.FontFamily, 9f);
            DoubleBuffered = true;

            _field.Resize(ClientSize.Width, ClientSize.Height);

            // ---- Title bar (shares the field) ----
            _titleBar = new GlowPanel { Field = _field, Location = new Point(0, 0), Size = new Size(ClientSize.Width, TitleH), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _titleBar.MouseDown += DragWindow;

            var brand = new Label { Text = "VIBRANCE", ForeColor = Theme.Accent, Font = new Font(Theme.FontFamily, 11f, FontStyle.Bold), Location = new Point(20, 16), AutoSize = true, BackColor = Color.Transparent };
            brand.MouseDown += DragWindow;
            var brand2 = new Label { Text = "HUD", ForeColor = Theme.TextDim, Font = new Font(Theme.FontFamily, 11f, FontStyle.Bold), Location = new Point(107, 16), AutoSize = true, BackColor = Color.Transparent };
            brand2.MouseDown += DragWindow;
            var close = TitleGlyph("✕", ClientSize.Width - 42);
            close.Click += (s, e) => Hide();
            var min = TitleGlyph("─", ClientSize.Width - 78);
            min.Click += (s, e) => WindowState = FormWindowState.Minimized;
            _titleBar.Controls.AddRange(new Control[] { brand, brand2, close, min });
            Controls.Add(_titleBar);

            // Pages exist before the nav wires click handlers to them.
            _vibrancePage = new VibrancePage(_engine, _settings, _store);
            _settingsPage = new SettingsPage(_settings, _store, SetWindowOpacity, _onThemeChanged);
            _accountPage = new AccountPage();
            foreach (var page in new GlowPage[] { _vibrancePage, _settingsPage, _accountPage })
                AttachField(page);

            // ---- Left nav (shares the field) ----
            _nav = new GlowPanel { Field = _field, Location = new Point(0, TitleH), Size = new Size(NavW, ClientSize.Height - TitleH), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom };
            _navVibrance = MakeNav("Vibrance", 0);
            _navGames = MakeNav("Games", 1);
            _navSettings = MakeNav("Settings", 2);
            _navAccount = MakeNav("Account", 3);
            _navVibrance.Click += (s, e) => ShowVibrance();
            _navGames.Click += (s, e) => ShowGames();
            _navSettings.Click += (s, e) => Select(_navSettings, _settingsPage);
            _navAccount.Click += (s, e) => Select(_navAccount, _accountPage);
            _nav.Controls.AddRange(new Control[] { _navVibrance, _navGames, _navSettings, _navAccount });
            Controls.Add(_nav);

            _contentHost = new Panel { Location = new Point(NavW, TitleH), Size = new Size(ClientSize.Width - NavW, ClientSize.Height - TitleH), BackColor = Theme.Background, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
            Controls.Add(_contentHost);

            AddDivider(new Point(0, TitleH), new Size(ClientSize.Width, 1), AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            AddDivider(new Point(NavW, TitleH), new Size(1, ClientSize.Height - TitleH), AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom);

            Resize += (s, e) => _field.Resize(ClientSize.Width, ClientSize.Height);

            _timer = new System.Windows.Forms.Timer { Interval = 33 };
            _timer.Tick += OnAnimationTick;
            _timer.Start();

            ShowVibrance();
        }

        private void AttachField(GlowPage page)
        {
            page.Field = _field;
            page.FieldOffset = new Point(NavW, TitleH);
        }

        private void OnAnimationTick(object? sender, EventArgs e)
        {
            var foreground = GetForegroundWindow() == Handle && Visible && WindowState != FormWindowState.Minimized;
            if (!foreground) { _last = DateTime.UtcNow; return; }

            var now = DateTime.UtcNow;
            _field.Update(Math.Min((now - _last).TotalSeconds, 0.1));
            _last = now;

            // invalidateChildren: true so transparent children (title labels, chips, slider)
            // re-sample the moving plexus instead of freezing the patch behind them.
            _titleBar.Invalidate(true);
            _nav.Invalidate(true);
            _currentPage?.Invalidate(true);
        }

        private NavButton MakeNav(string label, int index) => new()
        {
            IconKind = index, // 0 vibrance, 1 games, 2 settings, 3 account
            Text = label,
            Location = new Point(0, 16 + index * 48),
            Size = new Size(NavW, 46)
        };

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

        private void AddDivider(Point loc, Size size, AnchorStyles anchor)
        {
            var d = new Panel { Location = loc, Size = size, BackColor = Theme.Border, Anchor = anchor };
            Controls.Add(d);
            d.BringToFront();
        }

        private void SetWindowOpacity(int percent) => Opacity = Math.Clamp(percent, 50, 100) / 100.0;

        private void ShowVibrance()
        {
            Select(_navVibrance, _vibrancePage);
            _vibrancePage.Refresh(_engine.CurrentLevel);
        }

        private void ShowGames()
        {
            var page = new GamesHubPage(OnConfigureGame);
            AttachField(page);
            Select(_navGames, page);
        }

        private void OnConfigureGame(DetectedGame game)
        {
            var page = new RustSettingsPage(game, onBack: ShowGames);
            AttachField(page);
            SetContent(page);
            SetActive(_navGames);
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
            var old = _currentPage;
            _contentHost.SuspendLayout();
            _contentHost.Controls.Clear();
            page.Dock = DockStyle.Fill;
            _contentHost.Controls.Add(page);
            _contentHost.ResumeLayout();
            _currentPage = page;

            // Dispose transient pages (Games/Rust are rebuilt each visit); keep persistent ones.
            if (old != null && old != page &&
                old != _vibrancePage && old != _settingsPage && old != _accountPage)
                old.Dispose();
        }

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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                _store.Save(_settings);
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer?.Dispose();
            base.Dispose(disposing);
        }
    }
}
