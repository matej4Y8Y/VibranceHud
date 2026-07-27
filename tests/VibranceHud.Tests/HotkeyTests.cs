using System.IO;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Covers the two new settings keys (<see cref="AppSettings.HotkeyModifierMask"/>
    /// and <see cref="AppSettings.HotkeyVirtualKey"/>) plus the pure rendering helper on
    /// <see cref="HotkeyPicker.GetDisplay"/>. The picker control itself is exercised by
    /// the app (WinForms hotkey capture needs a real focus surface), so we stick to the
    /// bits the unit test boundary can reach without a window.
    /// </summary>
    public class HotkeyTests : System.IDisposable
    {
        private readonly string _dir;

        public HotkeyTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "plexusx_hotkey_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }

        // ---- Round-trip ----

        [Fact]
        public void HotkeySettings_RoundTripThroughJson()
        {
            var store = new SettingsStore(_dir);
            store.Save(new AppSettings
            {
                HotkeyModifierMask = HotkeyModifiers.Control | HotkeyModifiers.Shift,
                HotkeyVirtualKey = HotkeyKeys.F2
            });

            var loaded = new SettingsStore(_dir).Load();

            Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Shift, loaded.HotkeyModifierMask);
            Assert.Equal(HotkeyKeys.F2, loaded.HotkeyVirtualKey);
        }

        [Fact]
        public void HotkeySettings_DefaultToCtrlAltV()
        {
            // A fresh settings file (no JSON yet) should hand back the same combo the
            // old hardcoded behaviour used, so nothing changes for existing users.
            var loaded = new SettingsStore(Path.Combine(_dir, "missing")).Load();

            Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt, loaded.HotkeyModifierMask);
            Assert.Equal(HotkeyKeys.V, loaded.HotkeyVirtualKey);
        }

        // ---- GetDisplay ----

        [Fact]
        public void GetDisplay_CtrlPlusV()
        {
            Assert.Equal("Ctrl+V", HotkeyPicker.GetDisplay(HotkeyModifiers.Control, HotkeyKeys.V));
        }

        [Fact]
        public void GetDisplay_CtrlAltV()
        {
            Assert.Equal("Ctrl+Alt+V",
                HotkeyPicker.GetDisplay(HotkeyModifiers.Control | HotkeyModifiers.Alt, HotkeyKeys.V));
        }

        [Fact]
        public void GetDisplay_CtrlShiftF2()
        {
            Assert.Equal("Ctrl+Shift+F2",
                HotkeyPicker.GetDisplay(
                    HotkeyModifiers.Control | HotkeyModifiers.Shift, HotkeyKeys.F2));
        }

        [Fact]
        public void GetDisplay_AltF1()
        {
            Assert.Equal("Alt+F1", HotkeyPicker.GetDisplay(HotkeyModifiers.Alt, HotkeyKeys.F1));
        }

        [Fact]
        public void GetDisplay_WinShiftE()
        {
            // Modifier order is fixed by the picker (Ctrl, Alt, Shift, Win) so the
            // display reads the same way every time. Shift happens to come before Win
            // because that's the order the renderer appends.
            Assert.Equal("Shift+Win+E",
                HotkeyPicker.GetDisplay(
                    HotkeyModifiers.Win | HotkeyModifiers.Shift, HotkeyKeys.E));
        }

        [Theory]
        [InlineData((uint)0x30, "0")]
        [InlineData((uint)0x31, "1")]
        [InlineData((uint)0x32, "2")]
        [InlineData((uint)0x33, "3")]
        [InlineData((uint)0x34, "4")]
        [InlineData((uint)0x35, "5")]
        [InlineData((uint)0x36, "6")]
        [InlineData((uint)0x37, "7")]
        [InlineData((uint)0x38, "8")]
        [InlineData((uint)0x39, "9")]
        public void GetDisplay_CtrlPlusDigit(uint vk, string expected)
        {
            Assert.Equal("Ctrl+" + expected,
                HotkeyPicker.GetDisplay(HotkeyModifiers.Control, vk));
        }

        [Fact]
        public void GetDisplay_EmptyMask_ReturnsKeyOnly()
        {
            // The picker's capture logic rejects this combo, but the static renderer must
            // still produce something sensible (just the bare key) so callers don't have
            // to special-case the empty mask before passing it through.
            var s = HotkeyPicker.GetDisplay(0, HotkeyKeys.V);
            Assert.Equal("V", s);
        }

        [Fact]
        public void GetDisplay_UnknownVk_RendersHex()
        {
            // Falls back to a hex representation so we never lie about what's bound.
            var s = HotkeyPicker.GetDisplay(HotkeyModifiers.Control, 0xDEAD);
            Assert.Equal("Ctrl+0xDEAD", s);
        }
    }
}
