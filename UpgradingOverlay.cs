using System;

namespace VibranceHud
{
    /// <summary>
    /// Wraps whichever overlay we managed to start with and keeps trying to upgrade to the
    /// DX11 path if we had to fall back to Magnification.
    ///
    /// Why this exists: DX11 init used to get exactly one attempt, in the
    /// <see cref="TrayApplicationContext"/> constructor. When it failed, the process was
    /// stuck on <see cref="MagOverlay"/> for its entire lifetime - and that path is invisible
    /// to OBS, Discord and ShadowPlay, so the user's saturation silently does not appear in
    /// their stream. The only escape was a button in Settings that restarts the whole app.
    ///
    /// The causes are usually temporary, which is what makes one-shot init the wrong shape:
    ///   - GPU memory was tight because the game / OBS / a browser started first,
    ///   - the display or DWM was not ready yet (PlexusX launching with Windows),
    ///   - the driver was mid-reload after an update.
    ///
    /// So the fallback is now treated as provisional. Something outside pokes
    /// <see cref="TryUpgrade"/> - on a timer, and on display/focus changes - and the first
    /// time DX11 comes up we move to it, carrying the user's current colour state across so
    /// the swap is invisible to them.
    ///
    /// Deliberately does NOT downgrade in the other direction: if the DX11 overlay dies
    /// mid-session that is a different problem with a different fix, and silently dropping
    /// to a capture-invisible path is the exact behaviour this class exists to remove.
    /// </summary>
    public sealed class UpgradingOverlay : ISaturationOverlay, IDisplayOverlay, IDisposable
    {
        private readonly Func<ISaturationOverlay?> _tryCreatePreferred;

        // Why the DX11 attempt that got us here failed. Carried explicitly because the
        // failed DxOverlay is disposed before this wrapper is built, and MagOverlay
        // hardcodes LastFailure => None - so without this the reason is lost and the
        // Settings page shows "Fallback" with nothing actionable next to it.
        private readonly DxInitFailureKind _fallbackFailure;
        private readonly string _fallbackFailureMessage;

        private ISaturationOverlay _active;

        // The last colour state the caller asked for, replayed onto the new overlay after a
        // swap. Without this the upgrade would visibly reset the user's saturation to
        // neutral partway through a session.
        private float[]? _pendingMatrix;

        private bool _disposed;

        /// <param name="fallbackFailure">Why the DX11 attempt failed, so the Settings page
        /// can show an actionable reason while we're on the fallback.</param>
        /// <param name="fallbackFailureMessage">Short user-facing label for that failure.</param>
        public UpgradingOverlay(ISaturationOverlay initial, Func<ISaturationOverlay?> tryCreatePreferred,
            DxInitFailureKind fallbackFailure = DxInitFailureKind.None, string fallbackFailureMessage = "")
        {
            _active = initial ?? throw new ArgumentNullException(nameof(initial));
            _tryCreatePreferred = tryCreatePreferred ?? throw new ArgumentNullException(nameof(tryCreatePreferred));
            _fallbackFailure = fallbackFailure;
            _fallbackFailureMessage = fallbackFailureMessage ?? "";
        }

        public OverlayMode ActiveMode => (_active as IDisplayOverlay)?.ActiveMode ?? OverlayMode.Dx;

        /// <summary>True while we are on the fallback path and an upgrade is still worth
        /// attempting. Once DX11 is live there is nothing better to move to.</summary>
        public bool CanUpgrade => !_disposed && ActiveMode != OverlayMode.Dx;

        // While on the fallback, report the DX11 failure we were handed. Prefer the active
        // overlay's own reason if it has one (a future fallback might), but fall back to the
        // captured one because MagOverlay always says None.
        public DxInitFailureKind LastFailure
        {
            get
            {
                if (ActiveMode == OverlayMode.Dx) return DxInitFailureKind.None;
                var own = (_active as IDisplayOverlay)?.LastFailure ?? DxInitFailureKind.None;
                return own != DxInitFailureKind.None ? own : _fallbackFailure;
            }
        }

        public string LastFailureMessage
        {
            get
            {
                if (ActiveMode == OverlayMode.Dx) return "";
                var own = (_active as IDisplayOverlay)?.LastFailureMessage ?? "";
                return !string.IsNullOrEmpty(own) ? own : _fallbackFailureMessage;
            }
        }

        public void Apply(float[] matrix)
        {
            _pendingMatrix = matrix;
            _active.Apply(matrix);
        }

        public void Clear()
        {
            _pendingMatrix = null;
            _active.Clear();
        }

        /// <summary>
        /// Attempt to move up to the DX11 path. Returns true only when the active overlay
        /// actually changed, so a caller on a timer can stop polling.
        ///
        /// Safe to call as often as you like: it is a no-op once DX11 is live, and any
        /// exception from constructing a DX device is swallowed - a failed attempt must
        /// leave the user exactly where they were, never with no overlay at all.
        /// </summary>
        public bool TryUpgrade()
        {
            if (!CanUpgrade) return false;

            ISaturationOverlay? candidate;
            try
            {
                candidate = _tryCreatePreferred();
            }
            catch
            {
                // DX11 init throwing is ordinary here (no device, driver reloading, OOM).
                // Stay on the fallback and let the next poke try again.
                return false;
            }

            if (candidate == null) return false;

            // Only a real DX11 overlay is an upgrade. A factory that handed back another
            // fallback would otherwise churn the active overlay for no gain.
            var candidateMode = (candidate as IDisplayOverlay)?.ActiveMode ?? OverlayMode.Dx;
            if (candidateMode != OverlayMode.Dx)
            {
                Dispose(candidate);
                return false;
            }

            var old = _active;
            _active = candidate;

            // Tear the old effect down BEFORE the new one goes on, or both colour effects
            // stack and the screen ends up doubly saturated.
            try { old.Clear(); } catch { /* best-effort */ }
            Dispose(old);

            // Replay the user's colour state. Nothing to replay when they were at neutral -
            // re-applying a stale matrix there would switch the effect back on by itself.
            if (_pendingMatrix != null)
            {
                try { _active.Apply(_pendingMatrix); }
                catch { /* the overlay is live; a failed re-apply is recoverable on next change */ }
            }

            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Dispose(_active);
        }

        private static void Dispose(ISaturationOverlay overlay)
        {
            if (overlay is IDisposable d)
            {
                try { d.Dispose(); } catch { /* never throw from teardown */ }
            }
        }
    }
}
