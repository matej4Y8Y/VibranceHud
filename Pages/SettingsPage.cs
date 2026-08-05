using System;
using System.Drawing;
using System.Windows.Forms;
using VibranceHud.Controls;

namespace VibranceHud.Pages
{
    /// <summary>
    /// App-level settings: launch with Windows, the window's translucency, and the manual
    /// update check. Vibrance itself lives on the Vibrance page.
    /// </summary>
    public sealed class SettingsPage : GlowPage
    {
        /// <summary>Text gutter inside a card. Captions, labels and slider tracks all line
        /// up on it, on both edges.</summary>
        private const int Gutter = 18;
        private const int CardLeft = 40;
        private const int CardTop = 40;
        private const int CardGap = 20;

        private readonly AppSettings _settings;
        private readonly SettingsStore _store;
        private readonly Action<int> _onOpacityChanged;
        private readonly Action<string> _onThemeChanged;
        private readonly Theming.CustomThemeService? _custom;
        private readonly Action _onBackgroundChanged;
        private readonly System.Collections.Generic.List<SwatchButton> _swatches = new();

        public SettingsPage(AppSettings settings, SettingsStore store,
            Action<int> onOpacityChanged, Action<string> onThemeChanged,
            Theming.CustomThemeService? custom = null, Action? onBackgroundChanged = null,
            IVibranceEngine? engine = null)
        {
            _settings = settings;
            _store = store;
            _onOpacityChanged = onOpacityChanged;
            _onThemeChanged = onThemeChanged;
            _custom = custom;
            _onBackgroundChanged = onBackgroundChanged ?? (() => { });

            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            AutoScroll = true;
            // Centre the column rather than let it hug the left edge of a wide window.
            ContentWidth = 620 + 2 * CardLeft;
            Font = new Font(Theme.FontFamily, 9.5f);
            Padding = new Padding(40, 32, 40, 32);

            int width = 620;

            // Cards used to carry hand-written absolute Y positions. They had drifted out of
            // step - two of the seven gaps were 12px against 20px everywhere else, and the
            // About card's Y put it in the middle of the page while its code sat at the
            // bottom. A cursor keeps the rhythm even and makes the reading order on screen
            // the same as the reading order in this file.
            int cardY = CardTop;
            CardPanel Card(int height)
            {
                var c = new CardPanel { Location = new Point(CardLeft, cardY), Size = new Size(width, height) };
                cardY += height + CardGap;
                return c;
            }

            // Re-fit a card to whatever its children actually came out as, and shift the
            // cursor by the difference. Wrapped text can't be measured before it exists, and
            // guessing a fixed height is how the Recording card ended up one line short of
            // its own contents - silently, because a Label just clips.
            void FitToContent(CardPanel c, int bottomPad = 18)
            {
                int bottom = 0;
                foreach (Control child in c.Controls) bottom = Math.Max(bottom, child.Bottom);
                int wanted = bottom + bottomPad;
                cardY += wanted - c.Height;
                c.Height = wanted;
            }

            var general = Card(112);
            general.Controls.Add(UiHelpers.Caption("GENERAL", 18, 16, 200));
            general.Controls.Add(new Label
            {
                Text = "Launch with Windows",
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Location = new Point(18, 46),
                AutoSize = true
            });
            var startupToggle = new ToggleSwitch
            {
                Location = new Point(width - 62, 44),
                Checked = StartupManager.IsEnabled()
            };
            startupToggle.CheckedChanged += (s, e) =>
            {
                StartupManager.SetEnabled(startupToggle.Checked);
                _settings.StartWithWindows = startupToggle.Checked;
                _store.Save(_settings);
            };
            general.Controls.Add(startupToggle);

            // This line used to end with "For recordings, turn on Show my colours in
                        // recordings below." That was wrong, and wrong in the worst direction:
                        // the Magnification path is invisible to capture whether that switch is
                        // on or off, so anyone who followed the advice paid image quality for
                        // nothing and then blamed the app when their stream still looked flat.
                        // The Recording card now states the real position; this one sticks to
                        // naming the engine and its consequence.
                        bool usingFallback = _settings.OverlayMode == VibranceHud.OverlayMode.Mag;
                        general.Controls.Add(new Label
                        {
                            Text = usingFallback
                                ? "Display engine: Magnification - shows on your monitor, but "
                                  + "not in recordings. Same on every PC; see Recording below."
                                : "Display engine: DX11",
                            ForeColor = Theme.TextDim,
                            BackColor = Color.Transparent,
                            Font = new Font(Theme.FontFamily, 8.5f),
                            Location = new Point(Gutter, 78),
                            AutoSize = true,
                            // Wraps inside the card rather than running out of it - the
                            // fallback wording is long enough to reach the edge.
                            MaximumSize = new Size(width - 2 * Gutter, 0),
                        });
                        // A reason and a Retry button are only meaningful when DX11 failed for a
                        // machine-specific cause the user could act on. The DX path is currently
                        // disabled by design, so it always "fails" for the same reason on every
                        // PC - showing that as a diagnosis would just alarm people about a
                        // decision we made. Flip this back on with the DX overlay.
                        const bool DxDiagnosticsAreMeaningful = false;

                        if (DxDiagnosticsAreMeaningful && usingFallback
                            && _settings.DxFailure != DxInitFailureKind.None
                            && !string.IsNullOrEmpty(_settings.DxFailureMessage))
                        {
                            // "Why" - one short line under the engine label.
                            general.Controls.Add(new Label
                            {
                                Text = "Why:  " + _settings.DxFailureMessage,
                                ForeColor = Theme.Accent,
                                BackColor = Color.Transparent,
                                Font = new Font(Theme.FontFamily, 8.5f),
                                Location = new Point(18, 96),
                                AutoSize = true
                            });
                            // "Hint" - what the user can do about it. Looked up from the
                            // categorised kind so the suggestion is concrete, not "try
                            // something" hand-waving. We don't have the original HRESULT
                            // here (just the kind) so we look up the hint by synthesising
                            // a representative HRESULT for each kind.
                            string hint = DxInitFailureMapper.HintForKind(_settings.DxFailure);
                            general.Controls.Add(new Label
                            {
                                Text = "Try:  " + hint,
                                ForeColor = Theme.TextDim,
                                BackColor = Color.Transparent,
                                Font = new Font(Theme.FontFamily, 8.5f),
                                Location = new Point(18, 114),
                                AutoSize = true
                            });
                        }
                        // Same reasoning as the diagnostics above: a Retry that relaunches the
                        // app can never change the outcome while the DX path is switched off.
                        if (DxDiagnosticsAreMeaningful && usingFallback)
                        {
                            var retryBtn = new Button
                            {
                                Text = "Retry display engine",
                                // AutoSize so the button always shows the full label
                                // regardless of theme font width. A fixed Size(140,24)
                                // clipped "Retry" -> "Retrv" and "display" -> "disblav"
                                // when the theme font was wider than the dev machine's.
                                AutoSize = true,
                                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                                Padding = new Padding(12, 4, 12, 4),
                                MinimumSize = new Size(160, 28),
                                Location = new Point(width - 180, 70),
                                FlatStyle = FlatStyle.Flat,
                                BackColor = Color.Transparent,
                                ForeColor = Theme.Accent,
                                Cursor = Cursors.Hand
                            };
                            retryBtn.FlatAppearance.BorderColor = Theme.Border;
                            retryBtn.FlatAppearance.BorderSize = 1;
                            retryBtn.Click += (s, e) =>
                            {
                                // Restart the process so the DX11 init runs again. This is the only
                                // reliable way to retry - the overlay is constructed once at startup,
                                // and tearing it down + rebuilding mid-session risks leaving the
                                // DWM-composited window stranded on the user's desktop.
                                try
                                {
                                    var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                                    if (exe == null) return;
                                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                    {
                                        FileName = exe,
                                        UseShellExecute = true
                                    });
                                    Application.Exit();
                                }
                                catch
                                {
                                    // If the relaunch itself failed (no interactive session, AV
                                    // blocking Process.Start, etc.), the user still has the
                                    // diagnostic hint to read.
                                }
                            };
                            general.Controls.Add(retryBtn);
                        }
                        // The engine line wraps to two lines on the fallback wording, which
                        // would have run past a hardcoded 112px card.
                        FitToContent(general);
                        Controls.Add(general);

            // ---- Theme picker (colour swatches) ----
            var themeCard = Card(120);
            themeCard.Controls.Add(UiHelpers.Caption("THEME", 18, 16, 200));
            int sx = 18;
            foreach (var palette in ThemeCatalog.All)
            {
                var swatch = new SwatchButton(palette)
                {
                    Location = new Point(sx, 48),
                    Active = palette.Name == Theme.CurrentName
                };
                swatch.Click += (s, e) =>
                {
                    foreach (var b in _swatches) b.Active = ReferenceEquals(b, swatch);
                    _onThemeChanged(swatch.Palette.Name);
                };
                _swatches.Add(swatch);
                themeCard.Controls.Add(swatch);

                themeCard.Controls.Add(new Label
                {
                    Text = palette.Name,
                    ForeColor = palette.Name == Theme.CurrentName ? Theme.Text : Theme.TextDim,
                    BackColor = Color.Transparent,
                    Font = new Font(Theme.FontFamily, 8f),
                    Location = new Point(sx - 8, 84),
                    Size = new Size(46, 16),
                    TextAlign = ContentAlignment.MiddleCenter
                });
                sx += 92;
            }
            Controls.Add(themeCard);

            // ---- Custom background image ----
            // The picked image sits behind the plexus field, and its dominant colour
            // becomes the accent - so the theme matches whatever the user drops in.
            var bgCard = Card(208);
            bgCard.Controls.Add(UiHelpers.Caption("BACKGROUND IMAGE", 18, 16, 260));

            var bgHint = new Label
            {
                Text = _custom != null && _custom.HasImage
                    ? "Theme colour is taken from this image."
                    : "Pick an image - the theme takes its colour from it.",
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8.5f),
                Location = new Point(18, 40),
                Size = new Size(width - 40, 18)
            };
            bgCard.Controls.Add(bgHint);

            var dimValue = new Label
            {
                Text = $"{_settings.CustomBackgroundDim}%",
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8.5f),
                Location = new Point(width - 60, 92),
                Size = new Size(42, 16),
                TextAlign = ContentAlignment.MiddleRight
            };

            var chooseBtn = FlatButton("Choose imageâ€¦", 18, 62, 150);
            var clearBtn = FlatButton("Remove", 178, 62, 100);

            var dimCaption = UiHelpers.Caption("DIM", 18, 92, 120);
            var dimSlider = new FlatSlider
            {
                Minimum = Theming.ImagePalette.MinDim,
                Maximum = Theming.ImagePalette.MaxDim,
                Value = Math.Clamp(_settings.CustomBackgroundDim,
                                   Theming.ImagePalette.MinDim, Theming.ImagePalette.MaxDim)
            };
            dimSlider.SetTrackBounds(Gutter, 112, width - 2 * Gutter);
            dimSlider.ValueChanged += (s, e) =>
            {
                _custom?.SetDim(dimSlider.Value);
                dimValue.Text = $"{dimSlider.Value}%";
                _store.Save(_settings);
                _onBackgroundChanged();
            };

            var blurValue = new Label
            {
                Text = $"{_settings.CustomBackgroundBlur}%",
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8.5f),
                Location = new Point(width - 60, 148),
                Size = new Size(42, 16),
                TextAlign = ContentAlignment.MiddleRight
            };
            var blurCaption = UiHelpers.Caption("BLUR", 18, 148, 120);
            var blurSlider = new FlatSlider
            {
                Minimum = 0,
                Maximum = Theming.AppBackground.MaxBlur,
                Value = Math.Clamp(_settings.CustomBackgroundBlur, 0, Theming.AppBackground.MaxBlur)
            };
            blurSlider.SetTrackBounds(Gutter, 168, width - 2 * Gutter);
            blurSlider.ValueChanged += (s, e) =>
            {
                _custom?.SetBlur(blurSlider.Value);
                blurValue.Text = $"{blurSlider.Value}%";
                _store.Save(_settings);
                _onBackgroundChanged();
            };

            chooseBtn.Click += (s, e) =>
            {
                using var dlg = new OpenFileDialog
                {
                    Title = "Choose a background image",
                    Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All files|*.*"
                };
                if (dlg.ShowDialog(this) != DialogResult.OK || _custom == null) return;

                if (!_custom.SetImage(dlg.FileName))
                {
                    bgHint.Text = "That file couldn't be read as an image.";
                    return;
                }

                dimSlider.Value = _settings.CustomBackgroundDim;
                dimValue.Text = $"{_settings.CustomBackgroundDim}%";
                blurSlider.Value = _settings.CustomBackgroundBlur;
                blurValue.Text = $"{_settings.CustomBackgroundBlur}%";
                bgHint.Text = "Theme colour is taken from this image.";
                _store.Save(_settings);
                _onThemeChanged(ThemeCatalog.CustomName);
            };

            clearBtn.Click += (s, e) =>
            {
                _custom?.Remove();
                bgHint.Text = "Pick an image - the theme takes its colour from it.";
                _store.Save(_settings);
                _onThemeChanged(ThemeCatalog.DefaultName);
            };

            bgCard.Controls.Add(blurCaption);
            bgCard.Controls.Add(blurValue);
            bgCard.Controls.Add(blurSlider);
            bgCard.Controls.Add(chooseBtn);
            bgCard.Controls.Add(clearBtn);
            bgCard.Controls.Add(dimCaption);
            bgCard.Controls.Add(dimValue);
            bgCard.Controls.Add(dimSlider);
            Controls.Add(bgCard);

            var appearance = Card(100);
            appearance.Controls.Add(UiHelpers.Caption("WINDOW OPACITY", 18, 16, 240));
            var opacityValue = new Label
            {
                Text = $"{Clamp(settings.OpacityPercent)}%",
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8.5f),
                Location = new Point(width - 60, 16),
                Size = new Size(42, 16),
                TextAlign = ContentAlignment.MiddleRight
            };
            appearance.Controls.Add(opacityValue);
            var opacitySlider = new FlatSlider
            {
                Minimum = 50,
                Maximum = 100,
                Value = Clamp(settings.OpacityPercent)
            };
            opacitySlider.SetTrackBounds(Gutter, 52, width - 2 * Gutter);
            opacitySlider.ValueChanged += (s, e) =>
            {
                _onOpacityChanged(opacitySlider.Value);
                _settings.OpacityPercent = opacitySlider.Value;
                opacityValue.Text = $"{opacitySlider.Value}%";
                _store.Save(_settings);
            };
            appearance.Controls.Add(opacitySlider);
            Controls.Add(appearance);

            var updates = Card(92);
            updates.Controls.Add(UiHelpers.Caption("UPDATES", 18, 16, 200));
            var checkBtn = FlatButton("Check for updates", 18, 44, 180);
            checkBtn.Click += async (s, e) => await UpdateService.CheckManuallyAsync();
            updates.Controls.Add(checkBtn);
            Controls.Add(updates);

            // ---- Recording ----
            if (engine != null)
            {
                var overlayMode = _settings.OverlayMode;
                bool driverVibrance = engine.DriverAvailable;
                bool canHelp = CaptureStatus.ToggleCanHelp(overlayMode, driverVibrance);

                var recording = Card(150);   // re-fitted to its content below
                recording.Controls.Add(UiHelpers.Caption("RECORDING", Gutter, 16, 200));

                // Renamed. "Show my colours in recordings" promised an outcome the app can
                // only sometimes deliver, and said nothing when it couldn't. This names the
                // action; the live line underneath states the outcome.
                recording.Controls.Add(new Label
                {
                    Text = "Move my colours where recording can see them",
                    ForeColor = canHelp ? Theme.Text : Theme.TextDim,
                    BackColor = Color.Transparent,
                    Location = new Point(Gutter, 46),
                    AutoSize = true
                });

                var streaming = new ToggleSwitch
                {
                    Location = new Point(width - 62, 44),
                    // Never show it on when it isn't doing anything. Anyone on the fallback
                    // who had already been talked into turning it on was looking at a lit
                    // switch that was quietly costing them picture quality.
                    Checked = canHelp && _settings.StreamingMode,
                    Enabled = canHelp,
                };

                var verdict = new Label
                {
                    ForeColor = Theme.TextDim,
                    BackColor = Color.Transparent,
                    Font = new Font(Theme.FontFamily, 8.5f, FontStyle.Bold),
                    Location = new Point(Gutter, 74),
                    AutoSize = true,
                };

                var reason = new Label
                {
                    ForeColor = Theme.TextDim,
                    BackColor = Color.Transparent,
                    Font = new Font(Theme.FontFamily, 8.5f),
                    Location = new Point(Gutter, 96),
                    // AutoSize with a capped width, NOT a fixed box. The old fixed 62px box
                    // was one line short of its own text, so the sentence telling people to
                    // use Display Capture instead of Game Capture - the single thing that
                    // decides whether this feature appears to work - was clipped off the
                    // bottom and nobody ever read it.
                    AutoSize = true,
                    MaximumSize = new Size(width - 2 * Gutter, 0),
                };

                void RefreshVerdict()
                {
                    var state = CaptureStatus.Resolve(overlayMode, driverVibrance, streaming.Checked);
                    verdict.Text = CaptureStatus.Headline(state);
                    verdict.ForeColor = state == CaptureState.Visible ? Theme.Text : Theme.Accent;
                    reason.Text = CaptureStatus.Reason(state, driverVibrance)
                                + "\r\n\r\n" + CaptureStatus.AlwaysTrue;
                }

                streaming.CheckedChanged += (s2, e2) =>
                {
                    engine.StreamingMode = streaming.Checked;
                    _settings.StreamingMode = streaming.Checked;
                    _store.Save(_settings);
                    RefreshVerdict();
                };
                RefreshVerdict();

                recording.Controls.Add(streaming);
                recording.Controls.Add(verdict);
                recording.Controls.Add(reason);

                // Deliberately no "fallback reason" line here. The DX path is switched off on
                // purpose and fails identically on every machine, so printing an HRESULT next
                // to it would dress our own decision up as a fault on the user's PC - the
                // exact thing the engine label above was changed to stop doing.

                // ---- "why don't my colours record?" ----
                // 8 of 20 testers reported their colours DID reach a screen share, which the
                // code says should be impossible. Rather than keep guessing across machines
                // we don't have, each PC measures itself and hands back a report.
                var testBtn = FlatButton("Test my recording setup", Gutter, reason.Bottom + 16, 210);
                var copyBtn = FlatButton("Copy report", Gutter + 220, reason.Bottom + 16, 140);
                copyBtn.Enabled = false;

                var result = new Label
                {
                    ForeColor = Theme.TextDim,
                    BackColor = Color.Transparent,
                    Font = new Font(Theme.FontFamily, 8.5f),
                    Location = new Point(Gutter, testBtn.Bottom + 12),
                    AutoSize = true,
                    MaximumSize = new Size(width - 2 * Gutter, 0),
                    // Space is reserved up front rather than grown later: the card's height is
                    // fixed once at build time, and a label that got taller afterwards would
                    // push its text out through the bottom of the card.
                    MinimumSize = new Size(width - 2 * Gutter, 58),
                    Text = "Takes about five seconds and will flash your screen while it "
                         + "measures. Nothing is sent anywhere - it puts a report on your "
                         + "clipboard for you to paste to us.",
                };

                string report = "";
                testBtn.Click += async (s2, e2) =>
                {
                    var go = GlassDialog.Show(FindForm(), "Test my recording setup",
                        "This flashes your screen on and off for about five seconds while it "
                        + "measures what recording software actually receives.\r\n\r\nRun it now?",
                        GlassDialogButtons.YesNo);
                    if (go != DialogResult.Yes) return;

                    testBtn.Enabled = false;
                    testBtn.Text = "Measuringâ€¦";
                    result.ForeColor = Theme.TextDim;
                    result.Text = "Measuring - leave the screen alone for a few seconds.";

                    // Off the UI thread: the probe sleeps between samples, and running it here
                    // would freeze the window (and its animation) into "Not Responding".
                    var probe = await System.Threading.Tasks.Task.Run(() => engine.RunCaptureProbe());
                    report = CaptureDiagnostic.BuildReport(_settings, driverVibrance, probe);

                    testBtn.Enabled = true;
                    testBtn.Text = "Test my recording setup";
                    copyBtn.Enabled = true;

                    result.ForeColor = probe.ReachesCapture ? Theme.Text : Theme.Accent;
                    result.Text = (probe.Ran
                        ? "Result: " + probe.Note + "."
                        : "Couldn't measure: " + probe.Note + ".")
                        + "\r\n\r\nHit Copy report, then paste it to us on Discord.";
                };

                copyBtn.Click += (s2, e2) =>
                {
                    if (report.Length == 0) return;
                    try
                    {
                        Clipboard.SetText(report);
                        result.ForeColor = Theme.TextDim;
                        result.Text = "Report copied. Paste it to us on Discord - it has no "
                                    + "personal details in it, only your graphics setup.";
                    }
                    catch
                    {
                        result.ForeColor = Theme.Accent;
                        result.Text = "Couldn't reach the clipboard. Try again in a moment.";
                    }
                };

                recording.Controls.Add(testBtn);
                recording.Controls.Add(copyBtn);
                recording.Controls.Add(result);

                FitToContent(recording);
                Controls.Add(recording);
            }

            // ---- Share ----
            //
            // Moved to the Display page, where the sliders it describes actually live.
            // Three cards down a different tab is not where anyone looked for it, and a code
            // passed between friends is how this app spreads - so being findable is the whole
            // feature. Deliberately not duplicated here: two controls writing the same
            // settings is how they drift apart.

            // ---- About ---- (last, so the version and the Discord link close the page)
            var about = Card(150);
            about.Controls.Add(new LogoBox
            {
                Image = BrandAssets.HorizontalLogo(Theme.IsLight),
                Location = new Point(18, 18),
                Size = new Size(190, 26)
            });
            about.Controls.Add(new Label
            {
                Text = AppInfo.Tagline,
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 9f),
                Location = new Point(18, 54),
                AutoSize = true
            });
            about.Controls.Add(new Label
            {
                Text = $"{AppInfo.ProductName}  {AppInfo.VersionText}",
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8.5f),
                Location = new Point(18, 78),
                AutoSize = true
            });
            var discordBtn = FlatButton("Join our Discord", 18, 104, 170);
            discordBtn.Click += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppInfo.DiscordUrl)
                    {
                        UseShellExecute = true
                    });
                }
                catch { /* no browser / blocked - nothing we can do */ }
            };
            about.Controls.Add(discordBtn);
            Controls.Add(about);
        }

        private static int Clamp(int pct) => Math.Clamp(pct, 50, 100);


        internal static Button FlatButton(string text, int x, int y, int width)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.SurfaceHover,
                ForeColor = Theme.Text,
                Font = new Font(Theme.FontFamily, 9f),
                Size = new Size(width, 32),
                Location = new Point(x, y),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            b.FlatAppearance.BorderColor = Theme.Border;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = Theme.Border;
            return b;
        }

        /// <summary>
        /// The accent-filled version of <see cref="FlatButton"/>, for the one action a page
        /// exists to perform (Launch, Apply, Save).
        ///
        /// Exists because every caller used to write the same three lines by hand - accent
        /// background, bold font, taller - and every one of them also set ForeColor to
        /// <c>Theme.Text</c>, which on the light theme is near-black text on a near-black
        /// fill. <see cref="Theme.OnAccentDim"/> is the readable pairing.
        /// </summary>
        internal static Button PrimaryButton(string text, int x, int y, int width, int height = 32)
        {
            var b = FlatButton(text, x, y, width);
            b.Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold);
            b.Height = height;
            RestylePrimary(b);
            return b;
        }

        /// <summary>Re-read the accent colours onto a primary button. A stock
        /// <see cref="Button"/> keeps whatever colours it was built with, so anything that
        /// switches theme while a button is alive (the onboarding theme picker) has to call
        /// this or the button keeps painting the old palette.</summary>
        internal static void RestylePrimary(Button b)
        {
            b.BackColor = Theme.AccentDim;
            b.ForeColor = Theme.OnAccentDim;
            b.FlatAppearance.BorderColor = Theme.Border;
            // Hover lifts the fill towards the full accent rather than jumping to it - the
            // full accent is near-black on the light theme and near-white on the dark ones,
            // so either way it would collide with the label colour on hover.
            b.FlatAppearance.MouseOverBackColor = Blend(Theme.AccentDim, Theme.Accent, 0.35f);
        }

        private static Color Blend(Color from, Color to, float amount) => Color.FromArgb(
            (int)(from.R + (to.R - from.R) * amount),
            (int)(from.G + (to.G - from.G) * amount),
            (int)(from.B + (to.B - from.B) * amount));
    }
}
