using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VibranceHud.Monitors;

namespace VibranceHud.Pages
{
    /// <summary>
    /// The monitor's own settings, reachable from the desk instead of from the buttons on the
    /// back of the screen.
    ///
    /// The page is built entirely from what the scan found. Nothing here is a fixed list with
    /// half of it greyed out - if a monitor didn't answer for sharpness, sharpness isn't on
    /// the page. That means the layout is different on different machines, which is the point.
    /// </summary>
    public sealed class MonitorsPage : GlowPage
    {
        private readonly MonitorService _service;
        private readonly FlowLayoutPanel _body = new();
        private readonly Label _status = new();
        private readonly GlassButton _rescan = new();

        public MonitorsPage(MonitorService service)
        {
            _service = service;

            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(28, 24, 28, 24);
            AutoScroll = true;

            var heading = new Label
            {
                Text = "MONITOR",
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Theme.TextDim,
                AutoSize = true,
                Location = new Point(30, 26),
            };
            Controls.Add(heading);

            _status.Font = new Font("Segoe UI", 9f);
            _status.ForeColor = Theme.TextDim;
            _status.AutoSize = false;
            _status.SetBounds(30, 48, 620, 44);
            _status.Text = "Looking for monitors...";
            Controls.Add(_status);

            _rescan.Text = "Rescan";
            _rescan.SetBounds(30, 96, 110, 30);
            _rescan.Visible = false;
            _rescan.Click += (s, e) => StartScan();
            Controls.Add(_rescan);

            _body.SetBounds(24, 136, 700, 460);
            _body.FlowDirection = FlowDirection.TopDown;
            _body.WrapContents = false;
            _body.AutoSize = true;
            _body.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Controls.Add(_body);

            _service.Updated += OnScanFinished;
            StartScan();
        }

        private void StartScan()
        {
            _rescan.Enabled = false;
            _status.Text = "Looking for monitors...";
            _body.Controls.Clear();
            _service.ScanAsync();
        }

        /// <summary>The scan finishes on a background thread; everything below touches
        /// controls, so it has to be marshalled back first.</summary>
        private void OnScanFinished()
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(new Action(Rebuild)); } catch (InvalidOperationException) { }
        }

        private void Rebuild()
        {
            if (IsDisposed) return;

            _body.Controls.Clear();
            _rescan.Visible = true;
            _rescan.Enabled = true;

            var monitors = _service.Monitors;

            if (monitors.Count == 0)
            {
                _status.Text =
                    "No monitor here can be controlled this way. It's usually laptop screens - " +
                    "they're wired straight to the board and have no settings to reach.";
                return;
            }

            if (!_service.AnyMonitorResponded)
            {
                // The important message on this page. This state is almost always one setting
                // away from working, and telling someone "unsupported" makes them stop looking.
                _status.Text =
                    "Your monitor didn't answer. Open its own menu with the buttons on the " +
                    "screen and look for DDC/CI - it's usually switched off from the factory. " +
                    "Turn it on and hit Rescan.";
                return;
            }

            _status.Text = monitors.Count == 1
                ? "Changing these is the same as using the buttons on your monitor. " +
                  "Everything goes back when you close PlexusX."
                : $"{monitors.Count} monitors found. Everything goes back when you close PlexusX.";

            foreach (var monitor in monitors)
                _body.Controls.Add(BuildCard(monitor));
        }

        private Control BuildCard(MonitorSnapshot monitor)
        {
            var card = new CardPanel
            {
                Width = 660,
                Margin = new Padding(6, 6, 6, 14),
                Padding = new Padding(20, 18, 20, 18),
            };

            var title = new Label
            {
                Text = monitor.Label,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Theme.Text,
                AutoSize = true,
                Location = new Point(20, 16),
            };
            card.Controls.Add(title);

            int y = 52;

            if (!monitor.RespondedAtAll)
            {
                card.Controls.Add(new Label
                {
                    Text = "Didn't answer - check DDC/CI in this monitor's own menu.",
                    ForeColor = Theme.TextDim,
                    AutoSize = true,
                    Location = new Point(20, y),
                });
                card.Height = y + 42;
                return card;
            }

            // Fixed order so the page doesn't reshuffle between scans, but only the ones this
            // monitor actually has.
            foreach (var setting in Order.Where(monitor.Supports))
            {
                card.Controls.AddRange(BuildRow(monitor, setting, y));
                y += 54;
            }

            card.Height = y + 12;
            return card;
        }

        private static readonly MonitorSetting[] Order =
        {
            MonitorSetting.Brightness,
            MonitorSetting.Contrast,
            MonitorSetting.Sharpness,
            MonitorSetting.Red,
            MonitorSetting.Green,
            MonitorSetting.Blue,
            MonitorSetting.Volume,
        };

        private Control[] BuildRow(MonitorSnapshot monitor, MonitorSetting setting, int y)
        {
            var range = monitor.Range(setting)!;

            var label = new Label
            {
                Text = Name(setting),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Theme.TextDim,
                AutoSize = true,
                Location = new Point(20, y + 6),
            };

            var value = new Label
            {
                Text = Display(setting, range),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Theme.Accent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Bounds = new Rectangle(560, y + 2, 70, 22),
            };

            var slider = new FlatSlider
            {
                Minimum = 0,
                Maximum = 100,
                Value = range.Percent,
                Bounds = new Rectangle(150, y, 400, 26),
            };

            slider.ValueChanged += (s, e) =>
            {
                value.Text = Display(setting, range, slider.Value);
                _service.SetPercent(monitor.DeviceName, setting, slider.Value);
            };

            return new Control[] { label, slider, value };
        }

        private static string Name(MonitorSetting setting) => setting switch
        {
            MonitorSetting.Brightness => "BRIGHTNESS",
            MonitorSetting.Contrast => "CONTRAST",
            MonitorSetting.Sharpness => "SHARPNESS",
            MonitorSetting.Red => "RED",
            MonitorSetting.Green => "GREEN",
            MonitorSetting.Blue => "BLUE",
            MonitorSetting.Volume => "VOLUME",
            MonitorSetting.Preset => "PICTURE MODE",
            MonitorSetting.InputSource => "INPUT",
            _ => setting.ToString().ToUpperInvariant(),
        };

        private static string Display(MonitorSetting setting, MonitorRange range, int? percent = null)
            => (percent ?? range.Percent) + "%";

        protected override void Dispose(bool disposing)
        {
            if (disposing) _service.Updated -= OnScanFinished;
            base.Dispose(disposing);
        }
    }
}
