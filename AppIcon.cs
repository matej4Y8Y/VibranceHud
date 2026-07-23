using System.Drawing;
using System.Reflection;

namespace VibranceHud
{
    /// <summary>The embedded PlexusX app icon, loaded once for the window and tray.</summary>
    public static class AppIcon
    {
        private static Icon? _icon;

        public static Icon Value => _icon ??= Load();

        private static Icon Load()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PlexusX.ico");
            return stream != null ? new Icon(stream) : SystemIcons.Application;
        }
    }
}
