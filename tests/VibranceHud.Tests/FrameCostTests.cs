using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using VibranceHud.Pages;
using Xunit;
using Xunit.Abstractions;

namespace VibranceHud.Tests
{
    /// <summary>
    /// What one animation frame actually costs.
    ///
    /// "The app feels laggy" was diagnosed once by counting controls and timing a repaint, and
    /// the answer - a frame costing most of the frame budget - drove two changes that are now
    /// load-bearing: the 50ms tick and freezing the backdrop while a mouse button is held.
    /// Neither is written down anywhere the build can check, so both are one careless commit
    /// away from being undone silently.
    ///
    /// This measures it instead. A readout, plus a ceiling loose enough to survive a slow CI
    /// box and tight enough to catch the thing actually going wrong: a page that has quietly
    /// become several times more expensive to paint.
    /// </summary>
    [Collection("Theme serial")]
    public sealed class FrameCostTests
    {
        private readonly ITestOutputHelper _out;
        public FrameCostTests(ITestOutputHelper o) => _out = o;

        private static void OnSta(Action body)
        {
            Exception? failure = null;
            var t = new Thread(() => { try { body(); } catch (Exception ex) { failure = ex; } });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            if (failure != null) throw new Exception(failure.Message, failure);
        }

        private static int CountDescendants(Control root) =>
            root.Controls.Cast<Control>().Sum(c => 1 + CountDescendants(c));

        private static int CountTransparent(Control root) =>
            root.Controls.Cast<Control>()
                .Sum(c => (c.BackColor == Color.Transparent ? 1 : 0) + CountTransparent(c));

        [Fact]
        public void AFrameFitsInsideTheBudget()
        {
            OnSta(() =>
            {
                string scratch = Path.Combine(Path.GetTempPath(), "PxFrame_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(scratch);

                try
                {
                    Theme.Apply("Violet");
                    using var page = RenderHarness.BuildDisplay();
                    page.Size = new Size(830, 628);

                    using var host = new Form
                    {
                        FormBorderStyle = FormBorderStyle.None,
                        ShowInTaskbar = false,
                        StartPosition = FormStartPosition.Manual,
                        Location = new Point(-4000, -4000),
                        Size = page.Size,
                    };
                    host.Controls.Add(page);
                    host.Show();
                    Application.DoEvents();

                    // Warm: the first paint pays for font realisation and brush creation that
                    // no later frame pays again, and counting it would flatter or damn the
                    // measurement depending only on how cold the process was.
                    for (int i = 0; i < 5; i++) { page.Invalidate(true); page.Update(); }

                    const int frames = 30;
                    var sw = Stopwatch.StartNew();
                    for (int i = 0; i < frames; i++) { page.Invalidate(true); page.Update(); }
                    sw.Stop();

                    double perFrame = sw.Elapsed.TotalMilliseconds / frames;

                    _out.WriteLine($"controls      : {CountDescendants(page)}");
                    _out.WriteLine($"transparent   : {CountTransparent(page)}");
                    _out.WriteLine($"per frame     : {perFrame:F1} ms");
                    _out.WriteLine($"tick interval : 50 ms");
                    _out.WriteLine($"budget used   : {perFrame / 50.0 * 100:F0}%");

                    host.Controls.Remove(page);
                    host.Close();

                    Assert.True(perFrame < 50.0,
                        $"one frame costs {perFrame:F1}ms against a 50ms tick - the backdrop "
                        + "alone cannot keep up, so everything else is already late");
                }
                finally { try { Directory.Delete(scratch, true); } catch { } }
            });
        }
    }
}
