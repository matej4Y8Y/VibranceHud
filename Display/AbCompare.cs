using System;

namespace VibranceHud.Display
{
    /// <summary>A clock, so the cooldown can be tested without sleeping.</summary>
    public interface IClock
    {
        DateTime UtcNow { get; }
    }

    /// <summary>The real one.</summary>
    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>
    /// Before/after: flips between the user's colour and neutral so the difference is visible.
    ///
    /// This is the demo moment for a product whose whole promise is "your game looks way
    /// better" - the effect is invisible until you can see what it replaced.
    ///
    /// Rate-limited, and the limit is not politeness. Writing the display gamma ramp
    /// repeatedly in quick succession makes it misbehave, and rapid toggling is the known way
    /// to trigger that. A refused toggle does nothing at all rather than queueing: a queue
    /// would keep flipping the screen after the user stopped clicking, which is the worst
    /// possible answer for a control whose entire job is to show them a comparison.
    ///
    /// Holds no colour values of its own. It only knows which side is showing; the caller
    /// owns the settings and applies them.
    /// </summary>
    public sealed class AbCompare
    {
        private readonly IClock _clock;
        private readonly TimeSpan _cooldown;
        private DateTime _lastToggle = DateTime.MinValue;

        public AbCompare(IClock? clock = null, TimeSpan? cooldown = null)
        {
            _clock = clock ?? new SystemClock();

            // 250ms: long enough that a mashed button produces one flip, short enough that a
            // deliberate back-and-forth comparison never feels blocked.
            _cooldown = cooldown ?? TimeSpan.FromMilliseconds(250);
        }

        /// <summary>True while the screen is showing neutral rather than the user's own look.</summary>
        public bool ShowingNeutral { get; private set; }

        /// <summary>
        /// Flip, if the cooldown allows it.
        /// </summary>
        /// <returns>True if the state actually changed, so the caller knows whether to write
        /// anything to the display. False means do nothing - not "try again".</returns>
        public bool TryToggle()
        {
            var now = _clock.UtcNow;
            if (now - _lastToggle < _cooldown) return false;

            _lastToggle = now;
            ShowingNeutral = !ShowingNeutral;
            return true;
        }

        /// <summary>
        /// Put it back to the user's own look.
        ///
        /// Never rate-limited: this is the app tidying up - leaving a page, closing a window -
        /// not a user gesture, and refusing it would strand somebody on neutral with their
        /// settings apparently gone and nothing on screen explaining why.
        /// </summary>
        public void Reset() => ShowingNeutral = false;
    }
}
