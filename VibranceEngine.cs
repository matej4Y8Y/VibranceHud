using System;

namespace VibranceHud
{
    /// <summary>
    /// Coordinates every display adjustment behind one place:
    ///   Vibrance 0-100  -> driver vibrance (NVAPI), no software effect.
    ///   Vibrance 100-200 -> driver pinned at 100, software saturation supplies the rest.
    ///   Brightness / eye care -> folded into the same software color matrix.
    ///
    /// All three are combined into a single screen matrix so there's only ever one pass.
    /// </summary>
    public sealed class VibranceEngine
    {
        public const int Max = 200;
        public const int MinBrightness = 50;
        public const int MaxBrightness = 150;
        public const int MinGamma = 50;
        public const int MaxGamma = 150;

        /// <summary>Warmth used when the eye-care toggle is on (0-1).</summary>
        public const float EyeCareWarmth = 0.5f;

        private readonly IVibranceController _controller;
        private readonly ISaturationOverlay _overlay;
        private readonly IGammaRamp _gammaRamp;

        private int _level;
        private int _brightness = 100;
        private int _gamma = 100;
        private bool _eyeCare;

        public VibranceEngine(IVibranceController controller, ISaturationOverlay overlay, IGammaRamp gammaRamp)
        {
            _controller = controller;
            _overlay = overlay;
            _gammaRamp = gammaRamp;
            _level = Math.Clamp(controller.CurrentLevel, 0, Max);
        }

        public int CurrentLevel => _level;

        public int DefaultLevel => _controller.DefaultLevel;

        /// <summary>False when the 0-100 driver range has no NVIDIA driver to apply to.</summary>
        public bool DriverAvailable => _controller.IsAvailable;

        /// <summary>Screen brightness calibration, 50-150 (100 = untouched).</summary>
        public int Brightness
        {
            get => _brightness;
            set { _brightness = Math.Clamp(value, MinBrightness, MaxBrightness); ApplyAll(); }
        }

        /// <summary>Screen gamma, 50-150 (100 = untouched). Uses the display's gamma ramp,
        /// since gamma is non-linear and can't be folded into the color matrix.</summary>
        public int Gamma
        {
            get => _gamma;
            set
            {
                _gamma = Math.Clamp(value, MinGamma, MaxGamma);
                if (_gamma == 100) _gammaRamp.Reset();
                else _gammaRamp.Apply(GammaCurve.Build(_gamma / 100f));
            }
        }

        /// <summary>Blue-light reduction (warm tint) for comfortable late-night use.</summary>
        public bool EyeCare
        {
            get => _eyeCare;
            set { _eyeCare = value; ApplyAll(); }
        }

        public void SetLevel(int level)
        {
            _level = Math.Clamp(level, 0, Max);
            ApplyAll();
        }

        public void Reset()
        {
            _brightness = 100;
            _eyeCare = false;
            _gamma = 100;
            _gammaRamp.Reset();
            SetLevel(DefaultLevel);
        }

        private void ApplyAll()
        {
            // Driver handles vibrance up to its own ceiling; the rest is software.
            _controller.SetLevel(Math.Min(_level, 100));

            float saturation = _level > 100 ? _level / 100f : 1f;
            float brightness = _brightness / 100f;
            float warmth = _eyeCare ? EyeCareWarmth : 0f;

            if (ColorAdjust.IsIdentity(saturation, brightness, warmth))
                _overlay.Clear();
            else
                _overlay.Apply(ColorAdjust.Build(saturation, brightness, warmth));
        }
    }
}
