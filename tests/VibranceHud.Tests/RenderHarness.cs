using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using VibranceHud.Crosshair;
using VibranceHud.Games;
using VibranceHud.License;
using VibranceHud.Pages;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Renders real pages to PNG so a visual change can actually be looked at.
    ///
    /// This is a permanent file because it has already been written from scratch three times,
    /// and each time the same two facts had to be rediscovered:
    ///
    ///   - DrawToBitmap sends WM_PRINT, which a control with no realised handle cannot answer.
    ///     A TextBox captures as blank space unless the page is parented to a Form that has
    ///     genuinely been shown. CreateControl() on its own is not enough, and the failure is
    ///     silent - you get a picture with a hole in it and no error.
    ///
    ///   - Showing a window requires STA, and the xUnit worker thread is MTA.
    ///
    /// The host Form is parked at -4000,-4000 so nothing ever appears on the user's screen.
    /// An earlier version of this resized the user's real app window and persisted it; this one
    /// never touches anything outside its own process.
    /// </summary>
    internal static class RenderHarness
    {
        /// <summary>The shell's pages. Kept in step with MainWindow's navigation.</summary>
        internal static readonly string[] PageNames =
        {
            "Display", "Monitor", "Crosshair", "Settings", "Account",
        };

        /// <summary>Render every page into <paramref name="outputDirectory"/> as &lt;name&gt;.png.</summary>
        internal static void ShootAllPages(string outputDirectory)
        {
            OnSta(() =>
            {
                Directory.CreateDirectory(outputDirectory);
                foreach (var name in PageNames)
                {
                    string scratch = Path.Combine(Path.GetTempPath(), "PxShot_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(scratch);
                    try
                    {
                        var page = BuildPage(name, scratch);
                        ShootShown(page, Path.Combine(outputDirectory, name + ".png"));
                        page.Dispose();
                    }
                    finally { try { Directory.Delete(scratch, true); } catch { } }
                }
            });
        }

        /// <summary>Render one control. The factory runs on the STA thread, not the caller's.</summary>
        internal static void Shoot(Func<Control> make, string path) =>
            OnSta(() =>
            {
                var c = make();
                ShootShown(c, path);
                c.Dispose();
            });

        private static void ShootShown(Control content, string path)
        {
            if (content.Width <= 0 || content.Height <= 0) content.Size = new Size(900, 760);

            content.CreateControl();
            foreach (var c in Descendants(content)) c.CreateControl();

            using var host = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-4000, -4000),
                Size = content.Size,
                BackColor = Theme.Background,
            };

            host.Controls.Add(content);
            host.Show();
            Application.DoEvents();

            using (var bmp = new Bitmap(content.Width, content.Height))
            {
                content.DrawToBitmap(bmp, new Rectangle(0, 0, content.Width, content.Height));
                bmp.Save(path);
            }

            // Detached before the host closes, so the caller still owns the control's lifetime.
            host.Controls.Remove(content);
            host.Close();
        }

        private static GlowPage BuildPage(string name, string scratch)
        {
            Theme.Apply("Violet");

            var settings = new AppSettings();
            var store = new SettingsStore(scratch);
            var selection = new GameSelection(settings, store);

            GlowPage page = name switch
            {
                "Display" => new VibrancePage(
                    new VibranceEngine(new StubController(), new StubOverlay(), new StubGamma()), settings, store),
                "Monitor" => new MonitorPage(settings, store, selection),
                "Crosshair" => new CrosshairPage(settings, store, new CrosshairService()),
                "Settings" => new SettingsPage(settings, store, _ => { }, _ => { }),
                "Account" => new AccountPage(new LicenseService(scratch)),
                _ => throw new ArgumentException("unknown page: " + name, nameof(name)),
            };

            page.Size = new Size(900, 760);
            return page;
        }

        /// <summary>Run on an STA thread and rethrow anything it threw on the caller's.</summary>
        private static void OnSta(Action body)
        {
            Exception? failure = null;
            var t = new Thread(() => { try { body(); } catch (Exception ex) { failure = ex; } });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            if (failure != null) throw failure;
        }

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }

        private sealed class StubController : IVibranceController
        {
            public int CurrentLevel { get; set; } = 50;
            public int DefaultLevel => 50;
            public bool IsAvailable => true;
            public void SetLevel(int level) => CurrentLevel = level;
        }

        private sealed class StubOverlay : ISaturationOverlay
        {
            public void Apply(float[] matrix) { }
            public void Clear() { }
        }

        private sealed class StubGamma : IGammaRamp
        {
            public bool IsAvailable => true;
            public void Apply(ushort[] ramp) { }
            public void Reset() { }
        }
    }

    /// <summary>
    /// A page that renders blank is a silent failure - DrawToBitmap returns happily and you get
    /// a picture with a hole in it. This is the guard against that, and against a page that
    /// throws only once something actually paints it.
    /// </summary>
    [Collection("Theme serial")]
    public sealed class RenderHarnessTests
    {
        [Fact]
        public void EveryPageRendersToANonEmptyImage()
        {
            string dir = Path.Combine(Path.GetTempPath(), "PxShots_" + Guid.NewGuid().ToString("N"));
            try
            {
                RenderHarness.ShootAllPages(dir);

                foreach (var name in RenderHarness.PageNames)
                {
                    string file = Path.Combine(dir, name + ".png");
                    Assert.True(File.Exists(file), name + " did not render at all");
                    Assert.True(new FileInfo(file).Length > 5000,
                        $"{name} rendered essentially blank ({new FileInfo(file).Length} bytes)");
                }
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }
    }
}
