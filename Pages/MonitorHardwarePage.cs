using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
        private readonly DebouncedAction _write;

        private int _pendingBrightness = -1;
        private int _pendingContrast = -1;
        private int _pendingBlue = -1;

        public MonitorHardwarePage(AppSettings settings, SettingsStore store,
            IReadOnlyList<MonitorCapability>? capabilities = null)
        {
            _settings = settings;
            _store = store;
            _caps = capabilities ?? MonitorProbe.Probe();

            // DDC/CI is a serial protocol on the display cable - a write costs tens of
            // milliseconds and sometimes hundreds. Writing on every pixel of a slider drag
            // would stall the UI thread and flood the panel, so it lands once you stop.
            _write = new DebouncedAction(Flush, delayMs: 120);

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
                Size = new Size(ContentW, 22),
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

            int rowY = 48;

            if (cap.SupportsBrightness)
            {
                // From the capability the probe already read, not from a fresh hardware call.
                // Reading again here would make constructing this page touch the monitor -
                // which happens on every theme rebuild and in every test - and it would ignore
                // the capabilities the caller passed in.
                int start = _settings.MonitorBrightness >= 0
                    ? _settings.MonitorBrightness
                    : cap.BrightnessCurrent;

                rowY = AddRow(card, "BRIGHTNESS", rowY, start,
                    v => { _pendingBrightness = v; _settings.MonitorBrightness = v; _write.Trigger(); });
            }

            if (cap.SupportsContrast)
            {
                // 50 when unset, not 0. The probe cannot read contrast back, so this is a
                // starting position rather than a reading - and a slider parked at 0 would
                // claim the panel's contrast is off, which is a statement we cannot make.
                int start = _settings.MonitorContrast >= 0 ? _settings.MonitorContrast : 50;

                rowY = AddRow(card, "CONTRAST", rowY, start,
                    v => { _pendingContrast = v; _settings.MonitorContrast = v; _write.Trigger(); });
            }

            if (cap.SupportsRgbGain)
            {
                // 0 is genuinely correct here: off is the default, and off is what the panel
                // is doing until somebody moves this.
                rowY = AddRow(card, "LOW BLUE LIGHT", rowY, Math.Max(0, _settings.MonitorLowBlue),
                    v => { _pendingBlue = v; _settings.MonitorLowBlue = v; _write.Trigger(); });

                card.Controls.Add(new Label
                {
                    Text = "Warmer for late sessions. Done by lowering the panel's blue gain, "
                         + "which every monitor understands.",
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
        /// Push whatever changed to the panel, then save.
        ///
        /// Saved only after the write, so a setting the monitor refused is not recorded as if
        /// it had taken - the next launch would restore a value the panel never accepted.
        /// </summary>
        private void Flush()
        {
            if (_pendingBrightness >= 0 && MonitorControl.SetBrightness(_pendingBrightness))
                _pendingBrightness = -1;

            if (_pendingContrast >= 0 && MonitorControl.SetContrast(_pendingContrast))
                _pendingContrast = -1;

            if (_pendingBlue >= 0 && MonitorControl.SetLowBlueLight(_pendingBlue))
                _pendingBlue = -1;

            _store.Save(_settings);
        }

        /// <summary>The probe's verdict, for tests and for the capability report.</summary>
        internal IReadOnlyList<MonitorCapability> Capabilities => _caps;

        internal bool OffersAnyHardwareControl =>
            _caps.Any(c => c.SupportsBrightness || c.SupportsContrast || c.SupportsRgbGain);
    }
}
