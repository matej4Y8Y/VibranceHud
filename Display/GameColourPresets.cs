using System;
using System.Collections.Generic;
using System.Drawing;

namespace VibranceHud.Display
{
    /// <summary>
    /// One look. Everything a preset can set, and nothing it cannot.
    ///
    /// <paramref name="Why"/> is shown to the user and has to say what the preset is FOR, not
    /// what it does to the numbers - "the sliders are higher" is not a reason to pick one.
    /// </summary>
    public sealed record ColourPreset(
        string Name,
        string Why,
        int Vibrance,
        int Saturation,
        int Brightness,
        int Contrast,
        int Temperature,
        ToneSettings Tone);

    /// <summary>Presets for one game, in the order they are shown.</summary>
    public sealed record GamePresetGroup(string Game, IReadOnlyList<ColourPreset> Presets);

    /// <summary>
    /// Colour presets, keyed by game.
    ///
    /// This is what replaced the Games Hub. The Hub tried to be a worse version of each
    /// game's own settings menu; this does the one thing no config file can - it changes what
    /// the monitor shows, in a way tuned to that game's palette. Nothing here is written to
    /// any game, and nothing here can break when a game updates.
    ///
    /// Every preset is reachable on every machine: these are all software colour, so none of
    /// them depend on a GPU vendor or a monitor feature.
    /// </summary>
    public static class GameColourPresets
    {
        /// <summary>The look with nothing applied. Always first, so "put it back" is never
        /// more than one click away and never requires knowing what neutral was.</summary>
        /// <remarks>
        /// Vibrance 50, not 0.
        ///
        /// The vibrance scale is 0 = greyscale, 50 = untouched, 200 = the ceiling - it is not
        /// a percentage added to normal. This preset shipped at 0 for a few hours, which on
        /// any AMD or Intel machine meant "Neutral" drained every colour off the screen. On
        /// NVIDIA it happened to look fine, because the driver owns everything below 100 and
        /// the matrix is left at 1.0 either way, which is exactly the kind of bug that only
        /// shows up on somebody else's PC.
        /// </remarks>
        public static readonly ColourPreset Neutral = new(
            "Neutral", "Your screen exactly as Windows leaves it.",
            Vibrance: 50, Saturation: 100, Brightness: 100, Contrast: 100, Temperature: 0,
            Tone: ToneSettings.Neutral);

        /// <remarks>
        /// These use the engine's actual range.
        ///
        /// The first set topped out at vibrance 120 against a maximum of 350, which is under a
        /// third of the axis - every preset sat in a narrow band around neutral and none of
        /// them looked like a decision anybody had made. The scale to hold in mind:
        ///
        ///     vibrance    0 grey · 50 untouched · 200 strong · 350 maximum
        ///     saturation  0 grey · 100 untouched · 300 maximum
        ///     brightness  50 dark · 100 untouched · 170 maximum
        ///     gamma       50 · 100 untouched · 150      (shown as 0.50 to 1.50)
        ///     contrast    50 flat · 100 untouched · 150
        ///     temperature -100 cool · 0 neutral · 100 warm
        ///
        /// The PvP entries are deliberately extreme. They pair heavy vibrance with reduced
        /// brightness and lifted gamma: the picture gets darker overall while the midtones come
        /// up, so shadowed areas stay readable instead of being washed flat by raw brightness.
        /// </remarks>
        public static readonly IReadOnlyList<GamePresetGroup> All = new[]
        {
            // Rust is brown, grey and green almost everywhere, which is exactly the palette
            // the eye is worst at separating. These lean on separating cloth and skin from
            // terrain rather than on making the whole picture louder.
            new GamePresetGroup("Rust", new[]
            {
                Neutral,
                new ColourPreset("Daylight",
                    "For long sessions above ground. Lifts the brown-grey without going cartoon.",
                    Vibrance: 150, Saturation: 118, Brightness: 100, Contrast: 106, Temperature: -8,
                    Tone: new ToneSettings(Gamma: 102, Shadows: 14, Highlights: -8)),
                new ColourPreset("PvP",
                    "Built for fights. Heavy colour, darker picture, lifted midtones so nothing "
                    + "hides in shadow.",
                    Vibrance: 230, Saturation: 100, Brightness: 80, Contrast: 90, Temperature: -12,
                    Tone: new ToneSettings(Gamma: 115, Shadows: 34, Blacks: 20, Highlights: -14)),
                new ColourPreset("Caves",
                    "For underground and night. Opens up the dark end without washing out the rest.",
                    Vibrance: 175, Saturation: 110, Brightness: 92, Contrast: 94, Temperature: -6,
                    Tone: new ToneSettings(Gamma: 128, Shadows: 44, Blacks: 30, Highlights: -10)),
                new ColourPreset("Cloth",
                    "Pushes fabric and skin as far from dirt and rock as the app can. Very loud.",
                    Vibrance: 300, Saturation: 145, Brightness: 100, Contrast: 112, Temperature: -16,
                    Tone: new ToneSettings(Gamma: 104, Shadows: 12, MidtoneTint: -20)),
                new ColourPreset("Recording",
                    "Holds up after a stream's compression, which eats saturation first.",
                    Vibrance: 205, Saturation: 128, Brightness: 104, Contrast: 108, Temperature: -6,
                    Tone: new ToneSettings(Gamma: 102, Highlights: -16, Whites: -8)),
                new ColourPreset("Flat",
                    "Low contrast, easy on the eyes. For very long sessions.",
                    Vibrance: 44, Saturation: 104, Brightness: 100, Contrast: 88, Temperature: 6,
                    Tone: new ToneSettings(Gamma: 104, Fade: 16)),
            }),

            // CS2 is concrete, sand and orange light, with player models that are already
            // high-contrast against it. The work here is keeping highlights under control so
            // the bright side of a map does not flare.
            new GamePresetGroup("CS2", new[]
            {
                Neutral,
                new ColourPreset("Competitive",
                    "Clean and even. Keeps bright maps from flaring without dulling them.",
                    Vibrance: 190, Saturation: 116, Brightness: 100, Contrast: 108, Temperature: -10,
                    Tone: new ToneSettings(Gamma: 102, Highlights: -18, Shadows: 12)),
                new ColourPreset("PvP",
                    "Built for fights. Heavy colour, darker picture, lifted midtones so nothing "
                    + "hides in shadow.",
                    Vibrance: 230, Saturation: 100, Brightness: 80, Contrast: 90, Temperature: -14,
                    Tone: new ToneSettings(Gamma: 115, Shadows: 32, Blacks: 18, Highlights: -16)),
                new ColourPreset("Dust",
                    "For the sand maps. Cuts the orange cast so models stop blending into it.",
                    Vibrance: 215, Saturation: 112, Brightness: 98, Contrast: 106, Temperature: -30,
                    Tone: new ToneSettings(Gamma: 104, MidtoneTint: -30, Highlights: -14)),
                new ColourPreset("Dark corners",
                    "Lifts the black end for maps with a lot of unlit space.",
                    Vibrance: 165, Saturation: 108, Brightness: 94, Contrast: 96, Temperature: -8,
                    Tone: new ToneSettings(Gamma: 126, Shadows: 40, Blacks: 26)),
                new ColourPreset("Punchy",
                    "Maximum separation. Loud, and not for everybody.",
                    Vibrance: 320, Saturation: 150, Brightness: 100, Contrast: 116, Temperature: -14,
                    Tone: new ToneSettings(Gamma: 98, Shadows: 8, Highlights: -10)),
                new ColourPreset("Flat",
                    "Low contrast for long sessions.",
                    Vibrance: 46, Saturation: 104, Brightness: 100, Contrast: 90, Temperature: 4,
                    Tone: new ToneSettings(Gamma: 104, Fade: 14)),
            }),

            // The catch-all. Somebody playing something not listed still wants a starting
            // point, and "no preset applies to you" is a bad answer.
            new GamePresetGroup("Any game", new[]
            {
                Neutral,
                new ColourPreset("Richer",
                    "A little more colour everywhere. The safe one to start with.",
                    Vibrance: 130, Saturation: 112, Brightness: 100, Contrast: 104, Temperature: 0,
                    Tone: new ToneSettings(Gamma: 100)),
                new ColourPreset("PvP",
                    "Built for fights. Heavy colour, darker picture, lifted midtones so nothing "
                    + "hides in shadow.",
                    Vibrance: 230, Saturation: 100, Brightness: 80, Contrast: 90, Temperature: -10,
                    Tone: new ToneSettings(Gamma: 115, Shadows: 32, Blacks: 18, Highlights: -14)),
                new ColourPreset("Vivid",
                    "Noticeably stronger. Good for anything stylised.",
                    Vibrance: 240, Saturation: 135, Brightness: 100, Contrast: 108, Temperature: -6,
                    Tone: new ToneSettings(Gamma: 100, Shadows: 10)),
                new ColourPreset("Maximum",
                    "Everything the app can do at once. Try it, then back it off.",
                    Vibrance: 340, Saturation: 175, Brightness: 102, Contrast: 120, Temperature: -12,
                    Tone: new ToneSettings(Gamma: 104, Shadows: 16, Highlights: -12)),
                new ColourPreset("Bright rooms",
                    "For playing with a window open or a light on behind the screen.",
                    Vibrance: 160, Saturation: 120, Brightness: 112, Contrast: 112, Temperature: -8,
                    Tone: new ToneSettings(Gamma: 94, Blacks: -14, Highlights: 8)),
                new ColourPreset("Night",
                    "Warmer and softer for late sessions.",
                    Vibrance: 110, Saturation: 106, Brightness: 88, Contrast: 94, Temperature: 40,
                    Tone: new ToneSettings(Gamma: 104, Highlights: -14, Fade: 8)),
                new ColourPreset("Film",
                    "Lifted blacks and pulled highlights. A look rather than an advantage.",
                    Vibrance: 42, Saturation: 106, Brightness: 100, Contrast: 92, Temperature: 6,
                    Tone: new ToneSettings(Gamma: 102, Fade: 28, Highlights: -18, ShadowTint: -12)),
            }),
        };

        public static GamePresetGroup ForGame(string game) =>
            System.Linq.Enumerable.FirstOrDefault(All, g =>
                string.Equals(g.Game, game, StringComparison.OrdinalIgnoreCase))
            ?? All[All.Count - 1];

        // ---- preview -----------------------------------------------------------------------

        /// <summary>
        /// Run one colour through the same pipeline the screen gets.
        ///
        /// Not an approximation. The matrix from <see cref="ColorAdjust"/> and the ramp from
        /// <see cref="ToneCurve"/> are the two things the engine actually applies, in that
        /// order, so a preview tile shows what the preset will really do. A preview that only
        /// looked roughly right would be a promise the app then breaks.
        /// </summary>
        public static Color Preview(ColourPreset p, Color source,
            bool driverAvailable = false, bool streaming = false)
        {
            var m = ColorAdjust.Build(
                saturation: p.Saturation / 100f,

                // The engine's own function, not 1 + vibrance/100.
                //
                // Those are different numbers on every machine. On NVIDIA the driver owns
                // 0-100 through its own non-linear curve, so the matrix gets 1.0 and a tile
                // built from 1.55 was showing a saturation lift that would never happen. On
                // AMD the software path interpolates: vibrance 55 is about 1.03, not 1.55 -
                // roughly fifty times the real boost. This class claims not to approximate, so
                // it has to call what ApplyOverlay calls.
                vibrance: VibranceEngine.SoftwareVibranceFactor(p.Vibrance, driverAvailable, streaming),

                contrast: p.Contrast / 100f,
                brightness: p.Brightness / 100f,
                warmth: p.Temperature / 100f);

            float r = source.R / 255f, g = source.G / 255f, b = source.B / 255f;

            // Column-major output: column 0 is the new red, and row 4 is the translation.
            float nr = r * m[0] + g * m[5] + b * m[10] + m[20];
            float ng = r * m[1] + g * m[6] + b * m[11] + m[21];
            float nb = r * m[2] + g * m[7] + b * m[12] + m[22];

            var tone = p.Tone with { Gamma = p.Tone.ResolvedGamma };
            if (tone.IsNeutral) return FromUnit(nr, ng, nb);

            var ramp = ToneCurve.Build(tone);
            int entries = ramp.Length / 3;

            return Color.FromArgb(255,
                Lookup(ramp, 0, entries, nr),
                Lookup(ramp, 1, entries, ng),
                Lookup(ramp, 2, entries, nb));
        }

        private static int Lookup(ushort[] ramp, int channel, int entries, float value)
        {
            int index = Math.Clamp((int)MathF.Round(value * (entries - 1)), 0, entries - 1);
            // The ramp is 16-bit; the preview is 8.
            return ramp[channel * entries + index] >> 8;
        }

        private static Color FromUnit(float r, float g, float b) => Color.FromArgb(255,
            Math.Clamp((int)MathF.Round(r * 255f), 0, 255),
            Math.Clamp((int)MathF.Round(g * 255f), 0, 255),
            Math.Clamp((int)MathF.Round(b * 255f), 0, 255));

        /// <summary>
        /// The colours a preview strip is built from.
        ///
        /// Chosen to be the things a player actually looks at rather than a rainbow: skin,
        /// foliage, sky, rust-brown terrain, concrete, and a near-black so lifted shadows are
        /// visible. A rainbow would make every preset look dramatic and tell you nothing.
        /// </summary>
        public static readonly Color[] SampleColours =
        {
            Color.FromArgb(224, 172, 140),   // skin
            Color.FromArgb(74, 96, 54),      // foliage
            Color.FromArgb(96, 132, 178),    // sky
            Color.FromArgb(122, 84, 58),     // rust / dirt
            Color.FromArgb(150, 148, 142),   // concrete
            Color.FromArgb(24, 24, 28),      // near-black
        };
    }
}
