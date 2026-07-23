using System;
using System.Windows.Forms;
using Velopack;

namespace VibranceHud
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // Must be the very first thing that runs: during an install/update Velopack
            // launches the app with special hooks, handles them, and exits before any
            // UI appears. On a normal launch it does nothing and returns immediately.
            VelopackApp.Build().Run();

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
                    $"Vibrance HUD couldn't start:\n\n{ex.Message}\n\n" +
                    "Make sure you're on an NVIDIA GPU with the driver installed, " +
                    "and that at least one monitor is connected to it.",
                    "Vibrance HUD",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
