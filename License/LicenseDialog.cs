// Modal activation dialog. The user pastes their key, hits Activate, and we either
// write the license file and close, or show an inline error and keep the dialog
// open. There's no "Skip" or "Cancel" - no key, no app. The Account tab in the
// main window shows the same dialog if the user clicks Deactivate.
//
// Styled Plexus-style: black & white, no gradients, no accent colors. The dialog
// uses Theme.* colors directly so it tracks the global theme without hardcoded
// purple (that used to be the activation button color before v0.9.0).

using System;
using System.Drawing;
using System.Windows.Forms;

namespace VibranceHud.License
{
    public sealed class LicenseDialog : Form
    {
        private readonly LicenseService _service;
        private readonly TextBox _keyBox;
        private readonly Label _statusLabel;
        private readonly Button _activateBtn;
        private readonly Button _getKeyBtn;
        private readonly Button _closeBtn;

        public LicenseDialog(LicenseService service)
        {
            _service = service;
            Text = "PlexusX — Activate";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(560, 280);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = new Font(Theme.FontFamily, 9.5f);

            var title = new Label
            {
                Text = "PlexusX — Activate",
                Font = new Font(Theme.FontFamily, 14f, FontStyle.Bold),
                ForeColor = Theme.Text,
                AutoSize = true,
                Location = new Point(24, 20),
            };

            var subtitle = new Label
            {
                Text = "PlexusX refuses to start without a valid activation key.",
                ForeColor = Theme.TextDim,
                AutoSize = true,
                Location = new Point(24, 50),
            };

            var inputLabel = new Label
            {
                Text = "Paste your activation key:",
                ForeColor = Theme.TextDim,
                AutoSize = true,
                Location = new Point(24, 90),
            };

            _keyBox = new TextBox
            {
                Location = new Point(24, 114),
                Size = new Size(512, 28),
                Font = new Font("Consolas", 11f),
                BackColor = Theme.Surface,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
            };
            _keyBox.TextChanged += (s, e) => _activateBtn.Enabled = _keyBox.Text.Trim().Length > 0;

            _statusLabel = new Label
            {
                Text = StatusTextForInitial(),
                ForeColor = Theme.TextDim,
                AutoSize = true,
                Location = new Point(24, 156),
            };

            _activateBtn = new Button
            {
                Text = "Activate",
                Size = new Size(120, 32),
                Location = new Point(24, 196),
                BackColor = Theme.Accent,
                ForeColor = Theme.Background,
                FlatStyle = FlatStyle.Flat,
                Enabled = false,
            };
            _activateBtn.FlatAppearance.BorderSize = 0;
            _activateBtn.Click += ActivateBtn_Click;

            _getKeyBtn = new Button
            {
                Text = "Get a key",
                Size = new Size(120, 32),
                Location = new Point(160, 196),
                BackColor = Theme.Surface,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat,
            };
            _getKeyBtn.FlatAppearance.BorderColor = Theme.Border;
            _getKeyBtn.Click += (s, e) =>
            {
                // Discord, not the source repo. Keys are handed out by the developer, so a
                // releases page was the wrong destination for someone asking for one - and it
                // pointed users straight at the code, which isn't where we want them.
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = AppInfo.DiscordUrl,
                        UseShellExecute = true,
                    });
                }
                catch { /* no browser / blocked - the dialog still explains what's needed */ }
            };

            _closeBtn = new Button
            {
                Text = "Close application",
                Size = new Size(140, 32),
                Location = new Point(396, 196),
                BackColor = Theme.Surface,
                ForeColor = Theme.TextDim,
                FlatStyle = FlatStyle.Flat,
            };
            _closeBtn.FlatAppearance.BorderColor = Theme.Border;
            _closeBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.AddRange(new Control[]
            {
                title, subtitle, inputLabel, _keyBox, _statusLabel,
                _activateBtn, _getKeyBtn, _closeBtn,
            });

            AcceptButton = _activateBtn;
        }

        private string StatusTextForInitial()
        {
            return _service.State switch
            {
                LicenseState.Valid => "✓ License is already active. Click Activate to re-bind or Close to exit.",
                LicenseState.Expired => "⚠ Current license expired. Enter a new key below.",
                LicenseState.WrongMachine => "⚠ Current license is bound to a different machine. Enter a new key.",
                LicenseState.Tampered => "⚠ License file is corrupted. Enter a valid key.",
                LicenseState.Revoked => "⚠ This key has been deactivated by the developer. Contact support if you believe this is a mistake.",
                LicenseState.DebuggerDetected => "⚠ Debugger detected. Close it and try again.",
                _ => "Enter the key you received from the developer (looks like AAAA-R-F-XXXXXXXX-XXXXXXXX).",
            };
        }

        private void ActivateBtn_Click(object? sender, EventArgs e)
        {
            var key = _keyBox.Text.Trim();
            if (string.IsNullOrEmpty(key)) return;

            _statusLabel.Text = "Validating…";
            _statusLabel.ForeColor = Theme.TextDim;
            _activateBtn.Enabled = false;
            Application.DoEvents();

            var state = _service.TryActivate(key);
            switch (state)
            {
                case LicenseState.Valid:
                    _statusLabel.Text = "✓ Activated. Welcome to PlexusX.";
                    _statusLabel.ForeColor = Theme.Accent;
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                case LicenseState.InvalidKey:
                    _statusLabel.Text = "✗ Invalid key. Check the spelling and try again.";
                    _statusLabel.ForeColor = Theme.TextDim;
                    break;
                case LicenseState.Tampered:
                    _statusLabel.Text = "✗ Key rejected (signature mismatch).";
                    _statusLabel.ForeColor = Theme.TextDim;
                    break;
                case LicenseState.Revoked:
                    _statusLabel.Text = "✗ This key has been deactivated and can no longer be used.";
                    _statusLabel.ForeColor = Theme.TextDim;
                    break;
                default:
                    _statusLabel.Text = $"✗ Activation failed: {state}";
                    _statusLabel.ForeColor = Theme.TextDim;
                    break;
            }

            _activateBtn.Enabled = true;
        }
    }
}
