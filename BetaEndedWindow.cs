using System;
using System.Drawing;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// Shown when this build is below the published minimum version - i.e. the beta is over and
    /// the full release exists.
    ///
    /// Deliberately a dead end: there is no "continue anyway". The only ways out are downloading
    /// the new version or closing the app. A dismissible notice would just be dismissed, and the
    /// whole point is that nobody is left behind on a beta build with a beta key.
    ///
    /// Worded as an ending rather than a failure. The person reading it did nothing wrong - they
    /// were a tester, and the thing they were testing is finished.
    /// </summary>
    public sealed class BetaEndedWindow : Form
    {
        private static readonly Font TitleFont = new(Theme.FontFamily, 15f, FontStyle.Bold);
        private static readonly Font BodyFont = new(Theme.FontFamily, 9.5f);

        public BetaEndedWindow(string message)
        {
            Text = "PlexusX";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 260);
            BackColor = Theme.Background;
            Icon = AppIcon.Value;

            Controls.Add(new Label
            {
                Text = "The beta has ended",
                ForeColor = Theme.Text,
                Font = TitleFont,
                Location = new Point(30, 28),
                AutoSize = true,
                BackColor = Color.Transparent,
            });

            Controls.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(message)
                    ? "PlexusX has left beta and this version no longer runs.\r\n\r\n"
                      + "Thanks for testing - a lot of what shipped came from people\r\n"
                      + "reporting things during the beta.\r\n\r\n"
                      + "Grab the new version to carry on."
                    : message,
                ForeColor = Theme.TextDim,
                Font = BodyFont,
                Location = new Point(32, 68),
                Size = new Size(500, 120),
                BackColor = Color.Transparent,
            });

            var get = new Button
            {
                Text = "Get the new version",
                Size = new Size(180, 36),
                Location = new Point(30, ClientSize.Height - 62),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.AccentDim,
                ForeColor = Theme.OnAccentDim,
                Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold),
                Cursor = Cursors.Hand,
            };
            get.FlatAppearance.BorderColor = Theme.Border;
            get.Click += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = AppInfo.DiscordUrl,
                        UseShellExecute = true,
                    });
                }
                catch { /* no browser available - the text still says what to do */ }
            };
            Controls.Add(get);

            var close = new Button
            {
                Text = "Close",
                Size = new Size(120, 36),
                Location = new Point(ClientSize.Width - 150, ClientSize.Height - 62),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Surface,
                ForeColor = Theme.TextDim,
                Cursor = Cursors.Hand,
            };
            close.FlatAppearance.BorderColor = Theme.Border;
            close.Click += (s, e) => Close();
            Controls.Add(close);

            AcceptButton = get;
            CancelButton = close;
        }
    }
}
