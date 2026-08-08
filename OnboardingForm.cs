using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// First-run onboarding: a cinematic plexus intro (the whole screen fades up), then two
    /// quick setup steps - pick a theme (applied live as a preview) and a couple of options.
    /// Shown once; it writes the choices and sets <see cref="AppSettings.OnboardingComplete"/>.
    /// </summary>
    public sealed class OnboardingForm : Form
    {
        private readonly ParticleField _field = new(60);
        private readonly System.Windows.Forms.Timer _timer;
        private DateTime _last = DateTime.UtcNow;
        private float _reveal;   // 0..1 cinematic fade-in
        private int _step;

        private readonly AppSettings _settings;
        private readonly SettingsStore _store;

        private readonly GlassButton _primary;
        private readonly GlassLink _back;
        private readonly GlassLink _skip;
        private readonly List<SwatchButton> _swatches = new();
        private readonly List<Label> _swatchLabels = new();
        private readonly ToggleSwitch _startup;
        private readonly Label _startupLabel;
        private readonly List<ChipButton> _gameChips = new();
        private string _favoriteGame = "";

        private static readonly Font TitleFont = new(Theme.FontFamily, 20f, FontStyle.Bold);
        private static readonly Font SubFont = new(Theme.FontFamily, 10f);
        private static readonly Font StepFont = new(Theme.FontFamily, 8f, FontStyle.Bold);

        public OnboardingForm(AppSettings settings, SettingsStore store)
        {
            _settings = settings;
            _store = store;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            Text = "Welcome to PlexusX";
            ClientSize = new Size(660, 540);
            BackColor = Theme.Background;
            DoubleBuffered = true;
            Icon = AppIcon.Value;
            Font = new Font(Theme.FontFamily, 9.5f);
            _field.Resize(ClientSize.Width, ClientSize.Height);

            int cx = ClientSize.Width / 2;

            // ---- Theme swatches (step 1) ----
            int n = ThemeCatalog.All.Count;
            int gap = 84, totalW = (n - 1) * gap;
            int sx = cx - totalW / 2;
            foreach (var palette in ThemeCatalog.All)
            {
                var swatch = new SwatchButton(palette)
                {
                    Size = new Size(40, 40),
                    Location = new Point(sx - 20, 250),
                    Active = palette.Name == Theme.CurrentName,
                    Visible = false
                };
                swatch.Click += (s, e) =>
                {
                    Theme.Apply(swatch.Palette.Name);
                    foreach (var b in _swatches) b.Active = ReferenceEquals(b, swatch);
                    RefreshSwatchLabels();
                    // Stock WinForms controls hold whatever colour they were given at
                    // construction - unlike the owner-drawn ones, which read Theme at paint
                    // time. Picking a theme here therefore left them on the previous palette:
                    // the reported symptom was "Start PlexusX when Windows starts" turning
                    // invisible after switching to a dark theme, because the label was still
                    // painting the light theme's dark text on a now-dark background. The user
                    // was left with a toggle and no idea what it did.
                    ReapplyThemeColors();
                    Invalidate(true);
                };
                _swatches.Add(swatch);
                Controls.Add(swatch);

                var label = new Label
                {
                    Text = palette.Name,
                    ForeColor = Theme.TextDim,
                    BackColor = Color.Transparent,
                    Font = new Font(Theme.FontFamily, 8f),
                    Location = new Point(sx - 30, 296),
                    Size = new Size(60, 16),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Visible = false
                };
                _swatchLabels.Add(label);
                Controls.Add(label);
                sx += gap;
            }

            // ---- Setup options (step 2) ----
            _startupLabel = new Label
            {
                Text = "Start PlexusX when Windows starts",
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Location = new Point(cx - 180, 232),
                AutoSize = true,
                Visible = false
            };
            Controls.Add(_startupLabel);
            _startup = new ToggleSwitch
            {
                Location = new Point(cx + 136, 230),
                Checked = StartupManager.IsEnabled(),
                Visible = false
            };
            Controls.Add(_startup);

            string[] games = { "Rust", "CS2", "Both", "Something else" };
            int gw = 130, ggap = 12, gtot = games.Length * gw + (games.Length - 1) * ggap;
            int gsx = cx - gtot / 2;
            foreach (var name in games)
            {
                var chip = new ChipButton
                {
                    Text = name,
                    Font = new Font(Theme.FontFamily, 9f),
                    Size = new Size(gw, 34),
                    Location = new Point(gsx, 316),
                    Visible = false
                };
                chip.Click += (s, e) =>
                {
                    _favoriteGame = name;
                    foreach (var c in _gameChips) c.Active = ReferenceEquals(c, chip);
                };
                _gameChips.Add(chip);
                Controls.Add(chip);
                gsx += gw + ggap;
            }

            // ---- Nav ----
            // The footer sits inside the glass card, which ends 24px short of the window
            // (see OnPaint). The button + skip link used to run past that edge, so the last
            // thing on the first screen anyone ever sees was text hanging off the card.
            _primary = Pages.SettingsPage.PrimaryButton("Get started  ›", cx - 90, 440, 180, height: 40);
            _primary.Click += (s, e) => Advance();
            Controls.Add(_primary);

            // Skip button - explicit "I'll set this up later" path so the user
            // doesn't have to find the X in the corner. Skips write the bare-
            // minimum OnboardingComplete=true and use the current theme, so a
            // power user who just wants the app on screen is one click away from
            // the main window.
            _skip = new GlassLink
            {
                Text = "Skip — I'll set up later",
                // Centred properly. AutoSize anchors the left edge, so the old
                // "cx - 80" left this sitting visibly off-axis from the button above it -
                // a fixed centred box is the only way to line the two up.
                Location = new Point(cx - 150, 492),
                Size = new Size(300, 20),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            _skip.Click += (s, e) =>
            {
                // Mark onboarding done without persisting the unconfirmed toggles
                // (startup stays where StartupManager already reports it; theme
                // stays at the default the field is rendering).
                _settings.OnboardingComplete = true;
                _store.Save(_settings);
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(_skip);

            _back = new GlassLink
            {
                Text = "‹ Back",
                Location = new Point(46, 452),
                Size = new Size(90, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };
            _back.Click += (s, e) => ShowStep(_step - 1);
            Controls.Add(_back);

            WindowDrag.Enable(this, this);

            _timer = new System.Windows.Forms.Timer { Interval = 33 };
            _timer.Tick += OnTick;
            _timer.Start();

            ShowStep(0);
        }

        private void OnTick(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            double dt = Math.Min((now - _last).TotalSeconds, 0.1);
            _last = now;
            _field.Update(dt);
            if (_reveal < 1f) _reveal = Math.Min(1f, _reveal + (float)(dt / 1.1));
            Invalidate(true);
        }

        private void Advance()
        {
            if (_step < 2) ShowStep(_step + 1);
            else Finish();
        }

        private void ShowStep(int step)
        {
            _step = Math.Max(0, step);

            bool theme = _step == 1, setup = _step == 2;
            foreach (var s in _swatches) s.Visible = theme;
            foreach (var l in _swatchLabels) l.Visible = theme;
            _startup.Visible = setup;
            _startupLabel.Visible = setup;
            foreach (var c in _gameChips) c.Visible = setup;

            _back.Visible = _step > 0;
            _primary.Text = _step == 2 ? "Finish  ✓" : (_step == 0 ? "Get started  ›" : "Next  ›");
            Invalidate(true);
        }

        private void Finish()
        {
            _settings.ThemeName = Theme.CurrentName;
            // LightTheme was a legacy bool used before theme names existed; it's
            // kept as a field on AppSettings for one-way migration of old
            // settings.json files (see ThemeCatalog.Resolve) but new code never
            // reads it. Writing it here would be dead-store churn on the JSON
            // file, so we deliberately skip the back-write.
            _settings.StartWithWindows = _startup.Checked;
            StartupManager.SetEnabled(_startup.Checked);
            _settings.FavoriteGame = _favoriteGame;
            _settings.OnboardingComplete = true;
            _store.Save(_settings);

            DialogResult = DialogResult.OK;
            Close();
        }

        private void RefreshSwatchLabels()
        {
            for (int i = 0; i < _swatchLabels.Count; i++)
                _swatchLabels[i].ForeColor =
                    _swatches[i].Palette.Name == Theme.CurrentName ? Theme.Text : Theme.TextDim;
        }

        /// <summary>
        /// Re-read every theme colour held by a stock WinForms control.
        ///
        /// The owner-drawn controls on this form (swatches, toggle, chips) sample Theme inside
        /// OnPaint, so they follow a theme change for free. Plain Labels, LinkLabels and
        /// Buttons don't - they keep the colour assigned when they were constructed. Since step
        /// 1 of onboarding is a theme picker, every one of those was left rendering the
        /// palette the form happened to open with.
        ///
        /// Called on every theme switch. Any stock control added here later needs a line in
        /// this method too, or it will silently go stale the same way.
        /// </summary>
        private void ReapplyThemeColors()
        {
            BackColor = Theme.Background;

            // The one users actually reported: invisible on a dark theme after switching.
            _startupLabel.ForeColor = Theme.Text;

            Pages.SettingsPage.RestylePrimary(_primary);

            // GlassLink reads Theme at paint time, so a theme switch cannot leave it painting
            // the old palette - it only needs telling that something changed. This used to
            // reassign LinkColor and ActiveLinkColor, which a stock LinkLabel caches.
            foreach (var link in new[] { _back, _skip }) link.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var back = new SolidBrush(Theme.Background)) g.FillRectangle(back, ClientRectangle);
            _field.Paint(g, 0, 0);

            var card = new RectangleF(24.5f, 24.5f, Width - 49, Height - 49);
            Glass.PaintPanel(g, card, 22, fillAlpha: 175);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (_step == 0)
            {
                int size = 64;
                var iconRect = new Rectangle((Width - size) / 2, 120, size, size);
                using (var star = new SolidBrush(Theme.Accent)) g.FillPolygon(star, StarPoints(iconRect));
                DrawCentered(g, "Welcome to PlexusX", TitleFont, Theme.Text, 205);
                DrawCentered(g, AppInfo.Tagline, SubFont, Theme.TextDim, 246);
                DrawCentered(g, "Sharper colors and more FPS, in one app. Let's set it up.", SubFont, Theme.TextDim, 274);

                // Surface the DX11 / Magnification fallback status right at the
                // first screen so the user knows up-front whether the saturation
                // effect will show in OBS / Discord screen share, or only on their
                // monitor. Without this line, the first time they check a stream
                // and don't see the boost they blame PlexusX - because nothing
                // on the Welcome screen hinted the fallback was in play.
                // Worded the same way the Settings page is: this is a limitation of the app
                // on every PC, not a fault found on theirs. "Fallback" on the very first
                // screen reads as "your machine came up short", which isn't what happened.
                bool dx11 = _settings.OverlayMode == OverlayMode.Dx;
                var statusText = dx11
                    ? "Your colours will show on screen and in recordings."
                    : "Your colours show on your screen, but not in recordings yet.";
                var statusColor = dx11 ? Theme.TextDim : Theme.Accent;
                DrawCentered(g, statusText, SubFont, statusColor, 302);
            }
            else if (_step == 1)
            {
                DrawCentered(g, "Pick your look", TitleFont, Theme.Text, 120);
                DrawCentered(g, "Choose a theme — you can change it any time in Settings.", SubFont, Theme.TextDim, 162);
                DrawStep(g, "STEP 1 OF 2");
            }
            else
            {
                DrawCentered(g, "A couple quick things", TitleFont, Theme.Text, 120);
                DrawCentered(g, "Set these now, or tweak them later in Settings.", SubFont, Theme.TextDim, 162);
                DrawCentered(g, "What do you play?", StepFont, Theme.TextDim, 288);
                DrawStep(g, "STEP 2 OF 2");
            }

            // Cinematic fade-up on first show.
            if (_reveal < 1f)
            {
                using var veil = new SolidBrush(Color.FromArgb((int)(255 * (1 - _reveal)), Theme.Background));
                g.FillRectangle(veil, ClientRectangle);
            }
        }

        private void DrawCentered(Graphics g, string text, Font font, Color color, int y) =>
            TextRenderer.DrawText(g, text, font, new Rectangle(0, y, Width, font.Height + 6), color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);

        private void DrawStep(Graphics g, string text) =>
            TextRenderer.DrawText(g, UiHelpers.Spaced(text), StepFont, new Rectangle(0, 44, Width, 16),
                Theme.TextDim, TextFormatFlags.HorizontalCenter);

        private static Point[] StarPoints(Rectangle r)
        {
            const int points = 12;
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            float outer = r.Width / 2f, inner = outer * 0.44f;
            var pts = new Point[points * 2];
            for (int i = 0; i < points * 2; i++)
            {
                double ang = Math.PI / points * i - Math.PI / 2;
                float rad = i % 2 == 0 ? outer : inner;
                pts[i] = new Point((int)(cx + rad * Math.Cos(ang)), (int)(cy + rad * Math.Sin(ang)));
            }
            return pts;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Stop before Dispose - see the note in SplashForm: a queued WM_TIMER still
                // gets dispatched, and the tick handler touches this form.
                _timer?.Stop();
                _timer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
