using System;

namespace VibranceHud.Licensing
{
    /// <summary>
    /// How long the free trial lasts and whether a given one has ended.
    ///
    /// Pure arithmetic over a supplied clock so every boundary is testable without waiting
    /// four days. A clock earlier than the recorded start counts as expired rather than as
    /// extra time - otherwise winding the system date back is a one-click trial reset.
    /// </summary>
    public static class TrialPolicy
    {
        public static readonly TimeSpan Length = TimeSpan.FromDays(4);

        public static bool IsExpired(DateTime startedUtc, DateTime nowUtc) =>
            Remaining(startedUtc, nowUtc) <= TimeSpan.Zero;

        public static TimeSpan Remaining(DateTime startedUtc, DateTime nowUtc)
        {
            if (nowUtc < startedUtc) return TimeSpan.Zero; // clock rolled back
            var left = Length - (nowUtc - startedUtc);
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
    }
}
