using System;
using System.Collections.Generic;

namespace VibranceHud
{
    /// <summary>
    /// Builds per-monitor GPU resources while tolerating individual monitor failures.
    ///
    /// Why this exists: DxOverlay used to build a shader + a desktop-duplication capture for
    /// every output inside ONE try/catch. A single failing monitor threw, the catch tore down
    /// every already-built monitor, and the whole DX11 path was abandoned in favour of the
    /// Magnification fallback - which is invisible to OBS/Discord. So one problem display cost
    /// the user the effect on all of their displays, in the one mode that shows up in a stream.
    ///
    /// Desktop Duplication fails per-output for reasons that are entirely normal:
    ///   - DXGI_ERROR_NOT_CURRENTLY_AVAILABLE, because something else is already duplicating
    ///     that monitor. OBS "Display Capture" uses this exact API, as do ShadowPlay and
    ///     several overlay tools - so the user's own streaming setup can be what breaks it.
    ///   - virtual / phantom displays (VPN software, tablet drivers, a dummy HDMI plug) that
    ///     cannot be duplicated at all,
    ///   - an output on an adapter that won't permit duplication.
    ///
    /// Partial success is strictly better than none here: saturating the monitors that do work,
    /// visibly in capture, beats saturating all of them in a mode no viewer can see.
    /// </summary>
    public static class TolerantOutputBuilder
    {
        /// <summary>
        /// Runs <paramref name="build"/> for each index and returns the indices that
        /// succeeded. Never throws: a failing index is reported through
        /// <paramref name="onError"/> and skipped.
        ///
        /// The caller is responsible for cleaning up whatever a failed build half-created;
        /// this only decides which indices are usable.
        /// </summary>
        public static List<int> Build(int outputCount, Action<int> build,
            Action<int, Exception>? onError = null)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));

            var succeeded = new List<int>();
            for (int i = 0; i < outputCount; i++)
            {
                try
                {
                    build(i);
                    succeeded.Add(i);
                }
                catch (Exception ex)
                {
                    // Deliberately swallowed: one monitor that can't be duplicated must not
                    // cost the user every other monitor.
                    try { onError?.Invoke(i, ex); } catch { /* reporting must never throw */ }
                }
            }
            return succeeded;
        }
    }
}
