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

        private Rectangle _setBtnRect;

        private uint _modifierMask;
        private uint _virtualKey;
        private bool _capturing;
        private bool _hoverSet;
        private string _captureError = "";

        // Set when RegisterHotKey refused the combo the user just picked (another app owns
        // it). Without this the picker showed the new combo as though it were live while
        // nothing was bound, and the only hint was a "- unavailable" suffix buried in the
        // tray context menu, which nobody opens. That is the whole experience behind
        // "the hotkeys don't work".
        private bool _bindingFailed;

        /// <summary>Fires when the user picks a new combo. Argument is
        /// (modifier mask, virtual key).</summary>
        public event Action<uint, uint>? HotkeyChanged;

        /// <summary>
        /// Tell the picker whether the combo it last raised actually bound. Called by the
        /// owner after RegisterHotKey, so a combo another app already owns is reported where
        /// the user is looking instead of only in the tray menu.
        /// </summary>
        public void ReportBindingResult(bool succeeded)
        {
            _bindingFailed = !succeeded;
            Invalidate();
        }

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
            // No key means nothing is bound, and the modifiers on their own are not a
            // shortcut. "Ctrl+Shift+(none)" reads as a control that has broken rather than
            // one that is simply unset, which is what it actually is.
            if (virtualKey == 0) return "Not set";

            var parts = new System.Collections.Generic.List<string>(4);
            if ((modifierMask & HotkeyModifiers.Control) != 0) parts.Add("Ctrl");
            if ((modifierMask & HotkeyModifiers.Alt) != 0) parts.Add("Alt");
            if ((modifierMask & HotkeyModifiers.Shift) != 0) parts.Add("Shift");
            if ((modifierMask & HotkeyModifiers.Win) != 0) parts.Add("Win");
            parts.Add(KeyName(virtualKey));
            return string.Join("+", parts);
        }

        /// <summary>
        /// Human-readable name for a virtual-key code.
        ///
        /// This used to know only A-Z, 0-9, F1-F12 and the numpad digits, so anything else -
        /// PageDown, Home, the arrows - rendered as a raw "0x22". Now that a bare key can be
        /// bound, those are exactly the keys people reach for, and a hex code is not something
        /// a user can act on.
        /// </summary>
        public static string KeyName(uint vk)
        {
            // Letters A-Z
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
            // Top-row digits 0-9
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
            // Function keys. F1-F12 = 0x70-0x7B, and F13-F24 = 0x7C-0x87 (macro keyboards
            // expose these, and they're ideal bare hotkeys since nothing else uses them).
            if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F);
            // Numpad digits 0-9
            if (vk >= 0x60 && vk <= 0x69) return "Num" + (vk - 0x60);

            switch (vk)
            {
                // Navigation / editing - the keys most likely to be picked as a bare hotkey.
                case 0x21: return "PageUp";
                case 0x22: return "PageDown";
                case 0x23: return "End";
                case 0x24: return "Home";
                case 0x2D: return "Insert";
                case 0x2E: return "Delete";
                case 0x25: return "Left";
                case 0x26: return "Up";
                case 0x27: return "Right";
                case 0x28: return "Down";

                case 0x20: return "Space";
                case 0x09: return "Tab";
                case 0x0D: return "Enter";
                case 0x08: return "Backspace";
                case 0x1B: return "Esc";
                case 0x14: return "CapsLock";
                case 0x91: return "ScrollLock";
                case 0x90: return "NumLock";
                case 0x13: return "Pause";
                case 0x2C: return "PrintScreen";

                // Numpad operators.
                case 0x6A: return "Num*";
                case 0x6B: return "Num+";
                case 0x6D: return "Num-";
                case 0x6E: return "Num.";
                case 0x6F: return "Num/";

                // Punctuation, by their US-layout faces. OEM codes are layout-dependent, so
                // these labels can be wrong on a non-US keyboard - still far better than hex.
                case 0xBA: return ";";
                case 0xBB: return "=";
                case 0xBC: return ",";
                case 0xBD: return "-";
                case 0xBE: return ".";
                case 0xBF: return "/";
                case 0xC0: return "`";
                case 0xDB: return "[";
                case 0xDC: return "\\";
                case 0xDD: return "]";
                case 0xDE: return "'";

                // Media / browser keys, common on gaming keyboards.
                case 0xAD: return "Mute";
                case 0xAE: return "VolumeDown";
                case 0xAF: return "VolumeUp";
                case 0xB0: return "NextTrack";
                case 0xB1: return "PrevTrack";
                case 0xB2: return "StopMedia";
                case 0xB3: return "PlayPause";

                // Not a key. This is what an unbound hotkey looks like, and it used to render
                // as the meaningless "Ctrl+Shift+0x0".
                case 0x00: return "(none)";
            }

            // Genuinely unrecognised - hex, so we never claim a binding we can't name.
            return "0x" + vk.ToString("X");
        }

        /// <summary>
        /// Whether (mask, vk) is a hotkey we're willing to bind, and why not if it isn't.
        ///
        /// Notably a mask of 0 is fine. The picker used to refuse a bare key with "Pick at
        /// least one modifier", on the reasoning that a lone key can't be a global hotkey -
        /// that's simply not true, RegisterHotKey takes fsModifiers = 0 quite happily. The
        /// real consequence is that the key is then taken system-wide, so typing it anywhere
        /// fires the hotkey; that's the user's call to make, and for PageDown / F13-F24 /
        /// media keys it's exactly what people want.
        ///
        /// Pure and public so the rules are unit-testable without driving a real control.
        /// </summary>
        public static bool IsBindable(uint modifierMask, uint virtualKey, out string error)
        {
            error = "";

            if (virtualKey == 0)
            {
                error = "Press a key to bind.";
                return false;
            }

            if (IsModifierOnly(virtualKey))
            {
                // Still mid-chord: they're holding Ctrl and haven't reached the key yet.
                error = "Now press the key.";
                return false;
            }

            // Escape is the picker's own cancel key. Binding it bare would leave no way to
            // back out of capture mode.
            if (modifierMask == 0 && virtualKey == 0x1B)
            {
                error = "Esc cancels - pick another key.";
                return false;
            }

            if (IsReservedCombo(modifierMask, virtualKey))
            {
                error = "That combination is reserved by Windows.";
                return false;
            }

            return true;
        }

        private void EnterCapture()
        {
            _capturing = true;
            _captureError = "";
            _bindingFailed = false; // stale once they're picking a new one
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

        // The Set pill is the only clickable part of this control, and it used to give no
        // sign of that: no hand cursor, no hover. Every other button in PlexusX answers the
        // pointer, so a dead-looking one reads as broken.
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool over = _setBtnRect.Contains(e.Location);
            Cursor = over ? Cursors.Hand : Cursors.Default;
            if (over == _hoverSet) return;
            _hoverSet = over;
            Invalidate(Rectangle.Inflate(_setBtnRect, 2, 2));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Cursor = Cursors.Default;
            if (!_hoverSet) return;
            _hoverSet = false;
            Invalidate(Rectangle.Inflate(_setBtnRect, 2, 2));
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

            // Convert WinForms' Keys to a RegisterHotKey modifier mask. Win isn't in
            // Keys.Modifiers at all, so it's read from the live key state - without this the
            // picker had no way to produce a Win+ binding.
            uint mods = 0;
            if (e.Control) mods |= HotkeyModifiers.Control;
            if (e.Alt) mods |= HotkeyModifiers.Alt;
            if (e.Shift) mods |= HotkeyModifiers.Shift;
            if ((ModifierKeys & Keys.LWin) != 0 || (ModifierKeys & Keys.RWin) != 0
                || IsKeyDown(Keys.LWin) || IsKeyDown(Keys.RWin))
                mods |= HotkeyModifiers.Win;

            uint vk = (uint)e.KeyCode;

            // A bare key is allowed now - see IsBindable.
            if (!IsBindable(mods, vk, out var why))
            {
                // "Now press the key" while they're still holding modifiers isn't an error
                // worth shouting about; keep the prompt calm and wait for the real key.
                _captureError = IsModifierOnly(vk) ? "" : why;
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

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        /// <summary>Win isn't reported through KeyEventArgs, so it has to be sampled
        /// directly to allow a Win+ binding.</summary>
        private static bool IsKeyDown(Keys key) => (GetAsyncKeyState((int)key) & 0x8000) != 0;

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

        /// <summary>
        /// Combos the OS intercepts before our app ever sees the hotkey message. Binding
        /// these looks like it worked and then never fires.
        ///
        /// Takes the RegisterHotKey mask rather than WinForms' Keys, because the old
        /// Keys-based version checked <c>mods.HasFlag(Keys.LWin)</c> - and Keys.Modifiers
        /// never carries the Win key, so the Win+L guard could not ever have matched.
        /// </summary>
        private static bool IsReservedCombo(uint mask, uint vk)
        {
            bool ctrl = (mask & HotkeyModifiers.Control) != 0;
            bool alt = (mask & HotkeyModifiers.Alt) != 0;
            bool win = (mask & HotkeyModifiers.Win) != 0;

            const uint VK_TAB = 0x09, VK_ESC = 0x1B, VK_DELETE = 0x2E, VK_F4 = 0x73, VK_L = 0x4C;

            // Ctrl+Alt+Del - Secure Attention Sequence, not available to user-mode.
            if (ctrl && alt && vk == VK_DELETE) return true;
            // Ctrl+Esc - Start menu.
            if (ctrl && vk == VK_ESC) return true;
            // Alt+Tab / Alt+Esc / Alt+F4 - task switcher, window cycle, close.
            if (alt && (vk == VK_TAB || vk == VK_ESC || vk == VK_F4)) return true;
            // Win+L - lock workstation.
            if (win && vk == VK_L) return true;
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
            string comboText;
            Color comboColor;
            if (_capturing)
            {
                comboText = _captureError.Length > 0
                    ? _captureError
                    : "Press any key (Esc to cancel)";
                comboColor = _captureError.Length > 0 ? Theme.Accent : Theme.Text;
            }
            else if (_bindingFailed)
            {
                // Say it where the user is actually looking, and name the cause - an
                // in-use combo is the overwhelmingly common reason.
                comboText = GetDisplay(_modifierMask, _virtualKey) + "  - in use by another app";
                comboColor = Theme.Accent;
            }
            else
            {
                comboText = GetDisplay(_modifierMask, _virtualKey);
                comboColor = Theme.Text;
            }
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
                accent: _capturing ? Theme.Accent : Theme.Border,
                hover: _hoverSet);
        }

        private static void PaintPillButton(Graphics g, Rectangle rect, string text, Color accent, bool hover)
        {
            using var path = Glass.RoundedPath(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), rect.Height / 2f);
            using (var fill = new SolidBrush(Color.FromArgb(hover ? 210 : 160, accent)))
                g.FillPath(fill, path);
            using (var pen = new Pen(Color.FromArgb(hover ? 230 : 180, accent), 1f))
                g.DrawPath(pen, path);

            TextRenderer.DrawText(g, text, ButtonFont, rect, Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override bool IsInputKey(Keys keyData) => true;
    }
}