using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VibranceHud.Games;
using VibranceHud.License;
using VibranceHud.Pages;

namespace VibranceHud
{
    /// <summary>Which resize border a point falls in. See <see cref="MainWindow.HitTestBorder"/>.</summary>
    public enum BorderHit
    {
        None, Left, Right, Top, Bottom,
        TopLeft, TopRight, BottomLeft, BottomRight
    }

    public sealed class MainWindow : Form
    {
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private const int WM_NCHITTEST = 0x0084;
        private const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13,
                          HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

        /// <summary>How wide the invisible resize border is, in logical pixels.</summary>
        private const int ResizeGrip = 6;

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
        private readonly CrosshairPage _crosshairPage;
        private readonly Crosshair.CrosshairService _crosshair;
        private readonly NavButton _navVibrance, _navCrosshair, _navSettings, _navAccount, _navMonitor, _navPanel;
        private readonly MonitorHardwarePage _panelPage;
        private readonly MonitorPage _monitorPage;
        private readonly Games.GameSelection _selection;
        private readonly GameChooser _gameChooser;
        private readonly Audio.AudioEdgeService? _audio;
        private readonly ProfileEngineCoordinator? _profileCoordinator;
        private readonly LicenseService _license;

        public MainWindow(VibranceEngine engine, AppSettings settings, SettingsStore store,
            Audio.AudioEdgeService? audio,
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
            _audio = audio;
            _onThemeChanged = onThemeChanged;

            FormBorderStyle = FormBorderStyle.None;
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
            KeyPreview = true;

            // Come back where we were left. Validated against the monitors that exist right
            // now, so unplugging the screen the window was on brings it home rather than
            // leaving it somewhere the user cannot reach.
            var restored = WindowBounds.ClampToVisible(
                new Rectangle(settings.WindowX, settings.WindowY,
                              settings.WindowWidth, settings.WindowHeight),
                Screen.AllScreens.Select(s => s.WorkingArea));

            if (restored.IsEmpty)
            {
                StartPosition = FormStartPosition.CenterScreen;
            }
            else
            {
                StartPosition = FormStartPosition.Manual;
                Bounds = restored;
            }

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
            close.Click += (s, e) => { SaveWindowBounds(); Hide(); };
            var min = TitleGlyph("─", ClientSize.Width - Design.Tokens.Scale(78));
            min.Click += (s, e) => WindowState = FormWindowState.Minimized;
            _titleBar.Controls.AddRange(new Control[] { logo, close, min });

            // No maximize, by choice. It was added with the resize work and taken back out:
            // this is a utility window that sits beside a game, not something anybody wants
            // filling their screen, and a full-screen PlexusX is just a lot of empty card.
            // Dragging still resizes, and drag-to-edge snapping still works for free - that
            // goes through WM_NCLBUTTONDOWN/HTCAPTION, which Windows treats as a real title
            // bar regardless of whether we offer a maximize button.
            Controls.Add(_titleBar);

            _vibrancePage = new VibrancePage(_engine, _settings, _store);
            _vibrancePage.HotkeyChanged += (mask, vk) => onHotkeyChanged?.Invoke(mask, vk) ?? true;
            _vibrancePage.MainHotkeyChanged += (mask, vk, en) => onMainHotkeyChanged?.Invoke(mask, vk, en);
            _settingsPage = new SettingsPage(_settings, _store, SetWindowOpacity, _onThemeChanged,
                custom, onBackgroundChanged: RefreshBackdrop, engine: _engine, audio: _audio,
                onShowLegal: ShowLegal);
            _accountPage = new AccountPage(_license);
            _accountPage.LicenseChanged += (_, _) => ApplyLicenseVisibility();
            _crosshairPage = new CrosshairPage(_settings, _store, _crosshair);
            _monitorPage = new MonitorPage(_settings, _store, _selection);
            // Built empty and filled in when the probe answers. Probing is three DDC/CI reads
            // per panel at up to hundreds of milliseconds each, and doing it here would block
            // the window opening - and block it again on every theme change, which rebuilds
            // this whole window.
            _panelPage = new MonitorHardwarePage(_settings, _store);
            foreach (var page in new GlowPage[] { _vibrancePage, _settingsPage, _accountPage, _crosshairPage, _monitorPage, _panelPage })
                AttachField(page);

            _nav = new GlowPanel { Field = _field, Scrim = 0, Location = new Point(0, titleH), Size = new Size(navW, ClientSize.Height - titleH), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom };
            // Every row gets its own glyph. Crosshair used to share the vibrance circle and
            // Profile Editor used to share Settings' sliders, so three of the seven rows
            // were only told apart by their label.
            _navVibrance = MakeNav("Display", iconKind: 0);
            // Next to Display because they are the same subject seen at different layers:
            // Display is what colour the picture is, Resolution is what shape it is, and the
            // Monitor tab below is the panel those two land on.
            _navMonitor = MakeNav("Resolution", iconKind: 7);
            // The third layer of the stack: Display is the signal, Resolution is the mode,
            // Monitor is the glass they land on.
            // Icon 9, not 7: Resolution above it already uses the plain screen, and two
            // adjacent rows with the same glyph are told apart only by their label.
            _navPanel = MakeNav("Monitor", iconKind: 9);
            _navCrosshair = MakeNav("Crosshair", iconKind: 5);
            _navSettings = MakeNav("Settings", iconKind: 6);
            _navAccount = MakeNav("Account", iconKind: 3);

            // Until the license is valid, only the Account tab is reachable. The
            // other tabs are still constructed (so the user sees the full nav once
            // they activate) but their visibility is hidden. Called AFTER all _nav*
            // buttons exist - visibility=null would NRE.
            ApplyLicenseVisibility();
            _navVibrance.Click += (s, e) => ShowVibrance();
            _navMonitor.Click += (s, e) => Select(_navMonitor, _monitorPage);
            _navPanel.Click += (s, e) => Select(_navPanel, _panelPage);
            _navCrosshair.Click += (s, e) => Select(_navCrosshair, _crosshairPage);
            _navSettings.Click += (s, e) => Select(_navSettings, _settingsPage);
            _navAccount.Click += (s, e) => Select(_navAccount, _accountPage);
            _nav.Controls.AddRange(new Control[] { _navVibrance, _navMonitor, _navPanel, _navCrosshair, _navSettings, _navAccount });

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

            // Nothing in the shell is scoped to the selected game any more - the pages that
            // were (Game, Keybinds) are gone, and Monitor subscribes to the selection itself.
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

            // Last, so the restore does not fight the layout above it.
            ClearStaleMaximizedState();

            // The window normally hides rather than closes, so both paths have to record
            // the placement or closing via the tray would silently discard it.
            FormClosing += (s, e) => SaveWindowBounds();

            ShowVibrance();
        }

        /// <summary>
        /// Remember where the window is.
        ///
        /// Reads RestoreBounds rather than Bounds when maximized, so what gets stored is the
        /// size the window had BEFORE being maximized. Saving the maximized rectangle as the
        /// normal size gives you a screen-filling "restored" window that can never be made
        /// small again.
        /// </summary>
        private void SaveWindowBounds()
        {
            if (IsDisposed) return;

            var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            if (b.Width <= 0 || b.Height <= 0) return;

            _settings.WindowX = b.X;
            _settings.WindowY = b.Y;
            _settings.WindowWidth = b.Width;
            _settings.WindowHeight = b.Height;
            _settings.WindowMaximized = WindowState == FormWindowState.Maximized;

            try { _store.Save(_settings); }
            catch { /* a locked settings file must never break closing the window */ }
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
            LayoutNav();
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

            // Belt and braces alongside stopping the timer in WM_ENTERSIZEMOVE: a tick can
            // already be queued when the drag starts.
            if (_userInteracting || _movingOrSizing) { _last = DateTime.UtcNow; return; }

            // Freeze the backdrop while any mouse button is held.
            //
            // A frame repaints every transparent control on the page - seventy-eight of them
            // on Display - and measured at 27ms against a 33ms budget on a fast machine. That
            // leaves almost nothing for the control actually being dragged, which is why
            // sliders felt like they were lagging behind the cursor.
            //
            // Control.MouseButtons rather than wiring each control's drag events: it covers
            // the sliders, the colour wheel, the scrollbar and anything added later, with no
            // plumbing to forget. The plexus is a slow background - nobody notices it holding
            // still for the half second somebody is dragging a slider, and everybody notices
            // the slider not keeping up.
            if (Control.MouseButtons != MouseButtons.None) { _last = DateTime.UtcNow; return; }

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

        /// <summary>The nav rows in the order they appear. One list, so the order is stated
        /// once instead of being implied by a position argument at each construction site.</summary>
        private NavButton[] NavOrder => new[]
        {
            _navVibrance, _navMonitor, _navPanel, _navCrosshair, _navSettings, _navAccount
        };

        private NavButton MakeNav(string label, int iconKind) => new()
        {
            IconKind = iconKind,
            Text = label,
            Size = new Size(Design.Tokens.Scale(NavW), Design.Tokens.Scale(46))
        };

        /// <summary>
        /// Stack the visible rows with no holes.
        ///
        /// Rows used to carry a fixed position each, computed as 16 + index * 48. Keybinds
        /// hides itself whenever no game is selected - which is the default - so that left a
        /// 48px gap sitting between Game and FPS Tweaks on most launches, reading as a
        /// rendering fault rather than as a hidden tab. Positions are now assigned from
        /// whatever is actually visible.
        /// </summary>
        private void LayoutNav()
        {
            int y = Design.Tokens.Scale(16);
            int step = Design.Tokens.Scale(48);
            int width = Design.Tokens.Scale(NavW);
            int height = Design.Tokens.Scale(46);

            foreach (var button in NavOrder)
            {
                if (!button.Visible) continue;
                button.SetBounds(0, y, width, height);
                y += step;
            }
        }

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
            _navMonitor.Visible = has;
            _navPanel.Visible = has;
            _navCrosshair.Visible = has;
            _navSettings.Visible = has;
            _navAccount.Visible = true;
            LayoutNav();
        }

        private void ShowVibrance()
        {
            Select(_navVibrance, _vibrancePage);
            _vibrancePage.Refresh();
        }

        /// <summary>
        /// The legal documents, opened from Settings.
        ///
        /// Shown without a nav button of its own, so Settings stays the active row and Back
        /// returns there. Built fresh each time and disposed by SetContent, because it is
        /// opened rarely and holding it costs more than rebuilding it.
        /// </summary>
        private void ShowLegal()
        {
            var page = new LegalPage(onBack: () => Select(_navSettings, _settingsPage));
            AttachField(page);
            SetContent(page);
        }

        private void Select(NavButton button, Control page)
        {
            SetActive(button);
            SetContent(page);
        }

        private void SetActive(NavButton active)
        {
            foreach (var b in new[] { _navVibrance, _navMonitor, _navPanel, _navCrosshair, _navSettings, _navAccount })
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
                old != _crosshairPage && old != _monitorPage && old != _panelPage)
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
        private const int WM_ENTERSIZEMOVE = 0x0231;
        private const int WM_EXITSIZEMOVE = 0x0232;

        /// <summary>True while the user is dragging or resizing the window.</summary>
        private bool _movingOrSizing;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == SingleInstance.ShowWindowMessage && SingleInstance.ShowWindowMessage != 0)
                ShowAndFocus();

            // Stop animating while the window is being dragged or resized.
            //
            // The window is translucent by default, which makes it a LAYERED window, and
            // Windows moves those far more expensively than an opaque one - every frame is
            // composited rather than blitted. Running the particle field on top of that, which
            // invalidates the title bar, the nav and the whole current page on every tick,
            // is what made dragging feel like it was running at twenty frames a second.
            //
            // The field is decoration. Freezing it for the second somebody spends moving the
            // window costs nothing and gives that whole frame budget back to the move itself.
            if (m.Msg == WM_ENTERSIZEMOVE)
            {
                _movingOrSizing = true;
                _timer.Stop();
            }
            else if (m.Msg == WM_EXITSIZEMOVE)
            {
                _movingOrSizing = false;
                _last = DateTime.UtcNow;   // don't advance the field by the whole drag
                _timer.Start();
            }

            // Report a resize border to Windows. A borderless form gets none for free, which
            // is why this window could never be resized despite every page carrying Anchor
            // flags and MinimumSize being set. Suppressed while maximized, where dragging an
            // edge is meaningless.
            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                // LParam packs the screen position as two SIGNED 16-bit values. Masking with
                // 0xFFFF loses the sign, so a window dragged onto a monitor at a negative x
                // (any display left of the primary) would hit-test against nonsense.
                int lp = m.LParam.ToInt32();
                var screen = new Point(unchecked((short)(lp & 0xFFFF)),
                                       unchecked((short)((lp >> 16) & 0xFFFF)));

                int hit = HitTestBorder(PointToClient(screen), ClientSize,
                    Design.Tokens.Scale(ResizeGrip)) switch
                {
                    BorderHit.Left => HTLEFT,
                    BorderHit.Right => HTRIGHT,
                    BorderHit.Top => HTTOP,
                    BorderHit.Bottom => HTBOTTOM,
                    BorderHit.TopLeft => HTTOPLEFT,
                    BorderHit.TopRight => HTTOPRIGHT,
                    BorderHit.BottomLeft => HTBOTTOMLEFT,
                    BorderHit.BottomRight => HTBOTTOMRIGHT,
                    _ => 0,
                };

                if (hit != 0) { m.Result = (IntPtr)hit; return; }
            }

            base.WndProc(ref m);
        }

        /// <summary>
        /// Which resize border a client point falls in, if any.
        ///
        /// Pure geometry, so the corner-beats-edge precedence is testable without a window.
        /// </summary>
        public static BorderHit HitTestBorder(Point p, Size size, int grip)
        {
            if (grip <= 0) return BorderHit.None;

            bool left = p.X <= grip, right = p.X >= size.Width - grip;
            bool top = p.Y <= grip, bottom = p.Y >= size.Height - grip;

            if (top && left) return BorderHit.TopLeft;
            if (top && right) return BorderHit.TopRight;
            if (bottom && left) return BorderHit.BottomLeft;
            if (bottom && right) return BorderHit.BottomRight;
            if (left) return BorderHit.Left;
            if (right) return BorderHit.Right;
            if (top) return BorderHit.Top;
            if (bottom) return BorderHit.Bottom;
            return BorderHit.None;
        }

        /// <summary>Next nav row, wrapping at both ends. Pure so the wrap-around is testable.</summary>
        public static int NextNavIndex(int current, int count, bool forward)
        {
            if (count <= 0) return 0;
            return ((current + (forward ? 1 : -1)) % count + count) % count;
        }

        /// <summary>
        /// Shell-level shortcuts.
        ///
        /// ProcessCmdKey rather than KeyDown: these have to win over whatever child control
        /// has focus, and Ctrl+Tab in particular never reaches the normal key path.
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Escape:
                    // Hides rather than exits, matching the close glyph. The app lives in the
                    // tray, so Escape closing it outright would be a surprise.
                    SaveWindowBounds();
                    Hide();
                    return true;

                case Keys.Control | Keys.Tab:
                    StepNav(forward: true);
                    return true;

                case Keys.Control | Keys.Shift | Keys.Tab:
                    StepNav(forward: false);
                    return true;
            }

            // Ctrl+1..9 jumps straight to a tab, the same as every browser and chat app.
            // Counted over the VISIBLE rows, so the numbering matches what is on screen
            // rather than including tabs that are hidden.
            if ((keyData & Keys.Control) == Keys.Control)
            {
                int digit = (keyData & Keys.KeyCode) - Keys.D1;
                if (digit >= 0 && digit < 9)
                {
                    var visible = VisibleNavButtons();
                    if (digit < visible.Count) { visible[digit].PerformClick(); return true; }
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private List<NavButton> VisibleNavButtons() =>
            NavOrder.Where(b => b.Visible).ToList();

        private void StepNav(bool forward)
        {
            var visible = VisibleNavButtons();
            if (visible.Count == 0) return;

            int current = visible.FindIndex(b => b.Active);
            if (current < 0) current = 0;

            visible[NextNavIndex(current, visible.Count, forward)].PerformClick();
        }

        /// <summary>
        /// Never leave a maximized window behind.
        ///
        /// Maximize was removed, but a settings file written while it existed can still say
        /// the window was closed maximized - and with no button to undo it, that user would
        /// open a full-screen PlexusX with no obvious way back. Read once and cleared.
        /// </summary>
        private void ClearStaleMaximizedState()
        {
            if (!_settings.WindowMaximized) return;

            _settings.WindowMaximized = false;
            WindowState = FormWindowState.Normal;
            try { _store.Save(_settings); } catch { /* not worth failing startup over */ }
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
