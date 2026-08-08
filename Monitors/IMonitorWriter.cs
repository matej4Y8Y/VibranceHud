namespace VibranceHud.Monitors
{
    /// <summary>
    /// Everything the Monitor page does to a physical panel.
    ///
    /// A seam, not an abstraction for its own sake. Without it <see cref="MonitorControl"/> is
    /// static and a test can only build the page and look at its layout - which is why all ten
    /// of the page's tests passed with the entire write path emptied out. The rules that
    /// matter here (a refused write is not recorded, an original is captured before the first
    /// change, a revert restores exactly what was read) are all invisible from outside without
    /// something to stand in for the hardware.
    ///
    /// Every method returns whether the panel accepted the write. False is the expected case,
    /// not an error - DDC/CI support is patchy and refusal is normal.
    /// </summary>
    public interface IMonitorWriter
    {
        bool SetBrightnessPercent(int monitorIndex, PanelRange range, int percent);
        bool SetContrastPercent(int monitorIndex, PanelRange range, int percent);
        bool SetLowBlueLight(int monitorIndex, PanelRange gain, int strength);

        bool RestoreBrightness(int monitorIndex, int raw);
        bool RestoreContrast(int monitorIndex, int raw);
        bool RestoreBlueGain(int monitorIndex, int raw);

        /// <summary>The panel's current brightness, or null if it would not give a believable
        /// answer. See <see cref="MonitorControl.ReadTrustedBrightness"/>.</summary>
        int? ReadTrustedBrightness(int monitorIndex);
    }

    /// <summary>The real one. Nothing but a forward to the P/Invoke layer.</summary>
    public sealed class Dxva2MonitorWriter : IMonitorWriter
    {
        public bool SetBrightnessPercent(int monitorIndex, PanelRange range, int percent) =>
            MonitorControl.SetBrightnessPercent(monitorIndex, range, percent);

        public bool SetContrastPercent(int monitorIndex, PanelRange range, int percent) =>
            MonitorControl.SetContrastPercent(monitorIndex, range, percent);

        public bool SetLowBlueLight(int monitorIndex, PanelRange gain, int strength) =>
            MonitorControl.SetLowBlueLight(monitorIndex, gain, strength);

        public bool RestoreBrightness(int monitorIndex, int raw) =>
            MonitorControl.RestoreBrightness(monitorIndex, raw);

        public bool RestoreContrast(int monitorIndex, int raw) =>
            MonitorControl.RestoreContrast(monitorIndex, raw);

        public bool RestoreBlueGain(int monitorIndex, int raw) =>
            MonitorControl.RestoreBlueGain(monitorIndex, raw);

        public int? ReadTrustedBrightness(int monitorIndex) =>
            MonitorControl.ReadTrustedBrightness(monitorIndex);
    }
}
