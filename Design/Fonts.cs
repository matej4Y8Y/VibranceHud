using System.Drawing;

namespace VibranceHud.Design
{
    /// <summary>The named type roles. Seven, replacing twelve ad-hoc sizes.</summary>
    public enum FontRole { Display, Title, Heading, Body, Label, Caption, Micro }

    /// <summary>
    /// Cached fonts, one instance per role.
    ///
    /// Two problems solved at once. First, the app used twelve distinct point sizes across
    /// 98 <c>new Font(...)</c> calls, which is what a UI with no type scale looks like.
    /// Second, several of those calls were inside OnPaint - NavButton and GameCard among
    /// them - and the animation timer invalidates the nav thirty times a second across nine
    /// buttons. That was roughly 270 font objects allocated per second to draw text that
    /// never changes.
    ///
    /// Callers must NOT dispose what they get from here. Call <see cref="Rebuild"/> after a
    /// DPI or font-family change; it disposes the old set and the next access rebuilds.
    /// </summary>
    public static class Fonts
    {
        private static Font? _display, _title, _heading, _body, _label, _caption, _micro;
        private static Font? _bodyBold, _labelBold, _captionBold;

        public static Font Display => _display ??= Make(20f, FontStyle.Bold);
        public static Font Title => _title ??= Make(15f, FontStyle.Bold);
        public static Font Heading => _heading ??= Make(11.5f, FontStyle.Bold);
        public static Font Body => _body ??= Make(9.5f);
        public static Font Label => _label ??= Make(9f);
        public static Font Caption => _caption ??= Make(8.5f);
        public static Font Micro => _micro ??= Make(7.5f, FontStyle.Bold);

        public static Font BodyBold => _bodyBold ??= Make(9.5f, FontStyle.Bold);
        public static Font LabelBold => _labelBold ??= Make(9f, FontStyle.Bold);
        public static Font CaptionBold => _captionBold ??= Make(8.5f, FontStyle.Bold);

        /// <summary>Resolve a role to its regular-weight font.</summary>
        public static Font For(FontRole role) => role switch
        {
            FontRole.Display => Display,
            FontRole.Title => Title,
            FontRole.Heading => Heading,
            FontRole.Body => Body,
            FontRole.Label => Label,
            FontRole.Caption => Caption,
            _ => Micro,
        };

        private static Font Make(float size, FontStyle style = FontStyle.Regular)
            => new(Theme.FontFamily, size, style);

        /// <summary>
        /// Drop every cached font so the next access rebuilds. Safe to call repeatedly, and
        /// safe to call before anything has been created.
        /// </summary>
        public static void Rebuild()
        {
            foreach (var f in new[] { _display, _title, _heading, _body, _label, _caption,
                                      _micro, _bodyBold, _labelBold, _captionBold })
                f?.Dispose();

            _display = _title = _heading = _body = _label = _caption = _micro = null;
            _bodyBold = _labelBold = _captionBold = null;
        }
    }
}
