using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VibranceHud.Design;

namespace VibranceHud.Controls
{
    public enum GlassDialogButtons { Ok, OkCancel, YesNo }

    /// <summary>Info is the default; Warning and Danger only change the accent stripe.</summary>
    public enum GlassDialogTone { Info, Warning, Danger }

    /// <summary>
    /// The app's dialog.
    ///
    /// PlexusX had eleven raw MessageBox.Show calls inside a glass-themed app, including on
    /// the paths people hit most - update prompts and the game-page confirmations. A grey
    /// Win32 box in the middle of this UI is the moment it stops reading as something
    /// somebody paid for.
    ///
    /// Program.cs's fatal-error handler deliberately still uses a native MessageBox: it has
    /// to render when the themed UI is the thing that broke, possibly before Theme has ever
    /// been applied.
    /// </summary>
    public sealed class GlassDialog : Form
    {
        private const int WidthLogical = 440;

        public static int ButtonCount(GlassDialogButtons b) => b == GlassDialogButtons.Ok ? 1 : 2;

        /// <summary>
        /// Dialog height for a body at a given logical width.
        ///
        /// Measured against the real font rather than guessed from character counts, so a
        /// long message gets a tall enough box instead of a clipped one.
        /// </summary>
        public static int MeasureHeight(string body, int width)
        {
            const int chrome = 148;   // accent stripe + title + padding + button row
            int textW = Math.Max(80, width - 2 * Tokens.XL);

            int bodyH = string.IsNullOrEmpty(body)
                ? 0
                : TextRenderer.MeasureText(body, Fonts.Body, new Size(textW, 0),
                    TextFormatFlags.WordBreak).Height;

            return chrome + Math.Min(bodyH, 420);
        }

        private readonly string _title, _body;
        private readonly GlassDialogTone _tone;

        /// <summary>Internal rather than private so tests can lay one out without a message
        /// loop. Callers use <see cref="Show"/>.</summary>
        internal GlassDialog(string title, string body, GlassDialogButtons buttons, GlassDialogTone tone)
        {
            _title = title ?? "PlexusX";
            _body = body ?? "";
            _tone = tone;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = Theme.Background;
            DoubleBuffered = true;
            Icon = AppIcon.Value;
            KeyPreview = true;
            Font = Fonts.Body;

            ClientSize = new Size(Tokens.Scale(WidthLogical),
                                  Tokens.Scale(MeasureHeight(_body, WidthLogical)));

            int btnW = Tokens.Scale(96), btnH = Tokens.Scale(32), pad = Tokens.Scale(Tokens.XL);

            var primary = new GlassButton
            {
                Text = buttons == GlassDialogButtons.YesNo ? "Yes" : "OK",
                Kind = GlassButtonKind.Primary,
                Size = new Size(btnW, btnH),
                Location = new Point(ClientSize.Width - btnW - pad, ClientSize.Height - btnH - pad),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            };
            primary.Click += (s, e) => Finish(Affirmative(buttons));
            Controls.Add(primary);

            if (buttons != GlassDialogButtons.Ok)
            {
                var secondary = new GlassButton
                {
                    Text = buttons == GlassDialogButtons.YesNo ? "No" : "Cancel",
                    Kind = GlassButtonKind.Ghost,
                    Size = new Size(btnW, btnH),
                    Location = new Point(primary.Left - btnW - Tokens.Scale(Tokens.S), primary.Top),
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                };
                secondary.Click += (s, e) => Finish(Negative(buttons));
                Controls.Add(secondary);
            }

            // Enter and Escape by hand. GlassButton is an owner-drawn Control, not an
            // IButtonControl, so AcceptButton/CancelButton cannot see it - assigning them
            // would silently do nothing and the dialog would swallow both keys.
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) Finish(Affirmative(buttons));
                else if (e.KeyCode == Keys.Escape) Finish(Negative(buttons));
            };

            WindowDrag.Enable(this, this);
        }

        private static DialogResult Affirmative(GlassDialogButtons b) =>
            b == GlassDialogButtons.YesNo ? DialogResult.Yes : DialogResult.OK;

        /// <summary>Escape on a single-OK dialog still means OK - there is nothing to cancel.</summary>
        private static DialogResult Negative(GlassDialogButtons b) => b switch
        {
            GlassDialogButtons.Ok => DialogResult.OK,
            GlassDialogButtons.YesNo => DialogResult.No,
            _ => DialogResult.Cancel,
        };

        private void Finish(DialogResult result)
        {
            DialogResult = result;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (var back = new SolidBrush(Theme.Background))
                g.FillRectangle(back, ClientRectangle);

            Glass.PaintPanel(g, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1),
                Tokens.Scale(14), fillAlpha: 225);

            var accent = _tone switch
            {
                GlassDialogTone.Danger => Color.FromArgb(232, 84, 84),
                _ => Theme.Accent,
            };
            using (var bar = new SolidBrush(accent))
                g.FillRectangle(bar, 0, 0, Width, Tokens.Scale(3));

            int pad = Tokens.Scale(Tokens.XL);
            int textW = Width - pad * 2;

            TextRenderer.DrawText(g, _title, Fonts.Title,
                new Rectangle(pad, Tokens.Scale(Tokens.XL), textW, Tokens.Scale(30)),
                Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, _body, Fonts.Body,
                new Rectangle(pad, Tokens.Scale(Tokens.XXL + Tokens.XL), textW,
                              Height - Tokens.Scale(148)),
                Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.WordBreak);
        }

        /// <summary>Show a themed dialog. Pass the owning form where there is one so it
        /// centres on the window rather than the screen.</summary>
        public static DialogResult Show(IWin32Window? owner, string title, string body,
            GlassDialogButtons buttons = GlassDialogButtons.Ok,
            GlassDialogTone tone = GlassDialogTone.Info)
        {
            using var dialog = new GlassDialog(title, body, buttons, tone);
            return owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
        }
    }
}
