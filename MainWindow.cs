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
        private readonly NavButton _navVibrance, _navGames, _navFps, _navCrosshair, _navSettings, _navEditor, _navAccount, _navMonitor, _navKeybinds;
        private readonly MonitorPage _monitorPage;
        private readonly KeybindsPage _keybindsPage;
        private readonly Games.GameSelection _selection;
        private readonly GameChooser _gameChooser;
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
            Func<uint, uint, bool>? onHotkeyChanged = null,
            Action<uint, uint, bool>? onMainHotkeyChanged = null,
            Games.GameSelection? selection = null)
        {
            _crosshair = crosshair ?? new Crosshair.CrosshairService();
            _profileCoordinator = profileCoordinator;
            _license = license ?? new LicenseService();
            _engine = engine;
            _settings = settings;
            _store = store;
            // Owned by the tray so it survives the window rebuild a theme change causes;
            // constructed here only when nobody handed one in (tests, standalone use).
            _selection = selection ?? new Games.GameSelection(settings, store);
            _tweaks = tweaks;
            _audio = audio;
            _onThemeChanged = onThemeChanged;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "PlexusX";
            Icon = AppIcon.Value;
            BackColor = Theme.Background;

            // Everything below is laid out in logical pixels and scaled from here, so the
            // same numbers are correct at 100% and 200%.
            Design.Tokens.Dpi = DeviceDpi;

            ClientSize = new Size(Design.Tokens.Scale(1040), Design.Tokens.Scale(680));
            MinimumSize = new Size(Design.Tokens.Scale(900), Design.Tokens.Scale(600));
            Opacity = Math.Clamp(settings.OpacityPercent, 50, 100) / 100.0;
            Font = Design.Fonts.Label;
            DoubleBuffered = true;

            _field.Resize(ClientSize.Width, ClientSize.Height);
            Theming.AppBackground.Resize(ClientSize.Width, ClientSize.Height);

            int titleH = Design.Tokens.Scale(TitleH);
            int navW = Design.Tokens.Scale(NavW);

            _titleBar = new GlowPanel { Field = _field, Scrim = 110, Location = new Point(0, 0), Size = new Size(ClientSize.Width, titleH), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _titleBar.MouseDown += DragWindow;

            int logoH = Design.Tokens.Scale(24);
            var logo = new LogoBox
            {
                Image = BrandAssets.HorizontalLogo(Theme.IsLight),
                Location = new Point(Design.Tokens.Scale(20), (titleH - logoH) / 2),
                Size = new Size(Design.Tokens.Scale(180), logoH)
            };
            logo.MouseDown += DragWindow;
            var close = TitleGlyph("✕", ClientSize.Width - Design.Tokens.Scale(42));
            close.Click += (s, e) => Hide();
            var min = TitleGlyph("─", ClientSize.Width - Design.Tokens.Scale(78));
            min.Click += (s, e) => WindowState = FormWindowState.Minimized;
            _titleBar.Controls.AddRange(new Control[] { logo, close, min });
            Controls.Add(_titleBar);

            _vibrancePage = new VibrancePage(_engine, _settings, _store);
            _vibrancePage.HotkeyChanged += (mask, vk) => onHotkeyChanged?.Invoke(mask, vk) ?? true;
            _vibrancePage.MainHotkeyChanged += (mask, vk, en) => onMainHotkeyChanged?.Invoke(mask, vk, en);
            _settingsPage = new SettingsPage(_settings, _store, SetWindowOpacity, _onThemeChanged,
                custom, onBackgroundChanged: RefreshBackdrop, engine: _engine);
            _accountPage = new AccountPage(_license);
            _accountPage.LicenseChanged += (_, _) => ApplyLicenseVisibility();
            _crosshairPage = new CrosshairPage(_settings, _store, _crosshair);
            _fpsPage = new FpsTweaksPage(_tweaks);
            _monitorPage = new MonitorPage(_settings, _store, _selection);
            _keybindsPage = new KeybindsPage(_settings, _store, _selection);
            _profileEditorPage = new ProfileEditorPage();
            _profileEditorPage.SetSelectedGame(_selection.Current?.Id, _selection.Current?.DisplayName);
            _profileEditorPage.SetStatus(_profileCoordinator?.IsRunning ?? false);
            _profileEditorPage.OnSaved += (_, _) => Select(_navVibrance, _vibrancePage);
            _profileEditorPage.OnCancelled += (_, _) => Select(_navVibrance, _vibrancePage);
            foreach (var page in new GlowPage[] { _vibrancePage, _settingsPage, _accountPage, _fpsPage, _crosshairPage, _profileEditorPage, _monitorPage, _keybindsPage })
                AttachField(page);

            _nav = new GlowPanel { Field = _field, Scrim = 0, Location = new Point(0, titleH), Size = new Size(navW, ClientSize.Height - titleH), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom };
            // Every row gets its own glyph. Crosshair used to share the vibrance circle and
            // Profile Editor used to share Settings' sliders, so three of the seven rows
            // were only told apart by their label.
            _navVibrance = MakeNav("Display", position: 0, iconKind: 0);
            // Monitor sits next to Display because they are the same subject: Display is what
            // colour the picture is, Monitor is what shape it is. Resolution used to be a card
            // inside Rust's page, which meant a CS2 player could not reach it at all.
            _navMonitor = MakeNav("Monitor", position: 1, iconKind: 7);
            _navCrosshair = MakeNav("Crosshair", position: 2, iconKind: 5);
            // Singular. The app is pointed at one game now - chosen in the nav below - so
            // this tab is that game, not a catalogue to browse.
            _navGames = MakeNav("Game", position: 3, iconKind: 1);
            // Directly under Game because it only exists for a game, and it is hidden
            // entirely at Desktop - a bind has no meaning without one.
            _navKeybinds = MakeNav("Keybinds", position: 4, iconKind: 8);
            _navFps = MakeNav("FPS Tweaks", position: 5, iconKind: 4);
            _navEditor = MakeNav("Profile Editor", position: 6, iconKind: 2);
            _navSettings = MakeNav("Settings", position: 7, iconKind: 6);
            _navAccount = MakeNav("Account", position: 8, iconKind: 3);

            // Until the license is valid, only the Account tab is reachable. The
            // other tabs are still constructed (so the user sees the full nav once
            // they activate) but their visibility is hidden. Called AFTER all _nav*
            // buttons exist - visibility=null would NRE.
            ApplyLicenseVisibility();
            _navVibrance.Click += (s, e) => ShowVibrance();
            _navMonitor.Click += (s, e) => Select(_navMonitor, _monitorPage);
            _navKeybinds.Click += (s, e) => Select(_navKeybinds, _keybindsPage);
            _navGames.Click += (s, e) => ShowGames();
            _navFps.Click += (s, e) => Select(_navFps, _fpsPage);
            _navCrosshair.Click += (s, e) => Select(_navCrosshair, _crosshairPage);
            _navSettings.Click += (s, e) => Select(_navSettings, _settingsPage);
            _navEditor.Click += (s, e) => ShowProfileEditor();
            _navAccount.Click += (s, e) => Select(_navAccount, _accountPage);
            _nav.Controls.AddRange(new Control[] { _navVibrance, _navMonitor, _navGames, _navKeybinds, _navFps, _navCrosshair, _navSettings, _navEditor, _navAccount });

            // The game chooser sits directly above the version, anchored to the bottom of the
            // nav. Above rather than beside: 210px of nav is not enough for a readable game
            // name and a version string side by side.
            _gameChooser = new GameChooser(_selection)
            {
                Location = new Point(Design.Tokens.Scale(14), _nav.Height - Design.Tokens.Scale(88)),
                Size = new Size(navW - Design.Tokens.Scale(28), Design.Tokens.Scale(46)),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            };
            _nav.Controls.Add(_gameChooser);

            var version = new Label
            {
                Text = AppInfo.VersionText,
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = Design.Fonts.Caption,
                Location = new Point(Design.Tokens.Scale(22), _nav.Height - Design.Tokens.Scale(30)),
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            version.MouseDown += DragWindow;
            _nav.Controls.Add(version);

            // Everything that is scoped to a game rebuilds when the selection changes.
            _selection.Changed += (_, _) => OnGameChanged();
            Controls.Add(_nav);

            _contentHost = new Panel
            {
                Location = new Point(navW, titleH),
                Size = new Size(ClientSize.Width - navW, ClientSize.Height - titleH),
                BackColor = Theme.Background,
                AutoScroll = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            Controls.Add(_contentHost);

            AddDivider(new Point(0, titleH), new Size(ClientSize.Width, Design.Tokens.Scale(1)), AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            AddDivider(new Point(navW, titleH), new Size(Design.Tokens.Scale(1), ClientSize.Height - titleH), AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom);

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

        /// <summary>
        /// Follow the monitor the window is on.
        ///
        /// With PerMonitorV2 Windows raises this when the window crosses onto a display with
        /// a different scale factor, and hands over the rectangle it wants us to occupy.
        /// Taking that rectangle verbatim is what keeps the window the same physical size
        /// across the boundary; computing our own makes it jump.
        ///
        /// Order matters: tokens first, then fonts (which are sized from them), then layout.
        /// </summary>
        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);

            Design.Tokens.Dpi = e.DeviceDpiNew;
            Design.Fonts.Rebuild();
            RelayoutForDpi();
        }

        /// <summary>Re-place the chrome at the new scale. Children carry Anchor flags, so
        /// they follow from these three.</summary>
        private void RelayoutForDpi()
        {
            if (IsDisposed || !IsHandleCreated) return;

            int titleH = Design.Tokens.Scale(TitleH);
            int navW = Design.Tokens.Scale(NavW);

            SuspendLayout();

            MinimumSize = new Size(Design.Tokens.Scale(900), Design.Tokens.Scale(600));

            _titleBar.Height = titleH;
            _nav.Location = new Point(0, titleH);
            _nav.Size = new Size(navW, ClientSize.Height - titleH);
            _contentHost.Location = new Point(navW, titleH);
            _contentHost.Size = new Size(ClientSize.Width - navW, ClientSize.Height - titleH);

            if (_currentPage is GlowPage page) AttachField(page);

            ResumeLayout(true);
            Invalidate(true);
        }

        private void AttachField(GlowPage page)
        {
            page.Field = _field;
            // Scaled: the offset has to match where the content host actually sits, or the
            // particle field visibly jumps as it crosses from the nav into the page.
            page.FieldOffset = new Point(Design.Tokens.Scale(NavW), Design.Tokens.Scale(TitleH));
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

            var elapsed = Math.Min((DateTime.UtcNow - _last).TotalSeconds, 0.1);

            // The page's fade-in runs even while the user is holding a slider or the window
            // is otherwise "interacting" - it is a one-shot transition, and freezing it
            // half-faded would look like the app had hung.
            if (_currentPage is GlowPage fading) fading.TickIntro(elapsed);

            if (_userInteracting) { _last = DateTime.UtcNow; return; }

            var now = DateTime.UtcNow;
            _field.Update(elapsed);
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

        /// <summary>
        /// Per-scrollable-panel "user stopped scrolling" timers, kept so they can be torn down
        /// with the window.
        ///
        /// These used to be local variables that only disposed themselves from inside their own
        /// Tick. A window closed mid-scroll therefore left a live timer holding a closure over
        /// this form, and RebuildWindow (theme switch) disposes the old MainWindow and builds a
        /// new one - so every theme change leaked another set, each still ticking against a
        /// disposed form.
        /// </summary>
        private readonly List<System.Windows.Forms.Timer> _scrollIdleTimers = new();

        private void HookScrollEvents(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (c is Panel p && p.AutoScroll)
                {
                    var t = new System.Windows.Forms.Timer { Interval = 120 };
                    _scrollIdleTimers.Add(t);
                    t.Tick += (s2, e2) =>
                    {
                        t.Stop();
                        // Don't touch the form once it's going away - Dispose tears these
                        // down, but a tick can already be queued on the message loop.
                        if (IsDisposed || Disposing) return;
                        _userInteracting = false;
                    };
                    // One handler, not two: this used to subscribe to Scroll twice per panel,
                    // so a rebuild multiplied the handlers as well as the timers.
                    p.Scroll += (s, e) =>
                    {
                        BeginInteraction();
                        t.Stop();
                        t.Start();
                    };
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
            Location = new Point(0, Design.Tokens.Scale(16 + position * 48)),
            Size = new Size(Design.Tokens.Scale(NavW), Design.Tokens.Scale(46))
        };

        private Label TitleGlyph(string text, int x) => new()
        {
            Text = text,
            ForeColor = Theme.TextDim,
            Font = Design.Fonts.Body,
            Size = new Size(Design.Tokens.Scale(32), Design.Tokens.Scale(TitleH)),
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
            _navMonitor.Visible = has;
            _navFps.Visible = has;
            _navCrosshair.Visible = has;
            _navSettings.Visible = has;
            _navEditor.Visible = has;
            _navAccount.Visible = true;
            ApplyGameScopedVisibility();
        }

        /// <summary>
        /// Hide the tabs that only mean something with a game selected.
        ///
        /// Keybinds is the whole tab: the commands, their syntax and the file they land in are
        /// all per-game, so at Desktop there is nothing it could show. Hiding it beats showing
        /// an empty page that explains why it is empty.
        /// </summary>
        private void ApplyGameScopedVisibility()
        {
            bool licensed = _license.HasValidLicense;
            _navKeybinds.Visible = licensed && _selection.Current != null;

            // If the user was on Keybinds and just went back to Desktop, they are now looking
            // at a page whose tab has gone. Move them somewhere that still exists.
            if (!_navKeybinds.Visible && ReferenceEquals(_currentPage, _keybindsPage))
                ShowVibrance();
        }

        private void ShowVibrance()
        {
            Select(_navVibrance, _vibrancePage);
            _vibrancePage.Refresh();
        }

        /// <summary>
        /// The Game tab. With a game selected this IS that game's page - no grid, no
        /// click-through. At Desktop it falls back to the catalogue, which is now an empty
        /// state ("pick one") rather than a permanent hub.
        /// </summary>
        private void ShowGames()
        {
            // Catch a game installed or uninstalled since the window opened. Cheap, and it
            // means the tab is never pointed at something that has gone.
            _selection.Refresh();

            GlowPage page = _selection.Detected is { } detected
                ? BuildGamePage(detected)
                : new GamesHubPage(d => _selection.Select(d.Game.Id), OnEditProfile);

            AttachField(page);
            Select(_navGames, page);
        }

        /// <summary>Rebuild whatever is scoped to the selected game. Only the Game tab and the
        /// Profile Editor care - Display, Crosshair, FPS Tweaks and Settings are global and
        /// deliberately untouched.</summary>
        private void OnGameChanged()
        {
            ApplyGameScopedVisibility();
            _profileEditorPage.SetSelectedGame(_selection.Current?.Id, _selection.Current?.DisplayName);

            // Only rebuild the Game tab if it is what the user is looking at; otherwise it
            // rebuilds itself next time they open it.
            if (ReferenceEquals(_currentPage, _profileEditorPage))
                _profileEditorPage.BeginIntro();
            else if (_navGames.Active)
                ShowGames();
        }

        private GlowPage BuildGamePage(DetectedGame game) => GamePageRouter.Resolve(game.Game.Id) switch
        {
            // No back link any more - the chooser is how you change game, and there is
            // nothing behind this page to go back to.
            GamePageKind.Rust => new RustSettingsPage(game, _settings, _store, _audio, onBack: ShowGames),
            GamePageKind.Cs2 => new Cs2SettingsPage(game, onBack: ShowGames),
            GamePageKind.Apex => new ApexSettingsPage(game, onBack: ShowGames),
            GamePageKind.Fortnite => new FortniteSettingsPage(game, onBack: ShowGames),
            _ => new UnsupportedGamePage(game.Game, onBack: ShowGames),
        };

        /// <summary>"Edit profile" on a game card: point the app at that game, then open the
        /// editor on it. Selecting rather than just opening means the rest of the app follows
        /// too, which is the whole point of having one selection.</summary>
        private void OnEditProfile(SupportedGame game)
        {
            _selection.Select(game.Id);
            Select(_navEditor, _profileEditorPage);
        }

        private void ShowProfileEditor()
        {
            _profileEditorPage.SetStatus(_profileCoordinator?.IsRunning ?? false);
            Select(_navEditor, _profileEditorPage);
        }

        private void Select(NavButton button, Control page)
        {
            SetActive(button);
            SetContent(page);
        }

        private void SetActive(NavButton active)
        {
            foreach (var b in new[] { _navVibrance, _navMonitor, _navGames, _navKeybinds, _navFps, _navCrosshair, _navSettings, _navEditor, _navAccount })
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

            // Fade the incoming page up from the background so a change of tab - or of game -
            // reads as the app moving somewhere rather than the window flickering.
            if (page is GlowPage arriving) arriving.BeginIntro();

            if (old != null && old != page &&
                old != _vibrancePage && old != _settingsPage && old != _accountPage &&
                old != _fpsPage && old != _crosshairPage && old != _profileEditorPage &&
                old != _monitorPage && old != _keybindsPage)
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

                // Scroll-idle timers outlive their scope otherwise; RebuildWindow disposes
                // this form on every theme change, so without this each switch left another
                // set ticking against a dead window.
                foreach (var t in _scrollIdleTimers)
                {
                    t.Stop();
                    t.Dispose();
                }
                _scrollIdleTimers.Clear();
            }
            base.Dispose(disposing);
        }
    }
}
