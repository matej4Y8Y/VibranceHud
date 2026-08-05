using System;
using System.Drawing;
using System.Windows.Forms;
using VibranceHud.License;
using VibranceHud.Controls;

namespace VibranceHud.Pages
{
    /// <summary>
    /// Shows the current license (tier, key, expiry) and lets the user deactivate it.
    /// This used to be a static "Coming soon" placeholder that never touched
    /// <see cref="LicenseService"/> at all - Deactivate had nowhere to live even
    /// though <see cref="LicenseDialog"/>'s own header comment already promised
    /// "the Account tab shows the same dialog if the user clicks Deactivate".
    /// </summary>
    public sealed class AccountPage : GlowPage
    {
        private readonly LicenseService _license;
        private readonly CardPanel _card;
        private readonly Label _tierLabel;
        private readonly Label _detailLabel;
        private readonly Label _keyLabel;
        private readonly GlassButton _deactivateButton;

        /// <summary>Raised after a successful deactivate+reactivate cycle, so
        /// MainWindow can re-run <c>ApplyLicenseVisibility</c> - the nav items hide
        /// themselves whenever there is no valid license, and that check only ran
        /// once at startup before this page could ever change the license state.</summary>
        public event EventHandler? LicenseChanged;

        public AccountPage(LicenseService license)
        {
            _license = license;
            Dock = DockStyle.Fill;
            Font = new Font(Theme.FontFamily, 9.5f);

            _card = new CardPanel { Location = new Point(40, 40), Size = new Size(560, 260) };
            _card.Controls.Add(UiHelpers.Caption("ACCOUNT & LICENSE", 20, 20, 300));

            _tierLabel = new Label
            {
                ForeColor = Theme.Accent,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 15f, FontStyle.Bold),
                Location = new Point(20, 52),
                AutoSize = true,
            };

            _detailLabel = new Label
            {
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Location = new Point(20, 88),
                Size = new Size(520, 40),
            };

            _keyLabel = new Label
            {
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font("Consolas", 9f),
                Location = new Point(20, 132),
                Size = new Size(520, 20),
            };

            _deactivateButton = new GlassButton
            {
                Text = "Deactivate license",
                Kind = GlassButtonKind.Ghost,
                Location = new Point(20, 176),
                Size = new Size(160, 34),
            };
            _deactivateButton.Click += (_, _) => DeactivateAndReactivate();

            _card.Controls.Add(_tierLabel);
            _card.Controls.Add(_detailLabel);
            _card.Controls.Add(_keyLabel);
            _card.Controls.Add(_deactivateButton);
            Controls.Add(_card);

            Resize += (_, _) =>
            {
                _card.Left = Math.Max(20, (Width - _card.Width) / 2);
                _card.Top = Math.Max(20, (Height - _card.Height) / 2);
            };

            RefreshLicenseDisplay();
        }

        private void RefreshLicenseDisplay()
        {
            if (_license.HasValidLicense && _license.Current != null)
            {
                var tierName = _license.Current.Tier switch
                {
                    "paid" => "Paid",
                    "trial" => "Trial",
                    _ => "Free",
                };
                _tierLabel.Text = $"{tierName} plan — active";

                var expires = _license.ExpiresAt;
                _detailLabel.Text = expires.HasValue
                    ? $"Valid until {expires.Value:d MMMM yyyy}."
                    : "Valid.";

                _keyLabel.Text = string.IsNullOrEmpty(_license.KeyText)
                    ? ""
                    : $"Key: {_license.KeyText}";

                _deactivateButton.Visible = true;
            }
            else
            {
                _tierLabel.Text = "Not activated";
                _detailLabel.Text = "PlexusX needs a valid activation key to run.";
                _keyLabel.Text = "";
                _deactivateButton.Visible = false;
            }
        }

        /// <summary>
        /// Deactivate clears the current license and immediately re-opens the
        /// activation dialog - this app has no "logged out but still usable" state
        /// (Program.cs's own boot sequence enforces "no key, no app"), so letting the
        /// user deactivate without an immediate path back to activation would just
        /// strand them looking at a page with no supported next action.
        /// </summary>
        private void DeactivateAndReactivate()
        {
            var confirm = GlassDialog.Show(FindForm(), "Deactivate licence",
                "PlexusX will ask for a new activation key straight away. Continue?",
                GlassDialogButtons.YesNo, GlassDialogTone.Warning);
            if (confirm != DialogResult.Yes) return;

            _license.Deactivate();

            using var dialog = new LicenseDialog(_license);
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
            {
                // Same policy as the app's own startup gate: closing the activation
                // dialog without a valid key closes the app, not just this page.
                Application.Exit();
                return;
            }

            RefreshLicenseDisplay();
            LicenseChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
