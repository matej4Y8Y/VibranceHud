using System.Collections.Generic;

namespace VibranceHud
{
    /// <summary>
    /// A scene look: the four tone controls only.
    ///
    /// Deliberately does NOT carry saturation or vibrance. Those two are the user's own taste
    /// - the whole reason the page puts them at the top at full size - and a preset that
    /// overwrote them would throw away the thing someone had just spent a minute dialling in
    /// every time they changed biome. A preset corrects the tone underneath; the colour on
    /// top stays yours.
    /// </summary>
    public sealed record DisplayPreset(
        string Name,
        string Subtitle,
        int Brightness,
        int Gamma,
        int Contrast,
        int Temperature)
    {
        public bool Matches(int brightness, int gamma, int contrast, int temperature) =>
            Brightness == brightness && Gamma == gamma
            && Contrast == contrast && Temperature == temperature;
    }

    /// <summary>
    /// Per-biome tone baselines for Rust.
    ///
    /// Honest about what this can and cannot be: a display matrix is applied to every pixel
    /// on screen and has no idea which of them are a player. Nothing here detects anybody.
    /// What it can do is fix the specific thing each biome does badly - the canopy that
    /// swallows shadow detail, the sand that turns everything one shade of yellow, the snow
    /// that clips to a white sheet - so that a player who was lost in that failure is no
    /// longer lost in it.
    ///
    /// The rule that shapes all four: gamma and contrast pull opposite ways on shadows.
    /// Gamma above 100 opens them, contrast above 100 crushes them. Raising both together
    /// cancels out, which is the mistake in most "gaming filter" presets. Each look below
    /// picks the one that matches the problem and leaves the other near neutral.
    /// </summary>
    public static class DisplayPresets
    {
        /// <summary>Everything off. Also the way back from any of the others.</summary>
        public static readonly DisplayPreset Balanced = new(
            "Balanced", "Neutral - no tone change",
            Brightness: 100, Gamma: 100, Contrast: 100, Temperature: 0);

        /// <summary>
        /// Forest. The problem is darkness under the canopy: players stand in shade and the
        /// shade is a solid black mass.
        ///
        /// So gamma does the work - it lifts the shadows and midtones while leaving the sky
        /// alone, which is exactly the range a crouched player occupies. Contrast sits just
        /// BELOW neutral on purpose, because pushing it up would re-crush the shadows gamma
        /// just opened. Cooling shifts the greens bluer while leaving skin and brown gear
        /// warm, so the two stop being the same family of colour.
        /// </summary>
        public static readonly DisplayPreset Forest = new(
            "Forest", "Opens shadows under the trees",
            Brightness: 104, Gamma: 112, Contrast: 98, Temperature: -14);

        /// <summary>
        /// Desert. The opposite problem: too much light, and every surface within one shade
        /// of the same tan.
        ///
        /// Brightness comes down to pull the sand and sky back from clipping - past the clip
        /// point there is no detail left to recover. Contrast then goes up to define edges at
        /// the long sightlines this biome is played at. Strong cooling is the main move: it
        /// drags the yellow cast towards neutral, and anything not sand-coloured - clothing,
        /// weapons, blues and reds - stops blending into the ground.
        /// </summary>
        public static readonly DisplayPreset Desert = new(
            "Desert", "Kills glare and the yellow cast",
            Brightness: 94, Gamma: 98, Contrast: 112, Temperature: -26);

        /// <summary>
        /// Snow. The brightest and least colourful scene in the game, and the one people
        /// actually complain about - "I can't see anything in Arctic".
        ///
        /// Brightness drops the most of any preset here, because snow arrives already at the
        /// top of the range and everything above it is flat white. Gamma dips slightly to
        /// hold the midtones down and keep surface texture. Contrast rises only a little -
        /// snow clips faster than anything else, so a heavy hand turns the ground back into
        /// a blank sheet. The warm cast counters the blue-white and, more usefully, puts
        /// terrain and players on opposite sides of neutral.
        /// </summary>
        public static readonly DisplayPreset Snow = new(
            "Snow", "Cuts the glare, warms the blue",
            Brightness: 90, Gamma: 96, Contrast: 108, Temperature: 22);

        public static readonly IReadOnlyList<DisplayPreset> All =
            new[] { Balanced, Forest, Desert, Snow };
    }
}
