namespace VibranceHud.SystemTweaks
{
    /// <summary>How much we trust a tweak to be a safe, unambiguous win.</summary>
    public enum TweakTier
    {
        /// <summary>Clean, reversible, measurable benefit - fine to show on by default.</summary>
        Safe,
        /// <summary>Real but situational: helps on some systems, not others. Off by default,
        /// shown with a trade-off note so the user opts in knowingly.</summary>
        Advanced,
    }

    /// <summary>The outcome of applying a tweak, including the plain-language status line the
    /// UI shows (e.g. "High performance plan active") so the user sees it actually did something.</summary>
    public sealed record SystemTweakResult(bool Ok, string StatusText);

    /// <summary>
    /// One system-wide optimization the user can toggle. Kept behind an interface so the
    /// concrete kinds (registry value, power plan, GPU driver) share one UI and one catalog,
    /// and so the logic is unit-testable against fakes instead of the live machine.
    /// </summary>
    public interface ISystemTweak
    {
        string Id { get; }
        string Label { get; }
        string Description { get; }
        string Category { get; }
        TweakTier Tier { get; }

        /// <summary>True when applying this needs admin rights (e.g. it writes to HKLM).
        /// The UI routes those through a one-off elevated relaunch instead of failing.</summary>
        bool RequiresAdmin { get; }

        /// <summary>True when the system already sits at this tweak's optimized state.</summary>
        bool IsApplied();

        /// <summary>Move the system to the optimized state.</summary>
        SystemTweakResult Apply();

        /// <summary>Put the system back to its stock/default state.</summary>
        void Revert();
    }
}
