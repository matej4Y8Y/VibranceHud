using System;
using System.Globalization;
using System.Text;
using System.Threading;
using SharpDX.DXGI;
using SharpDX.Direct3D11;

namespace VibranceHud
{
    /// <summary>What the probe found out about this machine.</summary>
    public sealed record CaptureProbe(
        bool Ran,
        double MedianRatio,
        bool ReachesCapture,
        string Note)
    {
        public static CaptureProbe Failed(string why) => new(false, 0, false, why);
    }

    /// <summary>
    /// Answers "why don't my colours show in my recording?" on the machine it's running on,
    /// by measuring rather than reasoning.
    ///
    /// Why measuring: PlexusX's colour effect currently runs through the Magnification API,
    /// which applies after the desktop is composited - so capture tools should never see it.
    /// That holds on every machine tested so far, and yet 8 of 20 testers reported their
    /// colours DID show in a screen share. One of those two things is wrong, and no amount of
    /// reading the code settles it: the behaviour depends on GPU, driver and Windows version,
    /// none of which we have on the machines that matter.
    ///
    /// So this puts the experiment in the product. It sets a heavy saturation, captures the
    /// desktop through DXGI Desktop Duplication, clears it, captures again, and compares the
    /// colour in the two frames. Repeated and compared as pairs, because the desktop itself
    /// changes between samples and a single before/after measures the wallpaper as much as
    /// the effect.
    ///
    /// IMPORTANT - what this does and does not measure:
    ///
    ///   - It measures the SOFTWARE colour matrix only. It never touches driver vibrance, so
    ///     on an NVIDIA machine whose colour is mostly driver-side it reports "no change"
    ///     while the user's recordings look correct.
    ///   - It measures DESKTOP DUPLICATION only. OBS defaults to Windows Graphics Capture,
    ///     which reads from DWM composition and does see the effect. An AMD user with no
    ///     driver path at all measured 1.000 here and records his colours in full.
    ///
    /// Those two gaps are why the 8-of-20 result below was never a mystery. Until this probes
    /// Windows Graphics Capture too, its result is evidence about one API, not a verdict.
    /// </summary>
    public static class CaptureDiagnostic
    {
        /// <summary>Pairs of on/off samples. Enough that one window opening mid-run can't
        /// move the median.</summary>
        private const int Pairs = 10;

        /// <summary>Saturation used for the test - far past anything subtle, so a real
        /// difference cannot be mistaken for noise.</summary>
        private const float TestSaturation = 3.0f;

        /// <summary>A 3x saturation roughly doubles chroma. Landing near 1.0 means the frame
        /// came back untouched; the gap between the two is wide enough not to need
        /// statistics.</summary>
        private const double ReachesThreshold = 1.4;
        private const double MissesThreshold = 1.1;

        /// <summary>
        /// Run the measurement. Disturbs the screen for a few seconds (it deliberately
        /// flashes the effect on and off), so callers must warn first.
        /// </summary>
        public static CaptureProbe Probe(ISaturationOverlay overlay)
        {
            if (overlay == null) return CaptureProbe.Failed("no overlay");

            Factory1? factory = null;
            Adapter1? adapter = null;
            SharpDX.Direct3D11.Device? device = null;
            Output? output = null;
            Output1? output1 = null;

            try
            {
                factory = new Factory1();
                adapter = factory.GetAdapter1(0);
                device = new SharpDX.Direct3D11.Device(adapter, DeviceCreationFlags.BgraSupport);
                output = adapter.GetOutput(0);
                output1 = output.QueryInterface<Output1>();

                var ratios = new double[Pairs];
                for (int i = 0; i < Pairs; i++)
                {
                    overlay.Clear();
                    Thread.Sleep(180);
                    double off = MeanChroma(device, output1);

                    overlay.Apply(SaturationMatrix.Build(TestSaturation));
                    Thread.Sleep(180);
                    double on = MeanChroma(device, output1);

                    ratios[i] = off > 0.001 ? on / off : 1;
                }

                overlay.Clear();

                Array.Sort(ratios);
                double median = (ratios[Pairs / 2 - 1] + ratios[Pairs / 2]) / 2;

                // Worded as what was actually measured - the Desktop Duplication path - rather
                // than as a verdict on capture in general.
                //
                // The old wording said "is NOT in what capture reads on this PC" and was
                // repeatedly wrong: an AMD user whose colour comes entirely from the software
                // matrix gets 1.000 here and still records it in full through OBS, because OBS
                // defaults to Windows Graphics Capture and this probe uses Desktop
                // Duplication. Reporting a measurement as a universal conclusion is how the
                // app ended up telling people a working feature was broken.
                if (median >= ReachesThreshold)
                    return new CaptureProbe(true, median, true,
                        "the colour effect IS in the desktop-duplication path (Discord screen share sees it)");
                if (median <= MissesThreshold)
                    return new CaptureProbe(true, median, false,
                        "not in the desktop-duplication path (Discord screen share won't see it; OBS still will)");

                return new CaptureProbe(true, median, false,
                    "inconclusive - the screen was changing too much while measuring");
            }
            catch (Exception ex)
            {
                return CaptureProbe.Failed(ex.GetType().Name + ": " + Short(ex.Message));
            }
            finally
            {
                output1?.Dispose();
                output?.Dispose();
                device?.Dispose();
                adapter?.Dispose();
                factory?.Dispose();
            }
        }

        /// <summary>
        /// The report the user copies over to us.
        ///
        /// Deliberately contains no name, no machine id, no licence key and no file paths -
        /// nothing that identifies a person. Everything here is about the graphics stack,
        /// because that is the only thing that decides the answer, and asking people to send
        /// a file they can't read is how you lose their trust.
        /// </summary>
        public static string BuildReport(AppSettings settings, bool driverVibrance, CaptureProbe probe)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();

            sb.AppendLine("PlexusX capture report");
            sb.AppendLine("----------------------");
            sb.AppendLine($"app          : {AppInfo.VersionText}");
            sb.AppendLine($"windows      : {Environment.OSVersion.Version} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})");
            sb.AppendLine($"gpu          : {DescribeAdapters()}");
            sb.AppendLine($"driver vibr. : {(driverVibrance ? "available (NVIDIA)" : "not available")}");
            sb.AppendLine($"display path : {(settings.OverlayMode == OverlayMode.Dx ? "DX11" : "Magnification")}");

            if (settings.OverlayMode != OverlayMode.Dx)
            {
                sb.AppendLine($"dx11 failed  : {settings.DxFailure} - {settings.DxFailureMessage}");
                if (settings.DxFailureCode != 0)
                    sb.AppendLine($"dx11 code    : 0x{settings.DxFailureCode:X8}");
            }

            sb.AppendLine($"composited   : {(settings.KeepDesktopComposited ? "forced on" : "off")}");
            sb.AppendLine($"streaming    : {(settings.StreamingMode ? "on" : "off")}");
            sb.AppendLine($"vibrance/sat : {settings.VibrancePercent} / {settings.SaturationPercent}");
            sb.AppendLine();

            if (!probe.Ran)
            {
                sb.AppendLine($"capture test : could not run ({probe.Note})");
            }
            else
            {
                sb.AppendLine($"capture test : {probe.Note}");
                sb.AppendLine($"  measured   : {probe.MedianRatio.ToString("F3", c)} " +
                              "(1.0 = capture saw no change, ~2.0 = capture saw the effect)");
            }

            return sb.ToString();
        }

        /// <summary>Every GPU DXGI can see, so a hybrid laptop is obvious in the report.</summary>
        private static string DescribeAdapters()
        {
            try
            {
                using var factory = new Factory1();
                var names = new System.Collections.Generic.List<string>();
                for (int i = 0; ; i++)
                {
                    Adapter1 a;
                    try { a = factory.GetAdapter1(i); }
                    catch (SharpDX.SharpDXException) { break; }
                    using (a)
                    {
                        var d = a.Description1;
                        // Skip Microsoft's software renderer - it's on every machine and
                        // tells nobody anything.
                        if ((d.Flags & AdapterFlags.Software) != 0) continue;
                        names.Add(d.Description.Trim());
                    }
                }
                return names.Count == 0 ? "none found" : string.Join(" + ", names);
            }
            catch (Exception ex)
            {
                return "unavailable (" + ex.GetType().Name + ")";
            }
        }

        private static string Short(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Split('\n')[0].Trim();

        /// <summary>
        /// Mean chroma of the desktop as DXGI Desktop Duplication sees it - the same source
        /// OBS Display Capture reads.
        ///
        /// The duplication is recreated per sample on purpose: AcquireNextFrame only returns
        /// a frame when the desktop has changed, so a reused one on a still desktop times out
        /// forever. The first acquire after DuplicateOutput hands back the current desktop
        /// whether anything moved or not.
        /// </summary>
        private static double MeanChroma(SharpDX.Direct3D11.Device device, Output1 output1)
        {
            using var dup = output1.DuplicateOutput(device);

            SharpDX.DXGI.Resource? res = null;
            for (int attempt = 0; attempt < 40 && res == null; attempt++)
            {
                var r = dup.TryAcquireNextFrame(250, out _, out var got);
                if (r.Success && got != null) res = got;
                else Thread.Sleep(25);
            }
            if (res == null) return 0;

            using (res)
            using (var frame = res.QueryInterface<Texture2D>())
            {
                var desc = frame.Description;
                using var copy = new Texture2D(device, new Texture2DDescription
                {
                    Width = desc.Width,
                    Height = desc.Height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = desc.Format,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Staging,
                    BindFlags = BindFlags.None,
                    CpuAccessFlags = CpuAccessFlags.Read,
                    OptionFlags = ResourceOptionFlags.None,
                });

                device.ImmediateContext.CopyResource(frame, copy);
                var box = device.ImmediateContext.MapSubresource(
                    copy, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

                double total = 0;
                int count = 0;
                var row = new byte[box.RowPitch];
                // A grid sample, not every pixel: plenty for a mean and far quicker.
                for (int y = 0; y < desc.Height; y += 4)
                {
                    System.Runtime.InteropServices.Marshal.Copy(
                        box.DataPointer + y * box.RowPitch, row, 0, box.RowPitch);

                    for (int x = 0; x < desc.Width; x += 4)
                    {
                        int i = x * 4;
                        if (i + 2 >= row.Length) break;
                        byte b = row[i], g = row[i + 1], r2 = row[i + 2];
                        int max = Math.Max(r2, Math.Max(g, b));
                        int min = Math.Min(r2, Math.Min(g, b));
                        total += max - min;
                        count++;
                    }
                }

                device.ImmediateContext.UnmapSubresource(copy, 0);
                try { dup.ReleaseFrame(); } catch { /* already gone */ }

                return count == 0 ? 0 : total / count;
            }
        }
    }
}
