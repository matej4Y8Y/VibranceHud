using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using VibranceHud;
using VibranceHud.Pages;
using Xunit;
using Xunit.Abstractions;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Scrolling the REAL window, not an approximation of it.
    ///
    /// A hand-built stand-in for the shell scrolled perfectly while the shipped app did not,
    /// which is the whole lesson: an approximation only tests the parts you already thought
    /// about. This builds the actual MainWindow, shows it off-screen, and scrolls the page it
    /// puts up - so anything the window does that a stand-in leaves out is inside the test.
    /// </summary>
    [Collection("Theme serial")]
    public sealed class RealShellScrollTests
    {
        private readonly ITestOutputHelper _out;
        public RealShellScrollTests(ITestOutputHelper o) => _out = o;

        private static void OnSta(Action body)
        {
            Exception? failure = null;
            var t = new Thread(() => { try { body(); } catch (Exception ex) { failure = ex; } });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            if (failure != null) throw new Exception(failure.Message, failure);
        }

        private static MainWindow BuildWindow(string scratch)
        {
            Theme.Apply("Violet");
            var settings = new AppSettings();
            var store = new SettingsStore(scratch);
            return new MainWindow(RenderHarness.StubEngine(), settings, store, null, _ => { });
        }

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }

        private static GlowPage CurrentPage(Form window) =>
            Descendants(window).OfType<GlowPage>().First();

        /// <summary>Runs <paramref name="body"/> against a real, shown MainWindow.</summary>
        private void WithWindow(Action<MainWindow, GlowPage> body)
        {
            OnSta(() =>
            {
                string scratch = Path.Combine(Path.GetTempPath(), "PxShell_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(scratch);

                MainWindow? window = null;
                try
                {
                    WheelRouter.Install();   // the shipped app installs it; so does this

                    window = BuildWindow(scratch);
                    window.StartPosition = FormStartPosition.Manual;
                    window.Location = new Point(-4000, -4000);
                    window.ShowInTaskbar = false;
                    window.Show();
                    Application.DoEvents();

                    body(window, CurrentPage(window));
                }
                finally
                {
                    window?.Close();
                    window?.Dispose();
                    try { Directory.Delete(scratch, true); } catch { }
                }
            });
        }

        /// <summary>
        /// The reported symptom, against the real window: the wheel does nothing.
        /// </summary>
        [Fact]
        public void ScrollingDownInTheRealWindowMovesThePage()
        {
            WithWindow((window, page) =>
            {
                _out.WriteLine($"page {page.GetType().Name} client {page.ClientSize} "
                    + $"extent {page.ScrollExtent} viewport {page.ScrollViewport}");

                for (int i = 0; i < 10; i++)
                {
                    page.TestScrollWheel(-120);
                    Application.DoEvents();
                }

                _out.WriteLine($"offset after ten notches: {page.ScrollOffset}");

                Assert.True(page.ScrollOffset > 0,
                    $"the page did not move (extent {page.ScrollExtent}, viewport {page.ScrollViewport})");
            });
        }

        /// <summary>
        /// The wheel, with the pointer over the shell's own scrollbar.
        ///
        /// This is the one that shipped. The wheel router sends WM_MOUSEWHEEL to whatever
        /// window is under the pointer and reports it handled either way, so a window that
        /// ignores it swallows it. While the bar lived inside the page it was hooked along
        /// with every other descendant and passed the wheel on; moving it into the shell took
        /// it out of that sweep and nothing replaced it.
        ///
        /// So the wheel died in the one place a person is most likely to be pointing at:
        /// the scrollbar itself, which is where your hand goes the moment a page has one.
        /// </summary>
        [Fact]
        public void TheWheelWorksWithThePointerOverTheScrollBar()
        {
            WithWindow((window, page) =>
            {
                var bar = Descendants(window).OfType<Controls.GlassScrollBar>().Single();

                for (int i = 0; i < 10; i++)
                {
                    WheelOver(bar, -120);
                    Application.DoEvents();
                }

                Assert.True(page.ScrollOffset > 0,
                    "the wheel does nothing when the pointer is over the scrollbar");
            });
        }

        /// <summary>Delivering a wheel event to a control the way Windows does.</summary>
        private static void WheelOver(Control target, int delta)
        {
            var onMouseWheel = typeof(Control).GetMethod("OnMouseWheel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            onMouseWheel.Invoke(target, new object[]
            {
                new MouseEventArgs(MouseButtons.None, 0, 0, 0, delta)
            });
        }

        /// <summary>The thumb has to follow the page, or the bar lies about where you are.</summary>
        [Fact]
        public void TheThumbFollowsThePage()
        {
            WithWindow((window, page) =>
            {
                var bar = Descendants(window).OfType<Controls.GlassScrollBar>().Single();

                for (int i = 0; i < 10; i++) { page.TestScrollWheel(-120); Application.DoEvents(); }

                Assert.True(bar.Visible, "the bar is not showing on a page that scrolls");
                Assert.Equal(page.ScrollOffset, bar.Value);
            });
        }

        /// <summary>
        /// One wheel notch must not set off an avalanche of layout.
        ///
        /// The page reports a scroll, the shell repositions its bar, moving the bar lays out
        /// the shell, that lays out the page, and the page reports a scroll. Nothing in that
        /// chain is wrong on its own, which is what makes it easy to build by accident - and
        /// it is the difference between a scroll that glides and one that judders.
        /// </summary>
        [Fact]
        public void OneNotchDoesNotSetOffALayoutAvalanche()
        {
            WithWindow((window, page) =>
            {
                int layouts = 0;
                page.Layout += (_, _) => layouts++;

                page.TestScrollWheel(-120);
                Application.DoEvents();

                Assert.True(layouts < 20, $"one wheel notch triggered {layouts} page layouts");
            });
        }

        /// <summary>And back up again.</summary>
        [Fact]
        public void ScrollingDownThenUpInTheRealWindowReturnsToTheTop()
        {
            WithWindow((window, page) =>
            {
                for (int i = 0; i < 20; i++) { page.TestScrollWheel(-120); Application.DoEvents(); }
                Assert.True(page.ScrollOffset > 0, "never went down");

                for (int i = 0; i < 40; i++) { page.TestScrollWheel(120); Application.DoEvents(); }

                Assert.Equal(0, page.ScrollOffset);
            });
        }
    }
}
