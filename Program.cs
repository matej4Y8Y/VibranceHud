using System;
using System.Windows.Forms;
using VibranceHud.SystemTweaks;

namespace VibranceHud
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // Elevated relaunch to apply one admin-only FPS tweak, then exit - no UI, no tray.
            if (SystemTweakService.IsHeadlessTweakInvocation(args))
                return SystemTweakService.RunHeadless(args);

            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                Application.Run(new TrayApplicationContext());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"PlexusX couldn't start:\n\n{ex.Message}\n\n" +
                    "Make sure at least one monitor is connected and try again. " +
                    "If this keeps happening, please report it.",
                    "PlexusX",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            return 0;
        }
    }
}
