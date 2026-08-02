using System;
using System.Drawing;
using System.Windows.Forms;

namespace VibranceHud.Pages
{
    /// <summary>
    /// App-level settings: launch with Windows, the window's translucency, and the manual
    /// update check. Vibrance itself lives on the Vibrance page.
    /// </summary>
    public sealed class SettingsPage : GlowPage
    {
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
            Font = new Font(Theme.FontFamily, 9.5f);
            Padding = new Padding(40, 32, 40, 32);

            int width = 620;

            var general = new CardPanel { Location = new Point(40, 40), Size = new Size(width, 112) };
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

            // DX11 is deliberately disabled (see the note in DxDevice) because the
                        // overlay initialises but renders nothing, so every install runs the
                        // Magnification path. That used to surface here as an orange "Fallback"
                        // warning plus a failure reason and a Retry button - i.e. every single
                        // user was told their PC had a problem, and offered a retry that could
                        // never succeed. Now it states the known limitation plainly instead of
                        // dressing a shipped decision up as a fault on their machine.
                        bool usingFallback = _settings.OverlayMode == VibranceHud.OverlayMode.Mag;
                        general.Controls.Add(new Label
                        {
                            // No longer true as of Streaming Mode - and a stale "not supported
                            // yet" sitting above the switch that supports it is worse than
                            // saying nothing, because people believe the warning and never
                            // scroll down.
                            Text = usingFallback
                                ? "Colour effect runs on the Magnification path. For recordings, "
                                  + "turn on Show my colours in recordings below."
                                : "Display engine: DX11",
                            ForeColor = Theme.TextDim,
                            BackColor = Color.Transparent,
                            Font = new Font(Theme.FontFamily, 8.5f),
                            Location = new Point(18, 78),
                            AutoSize = true
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
                        Controls.Add(general);

            // ---- Theme picker (colour swatches) ----
            var themeCard = new CardPanel { Location = new Point(40, 172), Size = new Size(width, 120) };
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
            var bgCard = new CardPanel { Location = new Point(40, 312), Size = new Size(width, 208) };
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

            var chooseBtn = FlatButton("Choose image…", 18, 62, 150);
            var clearBtn = FlatButton("Remove", 178, 62, 100);

            var dimCaption = UiHelpers.Caption("DIM", 18, 92, 120);
            var dimSlider = new FlatSlider
            {
                Minimum = Theming.ImagePalette.MinDim,
                Maximum = Theming.ImagePalette.MaxDim,
                Location = new Point(16, 112),
                Width = width - 32,
                Value = Math.Clamp(_settings.CustomBackgroundDim,
                                   Theming.ImagePalette.MinDim, Theming.ImagePalette.MaxDim)
            };
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
                Location = new Point(16, 168),
                Width = width - 32,
                Value = Math.Clamp(_settings.CustomBackgroundBlur, 0, Theming.AppBackground.MaxBlur)
            };
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

            var appearance = new CardPanel { Location = new Point(40, 540), Size = new Size(width, 108) };
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
                Location = new Point(16, 52),
                Width = width - 32,
                Value = Clamp(settings.OpacityPercent)
            };
            opacitySlider.ValueChanged += (s, e) =>
            {
                _onOpacityChanged(opacitySlider.Value);
                _settings.OpacityPercent = opacitySlider.Value;
                opacityValue.Text = $"{opacitySlider.Value}%";
                _store.Save(_settings);
            };
            appearance.Controls.Add(opacitySlider);
            Controls.Add(appearance);

            var updates = new CardPanel { Location = new Point(40, 660), Size = new Size(width, 92) };
            updates.Controls.Add(UiHelpers.Caption("UPDATES", 18, 16, 200));
            var checkBtn = FlatButton("Check for updates", 18, 44, 180);
            checkBtn.Click += async (s, e) => await UpdateService.CheckManuallyAsync();
            updates.Controls.Add(checkBtn);
            Controls.Add(updates);

            // ---- About ----
            if (engine != null)
            {
                var recording = new CardPanel { Location = new Point(40, 934), Size = new Size(width, 150) };
                recording.Controls.Add(UiHelpers.Caption("RECORDING", 18, 16, 200));
                recording.Controls.Add(new Label
                {
                    Text = "Show my colours in recordings",
                    ForeColor = Theme.Text,
                    BackColor = Color.Transparent,
                    Location = new Point(18, 46),
                    AutoSize = true
                });

                var streaming = new ToggleSwitch
                {
                    Location = new Point(width - 62, 44),
                    Checked = _settings.StreamingMode
                };
                streaming.CheckedChanged += (s2, e2) =>
                {
                    engine.StreamingMode = streaming.Checked;
                    _settings.StreamingMode = streaming.Checked;
                    _store.Save(_settings);
                };
                recording.Controls.Add(streaming);

                recording.Controls.Add(new Label
                {
                    Text = "Normally your viewers see the game without your colours - the effect is " +
                           "added after the point recording software reads the screen. This moves it " +
                           "earlier so it shows up.\r\n\r\n" +
                           "In OBS use Display Capture, not Game Capture. Costs a little image quality.",
                    ForeColor = Theme.TextDim,
                    BackColor = Color.Transparent,
                    Location = new Point(18, 74),
                    Size = new Size(width - 40, 62),
                });
                Controls.Add(recording);
            }

            if (engine != null)
            {
                // Sits below Recording. Both are additive cards under the existing layout, so
                // nothing above them had to move.
                var share = new CardPanel { Location = new Point(40, 1104), Size = new Size(width, 150) };
                share.Controls.Add(UiHelpers.Caption("SHARE", 18, 16, 200));
                share.Controls.Add(new Label
                {
                    Text = "Send someone your exact colours, or paste theirs.",
                    ForeColor = Theme.TextDim,
                    BackColor = Color.Transparent,
                    Location = new Point(18, 44),
                    Size = new Size(width - 40, 20),
                });

                var codeBox = new TextBox
                {
                    Location = new Point(18, 72),
                    Size = new Size(width - 210, 26),
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = Theme.Background,
                    ForeColor = Theme.Text,
                    Font = new Font("Consolas", 10f),
                    CharacterCasing = CharacterCasing.Upper,
                };
                share.Controls.Add(codeBox);

                var copy = new GlassButton
                {
                    Text = "Copy mine",
                    Location = new Point(width - 184, 71),
                    Size = new Size(86, 28),
                };
                share.Controls.Add(copy);

                // Primary: the one action on this card that does something to your screen.
                var apply = new GlassButton
                {
                    Text = "Apply",
                    Kind = GlassButtonKind.Primary,
                    Location = new Point(width - 92, 71),
                    Size = new Size(74, 28),
                };
                share.Controls.Add(apply);

                var shareStatus = new Label
                {
                    ForeColor = Theme.TextDim,
                    BackColor = Color.Transparent,
                    Location = new Point(18, 108),
                    Size = new Size(width - 40, 20),
                };
                share.Controls.Add(shareStatus);

                copy.Click += (s2, e2) =>
                {
                    var code = ProfileCode.Encode(new ProfileCode(
                        engine.Vibrance, engine.Saturation, engine.Brightness, engine.Gamma));
                    codeBox.Text = code;
                    Clipboard.SetText(code);
                    shareStatus.ForeColor = Theme.TextDim;
                    shareStatus.Text = code + "  -  copied, paste it anywhere";
                };

                apply.Click += (s2, e2) =>
                {
                    if (!ProfileCode.TryDecode(codeBox.Text, out var incoming))
                    {
                        // Never half-apply. A wrong character means we don't know what they
                        // meant, and guessing lands someone on a stranger's screen.
                        shareStatus.ForeColor = Theme.Accent;
                        shareStatus.Text = "That code isn't right - check it and try again.";
                        return;
                    }

                    engine.Vibrance = incoming.Vibrance;
                    engine.Saturation = incoming.Saturation;
                    engine.Brightness = incoming.Brightness;
                    engine.Gamma = incoming.Gamma;

                    _settings.VibrancePercent = incoming.Vibrance;
                    _settings.SaturationPercent = incoming.Saturation;
                    _settings.BrightnessPercent = incoming.Brightness;
                    _settings.GammaPercent = incoming.Gamma;
                    _store.Save(_settings);

                    shareStatus.ForeColor = Theme.TextDim;
                    shareStatus.Text = "Applied.";
                };

                Controls.Add(share);
            }

            var about = new CardPanel { Location = new Point(40, 764), Size = new Size(width, 150) };
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
    }
}
