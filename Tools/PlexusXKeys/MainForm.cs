using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VibranceHud.Licensing;

namespace PlexusXKeys
{
    /// <summary>
    /// The whole tool: issue keys, see what every one of them is doing, and act on them.
    ///
    /// Deliberately plain WinForms. This is a private tool with one user - effort spent making
    /// it pretty is effort not spent on the product people pay for.
    /// </summary>
    public sealed class MainForm : Form
    {
        private static readonly Color Bg = Color.FromArgb(18, 18, 22);
        private static readonly Color PanelBg = Color.FromArgb(28, 28, 34);
        private static readonly Color Fg = Color.FromArgb(235, 235, 240);
        private static readonly Color Dim = Color.FromArgb(150, 150, 160);
        private static readonly Color Accent = Color.FromArgb(140, 110, 240);

        private IReadOnlyList<KeyRecord> _ledger = new List<KeyRecord>();

        private readonly ListView _list = new();
        private readonly Label _stats = new();
        private readonly ComboBox _plan = new();
        private readonly TextBox _note = new();
        private readonly Label _hint = new();

        public MainForm()
        {
            Text = "PlexusX Keys";
            ClientSize = new Size(1000, 640);
            BackColor = Bg;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            BuildIssueRow();
            BuildList();
            BuildActions();
            BuildStats();

            Load += (s, e) => FirstRunThenLoad();
        }

        // ---- first run -------------------------------------------------------------------

        private void FirstRunThenLoad()
        {
            try
            {
                if (KeyVault.NeedsSetup)
                {
                    var answer = MessageBox.Show(
                        "No signing key found, so this looks like the first run.\n\n" +
                        "A signing key will be created and stored at:\n" + KeyVault.Location +
                        "\n\nThis key is what proves a licence is genuine. Back that folder up " +
                        "somewhere safe - if you lose it you cannot issue keys that existing " +
                        "installs will accept, and there is no way to recover it.\n\n" +
                        "Create it now?",
                        "PlexusX Keys - first run", MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Information);

                    if (answer != DialogResult.OK) { Close(); return; }

                    KeyVault.CreateSigningKey();

                    MessageBox.Show(
                        "Signing key created.\n\nNext: paste the public half into the app so it " +
                        "can verify licences. Use the \"Copy public key\" button - the private " +
                        "half never leaves this machine.",
                        "PlexusX Keys", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                Reload();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "PlexusX Keys", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        // ---- layout ----------------------------------------------------------------------

        private void BuildIssueRow()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = PanelBg, Padding = new Padding(16) };

            bar.Controls.Add(Caption("PLAN", 16, 12));
            _plan.DropDownStyle = ComboBoxStyle.DropDownList;
            _plan.Items.AddRange(new object[] { PlanCatalog.Monthly, PlanCatalog.Lifetime600, PlanCatalog.Trial });
            _plan.SelectedIndex = 0;
            _plan.SetBounds(16, 34, 150, 26);
            _plan.FlatStyle = FlatStyle.Flat;
            _plan.BackColor = Bg;
            _plan.ForeColor = Fg;
            bar.Controls.Add(_plan);

            bar.Controls.Add(Caption("NOTE  (who is this for?)", 184, 12));
            _note.SetBounds(184, 34, 420, 26);
            _note.BorderStyle = BorderStyle.FixedSingle;
            _note.BackColor = Bg;
            _note.ForeColor = Fg;
            bar.Controls.Add(_note);

            var issue = Button("Generate key", 624, 33, 140, Accent);
            issue.Click += (s, e) => IssueKey();
            bar.Controls.Add(issue);

            var copyPub = Button("Copy public key", 776, 33, 150, Color.FromArgb(60, 60, 70));
            copyPub.Click += (s, e) => CopyPublicKey();
            bar.Controls.Add(copyPub);

            Controls.Add(bar);
        }

        private void BuildList()
        {
            _list.Dock = DockStyle.Fill;
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.GridLines = false;
            _list.BackColor = Bg;
            _list.ForeColor = Fg;
            _list.BorderStyle = BorderStyle.None;
            _list.Columns.Add("Key", 200);
            _list.Columns.Add("Plan", 100);
            _list.Columns.Add("Status", 90);
            _list.Columns.Add("Issued", 110);
            _list.Columns.Add("Expires", 110);
            _list.Columns.Add("Left", 90);
            _list.Columns.Add("Note", 260);
            _list.DoubleClick += (s, e) => CopySelectedCode();
            Controls.Add(_list);
        }

        private void BuildActions()
        {
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = PanelBg, Padding = new Padding(16) };

            var copy = Button("Copy key", 16, 14, 110, Color.FromArgb(60, 60, 70));
            copy.Click += (s, e) => CopySelectedCode();
            bar.Controls.Add(copy);

            var revoke = Button("Revoke", 136, 14, 110, Color.FromArgb(120, 50, 50));
            revoke.Click += (s, e) => ActOnSelection("revoke", KeyLedger.Revoke,
                "Revoke this key? It stops working for whoever has it.");
            bar.Controls.Add(revoke);

            var restore = Button("Un-revoke", 256, 14, 110, Color.FromArgb(60, 60, 70));
            restore.Click += (s, e) => ActOnSelection("restore", KeyLedger.Restore, null);
            bar.Controls.Add(restore);

            var release = Button("Release from PC", 376, 14, 150, Color.FromArgb(60, 60, 70));
            release.Click += (s, e) => ActOnSelection("release", KeyLedger.Release,
                "Release this key from the PC it's on?\n\n" +
                "Use this when a customer changes their GPU or reinstalls Windows - it lets " +
                "them activate again on the new machine.");
            bar.Controls.Add(release);

            _hint.SetBounds(544, 20, 430, 20);
            _hint.ForeColor = Dim;
            _hint.Text = "Double-click a row to copy its key.";
            bar.Controls.Add(_hint);

            Controls.Add(bar);
        }

        private void BuildStats()
        {
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Bg, Padding = new Padding(16, 0, 16, 0) };
            _stats.Dock = DockStyle.Fill;
            _stats.ForeColor = Dim;
            _stats.TextAlign = ContentAlignment.MiddleLeft;
            bar.Controls.Add(_stats);
            Controls.Add(bar);
        }

        // ---- actions ---------------------------------------------------------------------

        private void IssueKey()
        {
            try
            {
                var code = KeyCode.Generate();
                var record = new KeyRecord
                {
                    Code = code,
                    Plan = (string)_plan.SelectedItem!,
                    IssuedUtc = DateTime.UtcNow,
                    Note = _note.Text.Trim(),
                };

                _ledger = KeyLedger.Add(_ledger, record);
                KeyVault.SaveLedger(_ledger);
                _note.Clear();
                Reload();

                Clipboard.SetText(code);
                _hint.Text = $"{code}  -  copied to clipboard";
                SelectCode(code);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "PlexusX Keys", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ActOnSelection(
            string what,
            Func<IReadOnlyList<KeyRecord>, string, IReadOnlyList<KeyRecord>> action,
            string? confirm)
        {
            var code = SelectedCode();
            if (code == null) { _hint.Text = "Select a key first."; return; }

            if (confirm != null &&
                MessageBox.Show(confirm + "\n\n" + code, "PlexusX Keys",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                _ledger = action(_ledger, code);
                KeyVault.SaveLedger(_ledger);
                Reload();
                SelectCode(code);
                _hint.Text = $"{code}  -  {what} done";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "PlexusX Keys", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CopySelectedCode()
        {
            var code = SelectedCode();
            if (code == null) { _hint.Text = "Select a key first."; return; }
            Clipboard.SetText(code);
            _hint.Text = $"{code}  -  copied";
        }

        private void CopyPublicKey()
        {
            try
            {
                Clipboard.SetText(KeyVault.PublicKeyAsCSharp());
                _hint.Text = "Public key copied - paste it into the app. The private half stays here.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "PlexusX Keys", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---- rendering -------------------------------------------------------------------

        private void Reload()
        {
            _ledger = KeyVault.LoadLedger();
            var now = DateTime.UtcNow;

            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var k in _ledger.OrderByDescending(k => k.IssuedUtc))
            {
                var status = k.StatusAt(now);
                var left = k.RemainingAt(now);

                var row = new ListViewItem(new[]
                {
                    k.Code,
                    k.Plan,
                    status.ToString(),
                    k.IssuedUtc.ToLocalTime().ToString("yyyy-MM-dd"),
                    k.ExpiresUtc?.ToLocalTime().ToString("yyyy-MM-dd") ?? "-",
                    left == null ? "-" : Humanise(left.Value),
                    k.Note,
                });

                row.ForeColor = status switch
                {
                    KeyStatus.Active => Color.FromArgb(120, 220, 140),
                    KeyStatus.Unused => Fg,
                    KeyStatus.Expired => Dim,
                    KeyStatus.Revoked => Color.FromArgb(230, 110, 110),
                    _ => Fg,
                };
                _list.Items.Add(row);
            }
            _list.EndUpdate();

            var s = KeyLedger.Stats(_ledger, now);
            var soon = KeyLedger.ExpiringWithin(_ledger, TimeSpan.FromDays(7), now).Count;
            _stats.Text =
                $"{s.Total} issued     {s.Active} active     {s.Unused} unused     " +
                $"{s.Expired} expired     {s.Revoked} revoked     " +
                $"{s.ActivationRate:P0} activated" +
                (soon > 0 ? $"     {soon} expiring within 7 days" : "");
        }

        private static string Humanise(TimeSpan t)
        {
            if (t <= TimeSpan.Zero) return "0";
            if (t.TotalDays >= 1) return $"{(int)t.TotalDays}d";
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h";
            return $"{(int)t.TotalMinutes}m";
        }

        private string? SelectedCode() =>
            _list.SelectedItems.Count == 0 ? null : _list.SelectedItems[0].Text;

        private void SelectCode(string code)
        {
            foreach (ListViewItem item in _list.Items)
            {
                if (!string.Equals(item.Text, code, StringComparison.OrdinalIgnoreCase)) continue;
                item.Selected = true;
                item.EnsureVisible();
                return;
            }
        }

        // ---- small helpers ---------------------------------------------------------------

        private static Label Caption(string text, int x, int y) => new()
        {
            Text = text,
            ForeColor = Color.FromArgb(150, 150, 160),
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            Location = new Point(x, y),
            AutoSize = true,
        };

        private static Button Button(string text, int x, int y, int width, Color back)
        {
            var b = new Button { Text = text };
            b.SetBounds(x, y, width, 30);
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = back;
            b.ForeColor = Color.FromArgb(235, 235, 240);
            b.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 80);
            b.Cursor = Cursors.Hand;
            return b;
        }
    }
}
