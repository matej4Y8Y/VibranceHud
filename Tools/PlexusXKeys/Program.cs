using System;
using System.Windows.Forms;

namespace PlexusXKeys
{
    /// <summary>
    /// PlexusX Keys - the owner's private tool.
    ///
    /// NEVER distribute this. It holds the licence signing key, and anyone with that key can
    /// mint licences the app will accept. It is a separate project from PlexusX for exactly
    /// that reason: it cannot be accidentally bundled into a release.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // PerMonitorV2, matching the app. SystemAware makes Windows bitmap-stretch the
            // whole window above 100% scaling, and stretched text is the strongest "cheap
            // tool" signal there is - which matters here, because this is the window the
            // owner looks at while taking somebody's money.
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // The tool wears the app's own theme. Before this it approximated it with four
            // hardcoded colours, which drifted the moment the palette moved.
            VibranceHud.Theme.Apply("Violet");

            Application.Run(new MainForm());
        }
    }
}
