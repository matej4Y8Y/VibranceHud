using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using VibranceHud;
using VibranceHud.Crosshair;
using VibranceHud.Games;
using VibranceHud.License;
using VibranceHud.Pages;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Builds every page in the app and audits its geometry.
    ///
    /// Written after the same handful of checks - overlap, clipping, text that does not fit
    /// its box - found real defects nobody had spotted by looking: a Reset button painted
    /// underneath its own label, a page title with its descenders sliced off, an Outline
    /// label sitting on a slider track, and a whole SAVED section landing on top of the
    /// colour swatches.
    ///
    /// Every one of those is invisible in code review and obvious on screen. Checking them
    /// per page, automatically, is the only way this stays true as pages keep changing.
    /// </summary>
    [Collection("Theme serial")]
    public sealed class AllPagesLayoutAuditTests : IDisposable
    {
        private readonly TempDirectory _temp = new();

        public void Dispose() => _temp.Dispose();

        public static IEnumerable<object[]> Pages()
        {
            yield return new object[] { "Display" };
            yield return new object[] { "Monitor" };
            yield return new object[] { "Crosshair" };
            yield return new object[] { "Settings" };
            yield return new object[] { "Account" };
        }

        // ---- the audits ------------------------------------------------------------------

        /// <summary>
        /// Nothing may sit on top of anything else inside the same container.
        ///
        /// A couple of pixels of touching is normal for adjacent rows; a real overlap covers
        /// meaningful area in both directions.
        /// </summary>
        [Theory]
        [MemberData(nameof(Pages))]
        public void NothingOverlapsAnythingElse(string name) => Audit(name, page =>
        {
            foreach (var parent in Containers(page))
            {
                var kids = parent.Controls.Cast<Control>()
                    .Where(c => c.Visible && c.Width > 0 && c.Height > 0)
                    .ToList();

                for (int i = 0; i < kids.Count; i++)
                    for (int j = i + 1; j < kids.Count; j++)
                    {
                        var a = kids[i].Bounds;
                        a.Intersect(kids[j].Bounds);

                        Assert.False(a.Width > 2 && a.Height > 2,
                            $"{name}: '{Describe(kids[i])}' overlaps '{Describe(kids[j])}' " +
                            $"by {a.Width}x{a.Height}");
                    }
            }
        });

        /// <summary>
        /// A child cannot render past its immediate parent, whatever the page's scrolling
        /// says. A card too short for its own contents silently clips its last row.
        /// </summary>
        [Theory]
        [MemberData(nameof(Pages))]
        public void NothingIsClippedByItsOwnCard(string name) => Audit(name, page =>
        {
            foreach (var card in Descendants(page).OfType<CardPanel>())
                foreach (Control child in card.Controls)
                {
                    if (!child.Visible || child.Width <= 0) continue;

                    Assert.True(child.Bottom <= card.Height + 2,
                        $"{name}: '{Describe(child)}' runs {child.Bottom - card.Height}px " +
                        $"past the bottom of its card");

                    Assert.True(child.Right <= card.Width + 2,
                        $"{name}: '{Describe(child)}' runs {child.Right - card.Width}px " +
                        $"past the right edge of its card");
                }
        });

        /// <summary>
        /// Text has to fit the box it was given. This is how descenders get sliced off: the
        /// height is a number somebody picked for whatever font the label had at the time, and
        /// it goes quietly wrong the moment the type scale changes.
        /// </summary>
        [Theory]
        [MemberData(nameof(Pages))]
        public void NoLabelIsShorterThanItsOwnText(string name) => Audit(name, page =>
        {
            foreach (var label in Descendants(page).OfType<Label>())
            {
                if (!label.Visible || label.AutoSize) continue;
                if (string.IsNullOrWhiteSpace(label.Text)) continue;

                // Taller boxes are wrapping labels, sized for their content rather than for
                // one line of it.
                if (label.Height >= 30) continue;

                int needed = TextRenderer.MeasureText(label.Text, label.Font).Height;

                Assert.True(label.Height >= needed,
                    $"{name}: label '{Trim(label.Text)}' is {label.Height}px tall but its " +
                    $"{label.Font.Size}pt font needs {needed}px - text will be clipped");
            }
        });

        [Theory]
        [MemberData(nameof(Pages))]
        public void NoControlCollapsedToNothing(string name) => Audit(name, page =>
        {
            foreach (var c in Descendants(page))
            {
                if (!c.Visible) continue;

                // An auto-sizing label with nothing in it is legitimately zero-wide - status
                // lines start empty and get their text later. Only a control that was given
                // a size and came out with none is a defect.
                if (c is Label { AutoSize: true } && string.IsNullOrEmpty(c.Text)) continue;

                Assert.True(c.Width > 0 && c.Height > 0,
                    $"{name}: {Describe(c)} collapsed to {c.Width}x{c.Height}");
            }
        });

        /// <summary>A scrolling page must declare an extent that covers its content, or the
        /// bottom of the page is unreachable.</summary>
        [Theory]
        [MemberData(nameof(Pages))]
        public void AScrollingPageCanReachItsOwnContent(string name) => Audit(name, page =>
        {
            if (!page.AutoScroll) return;

            int lowest = page.Controls.Cast<Control>()
                .Where(c => c.Visible)
                .Select(c => c.Bottom)
                .DefaultIfEmpty(0)
                .Max();

            int reach = Math.Max(page.AutoScrollMinSize.Height, page.Height);

            Assert.True(reach >= lowest - 4,
                $"{name}: content reaches {lowest}px but the page can only scroll to {reach}px");
        });

        // ---- harness ---------------------------------------------------------------------

        /// <summary>
        /// Build the page and run an audit against it, on a real STA thread.
        ///
        /// xUnit runs tests on MTA thread-pool threads. Any control that sets AllowDrop - the
        /// Keybinds keyboard does, because commands are dragged onto it - calls
        /// RegisterDragDrop when its handle is created, and OLE refuses that outside STA. The
        /// result is a WinForms exception dialog rather than a test failure, so it hangs the
        /// run instead of reporting anything.
        ///
        /// Nothing about the app is wrong there; the harness just has to match the apartment
        /// the app actually runs in.
        /// </summary>
        private void Audit(string name, Action<GlowPage> check)
        {
            Exception? failure = null;

            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    using var page = Build(name);
                    check(page);
                }
                catch (Exception ex) { failure = ex; }
            });

            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            // Bounded: a page that never finishes constructing is itself a defect worth
            // failing on rather than hanging the suite.
            if (!thread.Join(TimeSpan.FromSeconds(30)))
                throw new TimeoutException($"{name}: page did not finish building within 30s");

            if (failure != null) throw failure;
        }

        private GlowPage Build(string name)
        {
            Theme.Apply("Violet");

            var settings = new AppSettings();
            var store = new SettingsStore(_temp.Path);
            var selection = new GameSelection(settings, store);

            GlowPage page = name switch
            {
                "Display" => new VibrancePage(
                    new VibranceEngine(new Controller(), new Overlay(), new Gamma()), settings, store),
                "Monitor" => new MonitorPage(settings, store, selection),
                "Crosshair" => new CrosshairPage(settings, store, new CrosshairService()),
                // Audio passed so the "Loud footsteps" card is built and therefore audited.
                "Settings" => new SettingsPage(settings, store, _ => { }, _ => { },
                    audio: new Audio.AudioEdgeService(new SilentOutput())),
                "Account" => new AccountPage(new LicenseService(_temp.Path)),
                _ => new AccountPage(new LicenseService(_temp.Path)),
            };

            page.Size = new Size(900, 700);
            page.CreateControl();
            return page;
        }

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

        private static string Trim(string s) =>
            s.Length <= 40 ? s.Trim() : s.Substring(0, 37).Trim() + "…";

        private static string Describe(Control c) =>
            (string.IsNullOrWhiteSpace(c.Text) ? c.GetType().Name : Trim(c.Text)) + " " + c.Bounds;

        /// <summary>Never peaks, so the limiter never moves anything during a layout audit.</summary>
        private sealed class SilentOutput : Audio.IAudioOutput
        {
            public float Peak => 0f;
            public float Volume { get; set; } = 1f;
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
                System.IO.Path.GetTempPath(), "PlexusXAudit_" + Guid.NewGuid().ToString("N"));

            public void Dispose()
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, true);
            }
        }
    }
}
