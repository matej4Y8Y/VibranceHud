using System;
using System.Drawing;
using System.Windows.Forms;
using VibranceHud.Design;

namespace VibranceHud
{
    /// <summary>
    /// "Use my current look for this game", on the game's own page.
    ///
    /// Replaces the Profile Editor page outright. That page was the only one in the app that
    /// duplicated another page's controls: to give a game its own colours you tuned Display,
    /// then tuned the same values a second time in the editor. Nobody does that twice, which
    /// is why the feature went unused - diagnosed in
    /// docs/superpowers/specs/2026-08-03-road-to-1.0-structure.md as the highest-value
    /// cleanup left.
    ///
    /// So there are no sliders here. What you see on Display is what gets saved, and the only
    /// decisions are "save it" and "forget it".
    ///
    /// Not a Control, for the same reason as <see cref="SliderRow"/> and
    /// <see cref="AdvancedColorSection"/>: nesting a transparent container inside the
    /// transparent card is what caused the ghosting documented in HANDOFF.md.
    /// </summary>
    public sealed class GameProfileSection
    {
        private readonly IVibranceEngine _engine;
        private readonly string _gameId;
        private readonly string _gameName;

        private readonly Label _caption;
        private readonly Label _summary;
        private readonly GlassButton _save;
        private readonly GlassButton _forget;

        public GameProfileSection(Control parent, string gameId, string gameName,
            IVibranceEngine engine, Font captionFont, Font bodyFont)
        {
            _engine = engine;
            _gameId = gameId;
            _gameName = gameName;

            _caption = new Label
            {
                Text = UiHelpers.Spaced("PROFILE"),
                Font = captionFont,
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            parent.Controls.Add(_caption);

            _summary = new Label
            {
                Font = bodyFont,
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                AutoSize = false,
                TextAlign = ContentAlignment.TopLeft,
            };
            parent.Controls.Add(_summary);

            _save = new GlassButton
            {
                Text = $"Use my current look for {Shorten(gameName)}",
                Kind = GlassButtonKind.Primary,
            };
            _save.Click += (_, _) => Save();
            parent.Controls.Add(_save);

            _forget = new GlassButton { Text = "Forget", Kind = GlassButtonKind.Ghost };
            _forget.Click += (_, _) => Forget();
            parent.Controls.Add(_forget);

            Refresh();
        }

        /// <summary>Long game names would push the button past the card; the section header
        /// already says which game this page is.</summary>
        private static string Shorten(string name) =>
            name.Length <= 14 ? name : name.Substring(0, 13).TrimEnd() + "…";

        // ---- behaviour -------------------------------------------------------------------

        private void Save()
        {
            GameProfileStore.Set(Capture(_gameId, _gameName, _engine, GameProfileStore.Get(_gameId)));
            Refresh();
        }

        /// <summary>
        /// Turn the live engine state into a profile.
        ///
        /// Separated from <see cref="Save"/> so it can be tested without writing to the
        /// user's real profiles.json - the store's parameterless Set goes straight to the
        /// live file.
        /// </summary>
        internal static GameProfile Capture(string gameId, string gameName,
            IVibranceEngine engine, GameProfile? existing) => new()
        {
            GameId = gameId,
            DisplayName = gameName,
            Vibrance = engine.Vibrance,
            Saturation = engine.Saturation,
            Brightness = engine.Brightness,
            Gamma = engine.Gamma,
            Contrast = engine.Contrast,
            Temperature = engine.Temperature,
            Tone = engine.Tone,

            // Whatever the game-hub side of the profile already held is preserved. This
            // section only owns the colour half; wiping the rest because the user pressed a
            // colour button would be silent data loss.
            GameHub = existing?.GameHub ?? new GameHubOptions(),
            LastUpdated = DateTime.UtcNow,
        };

        private void Forget()
        {
            GameProfileStore.Remove(_gameId);
            Refresh();
        }

        /// <summary>Re-read what is stored and restate it. Cheap, and called after every
        /// change so the line on screen can never describe a profile that is gone.</summary>
        public void Refresh()
        {
            var profile = GameProfileStore.Get(_gameId);

            if (profile == null)
            {
                _summary.Text =
                    $"No profile yet. Set your colours on Display, then save them here and "
                    + $"PlexusX will switch to them whenever {_gameName} opens.";
                _forget.Visible = false;
                _save.Text = $"Use my current look for {Shorten(_gameName)}";
                return;
            }

            _summary.Text = Describe(profile) + "  —  applied when " + _gameName
                          + " opens, put back when it closes.";
            _forget.Visible = true;
            _save.Text = "Update to my current look";
        }

        /// <summary>
        /// The saved look in one line.
        ///
        /// Only the values that are actually doing something are named. Listing every field
        /// including the neutral ones turns the line into noise and buries the one or two
        /// settings the user actually chose.
        /// </summary>
        internal static string Describe(GameProfile p)
        {
            var parts = new System.Collections.Generic.List<string>();

            if (p.Saturation != 100) parts.Add($"saturation {p.Saturation}");
            if (p.Vibrance != 100) parts.Add($"vibrance {p.Vibrance}");
            if (p.Brightness != 100) parts.Add($"brightness {p.Brightness}");
            // Invariant: this summary line is English prose, so the decimal has to be a point
            // regardless of the machine's locale.
            if (p.Gamma != 100)
                parts.Add("gamma " + (p.Gamma / 100f).ToString(
                    "0.00", System.Globalization.CultureInfo.InvariantCulture));
            if (p.ResolvedContrast != 100) parts.Add($"contrast {p.ResolvedContrast}");

            if (p.ResolvedTemperature != 0)
                parts.Add(p.ResolvedTemperature > 0
                    ? $"warm {p.ResolvedTemperature}"
                    : $"cool {-p.ResolvedTemperature}");

            if (!p.ResolvedTone.IsGammaOnly) parts.Add("advanced colour");

            return parts.Count == 0 ? "Saved: neutral" : "Saved: " + string.Join(", ", parts);
        }

        /// <summary>
        /// Build a ready-placed card for a game page.
        ///
        /// The game pages lay themselves out with an absolute y cursor, so this hands back a
        /// sized card they can drop in and advance past. Keeping the construction here means
        /// four pages get the section in three lines each instead of four copies of the same
        /// twenty, which is how they would drift apart.
        ///
        /// The returned card holds the buttons, the buttons hold the section's handlers, so
        /// the section stays alive without the page having to keep a field for it.
        /// </summary>
        public static CardPanel BuildCard(string gameId, string gameName,
            IVibranceEngine engine, int x, int y, int width)
        {
            var card = new CardPanel { Location = new Point(x, y), Size = new Size(width, 10) };

            var section = new GameProfileSection(card, gameId, gameName, engine,
                Fonts.Micro, Fonts.Caption);

            int pad = Tokens.Scale(18);
            section.Place(pad, pad, width - 2 * pad);
            card.Height = section.PreferredHeight + 2 * pad;

            return card;
        }

        // ---- layout ----------------------------------------------------------------------

        public int PreferredHeight =>
            Tokens.Scale(22) + Tokens.Scale(6)      // caption
            + Tokens.Scale(38)                       // summary, two lines
            + Tokens.Scale(38);                      // button row

        public void Place(int x, int y, int width)
        {
            int labelH = Tokens.Scale(22);
            int btnH = Tokens.Scale(32);
            int forgetW = Tokens.Scale(84);
            int gap = Tokens.Scale(Tokens.S);

            _caption.SetBounds(x, y, width, labelH);
            y += labelH + Tokens.Scale(6);

            _summary.SetBounds(x, y, width, Tokens.Scale(36));
            y += Tokens.Scale(38);

            int saveW = Math.Min(Tokens.Scale(260), width - forgetW - gap);
            _save.SetBounds(x, y, saveW, btnH);
            _forget.SetBounds(x + saveW + gap, y, forgetW, btnH);
        }
    }
}
