using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace VibranceHud.Rust
{
    /// <summary>
    /// System-level boosts applied around launching Rust:
    ///   - Auto High CPU Priority: raises RustClient's scheduling priority once it starts.
    ///   - GC buffer: Rust's real <c>gc.buffer</c> convar, sized from the PC's RAM.
    ///   - RAM cleaner: trims *this launcher's* working set so it stops holding memory
    ///     while you play. (That's all "free launcher memory" can honestly mean - it does
    ///     not speed the game up.)
    /// </summary>
    public static class RustSystemBoost
    {
        [DllImport("psapi.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        private const string RustProcess = "RustClient";

        /// <summary>RAM tiers and the gc.buffer (MB) each one maps to.</summary>
        public static readonly (string Label, int Gb, int GcBuffer)[] RamTiers =
        {
            ("8 GB", 8, 1024),
            ("16 GB", 16, 2048),
            ("32 GB+", 32, 4096),
        };

        /// <summary>The tier whose gc.buffer is closest to what the config already has.</summary>
        public static int TierIndexForBuffer(int gcBuffer)
        {
            int best = RamTiers.Length - 1, bestDelta = int.MaxValue;
            for (int i = 0; i < RamTiers.Length; i++)
            {
                int delta = Math.Abs(RamTiers[i].GcBuffer - gcBuffer);
                if (delta < bestDelta) { bestDelta = delta; best = i; }
            }
            return best;
        }

        /// <summary>Release the launcher's working set back to Windows.</summary>
        public static void TrimLauncherMemory()
        {
            try { EmptyWorkingSet(Process.GetCurrentProcess().Handle); }
            catch { /* best effort only */ }
        }

        /// <summary>
        /// Watch for Rust to appear and raise it to High priority. Fire-and-forget; gives
        /// up quietly after the timeout, or if Windows refuses (the game may be protected).
        /// </summary>
        public static void RaisePriorityWhenRustStarts(TimeSpan timeout)
        {
            _ = Task.Run(async () =>
            {
                var deadline = DateTime.UtcNow + timeout;
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        foreach (var p in Process.GetProcessesByName(RustProcess))
                        {
                            p.PriorityClass = ProcessPriorityClass.High;
                            return;
                        }
                    }
                    catch { return; } // access denied - nothing more we can do
                    await Task.Delay(2000);
                }
            });
        }
    }
}
