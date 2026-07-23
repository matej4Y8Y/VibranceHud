using System;

namespace VibranceHud
{
    /// <summary>
    /// Coordinates the two vibrance mechanisms behind a single 0-200 level:
    ///   0-100  -> driver vibrance (NVAPI), no software effect (tier 1, unchanged).
    ///   100-200 -> driver pinned at 100, software saturation matrix supplies the rest.
    ///
    /// This split lives here (not in the UI) so it is the one place to test and change.
    /// </summary>
    public sealed class VibranceEngine
    {
        public const int Max = 200;

        private readonly IVibranceController _controller;
        private readonly ISaturationOverlay _overlay;

        private int _level;

        public VibranceEngine(IVibranceController controller, ISaturationOverlay overlay)
        {
            _controller = controller;
            _overlay = overlay;
            _level = Math.Clamp(controller.CurrentLevel, 0, Max);
        }

        public int CurrentLevel => _level;

        public int DefaultLevel => _controller.DefaultLevel;

        public void SetLevel(int level)
        {
            _level = Math.Clamp(level, 0, Max);

            if (_level <= 100)
            {
                _controller.SetLevel(_level);
                _overlay.Clear();
            }
            else
            {
                _controller.SetLevel(100);
                _overlay.SetSaturation(_level / 100f);
            }
        }

        public void Reset() => SetLevel(DefaultLevel);
    }
}
