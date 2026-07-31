using System;

namespace VibranceHud.Licensing
{
    /// <summary>
    /// The plans PlexusX sells, and how long each lasts.
    ///
    /// Plan ids are written into signed licences, so these strings are permanent - changing
    /// one invalidates every licence already issued under it. Add new plans; never rename.
    ///
    /// An unknown plan deliberately has NO duration rather than a default. The beta system
    /// defaulted unrecognised tiers to its longest one, which meant a typo or a plan from a
    /// newer build granted a full year.
    /// </summary>
    public static class PlanCatalog
    {
        public const string Trial = "trial";
        public const string Monthly = "monthly";
        public const string Lifetime600 = "lifetime600";

        public static TimeSpan? DurationFor(string? planId) => planId switch
        {
            Trial => TimeSpan.FromDays(4),
            Monthly => TimeSpan.FromDays(30),
            Lifetime600 => TimeSpan.FromDays(600),
            _ => null,
        };

        public static bool IsKnown(string? planId) => DurationFor(planId) != null;
    }
}
