using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The design rules, as tests.
    ///
    /// Written because "this doesn't match the theme" kept arriving as a message rather than as
    /// a build failure. A stock Win32 control cannot be themed - a flat Button keeps square
    /// corners and draws the system focus rectangle no matter which colours it is given - so the
    /// only rule that holds is that it must not be constructed at all outside the one place
    /// that wraps it.
    ///
    /// Every rule here exists because of a real defect, named in its own comment. A rule with a
    /// story behind it does not get argued with.
    /// </summary>
    public sealed class UiContractTests
    {
        /// <summary>Repo root, found by walking up from the test binary until the csproj appears.</summary>
        internal static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "VibranceHud.csproj")))
                dir = dir.Parent;
            Assert.True(dir != null, "could not find the repo root from " + AppContext.BaseDirectory);
            return dir!.FullName;
        }

        /// <summary>Every source file that ships to a user. Tests, docs and build output excluded.</summary>
        internal static IEnumerable<(string Path, string Text)> ShippingSources()
        {
            string root = RepoRoot();
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (rel.StartsWith("tests/") || rel.StartsWith("docs/")
                    || rel.Contains("/bin/") || rel.Contains("/obj/")
                    || rel.StartsWith("bin/") || rel.StartsWith("obj/"))
                    continue;
                yield return (rel, File.ReadAllText(file));
            }
        }

        /// <summary>The controls that carry Win32 chrome the app cannot paint over.</summary>
        private static readonly string[] Banned =
        {
            "Button", "TextBox", "ComboBox", "CheckBox", "RadioButton",
            "LinkLabel", "NumericUpDown", "TrackBar", "GroupBox", "ListBox", "TabControl",
        };

        /// <summary>
        /// The files allowed to touch them: the wrappers themselves. GlassTextBox hosts a real
        /// TextBox on purpose - caret placement, selection, IME, undo and clipboard are not
        /// worth reimplementing to change a border - and that exception lives in one file.
        /// </summary>
        private static bool IsWrapper(string rel) =>
            rel.StartsWith("Controls/") || rel == "NavButton.cs" || rel == "SwatchButton.cs";

        [Fact(Skip = "Un-skipped at the end of Phase 3. 8 known offenders - see docs/OVERNIGHT-LOG.md.")]
        public void NoStockWin32ControlIsConstructedOutsideTheWrappers()
        {
            var offences = new List<string>();

            foreach (var (rel, text) in ShippingSources())
            {
                if (IsWrapper(rel)) continue;

                foreach (var control in Banned)
                {
                    // "new Button {" / "new Button(" - but never "new GlassButton" or "new ButtonX".
                    var rx = new Regex(@"(?<![\w.])new\s+" + control + @"\s*[({]");
                    foreach (Match m in rx.Matches(text))
                    {
                        int line = text.Take(m.Index).Count(c => c == '\n') + 1;
                        offences.Add($"{rel}:{line} constructs a stock {control}");
                    }
                }
            }

            Assert.True(offences.Count == 0,
                "Stock Win32 controls cannot be themed - use the Glass* equivalent:\n  "
                + string.Join("\n  ", offences));
        }

        /// <summary>
        /// SystemColors follows whatever the OS is set to, not the app's palette. One of these
        /// on a glass card is a light-grey rectangle in the middle of a dark window.
        /// </summary>
        [Fact]
        public void NoSystemColoursAnywhere()
        {
            var offences = new List<string>();
            foreach (var (rel, text) in ShippingSources())
                foreach (Match m in Regex.Matches(text, @"SystemColors\.\w+"))
                {
                    int line = text.Take(m.Index).Count(c => c == '\n') + 1;
                    offences.Add($"{rel}:{line} {m.Value}");
                }

            Assert.True(offences.Count == 0,
                "SystemColors follows the OS theme, not ours:\n  " + string.Join("\n  ", offences));
        }

        /// <summary>
        /// Fonts are cached in Design/Fonts because these are allocated inside OnPaint, which
        /// runs about thirty times a second per control while the plexus animates behind it.
        /// An explicit monospace face is the one legitimate exception - codes and hex values
        /// need fixed-width digits to be comparable by eye.
        /// </summary>
        [Fact(Skip = "Un-skipped at the end of Phase 3. 89 sites, most inside pages Phase 2 deletes.")]
        public void FontsComeFromTheDesignLayer()
        {
            var offences = new List<string>();
            foreach (var (rel, text) in ShippingSources())
            {
                if (rel.StartsWith("Design/")) continue;
                foreach (Match m in Regex.Matches(text, @"new\s+Font\(\s*Theme\.FontFamily"))
                {
                    int line = text.Take(m.Index).Count(c => c == '\n') + 1;
                    offences.Add($"{rel}:{line}");
                }
            }

            Assert.True(offences.Count == 0,
                "Use Design.Fonts.* - these are allocated per repaint:\n  "
                + string.Join("\n  ", offences));
        }

        /// <summary>
        /// UTF-8 read back as CP1250 leaves these sequences behind. Two of them shipped as
        /// visible button labels - "Choose imageâ€¦" and "Measuringâ€¦" sat in Settings for
        /// weeks - so a grep is a great deal cheaper than noticing by eye.
        /// </summary>
        [Fact]
        public void NoMojibakeInAnySource()
        {
            var offences = new List<string>();
            foreach (var (rel, text) in ShippingSources())
                foreach (var bad in new[] { "â€", "Ã¢", "â–", "Ã©", "Ã¼" })
                    if (text.Contains(bad)) offences.Add($"{rel} contains '{bad}'");

            Assert.True(offences.Count == 0, "Encoding damage:\n  " + string.Join("\n  ", offences));
        }

        /// <summary>
        /// The UI is English throughout. A composite format string picks up the machine's
        /// locale, so gamma rendered as "1,00" on a Czech Windows - the only number on the page
        /// with a decimal, in notation the rest of the interface never uses.
        /// </summary>
        [Fact]
        public void DecimalFormattingIsCultureIndependent()
        {
            var offences = new List<string>();
            foreach (var (rel, text) in ShippingSources())
            {
                foreach (Match m in Regex.Matches(text, @"[:(]\s*""0\.0+""|:0\.0+[}]"))
                {
                    int line = text.Take(m.Index).Count(c => c == '\n') + 1;
                    string window = text.Substring(m.Index, Math.Min(160, text.Length - m.Index));
                    if (window.Contains("InvariantCulture")) continue;
                    offences.Add($"{rel}:{line}");
                }
            }

            Assert.True(offences.Count == 0,
                "Pass CultureInfo.InvariantCulture - this renders '1,00' on a Czech Windows:\n  "
                + string.Join("\n  ", offences));
        }
    }
}
