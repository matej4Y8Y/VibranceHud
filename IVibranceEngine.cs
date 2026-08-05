namespace VibranceHud
{
    /// <summary>
    /// Minimal view of <see cref="VibranceEngine"/> that the auto-apply engine needs.
    /// Lets the tests inject a fake without dragging in the overlay / NVAPI / gamma-ramp
    /// collaborators that the real engine owns.
    /// </summary>
    public interface IVibranceEngine
    {
        int Vibrance { get; set; }

        /// <summary>Move the whole effect into the colour matrix so recordings and screen
        /// shares can see it. Default false everywhere.</summary>
        bool StreamingMode { get => false; set { } }

        /// <summary>Whether driver (NVIDIA) vibrance is in play. Decides whether
        /// <see cref="StreamingMode"/> has anything to move - without a driver, software
        /// already carries the whole range and the switch is inert. Defaults to false so
        /// fakes get the "nothing to move" answer rather than a false promise.</summary>
        bool DriverAvailable => false;

        /// <summary>Measure whether this machine's colour effect reaches screen capture.
        /// Briefly flashes the screen, so callers must warn first. Defaults to "couldn't
        /// run" so fakes don't have to implement a GPU probe.</summary>
        CaptureProbe RunCaptureProbe() => CaptureProbe.Failed("not supported by this engine");
        int Saturation { get; set; }
        int Brightness { get; set; }
        int Gamma { get; set; }

        /// <summary>Contrast, 50-150 (100 = untouched). Default no-op so fakes that predate
        /// this control don't need to implement it.</summary>
        int Contrast { get => 100; set { } }

        /// <summary>White balance, -100 (cool) to +100 (warm), 0 = untouched. Default no-op
        /// so fakes that predate this control don't need to implement it.</summary>
        int Temperature { get => 0; set { } }

        /// <summary>The advanced colour grade - highlights, shadows, whites, blacks, fade and
        /// split toning. Default no-op so fakes that predate it don't need to implement it,
        /// and so a fake returning neutral reads as "this engine does no grading" rather than
        /// as a deliberately zeroed one.</summary>
        ToneSettings Tone { get => ToneSettings.Neutral; set { } }

        /// <summary>Tell the engine a slider drag has started so it can suppress overlay writes
        /// while the user is dragging. The chip still tracks the cursor 1:1 via the
        /// WinForms control's own repaint; the screen catches up on
        /// <see cref="EndDrag"/>. See <see cref="VibranceEngine.BeginDrag"/>.</summary>
        void BeginDrag();

        /// <summary>Tell the engine a slider drag has ended. The final overlay value is
        /// committed in a single write so the screen matches the chip. See
        /// <see cref="VibranceEngine.EndDrag"/>.</summary>
        void EndDrag();

        /// <summary>Pause the screen overlay so the tint disappears (used when PlexusX
        /// loses focus). The chip and the UI values stay correct; only the screen
        /// overlay is gated.</summary>
        void SuspendOverlay();

        /// <summary>Resume the screen overlay using the current chip values (used when
        /// PlexusX becomes the foreground window again).</summary>
        void ResumeOverlay();
    }
}