using System;
using System.Drawing;
using System.Windows.Forms;
using VibranceHud.Licensing;

namespace PlexusXKeys
{
    /// <summary>
    /// Turn a key into a licence for one specific customer's PC.
    ///
    /// This is the step that actually costs the customer's key its value: the licence produced
    /// here names their machine, so it is worthless to anyone else. Until the website exists,
    /// this is done by hand - the customer sends their PC id, this produces the licence, they
    /// paste it back. When the site goes live the server will do exactly this, with the same
    /// signing key and the same code path.
    /// </summary>
    public sealed class ActivateDialog : Form
    {
        private static readonly Color Bg = Color.FromArgb(18, 18, 22);
        private static readonly Color Fg = Color.FromArgb(235, 235, 240);
        private static readonly Color Dim = Color.FromArgb(150, 150, 160);
        private static readonly Color Accent = Color.FromArgb(140, 110, 240);

        private readonly TextBox _pcId = new();
        private readonly TextBox _licence = new();
        private readonly Label _status = new();
        private readonly KeyRecord _key;

        /// <summary>The PC this key was activated for, once it has been. Null if the owner
        /// closed the dialog without producing a licence.</summary>
        public string? ActivatedFor { get; private set; }

        public ActivateDialog(KeyRecord key)
        {
            _key = key;

            Text = "Activate " + key.Code;
            ClientSize = new Size(640, 400);
            BackColor = Bg;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);

            Controls.Add(Line($"Key {key.Code}  -  {key.Plan}", 16, 16, Fg, bold: true));
            Controls.Add(Line("Ask the customer to open PlexusX and copy their PC id from the " +
                              "activation window.", 16, 40, Dim));

            Controls.Add(Line("CUSTOMER'S PC ID", 16, 74, Dim, small: true));
            _pcId.SetBounds(16, 94, 300, 26);
            _pcId.BorderStyle = BorderStyle.FixedSingle;
            _pcId.BackColor = Bg;
            _pcId.ForeColor = Fg;
            _pcId.CharacterCasing = CharacterCasing.Upper;
            _pcId.Font = new Font("Consolas", 10f);
            Controls.Add(_pcId);

            var make = new Button { Text = "Create licence" };
            make.SetBounds(330, 93, 140, 28);
            Style(make, Accent);
            make.Click += (s, e) => Create();
            Controls.Add(make);

            _status.SetBounds(16, 128, 600, 20);
            _status.ForeColor = Dim;
            Controls.Add(_status);

            Controls.Add(Line("LICENCE  (send this back to the customer)", 16, 156, Dim, small: true));
            _licence.SetBounds(16, 176, 608, 160);
            _licence.Multiline = true;
            _licence.ReadOnly = true;
            _licence.ScrollBars = ScrollBars.Vertical;
            _licence.BorderStyle = BorderStyle.FixedSingle;
            _licence.BackColor = Bg;
            _licence.ForeColor = Fg;
            _licence.Font = new Font("Consolas", 8.5f);
            Controls.Add(_licence);

            var copy = new Button { Text = "Copy licence" };
            copy.SetBounds(16, 348, 140, 30);
            Style(copy, Color.FromArgb(60, 60, 70));
            copy.Click += (s, e) => CopyLicence();
            Controls.Add(copy);

            var close = new Button { Text = "Done" };
            close.SetBounds(504, 348, 120, 30);
            Style(close, Color.FromArgb(60, 60, 70));
            close.Click += (s, e) => Close();
            Controls.Add(close);
        }

        private void Create()
        {
            var pc = _pcId.Text.Trim();

            if (!MachineId.LooksValid(pc))
            {
                _status.ForeColor = Color.FromArgb(230, 110, 110);
                _status.Text = "That doesn't look like a PC id. It's four groups of four, " +
                               "like A1B2-C3D4-E5F6-7890.";
                return;
            }

            var duration = PlanCatalog.DurationFor(_key.Plan);
            if (duration == null)
            {
                _status.ForeColor = Color.FromArgb(230, 110, 110);
                _status.Text = "Unknown plan on this key - it cannot be activated.";
                return;
            }

            try
            {
                var now = DateTime.UtcNow;
                var doc = new LicenceDocument(
                    Serial: _key.Code,
                    Plan: _key.Plan,
                    IssuedUtc: now,
                    ExpiresUtc: now + duration.Value,
                    HardwareId: pc);

                _licence.Text = LicenceSigner.Sign(doc, KeyVault.PrivateKey());
                ActivatedFor = pc;

                CopyLicence();
                _status.ForeColor = Color.FromArgb(120, 220, 140);
                _status.Text = $"Licence created and copied. Runs out " +
                               $"{doc.ExpiresUtc.ToLocalTime():d MMMM yyyy}.";
            }
            catch (Exception ex)
            {
                _status.ForeColor = Color.FromArgb(230, 110, 110);
                _status.Text = ex.Message;
            }
        }

        private void CopyLicence()
        {
            if (_licence.TextLength == 0) return;
            Clipboard.SetText(_licence.Text);
        }

        private static Label Line(string text, int x, int y, Color colour, bool bold = false, bool small = false)
            => new()
            {
                Text = text,
                ForeColor = colour,
                Location = new Point(x, y),
                AutoSize = true,
                MaximumSize = new Size(610, 0),
                Font = new Font("Segoe UI", small ? 7.5f : 9f,
                    bold || small ? FontStyle.Bold : FontStyle.Regular),
            };

        private static void Style(Button b, Color back)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = back;
            b.ForeColor = Color.FromArgb(235, 235, 240);
            b.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 80);
            b.Cursor = Cursors.Hand;
        }
    }
}
