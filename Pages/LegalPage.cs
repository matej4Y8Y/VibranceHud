using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace VibranceHud.Pages
{
    /// <summary>
    /// The terms, the privacy policy, the licence and the third-party attributions, readable
    /// inside the app.
    ///
    /// Reached from a button in Settings rather than getting a nav tab of its own: this is a
    /// page somebody opens once, and a permanent row in a five-row navigation would cost more
    /// than it is worth. The documents are embedded, so this works with no network, no install
    /// folder to hunt through, and on a machine where the user never kept the installer.
    /// </summary>
    public sealed class LegalPage : GlowPage
    {
        /// <summary>Display name -> embedded resource name. Order is the order shown.</summary>
        internal static readonly (string Title, string Resource)[] Documents =
        {
            ("Terms of use", "EULA.md"),
            ("Privacy", "PRIVACY.md"),
            ("Licence", "LICENSE.md"),
            ("Third-party notices", "THIRD-PARTY-NOTICES.md"),
        };

        private readonly GlassTextBox _body;

        public LegalPage(Action? onBack = null)
        {
            AutoScroll = false;   // the body scrolls, not the page
            ContentWidth = 660;

            var title = new Label
            {
                Text = "Legal & licences",
                // Without this the ampersand is eaten as a mnemonic prefix and the title
                // renders as "Legal  licences" with the L underlined.
                UseMnemonic = false,
                Font = Design.Fonts.Title,
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Location = new Point(28, 18),
                Size = new Size(400, 34),
            };
            Controls.Add(title);

            var card = new CardPanel
            {
                Location = new Point(20, 62),
                Size = new Size(660, 620),
            };

            card.Controls.Add(UiHelpers.Caption("DOCUMENT", 18, 16, 200));

            var picker = new GlassDropdown
            {
                Location = new Point(18, 38),
                Size = new Size(280, 34),
            };
            picker.SetItems(Documents.Select(d => d.Title));
            card.Controls.Add(picker);

            var back = new GlassButton
            {
                Text = "Back",
                Location = new Point(660 - 18 - 110, 38),
                Size = new Size(110, 34),
            };
            back.Click += (_, _) => onBack?.Invoke();
            card.Controls.Add(back);

            _body = new GlassTextBox
            {
                Multiline = true,
                ReadOnly = true,
                Location = new Point(18, 88),
                Size = new Size(624, 512),
            };
            _body.Inner.TabStop = false;
            // Opens un-selected. Without this the whole document arrives highlighted, which
            // reads as the page having done something rather than as text to read.
            _body.Inner.GotFocus += (_, _) => _body.Inner.Select(0, 0);
            card.Controls.Add(_body);

            picker.SelectedIndexChanged += (_, _) => Show(picker.SelectedIndex);
            Controls.Add(card);

            Show(0);
        }

        private void Show(int index)
        {
            if (index < 0 || index >= Documents.Length) index = 0;
            _body.Text = Load(Documents[index].Resource);
            _body.Inner.Select(0, 0);
        }

        /// <summary>
        /// Read one embedded document.
        ///
        /// Never throws and never returns empty: a legal page that silently shows nothing is
        /// worse than one that admits it cannot find the text, because the first looks like
        /// there are no terms at all.
        /// </summary>
        internal static string Load(string resource)
        {
            try
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
                if (stream == null)
                    return resource + " could not be loaded from this build.\r\n\r\n"
                         + "A copy is installed next to PlexusX.exe, and the current version is "
                         + "always in the repository.";

                using var reader = new StreamReader(stream);
                return Normalise(reader.ReadToEnd());
            }
            catch (Exception ex)
            {
                return resource + " could not be read: " + ex.Message;
            }
        }

        /// <summary>
        /// Markdown into something a plain text box can show.
        ///
        /// Not a renderer - it strips the handful of marks that would otherwise appear as
        /// literal punctuation ("## 3. Games" reading as hashes), and normalises line endings,
        /// because a multiline TextBox needs CRLF and the files are stored with LF.
        /// </summary>
        private static string Normalise(string markdown)
        {
            var lines = new List<string>();

            foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw;

                if (line.StartsWith("#"))
                    line = line.TrimStart('#').Trim().ToUpperInvariant();
                else if (line.StartsWith("> "))
                    line = "    " + line.Substring(2);
                else if (line.StartsWith("- "))
                    line = "  • " + line.Substring(2);

                // Bold and italic marks, which are noise without a renderer.
                line = line.Replace("**", "").Replace("`", "");

                lines.Add(line);
            }

            return string.Join("\r\n", lines);
        }
    }
}
