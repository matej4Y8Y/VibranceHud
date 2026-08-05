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
    /// Saturation, brightness, contrast and temperature all fold into a single screen matrix,
    /// so there's only ever one pass over the screen.
    /// </summary>
    public sealed class VibranceEngine : IVibranceEngine
    {
        // Ceilings are where the control stops being useful, not round numbers. Past roughly
        // 3x, colour hits the edge of what a monitor can show and flattens into blocks.
        public const int MaxVibrance = 350;
        public const int MaxSaturation = 300;

        /// <summary>The driver's own hard ceiling. Past this there is no hardware left to
        /// ask, so vibrance continues in software.</summary>
        public const int DriverVibranceCeiling = 100;
        public const int MinBrightness = 50;
        // Deliberately not doubled. Brightness multiplies pixel values, so anything already
        // bright clips to white and stays there - at 2x half the screen is blown out. That is
        // not headroom, it is damage.
        public const int MaxBrightness = 170;
        public const int MinGamma = 50;
        public const int MaxGamma = 150;

        /// <summary>Contrast, as a percentage. Stops at 150 for the same reason brightness
        /// stops at 170: past it the highlights clip to flat white and the shadows crush to
        /// flat black, which is lost detail, not more contrast.</summary>
        public const int MinContrast = 50;
        public const int MaxContrast = 150;

        /// <summary>White balance, -100 (cool) through 0 (neutral) to +100 (warm).</summary>
        public const int MinTemperature = -100;
        public const int MaxTemperature = 100;

        /// <summary>Legacy warmth strength used to migrate the old on/off control.</summary>
        public const float EyeCareWarmth = 0.5f;

        private readonly IVibranceController _controller;
        private readonly ISaturationOverlay _overlay;
        private readonly IGammaRamp _gammaRamp;

        private int _vibrance;
        private int _saturation = 100;
        private int _brightness = 100;
        private int _gamma = 100;
        private int _contrast = 100;
        private int _temperature;
        private bool _dragging;
        private bool _overlaySuspended;

        // Set when a drag changed the value each one guards, so EndDrag flushes only the
        // writes that are actually needed. Without these, EndDrag either has to flush
        // everything (a driver call and a gamma-ramp syscall on every mouse-release, even
        // for sliders that touch neither) or track nothing and skip the flush entirely.
        private bool _driverDirty;
        private bool _gammaDirty;

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
            // Start elapsed so the very first movement paints immediately instead of waiting
            // out an interval - otherwise every drag begins with a visible dead moment.
            _dragClock.Reset();
        }

        /// <summary>End a slider drag. Flushes the overlay with the current value in a
                /// single MagSetFullscreenColorEffect call so the screen catches up to the chip's
                /// final position.</summary>
                public void EndDrag()
                {
                    _dragging = false;
                    _dragClock.Stop();

                    // Flush the driver level only if the drag actually moved vibrance.
                    // Dragging saturation/brightness/gamma leaves the driver value alone,
                    // and an NVAPI write with an unchanged value is pure wasted latency.
                    if (_driverDirty)
                    {
                        _driverDirty = false;
                        ApplyDriverVibrance();
                    }

                    // Same for the gamma ramp: SetDeviceGammaRamp is a slow syscall, so it
                    // runs once here rather than on every mouse-move during the drag.
                    if (_gammaDirty)
                    {
                        _gammaDirty = false;
                        ApplyGammaRamp();
                    }

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
            set
            {
                _vibrance = Math.Clamp(value, 0, MaxVibrance);
                // Vibrance is the one control that really does need the driver. During a
                // drag, mark it dirty and let EndDrag issue the single write.
                if (_dragging) _driverDirty = true;
                else ApplyDriverVibrance();
                ScheduleOverlayApply();
            }
        }

        /// <summary>Software saturation, 0-200 (100 = untouched, 0 = greyscale).</summary>
        public int Saturation
        {
            get => _saturation;
            set
            {
                _saturation = Math.Clamp(value, 0, MaxSaturation);
                // No driver call here: saturation is applied entirely in the colour matrix,
                // so the driver's Digital Vibrance level is unaffected.
                ScheduleOverlayApply();
            }
        }

        public int DefaultLevel => _controller.DefaultLevel;

        /// <summary>False when the 0-100 driver range has no NVIDIA driver to apply to.</summary>
        private bool _streamingMode;

        public bool DriverAvailable => _controller.IsAvailable;

        /// <summary>Why the driver path is missing, when it is. The UI needs the reason, not
        /// just the fact - "no NVIDIA GPU" is a lie on a laptop that has one.</summary>
        public VibranceDriverState DriverState => _controller.DriverState;

        /// <summary>
        /// Move the whole effect into the colour matrix so recordings and screen shares can
        /// see it.
        ///
        /// The matrix is applied while the desktop is composited, which is where Desktop
        /// Duplication - and therefore OBS Display Capture - reads. Driver vibrance and the
        /// gamma ramp are applied after that, on the way to the cable, so no capture can ever
        /// pick them up. That is the whole reason the effect shows for some people and not
        /// others: it depends which slider they happened to use.
        ///
        /// Off by default. It trades a little image quality for being visible, and nobody who
        /// isn't recording should pay that without asking.
        /// </summary>
        public bool StreamingMode
        {
            get => _streamingMode;
            set
            {
                if (_streamingMode == value) return;
                _streamingMode = value;
                ApplyDriverVibrance();   // park the driver, or hand its value back
                ScheduleOverlayApply();
            }
        }

        /// <summary>Screen brightness calibration, 50-150 (100 = untouched).</summary>
        public int Brightness
        {
            get => _brightness;
            set
            {
                _brightness = Math.Clamp(value, MinBrightness, MaxBrightness);
                // Brightness folds into the colour matrix - no driver involvement.
                ScheduleOverlayApply();
            }
        }

        /// <summary>Screen gamma, 50-150 (100 = untouched). Uses the display's gamma ramp,
        /// since gamma is non-linear and can't be folded into the color matrix.</summary>
        public int Gamma
        {
            get => _gamma;
            set
            {
                _gamma = Math.Clamp(value, MinGamma, MaxGamma);
                // SetDeviceGammaRamp is a slow syscall (tens of ms on some drivers), so during
                // a drag it goes through the same throttle as everything else rather than
                // running on every mouse-move. It does have to run DURING the drag though -
                // gamma is not in the colour matrix, so leaving it to EndDrag meant this
                // slider showed nothing at all until you let go.
                if (_dragging) { _gammaDirty = true; ScheduleOverlayApply(); }
                else ApplyGammaRamp();
            }
        }

        private ToneSettings _tone = ToneSettings.Neutral;

        /// <summary>
        /// The advanced colour grade: highlights, shadows, whites, blacks, fade and split
        /// toning. Everything here resolves to the display gamma ramp, which is a per-channel
        /// lookup table and therefore the only path in this app that can express a non-linear
        /// curve at all — the colour matrix cannot, by construction.
        ///
        /// <see cref="Gamma"/> keeps its own property because it predates this and appears in
        /// every saved settings file and every share code, but it is really just one field of
        /// the same grade. Setting either rebuilds the same ramp.
        /// </summary>
        public ToneSettings Tone
        {
            get => _tone with { Gamma = _gamma };
            set
            {
                var incoming = value.Normalized;
                if (_tone == incoming && _gamma == incoming.Gamma) return;

                _tone = incoming;
                _gamma = Math.Clamp(incoming.Gamma, MinGamma, MaxGamma);

                // Same throttle as Gamma: SetDeviceGammaRamp is a slow syscall, but it still
                // has to run during a drag or the advanced sliders would show nothing until
                // the mouse came up.
                if (_dragging) { _gammaDirty = true; ScheduleOverlayApply(); }
                else ApplyGammaRamp();
            }
        }

        /// <summary>Contrast, 50-150 (100 = untouched). Folds into the same colour matrix as
        /// saturation and brightness, so it costs nothing extra to apply.</summary>
        public int Contrast
        {
            get => _contrast;
            set
            {
                _contrast = Math.Clamp(value, MinContrast, MaxContrast);
                ScheduleOverlayApply();
            }
        }

        /// <summary>
        /// White balance, -100 (cool) to +100 (warm), 0 = untouched.
        ///
        /// Replaces the old fixed warm-light switch. The same curve remains for migration:
        /// +50 produces exactly the old look, while the visible control now spans cool to warm.
        /// </summary>
        public int Temperature
        {
            get => _temperature;
            set
            {
                _temperature = Math.Clamp(value, MinTemperature, MaxTemperature);
                ScheduleOverlayApply();
            }
        }

        /// <summary>
        /// Legacy compatibility view onto <see cref="Temperature"/>. Old saved settings and
        /// existing engine tests still migrate to the same warm value; this is not exposed in UI.
        /// </summary>
        public bool EyeCare
        {
            get => _temperature >= EyeCareTemperature;
            set => Temperature = value ? EyeCareTemperature : 0;
        }

        /// <summary>Where the old fixed warm-light switch sat on the new scale.</summary>
        public const int EyeCareTemperature = (int)(EyeCareWarmth * 100);

        /// <summary>
        /// Measure whether the colour effect lands in what screen capture reads, then put the
        /// user's colours back exactly as they were.
        ///
        /// Overlay writes are suspended for the duration so nothing races the probe - the
        /// animation timer and any slider movement would otherwise be re-applying the user's
        /// real values in the middle of the measurement and turn it into noise.
        /// </summary>
        public CaptureProbe RunCaptureProbe()
        {
            SuspendOverlay();
            try { return CaptureDiagnostic.Probe(_overlay); }
            finally { ResumeOverlay(); }
        }

        public void Reset()
        {
            _brightness = 100;
            _temperature = 0;
            _contrast = 100;
            _gamma = 100;
            _gammaRamp.Reset();
            _saturation = 100;
            Vibrance = DefaultLevel;
        }

        /// <summary>
        /// How much software vibrance to fold into the colour matrix for a given slider value.
        /// 1.0 means "leave chroma alone".
        ///
        /// With an NVIDIA driver the 0-100 range belongs to NVAPI's Digital Vibrance, so the
        /// software term stays neutral there - applying both would double up. Above 100 the
        /// driver is pinned at its ceiling and software carries the remainder.
        ///
        /// Without a driver (AMD, Intel, or NVIDIA with no driver installed) the whole 0-200
        /// range goes through software instead. Previously this returned 1.0 below 100 on
        /// those machines while NullVibranceController's SetLevel did nothing, so both paths
        /// were inert simultaneously and the slider's entire default range was dead - it
        /// changed nothing on screen, in capture, or anywhere else.
        ///
        /// Note this is not identical to DVC: NVIDIA's curve is non-linear and deliberately
        /// spares skin tones, whereas this scales chroma linearly. The same number therefore
        /// looks slightly different on AMD than on NVIDIA. That's an accepted trade against a
        /// control that does nothing at all.
        ///
        /// Public + static so it can be unit-tested without a GPU, and so ApplyOverlay and
        /// the identity check can't drift apart about what counts as neutral.
        /// </summary>
        public static float SoftwareVibranceFactor(
            int vibrance, bool driverAvailable, bool streaming = false)
        {
            // Streaming Mode is exactly "pretend there is no driver": the driver's contribution
            // is applied after the desktop is composited, so no capture can ever see it, and
            // the matrix has to carry the whole range instead.
            if (driverAvailable && !streaming)
            {
                // NVIDIA is left exactly as it was. The driver owns 0-100 and its own neutral
                // already sits at 50, so nothing here was ever broken - and changing the curve
                // above the ceiling would alter the picture for users who have no complaint.
                return vibrance <= DriverVibranceCeiling
                    ? 1f
                    : vibrance / (float)DriverVibranceCeiling;
            }

            // Two straight lines meeting at the default, so the number means the same thing
            // here as it does on NVIDIA:
            //
            //     0 -> greyscale      50 -> untouched      200 -> the same ceiling as before
            //
            // It used to be value/100 throughout, which put the default at half saturation.
            // An AMD or Intel user opened the app and their screen looked worse than before
            // they installed it - and it only reached normal at 100, by which point they'd
            // already decided the product was broken.
            if (vibrance <= SoftwareNeutral)
                return vibrance / (float)SoftwareNeutral;

            // Fixed slope, NOT derived from MaxVibrance. Tying it to the maximum meant that
            // raising the ceiling silently re-scaled everything below it: 200 would have
            // stopped meaning 2.0 and every existing user's screen would have changed because
            // we added headroom they never asked for. The cap now just extends the same line.
            return 1f + (vibrance - SoftwareNeutral) / (float)SoftwareSlope;
        }

        /// <summary>Where the software curve is untouched - the same place the driver's own
        /// neutral sits, which is the whole point of it.</summary>
        public const int SoftwareNeutral = 50;

        /// <summary>How far past neutral doubles the chroma. Fixed forever: this is what makes
        /// a saved 200 keep meaning exactly what it meant the day it was saved.</summary>
        private const int SoftwareSlope = 150;

        /// <summary>
        /// Convert a value saved under the old meaning so it still looks the same.
        ///
        /// Without this, everyone on AMD or Intel wakes up after an update to a different
        /// picture and no explanation. We're preserving what they chose, not deciding it was
        /// wrong for them.
        /// </summary>
        public static int MigrateSoftwareVibrance(int savedValue)
        {
            float wanted = savedValue / (float)DriverVibranceCeiling;   // the old formula

            int migrated = wanted <= 1f
                ? (int)Math.Round(wanted * SoftwareNeutral)
                : SoftwareNeutral + (int)Math.Round((wanted - 1f) * SoftwareSlope);

            return Math.Clamp(migrated, 0, MaxVibrance);
        }

        /// <summary>Push the current vibrance to the driver. Called directly outside a drag
        /// and once from EndDrag; never per mouse-move.</summary>
        private void ApplyDriverVibrance() =>
            // In Streaming Mode the driver is parked at its own neutral, NOT at zero. Driver
            // vibrance 0 is fully grey, so handing it a 0 while software does the work would
            // drain the colour out of the screen and look like the app had broken.
            _controller.SetLevel(_streamingMode
                ? _controller.DefaultLevel
                : Math.Min(_vibrance, DriverVibranceCeiling));

        /// <summary>
        /// Install (or clear) the gamma ramp for the whole current grade.
        ///
        /// Neutral resets rather than pushing an identity ramp. Windows does not restore a
        /// gamma ramp when a process exits, so leaving one applied that does nothing is both
        /// wasted work and something that can be left behind on a crash.
        /// </summary>
        private void ApplyGammaRamp()
        {
            var tone = _tone with { Gamma = _gamma };

            if (tone.IsNeutral) _gammaRamp.Reset();
            else _gammaRamp.Apply(ToneCurve.Build(tone));
        }

        /// <summary>Everything the display state depends on, applied in one go. Used by the
        /// paths that change several values at once (Reset, settings load, resume).</summary>
        private void ApplyAll()
        {
            ApplyDriverVibrance();
            ScheduleOverlayApply();
        }

        /// <summary>
        /// How often the screen is allowed to update while a slider is being dragged.
        ///
        /// Dragging used to skip the overlay entirely and only apply on release, which made
        /// the screen change once, at the end - the colour appeared to lag a whole gesture
        /// behind the control. Applying on every mouse-move is the other extreme: the write
        /// is a syscall that blocks the UI thread for 10-30ms and there are 100+ moves a
        /// second, so the drag stutters.
        ///
        /// 45ms is about 22 updates a second. Fast enough to read as the screen following
        /// your hand, slow enough that the UI thread is free the rest of the time.
        /// </summary>
        private const int LiveDragIntervalMs = 45;

        private readonly System.Diagnostics.Stopwatch _dragClock = new();

        private void ScheduleOverlayApply()
                        {
                    // Live preview during a drag, throttled. EndDrag still flushes the exact
                    // final value, so whatever the throttle skipped is never what you're left
                    // looking at.
                    if (_dragging)
                    {
                        if (_overlaySuspended) return;
                        if (_dragClock.IsRunning && _dragClock.ElapsedMilliseconds < LiveDragIntervalMs)
                            return;
                        _dragClock.Restart();

                        // Flush the driver and the gamma ramp too, not just the matrix.
                        //
                        // Below DriverVibranceCeiling the software factor is exactly 1.0 - the
                        // matrix contributes nothing and the driver IS the whole effect. Gamma
                        // never touches the matrix at all; it is a ramp. So an overlay-only
                        // live update left both of those frozen until release: vibrance did
                        // nothing at all under 100, and dragging 0 -> 300 stayed grey the whole
                        // way because the driver was still parked at 0.
                        if (_driverDirty) { _driverDirty = false; ApplyDriverVibrance(); }
                        if (_gammaDirty) { _gammaDirty = false; ApplyGammaRamp(); }
                        ApplyOverlay();
                        return;
                    }
                    // While the host form has lost focus the overlay is gated off (Clear() at
                    // SuspendOverlay time). Re-applying here would silently re-enable the
                    // effect and undo the suspend - the alt-tab-from-game case then ends up
                    // with the saturation disappearing the next time the form is deactivated.
                    // ResumeOverlay flushes the current value back to the overlay on
                    // OnActivated, so dropping this write is the correct behaviour.
                    if (_overlaySuspended) return;
                    ApplyOverlay();
                        }

        private void ApplyOverlay()
        {
            // On a machine with no NVIDIA driver this now covers the whole 0-200 range,
            // rather than leaving 0-100 to a driver that isn't there.
            float vibrance = SoftwareVibranceFactor(_vibrance, _controller.IsAvailable, _streamingMode);
            float saturation = _saturation / 100f;
            float contrast = _contrast / 100f;
            float brightness = _brightness / 100f;
            float warmth = _temperature / 100f;

            if (ColorAdjust.IsIdentity(saturation, vibrance, contrast, brightness, warmth))
                _overlay.Clear();
            else
                _overlay.Apply(ColorAdjust.Build(saturation, vibrance, contrast, brightness, warmth));
        }
    }
}
