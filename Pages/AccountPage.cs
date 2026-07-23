using System.Drawing;
using System.Windows.Forms;

namespace VibranceHud.Pages
{
    /// <summary>
    /// Placeholder for the future subscription (trial status, activate/buy license). Wired
    /// up for real in the licensing/payments phase; for now it just states the plan so the
    /// nav slot exists.
    /// </summary>
    public sealed class AccountPage : GlowPage
    {
        public AccountPage()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Font = new Font(Theme.FontFamily, 9.5f);

            var card = new CardPanel { Size = new Size(560, 200) };
            card.Controls.Add(UiHelpers.Caption("ACCOUNT & LICENSE", 20, 20, 300));
            card.Controls.Add(new Label
            {
                Text = "Coming soon",
                ForeColor = Theme.Accent,
                BackColor = Theme.Surface,
                Font = new Font(Theme.FontFamily, 15f, FontStyle.Bold),
                Location = new Point(20, 52),
                AutoSize = true
            });
            card.Controls.Add(new Label
            {
                Text = "Vibrance HUD will offer a free trial, then a low monthly\n" +
                       "subscription. Your license and trial status will live here.",
                ForeColor = Theme.TextDim,
                BackColor = Theme.Surface,
                Location = new Point(20, 96),
                Size = new Size(520, 60)
            });
            Controls.Add(card);

            Resize += (s, e) =>
            {
                card.Left = System.Math.Max(20, (Width - card.Width) / 2);
                card.Top = System.Math.Max(20, (Height - card.Height) / 2);
            };
        }
    }
}
