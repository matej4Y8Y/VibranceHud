// The 1x1 topmost pixel that keeps Windows compositing the desktop, so the colour effect
// lands in what OBS / Discord share / Medal capture. See CompositionKeeper for the why.
//
// These are integration-level on purpose: the whole thing is Win32 window management, so a
// unit test with fakes would only assert that C# calls the methods I wrote. What matters is
// that a real window comes up, reports topmost + click-through + layered, and goes away
// cleanly.

using System;
using System.Runtime.InteropServices;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class CompositionKeeperTests
    {
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string? cls, string? title);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [Fact]
        public void Keeper_CreatesItsWindow()
        {
            using var keeper = new CompositionKeeper();
            Assert.True(keeper.IsActive, "the keeper window failed to come up");
        }

        /// <summary>Topmost is the whole mechanism - without it Windows is free to bypass
        /// composition again, which is the bug this exists to prevent.</summary>
        [Fact]
        public void KeeperWindow_IsTopmost()
        {
            using var keeper = new CompositionKeeper();
            var hwnd = FindWindow("PlexusXCompositionKeeper", null);
            Assert.NotEqual(IntPtr.Zero, hwnd);
            Assert.True((GetWindowLong(hwnd, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0,
                "keeper window is not topmost, so it won't force composition");
        }

        /// <summary>It sits over everything, so it must never eat a click or show up in
        /// alt-tab / the taskbar.</summary>
        [Fact]
        public void KeeperWindow_IsClickThrough_AndHiddenFromTaskbar()
        {
            using var keeper = new CompositionKeeper();
            var hwnd = FindWindow("PlexusXCompositionKeeper", null);
            var ex = GetWindowLong(hwnd, GWL_EXSTYLE);

            Assert.True((ex & WS_EX_TRANSPARENT) != 0, "would swallow mouse clicks");
            Assert.True((ex & WS_EX_TOOLWINDOW) != 0, "would appear in alt-tab / taskbar");
            Assert.True((ex & WS_EX_LAYERED) != 0, "needs to be layered to be near-invisible");
        }

        [Fact]
        public void Dispose_RemovesTheWindow()
        {
            IntPtr hwnd;
            using (var keeper = new CompositionKeeper())
            {
                hwnd = FindWindow("PlexusXCompositionKeeper", null);
                Assert.NotEqual(IntPtr.Zero, hwnd);
            }
            Assert.False(IsWindow(hwnd), "keeper window outlived its owner");
        }

        /// <summary>Called twice on shutdown paths; must not throw the second time.</summary>
        [Fact]
        public void Dispose_IsIdempotent()
        {
            var keeper = new CompositionKeeper();
            keeper.Dispose();
            keeper.Dispose();
            Assert.False(keeper.IsActive);
        }

        /// <summary>Several instances must not fight over the shared window class - the class
        /// is registered once per process.</summary>
        [Fact]
        public void MultipleKeepers_CanCoexist()
        {
            using var a = new CompositionKeeper();
            using var b = new CompositionKeeper();
            Assert.True(a.IsActive);
            Assert.True(b.IsActive);
        }
    }
}
