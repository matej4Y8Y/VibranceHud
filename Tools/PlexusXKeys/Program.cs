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
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
