using System;
using System.Text.RegularExpressions;

namespace VibranceHud
{
    /// <summary>
    /// Turns release notes into something that reads correctly in a plain WinForms TextBox.
    ///
    /// The "What's new" body is a Multiline TextBox, which renders no markup at all - so notes
    /// written as markdown showed up with the symbols intact ("## What's new", "**Date:**",
    /// backticked words). Notes can arrive either embedded in the build or from the update
    /// feed, and neither source is guaranteed to be plain, so the cleanup happens here rather
    /// than relying on whoever wrote the notes.
    ///
    /// Deliberately not a markdown renderer: it strips the handful of markers that actually
    /// turn up and normalises whitespace. Anything cleverer would mean owning a parser for the
    /// sake of one read-only dialog.
    /// </summary>
    public static class ReleaseNotesText
    {
        public static string ToPlainText(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return "";

            var s = notes.Replace("\r\n", "\n").Replace('\r', '\n');

            // Headings: drop the leading #s but keep the text as its own line.
            s = Regex.Replace(s, @"^\s{0,3}#{1,6}\s*", "", RegexOptions.Multiline);

            // Bullets: normalise -, * and + into a real bullet, preserving indentation.
            s = Regex.Replace(s, @"^(\s*)[-*+]\s+", "$1• ", RegexOptions.Multiline);

            // Emphasis markers. Done after bullets so a leading "* item" isn't mistaken for
            // emphasis, and non-greedy so two bold runs on one line don't merge.
            s = Regex.Replace(s, @"\*\*(.+?)\*\*", "$1");
            s = Regex.Replace(s, @"__(.+?)__", "$1");
            s = Regex.Replace(s, @"(?<!\*)\*(?!\s)(.+?)(?<!\s)\*(?!\*)", "$1");

            // Inline code and fenced blocks - keep the contents, drop the ticks.
            s = Regex.Replace(s, @"```[a-zA-Z0-9]*\n?", "");
            s = s.Replace("`", "");

            // Markdown links: keep the label, drop the URL plumbing. A bare URL on its own is
            // left intact so a Discord invite stays clickable-by-copy.
            s = Regex.Replace(s, @"\[([^\]]+)\]\([^)]+\)", "$1");

            // Horizontal rules read as junk in a text box.
            s = Regex.Replace(s, @"^\s*([-*_]\s*){3,}$", "", RegexOptions.Multiline);

            // Trailing spaces, then collapse runs of blank lines to a single separator so the
            // dialog doesn't open on a big empty gap.
            s = Regex.Replace(s, @"[ \t]+$", "", RegexOptions.Multiline);
            s = Regex.Replace(s, @"\n{3,}", "\n\n");

            return s.Trim().Replace("\n", Environment.NewLine);
        }
    }
}
