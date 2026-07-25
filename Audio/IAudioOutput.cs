namespace VibranceHud.Audio
{
    /// <summary>
    /// The speaker output, as far as the limiter cares: how loud it's playing right now, and
    /// the volume knob. Abstracted so the limiter loop is testable without audio hardware.
    /// </summary>
    public interface IAudioOutput
    {
        /// <summary>Current output peak level, 0-1.</summary>
        float Peak { get; }

        /// <summary>Master output volume, 0-1.</summary>
        float Volume { get; set; }
    }
}
