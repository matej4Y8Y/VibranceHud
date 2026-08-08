using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using VibranceHud.Monitors;

namespace VibranceHud.Pages
{
    /// <summary>
    /// The physical panel: brightness, contrast and low blue light, over DDC/CI.
    ///
    /// The third layer of the stack. Display changes the signal, Resolution changes the mode,
    /// and this changes the glass those land on - so nobody has to use the buttons on the back
    /// of the monitor again.
    ///
    /// What this page can offer depends entirely on the hardware, so it asks the probe first
    /// and says plainly when the answer is no. A tab full of controls that quietly do nothing
    /// is worse than one that admits the monitor will not talk.
    /// </summary>
    public sealed class MonitorHardwarePage : GlowPage
    {
        private const int CardW = 620;
        private const int Gutter = 18;
        private const int ContentW = CardW - 2 * Gutter;

        private readonly AppSettings _settings;
        private readonly SettingsStore _store;
        private readonly IReadOnlyList<MonitorCapability> _caps;

        /// <summary>
        /// One of these per monitor card. The first version kept a single set of pending
        /// values for the whole page, so on a two-screen desk every card wrote to the same
        /// panel and the two cards fought over one saved value.
        /// </summary>
        private sealed class PanelState
        {
            public required MonitorCapability Cap { get; init; }
            public required DebouncedAction Write { get; init; }

            // -1 means nothing pending. Read with Interlocked.Exchange so the write thread
            // takes the value rather than reading it, doing a slow DDC call, and then clearing
            // a newer one the user set in the meantime.
            public int PendingBrightness = -1;
            public int PendingContrast = -1;
            public int PendingBlue = -1;

            public GlassButton? Revert;
        }

        private readonly List<PanelState> _panels = new();

        public MonitorHardwarePage(AppSettings settings, SettingsStore store,
            IReadOnlyList<MonitorCapability>? capabilities = null)
        {
            _settings = settings;
            _store = store;
            _caps = capabilities ?? Array.Empty<MonitorCapability>();

            AutoScroll = true;
            ContentWidth = CardW + 80;

            Controls.Add(new Label
            {
                Text = "Monitor",
                Font = Design.Fonts.Title,
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Location = new Point(40, 16),
                Size = new Size(400, 34),
            });

            Controls.Add(new Label
            {
                Text = "Your monitor's own settings, without reaching round the back.",
                Font = Design.Fonts.Caption,
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Location = new Point(42, 52),
                Size = new Size(520, 20),
            });

            int y = 86;
            foreach (var cap in _caps) y = BuildMonitorCard(cap, y);

            if (_caps.Count == 0) BuildProbingCard();

            FitScrollToContent();
        }

        /// <summary>
        /// Shown while the probe has not answered yet.
        ///
        /// The page is built with no capabilities and filled in later, because probing is
        /// three DDC/CI reads per panel and each can take hundreds of milliseconds. Doing that
        /// in the constructor blocked the window opening - and blocked it again on every theme
        /// change, since that rebuilds the whole window.
        /// </summary>
        private void BuildProbingCard()
        {
            var card = new CardPanel { Location = new Point(40, 86), Size = new Size(CardW, 92) };
            card.Controls.Add(new Label
            {
                Text = "Asking your monitor what it supports…",
                Font = Design.Fonts.Body,
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Location = new Point(Gutter, 34),
                Size = new Size(ContentW, 24),
            });
            Controls.Add(card);
        }

        /// <summary>
        /// Probe off the UI thread and rebuild when it answers.
        ///
        /// Called by the shell after the window is up. Safe to call when the page has already
        /// been disposed - the marshalled continuation checks before touching anything.
        /// </summary>
        public void BeginProbe()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                var caps = MonitorProbe.Probe();

                try
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    BeginInvoke(new Action(() => Rebuild(caps)));
                }
                catch (ObjectDisposedException) { /* window closed mid-probe */ }
                catch (InvalidOperationException) { /* handle went away mid-probe */ }
            });
        }

        private void Rebuild(IReadOnlyList<MonitorCapability> caps)
        {
            if (IsDisposed) return;

            foreach (var p in _panels) p.Write.Dispose();
            _panels.Clear();

            // Everything below the two header labels.
            foreach (var c in Controls.Cast<Control>().Skip(2).ToList())
            {
                Controls.Remove(c);
                c.Dispose();
            }

            int y = 86;
            foreach (var cap in caps) y = BuildMonitorCard(cap, y);

            FitScrollToContent();
        }

        private int BuildMonitorCard(MonitorCapability cap, int y)
        {
            bool anything = cap.SupportsBrightness || cap.SupportsContrast || cap.SupportsRgbGain;

            var card = new CardPanel
            {
                Location = new Point(40, y),
                Size = new Size(CardW, anything ? 300 : 132),
            };

            // A real Label, not UiHelpers.Caption: captions are letter-spaced, which turns a
            // model number into "D E L L  U 2 7 2 3 Q E" and is unreadable. This is a name,
            // not a section heading.
            card.Controls.Add(new Label
            {
                Text = cap.Description,
                Font = Design.Fonts.Heading,
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Location = new Point(Gutter, 14),
                Size = new Size(ContentW - 130, 22),
                UseMnemonic = false,
            });

            if (!anything)
            {
                // The honest path. Says what happened and what to try, rather than showing
                // controls that would do nothing.
                card.Controls.Add(new Label
                {
                    Text = cap.Refusal,
                    Font = Design.Fonts.Caption,
                    ForeColor = Theme.TextDim,
                    BackColor = Color.Transparent,
                    Location = new Point(Gutter, 46),
                    Size = new Size(ContentW, 64),
                });

                Controls.Add(card);
                return y + card.Height + 20;
            }

            var state = new PanelState
            {
                Cap = cap,
                Write = null!,   // replaced below; the closure needs `state` to exist first
            };

            var write = new DebouncedAction(() => Flush(state), delayMs: 120);
            state = new PanelState { Cap = cap, Write = write };
            _panels.Add(state);

            var saved = _settings.PanelFor(cap.Index);

            // "Put it back" restores the values read before the first write. Disabled until
            // there is something to put back, so it never promises an undo it does not have.
            state.Revert = new GlassButton
            {
                Text = "Put it back",
                Location = new Point(CardW - Gutter - 120, 12),
                Size = new Size(120, 26),
                Enabled = saved.HasOriginals,
            };
            state.Revert.Click += (_, _) => Revert(state);
            card.Controls.Add(state.Revert);

            int rowY = 48;

            if (cap.SupportsBrightness)
            {
                // From the capability the probe already read, not a fresh hardware call, and
                // as a percentage of the panel's own range rather than an absolute number.
                int start = saved.Brightness >= 0
                    ? saved.Brightness
                    : cap.Brightness.ToPercent(cap.BrightnessCurrent);

                rowY = AddRow(card, "BRIGHTNESS", rowY, start,
                    v => { Interlocked.Exchange(ref state.PendingBrightness, v); write.Trigger(); });
            }

            if (cap.SupportsContrast)
            {
                // Seeded from what the panel reported. The probe reads contrast and used to
                // throw it away, which left the page inventing 50%.
                int start = saved.Contrast >= 0
                    ? saved.Contrast
                    : cap.Contrast.ToPercent(cap.Contrast.Current);

                rowY = AddRow(card, "CONTRAST", rowY, start,
                    v => { Interlocked.Exchange(ref state.PendingContrast, v); write.Trigger(); });
            }

            if (cap.SupportsRgbGain)
            {
                // 0 means "leave the panel's own gain alone", which is genuinely the default.
                rowY = AddRow(card, "LOW BLUE LIGHT", rowY, Math.Max(0, saved.LowBlue),
                    v => { Interlocked.Exchange(ref state.PendingBlue, v); write.Trigger(); });

                card.Controls.Add(new Label
                {
                    Text = "Warmer for late sessions. Lowers the panel's blue gain from wherever "
                         + "it already was, which every monitor understands.",
                    Font = Design.Fonts.Caption,
                    ForeColor = Theme.TextDim,
                    BackColor = Color.Transparent,
                    Location = new Point(Gutter, rowY),
                    Size = new Size(ContentW, 32),
                });
                rowY += 36;
            }

            card.Height = rowY + 12;
            Controls.Add(card);
            return y + card.Height + 20;
        }

        private int AddRow(Control parent, string caption, int y, int value, Action<int> apply)
        {
            parent.Controls.Add(UiHelpers.Caption(caption, Gutter, y, 220));

            var readout = new Label
            {
                Text = $"{value}%",
                Font = Design.Fonts.Caption,
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Location = new Point(CardW - 62, y),
                Size = new Size(44, 16),
                TextAlign = ContentAlignment.MiddleRight,
            };
            parent.Controls.Add(readout);

            var slider = new FlatSlider { Minimum = 0, Maximum = 100, Value = Math.Clamp(value, 0, 100) };
            slider.SetTrackBounds(Gutter, y + 24, ContentW);
            slider.ValueChanged += (_, _) =>
            {
                readout.Text = $"{slider.Value}%";
                apply(slider.Value);
            };
            parent.Controls.Add(slider);

            return y + 62;
        }

        /// <summary>
        /// Push whatever changed to this panel.
        ///
        /// Runs on a threadpool thread, so: everything is guarded, the pending values are
        /// TAKEN rather than read (a slow DDC write must not clear a newer value the user set
        /// while it was in flight), and the settings write is marshalled back to the UI thread
        /// because AppSettings is being mutated there.
        ///
        /// A value is only recorded once the panel accepted it. A refused write left in
        /// settings would be restored on the next launch as a reading the monitor never made.
        /// </summary>
        private void Flush(PanelState state)
        {
            try
            {
                var cap = state.Cap;
                var accepted = new List<Action<AppSettings.PanelSettings>>();

                int brightness = Interlocked.Exchange(ref state.PendingBrightness, -1);
                if (brightness >= 0)
                {
                    CaptureOriginals(state);
                    if (MonitorControl.SetBrightnessPercent(cap.Index, cap.Brightness, brightness))
                        accepted.Add(p => p.Brightness = brightness);
                }

                int contrast = Interlocked.Exchange(ref state.PendingContrast, -1);
                if (contrast >= 0)
                {
                    CaptureOriginals(state);
                    if (MonitorControl.SetContrastPercent(cap.Index, cap.Contrast, contrast))
                        accepted.Add(p => p.Contrast = contrast);
                }

                int blue = Interlocked.Exchange(ref state.PendingBlue, -1);
                if (blue >= 0)
                {
                    CaptureOriginals(state);
                    if (MonitorControl.SetLowBlueLight(cap.Index, cap.BlueGain, blue))
                        accepted.Add(p => p.LowBlue = blue);
                }

                if (accepted.Count == 0) return;

                Marshal(() =>
                {
                    var panel = _settings.PanelFor(cap.Index);
                    foreach (var set in accepted) set(panel);
                    _store.Save(_settings);

                    if (state.Revert != null) state.Revert.Enabled = panel.HasOriginals;
                });
            }
            catch
            {
                // A panel refusing, or going away mid-write, is the expected case. This runs
                // on a threadpool thread, where an escaping exception ends the process.
            }
        }

        /// <summary>
        /// Read and remember where the panel was, once, before anything is written to it.
        ///
        /// Without this there is no way back: the user drags three sliders, dislikes the
        /// result, and the only route to their original calibration is the buttons on the back
        /// of the monitor - which is exactly what this page's subtitle promises to replace.
        /// Stored in settings rather than in a field, so it survives a restart.
        /// </summary>
        private void CaptureOriginals(PanelState state)
        {
            var cap = state.Cap;
            var panel = _settings.PanelFor(cap.Index);
            if (panel.HasOriginals) return;

            // Brightness gets the two-read treatment; a lone zero from a lit screen is a lie,
            // and storing it would mean "put it back" blacks the panel.
            int? brightness = cap.SupportsBrightness ? MonitorControl.ReadTrustedBrightness(cap.Index) : null;

            Marshal(() =>
            {
                var p = _settings.PanelFor(cap.Index);
                if (p.HasOriginals) return;

                p.OriginalBrightness = brightness ?? cap.BrightnessCurrent;
                p.OriginalContrast = cap.Contrast.Current;
                p.OriginalBlueGain = cap.BlueGain.Current;
                p.HasOriginals = true;
            });
        }

        private void Revert(PanelState state)
        {
            var cap = state.Cap;
            var panel = _settings.PanelFor(cap.Index);
            if (!panel.HasOriginals) return;

            if (cap.SupportsBrightness) MonitorControl.RestoreBrightness(cap.Index, panel.OriginalBrightness);
            if (cap.SupportsContrast) MonitorControl.RestoreContrast(cap.Index, panel.OriginalContrast);
            if (cap.SupportsRgbGain) MonitorControl.RestoreBlueGain(cap.Index, panel.OriginalBlueGain);

            panel.Brightness = -1;
            panel.Contrast = -1;
            panel.LowBlue = -1;
            _store.Save(_settings);

            Rebuild(_caps);
        }

        /// <summary>Run on the UI thread, or inline if there is no handle to marshal to.</summary>
        private void Marshal(Action action)
        {
            try
            {
                if (IsDisposed) return;
                if (IsHandleCreated && InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        /// <summary>
        /// Tear the debounce timers down with the page.
        ///
        /// Without this every theme change - which rebuilds the whole window - leaks a live
        /// timer holding a dead page, and a write can fire on a background thread after the
        /// form has gone, mid-DDC-transaction. This is the same leak the shell's scroll-idle
        /// timers were written to fix.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                foreach (var p in _panels) p.Write.Dispose();

            base.Dispose(disposing);
        }

        /// <summary>The probe's verdict, for tests and for the capability report.</summary>
        internal IReadOnlyList<MonitorCapability> Capabilities => _caps;

        internal bool OffersAnyHardwareControl =>
            _caps.Any(c => c.SupportsBrightness || c.SupportsContrast || c.SupportsRgbGain);
    }
}
