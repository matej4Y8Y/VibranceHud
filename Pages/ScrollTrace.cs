using System;
using System.Globalization;

namespace VibranceHud.Pages
{
    /// <summary>
    /// A trace of what scrolling actually did, written only when PLEXUSX_SCROLL_TRACE is set.
    ///
    /// Scrolling has now been "fixed" three times, and each round of reasoning about it was
    /// wrong while the first measurement was right. The reason is that scrolling depends on
    /// things a test cannot see from here: the machine's DPI, the window's real size, the
    /// user's saved settings. A test builds a page at 100% on a default profile and it
    /// scrolls; the shipped app runs at 150% on a saved one and it does not.
    ///
    /// So the app can now say what it did, on the machine where it went wrong, instead of
    /// being guessed at from a clean one. Off unless the variable is set, because a wheel
    /// event should not touch the disk during normal use.
    /// </summary>
    internal static class ScrollTrace
    {
        private static readonly bool Enabled =
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLEXUSX_SCROLL_TRACE"));

        private static readonly string Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plexusx-scroll.log");

        private static readonly object Gate = new();

        /// <summary>The message is built by a callback so that nothing is formatted, and no
        /// string is allocated, when tracing is off.</summary>
        public static void Write(Func<string> message)
        {
            if (!Enabled) return;

            try
            {
                string line = string.Format(CultureInfo.InvariantCulture, "{0:HH:mm:ss.fff} {1}",
                    DateTime.Now, message());

                lock (Gate) System.IO.File.AppendAllText(Path, line + Environment.NewLine);
            }
            catch
            {
                // Diagnostics must never be the thing that breaks the app. A locked or full
                // disk is not a reason to stop scrolling.
            }
        }
    }
}
