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
        int Saturation { get; set; }
        int Brightness { get; set; }
        int Gamma { get; set; }

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