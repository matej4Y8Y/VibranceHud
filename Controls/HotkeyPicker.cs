using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// The Win32 RegisterHotKey modifier bits (named here so the rest of the codebase can
    /// talk about "Ctrl" instead of 0x0002). Same values as the user32 constants -
    /// MOD_ALT=0x0001, MOD_CONTROL=0x0002, MOD_SHIFT=0x0004, MOD_WIN=0x0008.
    /// </summary>
    public static class HotkeyModifiers
    {
        public const uint Alt = 0x0001;
        public const uint Control = 0x0002;
        public const uint Shift = 0x0004;
        public const uint Win = 0x0008;
    }

    /// <summary>
    /// Virtual-key codes for the non-modifier keys a user is realistically going to pick
    /// as a global hotkey. Any value we don't recognise here is rendered as 0xHEX so the
    /// picker still shows something when an older settings file hands us a key we don't
    /// ship a constant for.
    /// </summary>
    public static class HotkeyKeys
    {
        // Top-row digits and alphabet, matching WinForms Keys.
        public const uint D0 = 0x30, D1 = 0x31, D2 = 0x32, D3 = 0x33, D4 = 0x34;
        public const uint D5 = 0x35, D6 = 0x36, D7 = 0x37, D8 = 0x38, D9 = 0x39;
        public const uint A = 0x41, B = 0x42, C = 0x43, D = 0x44, E = 0x45;
        public const uint F = 0x46, G = 0x47, H = 0x48, I = 0x49, J = 0x4A;
        public const uint K = 0x4B, L = 0x4C, M = 0x4D, N = 0x4E, O = 0x4F;
        public const uint P = 0x50, Q = 0x51, R = 0x52, S = 0x53, T = 0x54;
        public const uint U = 0x55, V = 0x56, W = 0x57, X = 0x58, Y = 0x59, Z = 0x5A;

        // Function row.
        public const uint F1 = 0x70, F2 = 0x71, F3 = 0x72, F4 = 0x73;
        public const uint F5 = 0x74, F6 = 0x75, F7 = 0x76, F8 = 0x77;
        public const uint F9 = 0x78, F10 = 0x79, F11 = 0x7A, F12 = 0x7B;
    }

    /// <summary>
    /// A user-configurable keyboard shortcut, shrunk down to the bare essentials: a
    /// single-line chip that displays the current combo ("Ctrl + Alt + V") and a single
    /// "Set" button on the right. Click "Set" to enter capture mode (an Esc cancels,
    /// anything else commits). No reset button, no helper caption, no surrounding panel
    /// - the page paints its own caption above and the page lays us out as a small
    /// ~300x40 slot at the bottom of the column.
    ///
    /// The pure helper <see cref="GetDisplay"/> is exposed so the tray menu can render
    /// the same combo text without holding a control reference.
    /// </summary>
    public sealed class HotkeyPicker : Control
    {
        private const int ButtonWidth = 64;
        private const int ButtonHeight = 26;
        private const int ChipPadding = 10;
        // Compact single-row layout. The page sizes this control via SetBounds to a
        // ~300x40 slot at the bottom of the VibrancePage column; the defaults here
        // give a reasonable size when the control is created standalone.
        public static readonly Size PickerDefaultSize = new(300, 40);
        public static readonly Size PickerMinimumSize = new(240, 36);

        private static readonly Font ComboFont = new(Theme.FontFamily, 11f, FontStyle.Bold);
        private static readonly Font ButtonFont = new(Theme.FontFamily, 8.5f, FontStyle.Bold);
        private static readonly Font HintFont = new(Theme.FontFamily, 8f, FontStyle.Italic);

        private Rectangle _setBtnRect;

        private uint _modifierMask;
        private uint _virtualKey;
        private bool _capturing;
        private string _captureError = "";

        /// <summary>Fires when the user picks a new combo. Argument is
        /// (modifier mask, virtual key).</summary>
        public event Action<uint, uint>? HotkeyChanged;

        public HotkeyPicker()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.Selectable, true);
            // SupportsTransparentBackColor requires BackColor = Color.Transparent to be
            // assigned in the same constructor (the SetStyle call alone won't apply).
            BackColor = Color.Transparent;
            // Single-row layout: combo chip on the left, single Set button on the
            // right. The page paints its own caption above us so we stay borderless
            // and let the column's frosted-glass panel show through.
            Size = PickerDefaultSize;
            MinimumSize = PickerMinimumSize;
            TabStop = true;

            _modifierMask = HotkeyModifiers.Control | HotkeyModifiers.Alt;
            _virtualKey = HotkeyKeys.V;

            // Initial rects matter only until the first paint sets them for real.
            _setBtnRect = new Rectangle(0, 0, ButtonWidth, ButtonHeight);
        }

        /// <summary>Initial / currently-persisted value. Defaults to Ctrl+Alt+V when the
        /// property isn't used (preserves the prior hardcoded behaviour for existing
        /// users).</summary>
        public uint ModifierMask
        {
            get => _modifierMask;
            set { _modifierMask = value; _capturing = false; _captureError = ""; Invalidate(); }
        }

        public uint VirtualKey
        {
            get => _virtualKey;
            set { _virtualKey = value; _capturing = false; _captureError = ""; Invalidate(); }
        }

        /// <summary>Render a Win32 modifier mask + virtual key as a human-readable string,
        /// e.g. (MOD_CONTROL|MOD_ALT, VK_F1) -> "Ctrl+Alt+F1". Used by the tray menu and
        /// the picker itself so the two never disagree.</summary>
        public static string GetDisplay(uint modifierMask, uint virtualKey)
        {
            var parts = new System.Collections.Generic.List<string>(4);
            if ((modifierMask & HotkeyModifiers.Control) != 0) parts.Add("Ctrl");
            if ((modifierMask & HotkeyModifiers.Alt) != 0) parts.Add("Alt");
            if ((modifierMask & HotkeyModifiers.Shift) != 0) parts.Add("Shift");
            if ((modifierMask & HotkeyModifiers.Win) != 0) parts.Add("Win");
            parts.Add(KeyName(virtualKey));
            return string.Join("+", parts);
        }

        private static string KeyName(uint vk)
        {
            // Letters A-Z
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
            // Top-row digits 0-9
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
            // Function keys F1-F12
            if (vk >= 0x70 && vk <= 0x7B) return "F" + (vk - 0x6F);
            // Numpad digits 0-9 (VK_NUMPAD0..9 = 0x60..0x69)
            if (vk >= 0x60 && vk <= 0x69) return "Num" + (vk - 0x60);
            // Anything else - render as hex so we never lie about what's bound.
            return "0x" + vk.ToString("X");
        }

        private void EnterCapture()
        {
            _capturing = true;
            _captureError = "";
            Focus(); // so KeyDown actually fires
            Invalidate();
        }

        private void ExitCapture()
        {
            _capturing = false;
            _captureError = "";
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            if (_setBtnRect.Contains(e.Location))
            {
                if (_capturing) ExitCapture();
                else EnterCapture();
                return;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Capture mode is the only state that handles keys.
            if (!_capturing) return;

            // Esc cancels; everything else is "candidate until we have a non-modifier".
            if (e.KeyCode == Keys.Escape)
            {
                ExitCapture();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            // Reject Windows-reserved combos: those are owned by the shell / SAS, not by
            // user-mode apps. Trying to bind them looks like it worked until the user
            // notices their hotkey never fires.
            if (IsReservedCombo(e.Modifiers, e.KeyCode))
            {
                _captureError = "That combination is reserved by Windows.";
                Invalidate();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            // Convert WinForms Keys enum to a RegisterHotKey modifier mask. Same bits as
            // HotkeyModifiers, so the assignment is mechanical - we just sanity-check that
            // Keys.Control/Shift/Alt really do map to MOD_CONTROL/MOD_SHIFT/MOD_ALT.
            uint mods = 0;
            if (e.Control) mods |= HotkeyModifiers.Control;
            if (e.Alt) mods |= HotkeyModifiers.Alt;
            if (e.Shift) mods |= HotkeyModifiers.Shift;

            uint vk = (uint)e.KeyCode;

            // Reject bare modifier presses (no actual key picked yet). One of Ctrl/Alt/
            // Shift/Win alone is what the user gets while they're still holding the
            // modifiers down before the key; we wait.
            if (IsModifierOnly(vk))
            {
                // Allow the user to keep holding modifiers - just don't commit yet.
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            // Require at least one modifier so a single key on its own doesn't get bound
            // (you can't reliably register "press V" as a global hotkey anyway).
            if (mods == 0)
            {
                _captureError = "Pick at least one modifier (Ctrl, Alt, Shift or Win).";
                Invalidate();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            _modifierMask = mods;
            _virtualKey = vk;
            ExitCapture();
            e.Handled = true;
            e.SuppressKeyPress = true;
            HotkeyChanged?.Invoke(_modifierMask, _virtualKey);
        }

        /// <summary>True when the user only has a modifier pressed (no real key yet).</summary>
        private static bool IsModifierOnly(uint vk)
        {
            return vk == (uint)Keys.ControlKey
                || vk == (uint)Keys.ShiftKey
                || vk == (uint)Keys.Menu    // Alt
                || vk == (uint)Keys.LMenu
                || vk == (uint)Keys.RMenu
                || vk == (uint)Keys.LControlKey
                || vk == (uint)Keys.RControlKey
                || vk == (uint)Keys.LShiftKey
                || vk == (uint)Keys.RShiftKey
                || vk == (uint)Keys.LWin
                || vk == (uint)Keys.RWin;
        }

        /// <summary>Combos the OS intercepts before our app ever sees the hotkey
        /// message. Trying to bind these would silently never fire.</summary>
        private static bool IsReservedCombo(Keys mods, Keys key)
        {
            // Ctrl+Alt+Del -> handled by SAS, not user-mode.
            if (mods.HasFlag(Keys.Control) && mods.HasFlag(Keys.Alt) && key == Keys.Delete) return true;
            // Ctrl+Esc -> opens Start menu (Windows reserved).
            if (mods.HasFlag(Keys.Control) && key == Keys.Escape) return true;
            // Alt+Tab, Alt+Esc, Alt+F4 -> task switcher / Start menu / close-app.
            if (mods.HasFlag(Keys.Alt) && (key == Keys.Tab || key == Keys.Escape || key == Keys.F4)) return true;
            // Win+L -> lock workstation.
            if (mods.HasFlag(Keys.LWin) && key == Keys.L) return true;
            if (mods.HasFlag(Keys.RWin) && key == Keys.L) return true;
            return false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Transparent, borderless single row. No rounded panel, no border pen -
            // just the chip text on the left and the Set/Cancel button on the right,
            // so the page's frosted-glass card shows straight through.

            // Vertically centre everything in our height.
            int btnY = (Height - ButtonHeight) / 2;

            // Combo chip text on the left, leaving room on the right for the button.
            var comboRect = new RectangleF(
                ChipPadding, 0,
                Math.Max(60, Width - ButtonWidth - ChipPadding * 3), Height);
            string comboText = _capturing
                ? (_captureError.Length > 0 ? _captureError : "Press new shortcut (Esc to cancel)")
                : GetDisplay(_modifierMask, _virtualKey);
            var comboColor = _capturing
                ? (_captureError.Length > 0 ? Theme.Accent : Theme.Text)
                : Theme.Text;
            TextRenderer.DrawText(g, comboText, ComboFont,
                Rectangle.Round(comboRect), comboColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis);

            // Set/Cancel button on the right edge.
            _setBtnRect.X = Width - ButtonWidth - ChipPadding;
            _setBtnRect.Y = btnY;
            _setBtnRect.Width = ButtonWidth;
            _setBtnRect.Height = ButtonHeight;
            PaintPillButton(g, _setBtnRect,
                _capturing ? "Cancel" : "Set",
                accent: _capturing ? Theme.Accent : Theme.Border);
        }

        private static void PaintPillButton(Graphics g, Rectangle rect, string text, Color accent)
        {
            using var path = Glass.RoundedPath(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), rect.Height / 2f);
            using (var fill = new SolidBrush(Color.FromArgb(160, accent)))
                g.FillPath(fill, path);
            using (var pen = new Pen(Color.FromArgb(180, accent), 1f))
                g.DrawPath(pen, path);

            TextRenderer.DrawText(g, text, ButtonFont, rect, Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override bool IsInputKey(Keys keyData) => true;
    }
}