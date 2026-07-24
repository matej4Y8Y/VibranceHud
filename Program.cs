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
                    $"PlexusX couldn't start:\n\n{ex.Message}\n\n" +
                    "Make sure at least one monitor is connected and try again. " +
                    "If this keeps happening, please report it.",
                    "PlexusX",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
