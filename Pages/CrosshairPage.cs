using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using VibranceHud.Crosshair;

namespace VibranceHud.Pages
{
    /// <summary>
    /// Build a crosshair, see it live, save it under a name. Deliberately small: shape,
    /// colour, and three sliders cover what people actually change.
    /// </summary>
    public sealed class CrosshairPage : GlowPage
    {
        private readonly AppSettings _settings;
        private readonly SettingsStore _store;
        private readonly CrosshairService _service;

        private readonly PreviewBox _preview;
        private readonly CardPanel _card;
        private readonly FlowLayoutPanel _savedFlow;
        private readonly Label _savedEmpty;
        private readonly ToggleSwitch _enabled;
        private readonly Label _status;
        private readonly List<ChipButton> _shapes = new();

        private const int BaseCardHeight = 630;

        private CrosshairConfig _current;

        public CrosshairPage(AppSettings settings, SettingsStore store, CrosshairService service)
        {
            _settings = settings;
            _store = store;
            _service = service;
            _current = service.Config.Clone();

            Font = new Font(Theme.FontFamily, 9.5f);
            AutoScroll = true;

            // Tall enough for every row including SAVED at the bottom (y=300 sliders start,
            // combo/buttons land around y=572-600): a shorter card clips its own last row
            // regardless of the page's AutoScroll, since a child can't render past its
            // immediate parent's bounds.
            _card = new CardPanel { Location = new Point(40, 34), Size = new Size(620, BaseCardHeight) };
            _card.Controls.Add(UiHelpers.Caption("CROSSHAIR", 18, 16, 240));

            _enabled = new ToggleSwitch { Location = new Point(560, 14), Checked = _service.IsVisible };
            _enabled.CheckedChanged += (s, e) =>
            {
                if (_enabled.Checked) _service.Show(); else _service.Hide();
                _settings.CrosshairEnabled = _enabled.Checked;
                _store.Save(_settings);
                UpdateStatus();
            };
            _card.Controls.Add(_enabled);

            _preview = new PreviewBox { Location = new Point(18, 44), Size = new Size(584, 150) };
            _card.Controls.Add(_preview);

            _status = new Label
            {
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8.5f),
                Location = new Point(18, 200),
                Size = new Size(584, 18)
            };
            _card.Controls.Add(_status);

            // ---- Shape ----
            _card.Controls.Add(UiHelpers.Caption("SHAPE", 18, 228, 200));
            int sx = 18;
            foreach (var shape in new[] { CrosshairShape.Cross, CrosshairShape.Dot,
                                          CrosshairShape.Circle, CrosshairShape.T })
            {
                var chip = new ChipButton
                {
                    Text = shape.ToString(),
                    Location = new Point(sx, 250),
                    Size = new Size(140, 34),
                    Font = new Font(Theme.FontFamily, 9f),
                    Active = _current.Shape == shape
                };
                var captured = shape;
                chip.Click += (s, e) =>
                {
                    _current.Shape = captured;
                    foreach (var c in _shapes) c.Active = c.Text == captured.ToString();
                    Push();
                };
                _shapes.Add(chip);
                _card.Controls.Add(chip);
                sx += 148;
            }

            // ---- Sliders ----
            int y = 300;
            AddSlider(_card, "SIZE", y, 1, 30, _current.Size, v => { _current.Size = v; Push(); });
            AddSlider(_card, "THICKNESS", y + 62, 1, 10, _current.Thickness, v => { _current.Thickness = v; Push(); });
            AddSlider(_card, "GAP", y + 124, 0, 30, _current.Gap, v => { _current.Gap = v; Push(); });

            // ---- Colour + options ----
            _card.Controls.Add(UiHelpers.Caption("COLOUR", 18, y + 186, 200));
            int cx = 18;
            foreach (var colour in new[]
            {
                Color.FromArgb(255, 0, 255, 102), Color.FromArgb(255, 0, 255, 255),
                Color.FromArgb(255, 255, 0, 170), Color.FromArgb(255, 255, 60, 60),
                Color.FromArgb(255, 255, 220, 0), Color.White
            })
            {
                var dot = new SwatchDot(colour) { Location = new Point(cx, y + 208) };
                var captured = colour;
                dot.Click += (s, e) => { _current.ColourArgb = captured.ToArgb(); Push(); };
                _card.Controls.Add(dot);
                cx += 44;
            }

            var outline = new ToggleSwitch { Location = new Point(560, y + 206), Checked = _current.Outline };
            outline.CheckedChanged += (s, e) => { _current.Outline = outline.Checked; Push(); };
            _card.Controls.Add(new Label
            {
                Text = "Outline",
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Location = new Point(430, y + 208),
                AutoSize = true
            });
            _card.Controls.Add(outline);

            // ---- Saved configs ----
            _card.Controls.Add(UiHelpers.Caption("SAVED", 18, y + 250, 200));

            var saveBtn = Button("Save as…", 472, y + 272, 130);
            saveBtn.Click += (s, e) => SaveCurrent();
            _card.Controls.Add(saveBtn);

            // Fixed width so WrapContents actually wraps; height grows with the row count
            // (RefreshSavedList grows the card to match, same trick already used for the
            // card's own AutoScrollMinSize - see the comment at the top of this file about
            // a child positioned outside its parent's bounds being invisible regardless of
            // the page's own scroll setting).
            _savedFlow = new FlowLayoutPanel
            {
                Location = new Point(18, y + 272),
                Size = new Size(444, 36),
                MaximumSize = new Size(444, 0),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                BackColor = Theme.Surface
            };
            _card.Controls.Add(_savedFlow);

            _savedEmpty = new Label
            {
                Text = "No saved crosshairs yet — tweak, then Save as…",
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8.5f),
                Location = new Point(18, y + 272),
                Size = new Size(444, 36),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _card.Controls.Add(_savedEmpty);

            Controls.Add(_card);

            RefreshSavedList();
            Push();
        }

        private void AddSlider(Control parent, string label, int y, int min, int max,
            int value, Action<int> onChange)
        {
            parent.Controls.Add(UiHelpers.Caption(label, 18, y, 200));
            var readout = new Label
            {
                Text = value.ToString(),
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8.5f),
                Location = new Point(560, y),
                Size = new Size(42, 16),
                TextAlign = ContentAlignment.MiddleRight
            };
            parent.Controls.Add(readout);

            var slider = new FlatSlider
            {
                Minimum = min,
                Maximum = max,
                Location = new Point(16, y + 20),
                Width = 586,
                Value = Math.Clamp(value, min, max)
            };
            slider.ValueChanged += (s, e) => { readout.Text = slider.Value.ToString(); onChange(slider.Value); };
            parent.Controls.Add(slider);
        }

        /// <summary>Push the edited config to the preview and the live overlay together, so
        /// what's on screen always matches what's in the card.</summary>
        private void Push()
        {
            _preview.Config = _current;
            _preview.Invalidate();
            _service.Apply(_current.Clone());
            _settings.ActiveCrosshair = _current.Clone();
            _store.Save(_settings);
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (!_enabled.Checked)
            {
                _status.Text = "Crosshair is off.";
                _status.ForeColor = Theme.TextDim;
                return;
            }

            if (CrosshairService.IsExclusiveFullscreen())
            {
                _status.Text = "A game is in exclusive fullscreen - no overlay can draw there. "
                             + "Switch it to borderless and the crosshair will show.";
                _status.ForeColor = Theme.Accent;
            }
            else
            {
                _status.Text = "Crosshair is showing, centred on your screen.";
                _status.ForeColor = Theme.TextDim;
            }
        }

        /// <summary>Rebuilds the chip list from <c>_settings.SavedCrosshairs</c> and grows the
        /// card to fit however many rows that wraps to (a fixed card height would clip chips
        /// past its bottom edge regardless of the page's own AutoScroll - see the comment
        /// on the card's construction above).</summary>
        private void RefreshSavedList()
        {
            _savedFlow.SuspendLayout();
            _savedFlow.Controls.Clear();
            foreach (var saved in _settings.SavedCrosshairs)
            {
                var chip = new SavedChip(saved)
                {
                    Active = saved.Name == _current.Name,
                    Margin = new Padding(0, 0, 8, 8)
                };
                chip.LoadRequested += (s, e) => LoadSaved(saved.Name);
                chip.DeleteRequested += (s, e) => DeleteSaved(saved.Name);
                _savedFlow.Controls.Add(chip);
            }
            _savedFlow.ResumeLayout(true);

            bool any = _settings.SavedCrosshairs.Count > 0;
            _savedFlow.Visible = any;
            _savedEmpty.Visible = !any;

            // Set explicitly rather than relying on WinForms to infer it from children - a
            // UserControl doesn't always recompute this reliably on its own.
            _card.Height = Math.Max(BaseCardHeight, _savedFlow.Bottom + 20);
            AutoScrollMinSize = new Size(0, _card.Bottom + 20);
        }

        private void LoadSaved(string name)
        {
            var found = _settings.SavedCrosshairs.FirstOrDefault(c => c.Name == name);
            if (found == null) return;
            _current = found.Clone();
            foreach (var c in _shapes) c.Active = c.Text == _current.Shape.ToString();
            Push();
            RefreshSavedList();
        }

        private void SaveCurrent()
        {
            string name = Prompt.Ask("Save crosshair as", _current.Name);
            if (string.IsNullOrWhiteSpace(name)) return;

            _current.Name = name.Trim();
            _settings.SavedCrosshairs.RemoveAll(c => c.Name == _current.Name);
            _settings.SavedCrosshairs.Add(_current.Clone());
            _store.Save(_settings);
            RefreshSavedList();
        }

        /// <summary>Deletes immediately, no confirmation - matches the approved chip design.
        /// If the deleted config was the active one, the on-screen config is left as-is and
        /// only the highlight disappears (there's nothing left to highlight).</summary>
        private void DeleteSaved(string name)
        {
            _settings.SavedCrosshairs.RemoveAll(c => c.Name == name);
            _store.Save(_settings);
            RefreshSavedList();
        }

        private static Button Button(string text, int x, int y, int w)
        {
            var b = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.SurfaceHover,
                ForeColor = Theme.Text,
                Font = new Font(Theme.FontFamily, 9f),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = Theme.Border;
            return b;
        }

        /// <summary>Draws the crosshair over a checkerboard, so light and dark colours can
        /// both be judged before taking it into a game.</summary>
        private sealed class PreviewBox : Panel
        {
            public CrosshairConfig Config { get; set; } = new();

            public PreviewBox()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                       | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                       | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                CrosshairRender.DrawCheckerboard(g, new Rectangle(0, 0, Width, Height), 16);

                var state = g.Save();
                g.TranslateTransform(Width / 2f, Height / 2f);
                CrosshairRender.Draw(g, Config);
                g.Restore(state);
                base.OnPaint(e);
            }
        }

        private sealed class SwatchDot : Control
        {
            private readonly Color _colour;

            public SwatchDot(Color colour)
            {
                // SupportsTransparentBackColor must be enabled BEFORE assigning a
                // transparent BackColor - a plain Control rejects it otherwise.
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                       | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                _colour = colour;
                Size = new Size(30, 30);
                Cursor = Cursors.Hand;
                BackColor = Color.Transparent;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var b = new SolidBrush(_colour);
                e.Graphics.FillEllipse(b, 3, 3, Width - 7, Height - 7);
                using var p = new Pen(Color.FromArgb(120, 255, 255, 255), 1f);
                e.Graphics.DrawEllipse(p, 3, 3, Width - 7, Height - 7);
            }
        }

        /// <summary>One saved crosshair, shown as a pill: mini preview, name, and a "×" that
        /// deletes it. Sits inside the (opaque, Theme.Surface) saved-list FlowLayoutPanel and
        /// uses the same transparent-control trick as SwatchDot/PreviewBox - one level of it
        /// against an opaque parent is the pattern proven safe elsewhere in this app; the
        /// FlowLayoutPanel is deliberately NOT transparent itself, since stacking two levels
        /// of the trick is what caused the ghosting bug on the Games Hub (see HANDOFF.md).</summary>
        private sealed class SavedChip : Control
        {
            private const int PreviewSize = 22;
            private const int DeleteWidth = 24;

            private readonly CrosshairConfig _config;
            private bool _hover;
            private bool _active;

            public bool Active
            {
                get => _active;
                set { if (_active == value) return; _active = value; Invalidate(); }
            }

            public event EventHandler? LoadRequested;
            public event EventHandler? DeleteRequested;

            public SavedChip(CrosshairConfig config)
            {
                _config = config;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                       | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor
                       | ControlStyles.ResizeRedraw, true);
                BackColor = Color.Transparent;
                Cursor = Cursors.Hand;
                Font = new Font(Theme.FontFamily, 9f);
                Size = new Size(190, 36);
            }

            private Rectangle DeleteRect => new(Width - DeleteWidth, 0, DeleteWidth, Height);

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                _hover = true;
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _hover = false;
                Invalidate();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (e.Button != MouseButtons.Left) return;
                if (DeleteRect.Contains(e.Location)) DeleteRequested?.Invoke(this, EventArgs.Empty);
                else LoadRequested?.Invoke(this, EventArgs.Empty);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
                using (var path = Glass.RoundedPath(rect, (Height - 1) / 2f))
                {
                    using (var fill = new SolidBrush(_hover ? Theme.SurfaceHover : Theme.Surface))
                        g.FillPath(fill, path);
                    using (var border = new Pen(_active ? Theme.Accent : Theme.Border, _active ? 1.6f : 1f))
                        g.DrawPath(border, path);
                }

                var previewRect = new Rectangle(7, (Height - PreviewSize) / 2, PreviewSize, PreviewSize);
                CrosshairRender.DrawCheckerboard(g, previewRect, 6);
                CrosshairRender.Draw(g, _config, previewRect);

                var textRect = new Rectangle(previewRect.Right + 8, 0,
                    Width - previewRect.Right - 8 - DeleteWidth, Height);
                TextRenderer.DrawText(g, _config.Name, Font, textRect, Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                TextRenderer.DrawText(g, "×", Font, DeleteRect, Theme.TextDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        /// <summary>Minimal name prompt - WinForms has no built-in one.</summary>
        private static class Prompt
        {
            public static string Ask(string title, string initial)
            {
                using var form = new Form
                {
                    Text = title,
                    Size = new Size(360, 150),
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    StartPosition = FormStartPosition.CenterParent,
                    MaximizeBox = false,
                    MinimizeBox = false,
                    BackColor = Theme.Background,
                    ForeColor = Theme.Text
                };
                var box = new TextBox
                {
                    Text = initial,
                    Location = new Point(16, 20),
                    Width = 310,
                    BackColor = Theme.Surface,
                    ForeColor = Theme.Text,
                    BorderStyle = BorderStyle.FixedSingle
                };
                var ok = new Button
                {
                    Text = "Save",
                    DialogResult = DialogResult.OK,
                    Location = new Point(230, 60),
                    Size = new Size(96, 30),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Theme.SurfaceHover,
                    ForeColor = Theme.Text
                };
                form.Controls.Add(box);
                form.Controls.Add(ok);
                form.AcceptButton = ok;
                return form.ShowDialog() == DialogResult.OK ? box.Text : "";
            }
        }
    }
}
