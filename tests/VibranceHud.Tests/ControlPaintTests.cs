using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using VibranceHud;
using VibranceHud.Controls;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Actually draws every control, in every state.
    ///
    /// This exists because a crash shipped that 1025 tests did not catch. The focus-ring work
    /// was tested for the things around painting - TabStop set, role correct, Space activates
    /// - and never once for painting itself. NavButton asked for a focus ring with a radius of
    /// zero, GDI+ throws ArgumentException on a zero-diameter arc, and the exception came out
    /// of OnPaint: the user got "Parameter is not valid" and a blank white box where the
    /// control should have been.
    ///
    /// Laying a control out is not the same as rendering it. These render.
    /// </summary>
    [Collection("Theme serial")]
    public sealed class ControlPaintTests
    {
        /// <summary>Every owner-drawn control in the app, as a factory.</summary>
        public static IEnumerable<object[]> AllControls()
        {
            yield return new object[] { "GlassButton", () => (Control)new GlassButton { Text = "Apply", Size = new Size(120, 34) } };
            yield return new object[] { "GlassButton-primary", () => (Control)new GlassButton { Text = "Apply", Kind = GlassButtonKind.Primary, Size = new Size(120, 34) } };
            yield return new object[] { "ToggleSwitch", () => (Control)new ToggleSwitch() };
            yield return new object[] { "ChipButton", () => (Control)new ChipButton { Text = "Balanced", Size = new Size(140, 32) } };
            yield return new object[] { "NavButton", () => (Control)new NavButton { Text = "Display", Size = new Size(210, 46) } };
            yield return new object[] { "FlatSlider", () => (Control)new FlatSlider { Size = new Size(300, 32), Minimum = 0, Maximum = 100, Value = 50 } };
            yield return new object[] { "TwoColorSlider", () => (Control)new TwoColorSlider { Size = new Size(300, 32), Minimum = 0, Maximum = 100, Value = 50 } };
            yield return new object[] { "PresetChip", () => (Control)new PresetChip { Caption = "Forest", Size = new Size(140, 74) } };
            yield return new object[] { "CardPanel", () => (Control)new CardPanel { Size = new Size(400, 200) } };
            yield return new object[] { "SwatchButton", () => (Control)new SwatchButton(ThemeCatalog.ByName("Violet")) { Size = new Size(40, 40) } };
            yield return new object[] { "KeyboardView", () => (Control)new KeyboardView { Size = new Size(600, 300) } };
        }

        [Theory]
        [MemberData(nameof(AllControls))]
        public void PaintsWithoutThrowing(string name, Func<Control> make)
        {
            Theme.Apply("Violet");
            using var control = make();

            Render(control, name, "normal");
        }

        /// <summary>
        /// The exact case that shipped broken. A focused control has to draw, and the ring is
        /// the newest and least-exercised part of every one of these paint methods.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllControls))]
        public void PaintsWhileFocusedWithoutThrowing(string name, Func<Control> make)
        {
            Theme.Apply("Violet");
            using var control = make();

            SetFocused(control, true);
            Render(control, name, "focused");
        }

        /// <summary>
        /// Degenerate sizes. Layout can hand a control a zero or one-pixel box while a page is
        /// mid-resize, and a paint that throws there takes the whole app down.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllControls))]
        public void PaintsAtAbsurdSizesWithoutThrowing(string name, Func<Control> make)
        {
            Theme.Apply("Violet");

            foreach (var size in new[] { new Size(1, 1), new Size(4, 4), new Size(2000, 40) })
            {
                using var control = make();
                control.Size = size;
                Render(control, name, $"{size.Width}x{size.Height}");
            }
        }

        [Theory]
        [MemberData(nameof(AllControls))]
        public void PaintsInEveryThemeWithoutThrowing(string name, Func<Control> make)
        {
            foreach (var palette in ThemeCatalog.All)
            {
                Theme.Apply(palette.Name);
                using var control = make();
                Render(control, name, palette.Name);
            }

            Theme.Apply("Violet");
        }

        // ---- helpers ---------------------------------------------------------------------

        /// <summary>
        /// Drive the control's own OnPaint against a real GDI+ surface.
        ///
        /// DrawToBitmap is deliberately not used: it routes through WM_PRINT and swallows
        /// exceptions on some paths, which is exactly the failure mode being tested for.
        /// Calling OnPaint directly means anything it throws reaches the test.
        /// </summary>
        private static void Render(Control control, string name, string state)
        {
            int w = Math.Max(1, control.Width);
            int h = Math.Max(1, control.Height);

            using var bitmap = new Bitmap(w, h);
            using var g = Graphics.FromImage(bitmap);

            var onPaint = control.GetType().GetMethod("OnPaint",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.True(onPaint != null, $"{name} has no OnPaint to exercise");

            var args = new PaintEventArgs(g, new Rectangle(0, 0, w, h));

            var ex = Record.Exception(() => onPaint!.Invoke(control, new object[] { args }));

            // Reflection wraps whatever OnPaint threw; the inner one is the real fault.
            if (ex is TargetInvocationException tie && tie.InnerException != null)
                Assert.Fail($"{name} ({state}) threw while painting: " +
                            $"{tie.InnerException.GetType().Name}: {tie.InnerException.Message}");

            Assert.Null(ex);
        }

        /// <summary>Control.Focused is read-only and needs a real message loop, so the
        /// underlying state flag is set directly.</summary>
        private static void SetFocused(Control control, bool focused)
        {
            const int STATE_FOCUSED = 0x00000002;

            var setState = typeof(Control).GetMethod("SetState",
                BindingFlags.Instance | BindingFlags.NonPublic);

            setState?.Invoke(control, new object[] { STATE_FOCUSED, focused });
        }
    }
}
