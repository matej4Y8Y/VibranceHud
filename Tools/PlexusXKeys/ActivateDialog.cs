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
        private static Color Bg => VibranceHud.Theme.Background;
        // Read from Theme rather than hardcoded, so the tool cannot drift away from the app it
        // issues keys for. Properties, not static readonly fields: those initialise at type
        // load, which happens before Program.cs has applied a theme.
        private static Color Fg => VibranceHud.Theme.Text;
        private static Color Dim => VibranceHud.Theme.TextDim;

        private readonly VibranceHud.GlassTextBox _pcId = new();
        private readonly VibranceHud.GlassTextBox _licence = new();
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
            _pcId.SetBounds(16, 94, 300, 30);
            _pcId.CharacterCasing = CharacterCasing.Upper;
            _pcId.Inner.Font = new Font("Consolas", 10f);
            Controls.Add(_pcId);

            var make = new VibranceHud.GlassButton
            {
                Text = "Create licence",
                Kind = VibranceHud.GlassButtonKind.Primary,
            };
            make.SetBounds(330, 93, 140, 30);
            make.Click += (s, e) => Create();
            Controls.Add(make);

            _status.SetBounds(16, 128, 600, 20);
            _status.ForeColor = Dim;
            Controls.Add(_status);

            Controls.Add(Line("LICENCE  (send this back to the customer)", 16, 156, Dim, small: true));
            _licence.SetBounds(16, 176, 608, 160);
            _licence.Multiline = true;
            _licence.ReadOnly = true;
            _licence.Inner.Font = new Font("Consolas", 8.5f);
            Controls.Add(_licence);

            var copy = new VibranceHud.GlassButton { Text = "Copy licence" };
            copy.SetBounds(16, 348, 140, 30);
            copy.Click += (s, e) => CopyLicence();
            Controls.Add(copy);

            var close = new VibranceHud.GlassButton { Text = "Done" };
            close.SetBounds(504, 348, 120, 30);
            close.Click += (s, e) => Close();
            Controls.Add(close);

            // Enter and Escape by hand: GlassButton is an owner-drawn Control, not an
            // IButtonControl, so AcceptButton/CancelButton would compile to nothing.
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) Close();
                else if (e.KeyCode == Keys.Enter && _pcId.Inner.Focused) Create();
            };
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
            if (_licence.Text.Length == 0) return;
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

    }
}
