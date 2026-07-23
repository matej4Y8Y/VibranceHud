using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>Small shared builders so pages stay consistent and DRY.</summary>
    internal static class UiHelpers
    {
        /// <summary>"PRESETS" -> "P R E S E T S" with wider gaps between words.</summary>
        public static string Spaced(string text) =>
            string.Join("   ", text.Split(' ').Select(w => string.Join(" ", w.ToCharArray())));

        public static Label Caption(string text, int x, int y, int width,
            ContentAlignment align = ContentAlignment.MiddleLeft) => new()
        {
            Text = Spaced(text),
            ForeColor = Theme.TextDim,
            Font = new Font(Theme.FontFamily, 8f, FontStyle.Bold),
            Location = new Point(x, y),
            Size = new Size(width, 16),
            TextAlign = align,
            BackColor = Color.Transparent
        };
    }
}
