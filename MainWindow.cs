using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VibranceHud.Games;
using VibranceHud.License;
using VibranceHud.Pages;

namespace VibranceHud
{
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
        private readonly Action<string> _onThemeChanged;

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
        private readonly FpsTweaksPage _fpsPage;
        private readonly CrosshairPage _crosshairPage;
        private readonly ProfileEditorPage _profileEditorPage;
        private readonly Crosshair.CrosshairService _crosshair;
        private readonly NavButton _navVibrance, _navGames, _navFps, _navCrosshair, _navSettings, _navEditor, _navAccount;
        private readonly SystemTweaks.SystemTweakService _tweaks;
        private readonly Audio.AudioEdgeService? _audio;
        private readonly ProfileEngineCoordinator? _profileCoordinator;
        private readonly LicenseService _license;

        public MainWindow(VibranceEngine engine, AppSettings settings, SettingsStore store,
            SystemTweaks.SystemTweakService tweaks, Audio.AudioEdgeService? audio,
            Action<string> onThemeChanged, Theming.CustomThemeService? custom = null,
            Crosshair.CrosshairService? crosshair = null,
            ProfileEngineCoordinator? profileCoordinator = null,
            LicenseService? license = null,
            Action<uint, uint>? onHotkeyChanged = null,
            Action<uint, uint, bool>? onMainHotkeyChanged = null)
        {
            _crosshair = crosshair ?? new Crosshair.CrosshairService();
            _profileCoordinator = profileCoordinator;
            _license = license ?? new LicenseService();
            _engine = engine;
            _settings = settings;
            _store = store;
            _tweaks = tweaks;
            _audio = audio;
            _onThemeChanged = onThemeChanged;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "PlexusX";
            Icon = AppIcon.Value;
            BackColor = Theme.Background;
            ClientSize = new Size(1040, 680);
            MinimumSize = new Size(900, 600);
            Opacity = Math.Clamp(settings.OpacityPercent, 50, 100) / 100.0;
            Font = new Font(Theme.FontFamily, 9f);
            DoubleBuffered = true;

            _field.Resize(ClientSize.Width, ClientSize.Height);
            Theming.AppBackground.Resize(ClientSize.Width, ClientSize.Height);

            _titleBar = new GlowPanel { Field = _field, Scrim = 110, Location = new Point(0, 0), Size = new Size(ClientSize.Width, TitleH), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _titleBar.MouseDown += DragWindow;

            var logo = new LogoBox
            {
                Image = BrandAssets.HorizontalLogo(Theme.IsLight),
                Location = new Point(20, (TitleH - 24) / 2),
                Size = new Size(180, 24)
            };
            logo.MouseDown += DragWindow;
            var close = TitleGlyph("✕", ClientSize.Width - 42);
            close.Click += (s, e) => Hide();
            var min = TitleGlyph("─", ClientSize.Width - 78);
            min.Click += (s, e) => WindowState = FormWindowState.Minimized;
            _titleBar.Controls.AddRange(new Control[] { logo, close, min });
            Controls.Add(_titleBar);

            _vibrancePage = new VibrancePage(_engine, _settings, _store);
            _vibrancePage.HotkeyChanged += (mask, vk) => onHotkeyChanged?.Invoke(mask, vk);
            _vibrancePage.MainHotkeyChanged += (mask, vk, en) => onMainHotkeyChanged?.Invoke(mask, vk, en);
            _settingsPage = new SettingsPage(_settings, _store, SetWindowOpacity, _onThemeChanged,
                custom, onBackgroundChanged: RefreshBackdrop);
            _accountPage = new AccountPage(_license);
            _accountPage.LicenseChanged += (_, _) => ApplyLicenseVisibility();
            _crosshairPage = new CrosshairPage(_settings, _store, _crosshair);
            _fpsPage = new FpsTweaksPage(_tweaks);
            _profileEditorPage = new ProfileEditorPage();
            _profileEditorPage.PopulateGames(GetEditorGames());
            _profileEditorPage.SetStatus(_profileCoordinator?.IsRunning ?? false);
            _profileEditorPage.OnSaved += (_, _) => Select(_navVibrance, _vibrancePage);
            _profileEditorPage.OnCancelled += (_, _) => Select(_navVibrance, _vibrancePage);
            foreach (var page in new GlowPage[] { _vibrancePage, _settingsPage, _accountPage, _fpsPage, _crosshairPage, _profileEditorPage })
                AttachField(page);

            _nav = new GlowPanel { Field = _field, Scrim = 0, Location = new Point(0, TitleH), Size = new Size(NavW, ClientSize.Height - TitleH), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom };
            _navVibrance = MakeNav("Vibrance", position: 0, iconKind: 0);
            _navGames = MakeNav("Games", position: 1, iconKind: 1);
            _navFps = MakeNav("FPS Tweaks", position: 2, iconKind: 4);
            _navCrosshair = MakeNav("Crosshair", position: 3, iconKind: 0);
            _navSettings = MakeNav("Settings", position: 4, iconKind: 2);
            _navEditor = MakeNav("Profile Editor", position: 5, iconKind: 2);
            _navAccount = MakeNav("Account", position: 6, iconKind: 3);

            // Until the license is valid, only the Account tab is reachable. The
            // other tabs are still constructed (so the user sees the full nav once
            // they activate) but their visibility is hidden. Called AFTER all _nav*
            // buttons exist - visibility=null would NRE.
            ApplyLicenseVisibility();
            _navVibrance.Click += (s, e) => ShowVibrance();
            _navGames.Click += (s, e) => ShowGames();
            _navFps.Click += (s, e) => Select(_navFps, _fpsPage);
            _navCrosshair.Click += (s, e) => Select(_navCrosshair, _crosshairPage);
            _navSettings.Click += (s, e) => Select(_navSettings, _settingsPage);
            _navEditor.Click += (s, e) => ShowProfileEditor();
            _navAccount.Click += (s, e) => Select(_navAccount, _accountPage);
            _nav.Controls.AddRange(new Control[] { _navVibrance, _navGames, _navFps, _navCrosshair, _navSettings, _navEditor, _navAccount });

            var version = new Label
            {
                Text = AppInfo.VersionText,
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8f),
                Location = new Point(22, _nav.Height - 30),
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            version.MouseDown += DragWindow;
            _nav.Controls.Add(version);
            Controls.Add(_nav);

            _contentHost = new Panel
            {
                Location = new Point(NavW, TitleH),
                Size = new Size(ClientSize.Width - NavW, ClientSize.Height - TitleH),
                BackColor = Theme.Background,
                AutoScroll = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            Controls.Add(_contentHost);

            AddDivider(new Point(0, TitleH), new Size(ClientSize.Width, 1), AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            AddDivider(new Point(NavW, TitleH), new Size(1, ClientSize.Height - TitleH), AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom);

            Resize += (s, e) =>
            {
                _field.Resize(ClientSize.Width, ClientSize.Height);
                Theming.AppBackground.Resize(ClientSize.Width, ClientSize.Height);
            };

            _timer = new System.Windows.Forms.Timer { Interval = 33 };
            _timer.Tick += OnAnimationTick;
            _timer.Start();

            HookInteractionPauses();

            ShowVibrance();
        }

        private void AttachField(GlowPage page)
        {
            page.Field = _field;
            page.FieldOffset = new Point(NavW, TitleH);
        }

        private void OnAnimationTick(object? sender, EventArgs e)
        {
            // Belt and braces alongside the timer teardown in Dispose: a tick can
            // already be queued on the message loop when disposal starts, and every
            // line below touches Handle or a child control. Bailing out here keeps a
            // clean exit from surfacing as a crash dialog.
            if (IsDisposed || Disposing || !IsHandleCreated) return;

            var foreground = GetForegroundWindow() == Handle && Visible && WindowState != FormWindowState.Minimized;
            if (!foreground) { _last = DateTime.UtcNow; return; }

            if (_userInteracting) { _last = DateTime.UtcNow; return; }

            var now = DateTime.UtcNow;
            _field.Update(Math.Min((now - _last).TotalSeconds, 0.1));
            _last = now;

            _titleBar.Invalidate(true);
            _nav.Invalidate(true);
            _currentPage?.Invalidate(true);
        }

        private bool _userInteracting;

        private void HookInteractionPauses()
        {
            MouseDown += (s, e) => _userInteracting = true;
            MouseUp   += (s, e) => _userInteracting = false;
            if (Controls.Count > 0)
            {
                HookScrollEvents(this);
            }
        }

        private void HookScrollEvents(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (c is Panel p && p.AutoScroll)
                {
                    p.Scroll += (s, e) => BeginInteraction();
                    var t = new System.Windows.Forms.Timer { Interval = 120 };
                    t.Tick += (s2, e2) => { _userInteracting = false; t.Stop(); t.Dispose(); };
                    p.Scroll += (s, e) => { t.Stop(); t.Start(); };
                }
                if (c.HasChildren) HookScrollEvents(c);
            }
        }

        private void BeginInteraction()
        {
            _userInteracting = true;
        }

        private NavButton MakeNav(string label, int position, int iconKind) => new()
        {
            IconKind = iconKind,
            Text = label,
            Location = new Point(0, 16 + position * 48),
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

        private void DragWindow(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }

        private void RefreshBackdrop()
        {
            Invalidate(true);
            Theming.AppBackground.Resize(ClientSize.Width, ClientSize.Height);
        }

        private void ApplyLicenseVisibility()
        {
            bool has = _license.HasValidLicense;
            _navVibrance.Visible = has;
            _navGames.Visible = has;
            _navFps.Visible = has;
            _navCrosshair.Visible = has;
            _navSettings.Visible = has;
            _navEditor.Visible = has;
            _navAccount.Visible = true;
        }

        private void ShowVibrance()
        {
            Select(_navVibrance, _vibrancePage);
            _vibrancePage.Refresh();
        }

        private void ShowGames()
        {
            var page = new GamesHubPage(OnConfigureGame, OnEditProfile);
            AttachField(page);
            Select(_navGames, page);
        }

        private void OnEditProfile(SupportedGame game)
        {
            _profileEditorPage.PopulateGames(new[] { (game.Id, game.DisplayName) });
            _profileEditorPage.SelectGame(game.Id);
            Select(_navEditor, _profileEditorPage);
        }

        private void ShowProfileEditor()
        {
            _profileEditorPage.SetStatus(_profileCoordinator?.IsRunning ?? false);
            Select(_navEditor, _profileEditorPage);
        }

        private IEnumerable<(string Id, string Name)> GetEditorGames()
        {
            foreach (var g in VibranceHud.Games.SupportedGames.All)
                yield return (g.Id, g.DisplayName);
        }

        private void OnConfigureGame(DetectedGame game)
        {
            GlowPage page = GamePageRouter.Resolve(game.Game.Id) switch
            {
                GamePageKind.Rust => new RustSettingsPage(game, _settings, _store, _audio, onBack: ShowGames),
                GamePageKind.Cs2 => new Cs2SettingsPage(game, onBack: ShowGames),
                GamePageKind.Apex => new ApexSettingsPage(game, onBack: ShowGames),
                GamePageKind.Fortnite => new FortniteSettingsPage(game, onBack: ShowGames),
                _ => new UnsupportedGamePage(game.Game, onBack: ShowGames),
            };
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
            foreach (var b in new[] { _navVibrance, _navGames, _navFps, _navCrosshair, _navSettings, _navEditor, _navAccount })
                b.Active = ReferenceEquals(b, active);
        }

        private void SetContent(Control page)
        {
            var old = _currentPage;
            _contentHost.SuspendLayout();
            _contentHost.Controls.Clear();
            page.Dock = DockStyle.Fill;
            // Reset scroll position for persistent pages - without this, returning
            // to the profile editor after scrolling down makes the GAME section
            // (the first row) invisible. AutoScrollPosition is on ScrollableControl,
            // not Control, so we cast.
            if (page is ScrollableControl scrollable)
                scrollable.AutoScrollPosition = Point.Empty;
            _contentHost.Controls.Add(page);
            _contentHost.ResumeLayout();
            _currentPage = page;

            if (old != null && old != page &&
                old != _vibrancePage && old != _settingsPage && old != _accountPage &&
                old != _fpsPage && old != _crosshairPage && old != _profileEditorPage)
                old.Dispose();
        }

        public void ShowAndFocus()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            BringToFront();
            Activate();
        }

        /// <summary>
        /// Answer the broadcast a second copy of PlexusX sends before exiting: the
        /// user double-clicked the icon while we were already running (usually hidden
        /// in the tray), so surface this window instead of leaving them thinking
        /// nothing happened. See <see cref="SingleInstance"/>.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == SingleInstance.ShowWindowMessage && SingleInstance.ShowWindowMessage != 0)
                ShowAndFocus();

            base.WndProc(ref m);
        }

        public void OnEngineChanged()
        {
            _vibrancePage.Refresh();
        }

        /// <summary>
        /// Stop the animation timer before the rest of the form tears down.
        ///
        /// The timer ticks every 33ms and <see cref="OnAnimationTick"/> reads
        /// <c>Handle</c> and invalidates child controls. Nothing used to stop it, so
        /// the first tick after disposal threw <see cref="ObjectDisposedException"/>
        /// on a WinForms timer callback - which lands in Program.cs's
        /// <c>Application.ThreadException</c> hook and shows the user a
        /// "PlexusX hit an unexpected problem and had to close" dialog plus a crash
        /// log, on what was actually a clean exit. Closing the window is the single
        /// most common thing a user does, so this fired constantly.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Tick -= OnAnimationTick;
                _timer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
