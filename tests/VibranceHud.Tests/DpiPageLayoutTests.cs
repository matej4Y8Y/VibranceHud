using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using VibranceHud;
using VibranceHud.Design;
using VibranceHud.Pages;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Lays out real pages at every scale factor people actually run and checks the geometry.
    ///
    /// The DPI conversion is the largest and riskiest change in the 1.0 work: it touches every
    /// page, and "mostly converted" still looks broken. Verifying it by changing Windows'
    /// display scaling and squinting does not scale to a dozen pages and does not survive
    /// somebody editing a page six months from now.
    ///
    /// So instead of eyeballing, drive Tokens.Dpi directly, run the page's own layout pass,
    /// and assert the two things that actually go wrong: controls landing on top of each
    /// other, and controls escaping the panel that owns them.
    /// </summary>
    [Collection("Theme serial")]
    public sealed class DpiPageLayoutTests : IDisposable
    {
        private readonly int _originalDpi = Tokens.Dpi;

        public void Dispose()
        {
            // Tokens.Dpi is global. Leaving it at 192 would silently corrupt every later
            // test in the run, so it is always put back.
            Tokens.Dpi = _originalDpi;
            Fonts.Rebuild();
        }

        public static IEnumerable<object[]> ScaleFactors => new[]
        {
            new object[] { 96 },    // 100%
            new object[] { 120 },   // 125%
            new object[] { 144 },   // 150%
            new object[] { 192 },   // 200%
        };

        [Theory]
        [MemberData(nameof(ScaleFactors))]
        public void DisplayPageChildrenNeverOverlapAtAnyScale(int dpi)
        {
            using var temp = new TempDirectory();
            using var page = LaidOutPage(temp.Path, dpi);

            foreach (var parent in Containers(page))
            {
                var kids = parent.Controls.Cast<Control>()
                    .Where(c => c.Visible && c.Width > 0 && c.Height > 0)
                    .ToList();

                for (int i = 0; i < kids.Count; i++)
                    for (int j = i + 1; j < kids.Count; j++)
                    {
                        var a = kids[i].Bounds;
                        var b = kids[j].Bounds;
                        a.Intersect(b);

                        // A couple of pixels of touching is normal for adjacent rows; a real
                        // overlap covers meaningful area in both directions.
                        Assert.False(a.Width > 2 && a.Height > 2,
                            $"at {dpi} DPI, {Describe(kids[i])} overlaps {Describe(kids[j])} by {a.Width}x{a.Height}");
                    }
            }
        }

        [Theory]
        [MemberData(nameof(ScaleFactors))]
        public void DisplayPageChildrenStayInsideTheirParentAtAnyScale(int dpi)
        {
            using var temp = new TempDirectory();
            using var page = LaidOutPage(temp.Path, dpi);

            foreach (var parent in Containers(page))
            {
                // The page itself scrolls, so vertical overflow there is expected and correct.
                if (ReferenceEquals(parent, page)) continue;

                foreach (Control child in parent.Controls)
                {
                    if (!child.Visible || child.Width <= 0) continue;

                    Assert.True(child.Right <= parent.ClientSize.Width + 2,
                        $"at {dpi} DPI, {Describe(child)} runs {child.Right - parent.ClientSize.Width}px " +
                        $"past the right edge of {parent.GetType().Name}");

                    Assert.True(child.Bottom <= parent.ClientSize.Height + 2,
                        $"at {dpi} DPI, {Describe(child)} runs {child.Bottom - parent.ClientSize.Height}px " +
                        $"past the bottom of {parent.GetType().Name}");

                    Assert.True(child.Left >= -2,
                        $"at {dpi} DPI, {Describe(child)} starts at x={child.Left}, outside its parent");
                }
            }
        }

        [Theory]
        [MemberData(nameof(ScaleFactors))]
        public void EveryControlHasARealSizeAtAnyScale(int dpi)
        {
            using var temp = new TempDirectory();
            using var page = LaidOutPage(temp.Path, dpi);

            foreach (var c in Descendants(page))
            {
                if (!c.Visible) continue;
                Assert.True(c.Width > 0 && c.Height > 0,
                    $"at {dpi} DPI, {Describe(c)} collapsed to {c.Width}x{c.Height}");
            }
        }

        /// <summary>
        /// Every label must be tall enough for the font it was given.
        ///
        /// This is how text silently loses its descenders - the 'p' and 'y' in "Display"
        /// getting sliced off - and it is invisible in code review because the height is a
        /// number chosen for whatever font size the label had at the time. Change the type
        /// scale and every one of those numbers is quietly wrong.
        /// </summary>
        [Theory]
        [MemberData(nameof(ScaleFactors))]
        public void NoLabelIsShorterThanItsOwnText(int dpi)
        {
            using var temp = new TempDirectory();
            using var page = LaidOutPage(temp.Path, dpi);

            foreach (var label in Descendants(page).OfType<Label>())
            {
                if (!label.Visible || label.AutoSize || string.IsNullOrWhiteSpace(label.Text)) continue;

                int needed = TextRenderer.MeasureText(label.Text, label.Font).Height;

                Assert.True(label.Height >= needed,
                    $"at {dpi} DPI, label '{label.Text.Trim()}' is {label.Height}px tall but its " +
                    $"{label.Font.Size}pt font needs {needed}px - text will be clipped");
            }
        }

        /// <summary>
        /// The card holds the page's content and sizes itself to where that content finishes.
        /// If it stops growing with DPI, everything below the fold gets clipped - the exact
        /// failure a fixed pixel height would produce.
        /// </summary>
        [Fact]
        public void TheContentCardGrowsWithScale()
        {
            using var temp = new TempDirectory();

            int At(int dpi)
            {
                using var page = LaidOutPage(temp.Path, dpi);
                return Descendants(page).OfType<CardPanel>().First().Height;
            }

            int small = At(96);
            int large = At(192);

            Assert.True(large > small * 1.5,
                $"card was {small}px at 100% but only {large}px at 200% - it is not scaling");
        }

        // ---- helpers ----------------------------------------------------------------

        private static VibrancePage LaidOutPage(string directory, int dpi)
        {
            Theme.Apply("Violet");
            Tokens.Dpi = dpi;
            Fonts.Rebuild();

            var engine = new VibranceEngine(new Controller(), new Overlay(), new Gamma());
            var page = new VibrancePage(engine, new AppSettings(), new SettingsStore(directory));

            // A window this size at 100% is the app's default content area; scaling it with
            // DPI is what a real window does, so the page gets proportionally the same room.
            page.Size = new Size(Tokens.ScaleAt(dpi, 830), Tokens.ScaleAt(dpi, 628));
            page.CreateControl();
            InvokeLayout(page);

            return page;
        }

        private static void InvokeLayout(VibrancePage page) =>
            typeof(VibrancePage).GetMethod("LayoutContent", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(page, null);

        private static IEnumerable<Control> Containers(Control root) =>
            new[] { root }.Concat(Descendants(root).Where(c => c.Controls.Count > 1));

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }

        private static string Describe(Control c)
        {
            string label = c switch
            {
                PresetChip chip => $"chip '{chip.Caption}'",
                Label l when !string.IsNullOrWhiteSpace(l.Text) => $"label '{l.Text.Trim()}'",
                _ when !string.IsNullOrWhiteSpace(c.Text) => $"{c.GetType().Name} '{c.Text.Trim()}'",
                _ => c.GetType().Name,
            };
            return $"{label} {c.Bounds}";
        }

        private sealed class Controller : IVibranceController
        {
            public int CurrentLevel { get; set; } = 50;
            public int DefaultLevel => 50;
            public bool IsAvailable => true;
            public void SetLevel(int level) => CurrentLevel = level;
        }

        private sealed class Overlay : ISaturationOverlay
        {
            public void Apply(float[] matrix) { }
            public void Clear() { }
        }

        private sealed class Gamma : IGammaRamp
        {
            public void Apply(ushort[] ramp) { }
            public void Reset() { }
        }

        private sealed class TempDirectory : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "PlexusXDpi_" + Guid.NewGuid().ToString("N"));

            public void Dispose()
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, true);
            }
        }
    }
}
