using System.Windows.Forms;
using Microsoft.Win32;

namespace VibranceHud
{
    /// <summary>
    /// Toggles "launch with Windows" via the per-user Run registry key. No elevation
    /// needed - HKCU is always writable by the current user.
    /// </summary>
    public static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "PlexusX";

        public static bool IsEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) != null;
        }

        public static void SetEnabled(bool enabled)
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled)
            {
                key.SetValue(ValueName, $"\"{Application.ExecutablePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
    }
}
