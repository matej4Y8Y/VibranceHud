using System;

namespace VibranceHud
{
    /// <summary>
    /// Coordinates every display adjustment behind one place.
    ///
    /// Vibrance and Saturation are deliberately separate controls, because they are two
    /// different effects produced by two different mechanisms:
    ///   Vibrance   0-100 -> the NVIDIA driver's Digital Vibrance (NVAPI). Non-linear:
    ///                       it lifts muted colours while largely sparing ones that are
    ///                       already saturated, so it stays natural. Needs an NVIDIA GPU.
    ///   Saturation 0-200 -> the software colour matrix. Linear: every colour's chroma
    ///                       scaled equally. Works on any GPU, and is what takes the
    ///                       picture past the driver's own 100% ceiling.
    ///
    /// Saturation, brightness and eye care all fold into a single screen matrix, so
    /// there's only ever one pass over the screen.
    /// </summary>
    public sealed class VibranceEngine : IVibranceEngine
    {
        public const int MaxVibrance = 200;
        public const int MaxSaturation = 200;

        /// <summary>The driver's own hard ceiling. Past this there is no hardware left to
        /// ask, so vibrance continues in software.</summary>
        public const int DriverVibranceCeiling = 100;
        public const int MinBrightness = 50;
        public const int MaxBrightness = 150;
        public const int MinGamma = 50;
        public const int MaxGamma = 150;

        /// <summary>Warmth used when the eye-care toggle is on (0-1).</summary>
        public const float EyeCareWarmth = 0.5f;

        private readonly IVibranceController _controller;
        private readonly ISaturationOverlay _overlay;
        private readonly IGammaRamp _gammaRamp;

        private int _vibrance;
        private int _saturation = 100;
        private int _brightness = 100;
        private int _gamma = 100;
        private bool _eyeCare;
        private bool _dragging;
        private bool _overlaySuspended;

        // When the user is dragging a slider, the value setter is called on every
        // mouse-move event. Calling MagSetFullscreenColorEffect (or the DX11 swap-chain
        // write) on every move is slow on systems where DWM is software-rendered or the
        // Magnification API is in use (~10-30ms per call), which blocks the UI thread
        // and makes the slider feel jumpy. ScheduleOverlayApply short-circuits during a
        // drag so the chip tracks the cursor 1:1 and the screen catches up on EndDrag
        // with one immediate write.

        public VibranceEngine(IVibranceController controller, ISaturationOverlay overlay, IGammaRamp gammaRamp)
        {
            _controller = controller;
            _overlay = overlay;
            _gammaRamp = gammaRamp;
            _vibrance = Math.Clamp(controller.CurrentLevel, 0, MaxVibrance);
        }

        /// <summary>Begin a slider drag. From this point until <see cref="EndDrag"/> the engine
        /// suppresses overlay writes so the slow MagSetFullscreenColorEffect syscall doesn't
        /// block the UI thread (which is what causes the user-perceived "jumping" lag on
        /// systems where the DWM gradient compositor is software-rendered or the Mag path
        /// is in use). The slider chip itself tracks the cursor 1:1 via WinForms' own
        /// repaint, so the user still sees the value change. On EndDrag, the final
        /// overlay value is committed in a single write.</summary>
        public void BeginDrag()
                {
            _dragging = true;
        }

        /// <summary>End a slider drag. Flushes the overlay with the current value in a
                /// single MagSetFullscreenColorEffect call so the screen catches up to the chip's
                /// final position.</summary>
                public void EndDrag()
                {
                    _dragging = false;
                    // Force one immediate overlay write so the screen matches the chip's
                    // final position. This runs synchronously on the UI thread, which is fine
                    // because the user has stopped dragging and the brief freeze is invisible.
                    ApplyOverlay();
                }

                /// <summary>Pause the screen overlay. Used when PlexusX loses focus so the
                /// tint doesn't follow the user into other apps. The chip and UI values
                /// stay correct; only the overlay write is suspended. Resuming re-applies
                /// the current chip values.</summary>
                public void SuspendOverlay()
                {
                    if (_overlaySuspended) return;
                    _overlaySuspended = true;
                    _overlay.Clear();
                }

                /// <summary>Resume the screen overlay using the current chip values.</summary>
                public void ResumeOverlay()
                {
                    if (!_overlaySuspended) return;
                    _overlaySuspended = false;
                    ApplyOverlay();
                }

        /// <summary>Vibrance 0-200. Up to 100 this is the driver's own Digital Vibrance
        /// (true non-linear, skin-tone sparing); above 100 the driver is pinned at its
        /// ceiling and a software vibrance boost carries the rest.</summary>
        public int Vibrance
        {
            get => _vibrance;
            set { _vibrance = Math.Clamp(value, 0, MaxVibrance); ApplyAll(); }
        }

        /// <summary>Software saturation, 0-200 (100 = untouched, 0 = greyscale).</summary>
        public int Saturation
        {
            get => _saturation;
            set { _saturation = Math.Clamp(value, 0, MaxSaturation); ApplyAll(); }
        }

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

        public void Reset()
        {
            _brightness = 100;
            _eyeCare = false;
            _gamma = 100;
            _gammaRamp.Reset();
            _saturation = 100;
            Vibrance = DefaultLevel;
        }

        private void ApplyAll()
        {
            // Driver vibrance is cheap (NVAPI call) - apply immediately so the user
                // sees the slider-chip feedback match the driver-level change without
                // a one-frame delay.
                _controller.SetLevel(Math.Min(_vibrance, DriverVibranceCeiling));

                // The screen overlay (DX11 swap chain / Magnification API) is expensive.
                // ScheduleOverlayApply short-circuits during a slider drag and commits a
                // single write on EndDrag.
                ScheduleOverlayApply();
            }

        private void ScheduleOverlayApply()
                {
            // During a slider drag, skip the overlay write entirely. The chip on the
            // page tracks the cursor 1:1 via WinForms' own repaint cycle, so the user
            // still sees the value change. The expensive MagSetFullscreenColorEffect
            // syscall would otherwise block the UI thread for ~10-30ms per call, which
            // is what makes the slider feel jumpy on Mag-path systems. EndDrag flushes
            // the final value in a single write.
            if (_dragging) return;
            ApplyOverlay();
                }

        private void ApplyOverlay()
        {
            float vibrance = _vibrance > DriverVibranceCeiling
                ? _vibrance / (float)DriverVibranceCeiling
                : 1f;
            float saturation = _saturation / 100f;
            float brightness = _brightness / 100f;
            float warmth = _eyeCare ? EyeCareWarmth : 0f;

            if (ColorAdjust.IsIdentity(saturation, vibrance, brightness, warmth))
                _overlay.Clear();
            else
                _overlay.Apply(ColorAdjust.Build(saturation, vibrance, brightness, warmth));
        }
    }
}
