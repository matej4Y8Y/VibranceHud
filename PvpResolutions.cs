using System.Collections.Generic;

namespace VibranceHud
{
    /// <summary>One competitive resolution, and the honest case for it.</summary>
    public sealed record PvpResolution(
        string Name,
        int Width,
        int Height,
        string Aspect,
        string Why,
        string TradeOff,
        bool NeedsStretching);

    /// <summary>
    /// The three resolutions competitive FPS players actually use.
    ///
    /// Deliberately three, and deliberately honest about what each costs. There is no
    /// resolution that is "the best in every game" - the popular competitive ones trade
    /// horizontal field of view for larger-looking targets and more frames, and whether that
    /// is worth it depends on the game and the player. Presenting them as a free upgrade
    /// would be the same overclaim the capture messaging used to make.
    ///
    /// The 4:3 options only do what people expect if the GPU is set to stretch rather than
    /// letterbox, and that setting lives in the NVIDIA or AMD control panel - PlexusX cannot
    /// reach it. Every stretched entry says so, because the alternative is a user switching
    /// to 1280x960, getting black bars down the sides, and concluding the feature is broken.
    /// </summary>
    public static class PvpResolutions
    {
        public static IReadOnlyList<PvpResolution> All { get; } = new[]
        {
            new PvpResolution(
                Name: "Native 1080p",
                Width: 1920, Height: 1080,
                Aspect: "16:9",
                Why: "Sharpest image and the full field of view. Nothing is stretched or "
                   + "cropped, so what you see is what the game intends.",
                TradeOff: "Targets look smaller than on a stretched 4:3, and it costs more "
                        + "frames than the options below.",
                NeedsStretching: false),

            new PvpResolution(
                Name: "1440 x 1080 stretched",
                Width: 1440, Height: 1080,
                Aspect: "4:3",
                Why: "The usual modern compromise. Player models come out noticeably wider "
                   + "than at 16:9 while the picture stays reasonably sharp, and it runs "
                   + "faster than native.",
                TradeOff: "You lose horizontal field of view, so more of the map sits off "
                        + "screen to your left and right.",
                NeedsStretching: true),

            new PvpResolution(
                Name: "1280 x 960 stretched",
                Width: 1280, Height: 960,
                Aspect: "4:3",
                Why: "The long-standing Counter-Strike choice. The widest-looking player "
                   + "models of the three and the highest frame rate.",
                TradeOff: "Softest image of the three, and the same loss of horizontal field "
                        + "of view as above.",
                NeedsStretching: true),
        };

        /// <summary>Shown wherever a stretched option is offered. The single most common
        /// reason somebody thinks these presets are broken.</summary>
        public const string StretchNote =
            "4:3 only stretches if your graphics driver is set to do it — NVIDIA Control Panel "
            + "or AMD Software, scaling mode \"Full panel\". PlexusX can't change that setting. "
            + "Without it you'll get black bars down the sides instead.";
    }
}
