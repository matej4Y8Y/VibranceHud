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
        private readonly Crosshair.CrosshairService _crosshair;
        private readonly NavButton _navVibrance, _navGames, _navFps, _navCrosshair, _navSettings, _navAccount, _navProfile;
        private readonly SystemTweaks.SystemTweakService _tweaks;
        private readonly Audio.AudioEdgeService? _audio;
        private readonly ProfileEngineCoordinator? _profileCoordinator;
        private ProfileEditorCard? _profileCard;
        private Panel _profileHost = null!;

        public MainWindow(VibranceEngine engine, AppSettings settings, SettingsStore store,
            SystemTweaks.SystemTweakService tweaks, Audio.AudioEdgeService? audio,
            Action<string> onThemeChanged, Theming.CustomThemeService? custom = null,
            Crosshair.CrosshairService? crosshair = null,
            ProfileEngineCoordinator? profileCoordinator = null)
        {
            _crosshair = crosshair ?? new Crosshair.CrosshairService();
            _profileCoordinator = profileCoordinator;
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

            // ---- Title bar (shares the field) ----
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

            // Pages exist before the nav wires click handlers to them.
            _vibrancePage = new VibrancePage(_engine, _settings, _store);
            _settingsPage = new SettingsPage(_settings, _store, SetWindowOpacity, _onThemeChanged,
                custom, onBackgroundChanged: RefreshBackdrop);
            _accountPage = new AccountPage();
            _crosshairPage = new CrosshairPage(_settings, _store, _crosshair);
            _fpsPage = new FpsTweaksPage(_tweaks);
            foreach (var page in new GlowPage[] { _vibrancePage, _settingsPage, _accountPage, _fpsPage, _crosshairPage })
                AttachField(page);

            // ---- Left nav (shares the field) ----
            // The left nav panel is fully transparent (Scrim=0) so the animated backdrop
            // shows through cleanly. Both GlowPanel and NavButton declare
            // SupportsTransparentBackColor in their SetStyle flags — without that bit,
            // WinForms falls back to white when BackColor=Color.Transparent. The fix is
            // to make every custom-paint panel transparent-aware, not to keep painting
            // a background fill that hides the field.
            _nav = new GlowPanel { Field = _field, Scrim = 0, Location = new Point(0, TitleH), Size = new Size(NavW, ClientSize.Height - TitleH), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom };
            _navVibrance = MakeNav("Vibrance", position: 0, iconKind: 0);
            _navGames = MakeNav("Games", position: 1, iconKind: 1);
            _navFps = MakeNav("FPS Tweaks", position: 2, iconKind: 4);
            _navCrosshair = MakeNav("Crosshair", position: 3, iconKind: 0);
            _navSettings = MakeNav("Settings", position: 4, iconKind: 2);
            _navProfile = MakeNav("Set Profile", position: 5, iconKind: 2);
            _navAccount = MakeNav("Account", position: 6, iconKind: 3);
            _navVibrance.Click += (s, e) => ShowVibrance();
            _navGames.Click += (s, e) => ShowGames();
            _navFps.Click += (s, e) => Select(_navFps, _fpsPage);
            _navCrosshair.Click += (s, e) => Select(_navCrosshair, _crosshairPage);
            _navSettings.Click += (s, e) => Select(_navSettings, _settingsPage);
            _navProfile.Click += (s, e) => ShowProfileEditor();
            _navAccount.Click += (s, e) => Select(_navAccount, _accountPage);
            _nav.Controls.AddRange(new Control[] { _navVibrance, _navGames, _navFps, _navCrosshair, _navSettings, _navProfile, _navAccount });

            // Version pinned faint in the bottom-left corner - makes it feel like a real build.
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

            _contentHost = new Panel { Location = new Point(NavW, TitleH), Size = new Size(ClientSize.Width - NavW, ClientSize.Height - TitleH), BackColor = Theme.Background, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
            Controls.Add(_contentHost);

            // The profile editor slides in over the content host from the right edge.
            // Created once but kept hidden until the nav button is pressed. Sized so the
            // card has a 360-wide panel docked to the right of the content host - large
            // enough for the slider rows + save/cancel buttons without crowding the page.
            _profileHost = new Panel
            {
                BackColor = Color.FromArgb(30, 28, 36),
                Location = new Point(ClientSize.Width, TitleH),
                Size = new Size(360, ClientSize.Height - TitleH),
                Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
                Visible = false
            };
            Controls.Add(_profileHost);

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
            // Open the editor pre-filtered to the picked game; populate the picker
            // with just this game so the user sees the right title and can hit save.
            ShowProfileEditor();
            if (_profileCard != null)
            {
                _profileCard.PopulateGames(new[] { (game.Id, game.DisplayName) });
                _profileCard.SelectGame(game.Id);
            }
        }

        /// <summary>Show the editor card with the 240ms slide-in animation.
        /// Lazily creates the card the first time, populating it with every
        /// supported game (so users can browse profiles for games they
        /// haven't installed too — though those only persist as future-applicable
        /// placeholders).</summary>
        private void ShowProfileEditor()
        {
            if (_profileCard == null)
            {
                _profileCard = new ProfileEditorCard();
                _profileCard.PopulateGames(GetEditorGames());
                _profileCard.SetStatus(_profileCoordinator?.IsRunning ?? false);
                _profileCard.OnSaved += (_, _) => HideProfileEditor();
                _profileCard.OnCancelled += (_, _) => HideProfileEditor();
                _profileCard.Dock = DockStyle.Fill;
                _profileHost.Controls.Add(_profileCard);
                _profileCard.BringToFront();
            }
            else
            {
                // Refreshing each open keeps the status dot honest if the user
                // toggled anything while the editor was hidden.
                _profileCard.PopulateGames(GetEditorGames());
                _profileCard.SetStatus(_profileCoordinator?.IsRunning ?? false);
            }

            _profileHost.Visible = true;
            _profileHost.BringToFront();
            AnimateSlide(_profileHost, inDirection: true);
            SetActive(_navProfile);
        }

        /// <summary>Animate the panel back out and hide it.</summary>
        private void HideProfileEditor()
        {
            AnimateSlide(_profileHost, inDirection: false, onComplete: () =>
            {
                _profileHost.Visible = false;
                SetActive(_navVibrance);
                _vibrancePage.Refresh();
            });
        }

        /// <summary>The list the picker shows. Today: every supported game,
        /// installed or not. The applier no-ops on missing games anyway.</summary>
        private IEnumerable<(string Id, string Name)> GetEditorGames()
        {
            foreach (var g in VibranceHud.Games.SupportedGames.All)
                yield return (g.Id, g.DisplayName);
        }

        /// <summary>240ms ease-out cubic: slide the panel in from off-screen-right
        /// by its full width, fading its background in to opaque at the same time.
        /// On exit, the reverse runs in 180ms ease-in.</summary>
        private void AnimateSlide(Panel panel, bool inDirection, Action? onComplete = null)
        {
            const int width = 360;
            var dur = inDirection ? 240 : 180;
            var startLocation = panel.Location; // remember so we revert cleanly
            var offX = panel.Parent!.ClientSize.Width;          // off-screen-right
            var onX = panel.Parent.ClientSize.Width - width;    // docked left edge
            var startX = inDirection ? offX : onX;
            var endX = inDirection ? onX : offX;
            panel.Location = new Point(startX, panel.Location.Y);
            panel.Visible = true;

            // Opacity-only animation: slide via Location (transform), opacity via the
            // panel's translucent background alpha so the WinForms-rendered content
            // underneath can shine through during the entry.
            var baseBg = inDirection ? Color.FromArgb(0, 28, 28, 36) : Color.FromArgb(30, 28, 36);

            var startedAt = DateTime.UtcNow;
            var totalMs = dur;
            var timer = new System.Windows.Forms.Timer { Interval = 16 };
            timer.Tick += (_, _) =>
            {
                var elapsed = (DateTime.UtcNow - startedAt).TotalMilliseconds;
                var raw = Math.Clamp(elapsed / totalMs, 0, 1);
                // Ease-out cubic when entering, ease-in cubic when leaving.
                var eased = inDirection
                    ? 1 - Math.Pow(1 - raw, 3)
                    : Math.Pow(raw, 3);

                var x = (int)Math.Round(startX + (endX - startX) * eased);
                panel.Location = new Point(x, panel.Location.Y);

                var a = inDirection ? (int)(30 * eased) : (int)(30 * (1 - eased));
                panel.BackColor = Color.FromArgb(Math.Clamp(a, 0, 255), baseBg.R, baseBg.G, baseBg.B);

                if (raw >= 1.0)
                {
                    timer.Stop();
                    timer.Dispose();
                    panel.Location = new Point(endX, panel.Location.Y);
                    if (inDirection) panel.BackColor = Color.FromArgb(30, 28, 36);
                    _ = startLocation; // (suppress unused warning)
                    onComplete?.Invoke();
                }
            };
            timer.Start();
        }

        private void OnConfigureGame(DetectedGame game)
        {
            // Routing itself is a pure, unit-tested lookup (GamePageRouter) - a game id with
            // no explicit page must fail closed to UnsupportedGamePage rather than falling
            // through to some other game's page (Rust's writes directly to client.cfg).
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
            foreach (var b in new[] { _navVibrance, _navGames, _navFps, _navCrosshair, _navSettings, _navAccount })
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
                old != _vibrancePage && old != _settingsPage && old != _accountPage &&
                old != _fpsPage && old != _crosshairPage)
                old.Dispose();
        }

        public void ShowAndFocus()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            BringToFront();
            Activate();
            _vibrancePage.Refresh();
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
        /// <summary>Repaint every panel and page after the background image or its dim
        /// changes - the backdrop is shared, so all of them are stale at once.</summary>
        private void RefreshBackdrop()
        {
            Invalidate(true);
            foreach (Control c in Controls) c.Invalidate(true);
        }

    }
}
