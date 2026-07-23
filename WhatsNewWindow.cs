using System;
using System.Drawing;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// Shown once after an update: the release notes for the version now running, so the
    /// user sees what changed before they land in the app.
    /// </summary>
    public sealed class WhatsNewWindow : Form
    {
        private static readonly Font TitleFont = new(Theme.FontFamily, 15f, FontStyle.Bold);

        public WhatsNewWindow(Version version, string notes)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            TopMost = true;
            ClientSize = new Size(520, 420);
            BackColor = Theme.Background;
            DoubleBuffered = true;
            Icon = AppIcon.Value;

            Controls.Add(new Label
            {
                Text = $"What's new in {version}",
                ForeColor = Theme.Text,
                Font = TitleFont,
                Location = new Point(28, 26),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            Controls.Add(new Label
            {
                Text = "PlexusX updated automatically.",
                ForeColor = Theme.TextDim,
                Location = new Point(30, 58),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            var body = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Surface,
                ForeColor = Theme.Text,
                Font = new Font(Theme.FontFamily, 9.5f),
                Location = new Point(28, 92),
                Size = new Size(ClientSize.Width - 56, 250),
                Text = string.IsNullOrWhiteSpace(notes)
                    ? "This release has no notes."
                    : notes.Replace("\r\n", "\n").Replace("\n", Environment.NewLine),
                TabStop = false
            };
            body.GotFocus += (s, e) => body.Select(0, 0);
            Controls.Add(body);

            var ok = SettingsPageButton("OK", ClientSize.Width - 148, ClientSize.Height - 58, 120);
            ok.BackColor = Theme.AccentDim;
            ok.Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold);
            ok.Click += (s, e) => Close();
            Controls.Add(ok);
            AcceptButton = ok;
        }

        private static Button SettingsPageButton(string text, int x, int y, int width)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.SurfaceHover,
                ForeColor = Theme.Text,
                Size = new Size(width, 34),
                Location = new Point(x, y),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            b.FlatAppearance.BorderColor = Theme.Border;
            b.FlatAppearance.BorderSize = 1;
            return b;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Glass.PaintPanel(e.Graphics, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 16, fillAlpha: 180);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var back = new SolidBrush(Theme.Background);
            e.Graphics.FillRectangle(back, ClientRectangle);
        }
    }
}
