using System;
using System.Runtime.InteropServices;

namespace VibranceHud.Crosshair
{
    /// <summary>
    /// Owns the overlay window: show, hide, and re-push it above whatever the game does.
    ///
    /// The window is created lazily so a user who never turns the crosshair on never pays
    /// for it, and it is always torn down on dispose so nothing is left floating on screen.
    /// </summary>
    public sealed class CrosshairService : IDisposable
    {
        /// <summary>The shell reports this when an exclusive-fullscreen D3D app is running -
        /// one call, instead of polling window rectangles and guessing.</summary>
        private const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;

        [DllImport("shell32.dll")]
        private static extern int SHQueryUserNotificationState(out int state);

        private CrosshairWindow? _window;

        public bool IsVisible { get; private set; }

        public CrosshairConfig Config { get; private set; } = new();

        /// <summary>
        /// True when a game is running in true exclusive fullscreen, where no overlay can
        /// draw. The UI uses this to offer switching the game to borderless rather than
        /// letting the feature look broken.
        /// </summary>
        public static bool IsExclusiveFullscreen()
        {
            try
            {
                return SHQueryUserNotificationState(out int state) == 0
                       && state == QUNS_RUNNING_D3D_FULL_SCREEN;
            }
            catch
            {
                return false; // never let a shell quirk break the feature
            }
        }

        public void Apply(CrosshairConfig config)
        {
            Config = config;
            if (IsVisible) _window?.Apply(config);
        }

        public void Show()
        {
            _window ??= new CrosshairWindow();
            if (!_window.Visible) _window.Show();
            _window.Apply(Config);
            IsVisible = true;
        }

        public void Hide()
        {
            _window?.Hide();
            IsVisible = false;
        }

        public void Toggle()
        {
            if (IsVisible) Hide(); else Show();
        }

        public void Dispose()
        {
            _window?.Dispose();
            _window = null;
            IsVisible = false;
        }
    }
}
