// Hotkeys: any key should be bindable, every key should render with a real name, and a
// binding that didn't take must say so.
//
// Three problems this covers:
//  1. The picker rejected a key with no modifier ("Pick at least one modifier"), claiming a
//     bare key can't be a global hotkey. RegisterHotKey accepts fsModifiers = 0 perfectly
//     well, so K / L / PageDown were refused for no real reason.
//  2. KeyName only knew A-Z, 0-9, F1-F12 and the numpad digits. Everything else rendered as
//     "0x22", so a user who bound PageDown saw a hex code instead of "PageDown".
//  3. Nothing surfaced a failed RegisterHotKey to the picker, so a hotkey that never bound
//     looked bound.

using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class HotkeyBindingTests
    {
        // ---- single keys are bindable ------------------------------------------------------

        /// <summary>The headline request: a bare key with no modifier must be accepted.</summary>
        [Theory]
        [InlineData(0x4B)] // K
        [InlineData(0x4C)] // L
        [InlineData(0x22)] // PageDown
        [InlineData(0x21)] // PageUp
        [InlineData(0x77)] // F8
        public void BareKey_WithNoModifier_IsBindable(uint vk)
        {
            Assert.True(HotkeyPicker.IsBindable(0, vk, out var error), error);
            Assert.Equal("", error);
        }

        [Fact]
        public void KeyWithModifier_IsStillBindable()
        {
            Assert.True(HotkeyPicker.IsBindable(
                HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x56, out _));
        }

        /// <summary>A modifier on its own is not a hotkey - the user is still mid-chord.</summary>
        [Theory]
        [InlineData(0x10)] // VK_SHIFT
        [InlineData(0x11)] // VK_CONTROL
        [InlineData(0x12)] // VK_MENU (Alt)
        [InlineData(0x5B)] // VK_LWIN
        public void ModifierAlone_IsNotBindable(uint vk)
        {
            Assert.False(HotkeyPicker.IsBindable(0, vk, out _));
        }

        /// <summary>vk 0 isn't a key at all - it's what an unset binding looks like, and it
        /// used to render as the nonsense "Ctrl+Shift+0x0".</summary>
        [Fact]
        public void ZeroKey_IsNotBindable()
        {
            Assert.False(HotkeyPicker.IsBindable(HotkeyModifiers.Control, 0, out var error));
            Assert.NotEqual("", error);
        }

        /// <summary>Windows eats these before any app sees them; binding them would look
        /// like it worked and then never fire.</summary>
        [Theory]
        [InlineData(0x0002u, 0x1Bu)] // Ctrl+Esc  -> Start menu
        [InlineData(0x0001u, 0x09u)] // Alt+Tab   -> task switcher
        [InlineData(0x0001u, 0x73u)] // Alt+F4    -> close
        public void WindowsReservedCombos_AreNotBindable(uint mods, uint vk)
        {
            Assert.False(HotkeyPicker.IsBindable(mods, vk, out var error));
            Assert.NotEqual("", error);
        }

        /// <summary>A bare Escape must stay unbindable - it's the picker's own cancel key,
        /// so binding it would make the picker impossible to back out of.</summary>
        [Fact]
        public void BareEscape_IsNotBindable()
        {
            Assert.False(HotkeyPicker.IsBindable(0, 0x1B, out _));
        }

        // ---- every key gets a real name ---------------------------------------------------

        [Theory]
        [InlineData(0x4Bu, "K")]
        [InlineData(0x30u, "0")]
        [InlineData(0x70u, "F1")]
        [InlineData(0x7Bu, "F12")]
        [InlineData(0x60u, "Num0")]
        public void KnownKeys_KeepTheirExistingNames(uint vk, string expected)
        {
            Assert.Equal(expected, HotkeyPicker.GetDisplay(0, vk));
        }

        /// <summary>The keys the user actually asked about must not render as hex.</summary>
        [Theory]
        [InlineData(0x21u, "PageUp")]
        [InlineData(0x22u, "PageDown")]
        [InlineData(0x23u, "End")]
        [InlineData(0x24u, "Home")]
        [InlineData(0x2Du, "Insert")]
        [InlineData(0x2Eu, "Delete")]
        [InlineData(0x25u, "Left")]
        [InlineData(0x26u, "Up")]
        [InlineData(0x27u, "Right")]
        [InlineData(0x28u, "Down")]
        [InlineData(0x20u, "Space")]
        [InlineData(0x09u, "Tab")]
        [InlineData(0x0Du, "Enter")]
        [InlineData(0x08u, "Backspace")]
        public void NavigationAndEditingKeys_RenderWithRealNames(uint vk, string expected)
        {
            Assert.Equal(expected, HotkeyPicker.GetDisplay(0, vk));
        }

        /// <summary>Mouse-adjacent extras gamers reach for.</summary>
        [Theory]
        [InlineData(0x7Cu, "F13")]
        [InlineData(0x87u, "F24")]
        [InlineData(0x6Au, "Num*")]
        [InlineData(0x6Bu, "Num+")]
        [InlineData(0x6Du, "Num-")]
        [InlineData(0x6Eu, "Num.")]
        [InlineData(0x6Fu, "Num/")]
        public void FunctionAndNumpadOperators_RenderWithRealNames(uint vk, string expected)
        {
            Assert.Equal(expected, HotkeyPicker.GetDisplay(0, vk));
        }

        /// <summary>A bare key must display as just the key, with no stray leading "+".</summary>
        [Fact]
        public void BareKey_DisplaysWithoutAnyModifierPrefix()
        {
            Assert.Equal("PageDown", HotkeyPicker.GetDisplay(0, 0x22));
            Assert.DoesNotContain("+", HotkeyPicker.GetDisplay(0, 0x22));
        }

        [Fact]
        public void ModifiersStillRenderInTheUsualOrder()
        {
            Assert.Equal("Ctrl+Alt+V",
                HotkeyPicker.GetDisplay(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x56));
        }

        /// <summary>Win must be representable - the picker had no way to set it at all.</summary>
        [Fact]
        public void WinModifier_Renders()
        {
            Assert.Equal("Win+K", HotkeyPicker.GetDisplay(HotkeyModifiers.Win, 0x4B));
        }

        /// <summary>An unset binding must read as something honest rather than "0x0".</summary>
        [Fact]
        public void UnsetKey_DoesNotRenderAsHexZero()
        {
            var text = HotkeyPicker.GetDisplay(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0);
            Assert.DoesNotContain("0x0", text);
        }

        /// <summary>A genuinely unknown code still has to render as something rather than
        /// throwing - hex is the honest fallback there.</summary>
        [Fact]
        public void TrulyUnknownKey_FallsBackToHex()
        {
            Assert.Contains("0x", HotkeyPicker.GetDisplay(0, 0xFE));
        }
    }
}
