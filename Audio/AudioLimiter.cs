using System;

namespace VibranceHud.Audio
{
    /// <summary>
    /// The maths behind Audio Edge: a peak limiter. We watch how loud the output actually is
    /// and, when it punches above the ceiling, pull the volume down so the loud sound (a gun
    /// shot) can't exceed it. Quiet sounds (footsteps) are left alone, so once you turn the
    /// game up they end up sitting at nearly the same level as the loud ones.
    ///
    /// Pure and frame-independent so it can be unit-tested without any audio hardware.
    /// Attack is fast (catch the transient), release is slow (no pumping on the way back up).
    /// </summary>
    public static class AudioLimiter
    {
        /// <summary>Never duck below this - at some point it's just muted.</summary>
        public const float MinVolume = 0.05f;

        /// <summary>How quickly we duck when the sound is too loud (0-1 per tick).</summary>
        public const float DefaultAttack = 0.6f;

        /// <summary>How slowly we come back up once it's quiet again (0-1 per tick).</summary>
        public const float DefaultRelease = 0.04f;

        /// <summary>
        /// The volume to use for the next tick.
        /// </summary>
        /// <param name="peak">Measured output peak this tick, 0-1.</param>
        /// <param name="threshold">The ceiling the user picked, 0-1 (e.g. 0.30).</param>
        /// <param name="currentVolume">The volume currently set, 0-1.</param>
        /// <param name="maxVolume">The volume to return to when it's quiet, 0-1.</param>
        public static float NextVolume(float peak, float threshold, float currentVolume,
            float maxVolume, float attack = DefaultAttack, float release = DefaultRelease)
        {
            // The volume that would put this sound exactly on the ceiling. When the signal is
            // quiet (or silent) nothing needs holding back, so aim for full volume.
            float target = peak > 0.0001f
                ? MathF.Min(maxVolume, threshold / peak)
                : maxVolume;

            // Duck fast, recover slow - a gun shot has to be caught on the transient, but
            // crawling back up avoids the volume audibly pumping between shots.
            float rate = target < currentVolume ? attack : release;
            float next = currentVolume + (target - currentVolume) * rate;

            return Math.Clamp(next, MinVolume, maxVolume);
        }
    }
}
