using System;
using System.Windows.Forms;

namespace VibranceHud
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
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
                    "PlexusX",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
