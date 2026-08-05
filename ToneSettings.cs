using System.Text.Json.Serialization;

namespace VibranceHud
{
    /// <summary>
    /// The advanced colour grade — every control here resolves to the display gamma ramp.
    ///
    /// Ranges are the ones the UI shows. Each field's neutral is exactly the value that
    /// leaves the ramp untouched, which is what makes <see cref="IsNeutral"/> a reliable
    /// "skip the ramp entirely" check rather than an approximation.
    ///
    /// Gamma lives here too even though it predates the rest: it is the same kind of thing,
    /// and keeping it in the grade means one curve builder rather than two that have to
    /// agree with each other.
    /// </summary>
    public readonly record struct ToneSettings(
        int Gamma = 100,            // 50..150   (the existing control)
        int Highlights = 0,         // -100..100
        int Shadows = 0,            // -100..100
        int Whites = 0,             // -100..100
        int Blacks = 0,             // -100..100
        int Fade = 0,               // 0..100
        int ShadowTint = 0,         // -100 cool .. 100 warm
        int MidtoneTint = 0,
        int HighlightTint = 0)
    {
        /// <summary>
        /// Gamma actually in force, with 0 read as "untouched".
        ///
        /// A record struct's parameterless constructor zeroes every field and does NOT apply
        /// the defaults declared on the primary constructor, so `new()`, `default`, and any
        /// JSON missing the field all yield Gamma = 0 rather than 100. Left raw, that clamps
        /// to the minimum of 50 and darkens the user's screen on upgrade for no reason.
        /// Everything that consumes gamma goes through here.
        /// </summary>
        [JsonIgnore]
        public int ResolvedGamma => Gamma == 0 ? 100 : Gamma;

        [JsonIgnore]
        public static ToneSettings Neutral => new(Gamma: 100);

        [JsonIgnore]
        public bool IsNeutral =>
            ResolvedGamma == 100 && Highlights == 0 && Shadows == 0 && Whites == 0 &&
            Blacks == 0 && Fade == 0 && ShadowTint == 0 && MidtoneTint == 0 &&
            HighlightTint == 0;

        /// <summary>True when only the tonal controls are neutral, ignoring gamma. Used to
        /// tell "this is the old single-gamma case" from "this is a real grade".</summary>
        [JsonIgnore]
        public bool IsGammaOnly =>
            Highlights == 0 && Shadows == 0 && Whites == 0 && Blacks == 0 &&
            Fade == 0 && ShadowTint == 0 && MidtoneTint == 0 && HighlightTint == 0;

        /// <summary>
        /// A copy with gamma pinned to its resolved value, so anything that persists or
        /// encodes this never writes the ambiguous 0.
        ///
        /// JsonIgnore is load-bearing, not tidiness: this returns a ToneSettings, which has
        /// a Normalized of its own, so the serializer would recurse until it hit its depth
        /// limit and throw - taking every settings save down with it.
        /// </summary>
        [JsonIgnore]
        public ToneSettings Normalized => this with { Gamma = ResolvedGamma };
    }
}
