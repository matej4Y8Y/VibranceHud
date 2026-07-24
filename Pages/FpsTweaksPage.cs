using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VibranceHud.SystemTweaks;

namespace VibranceHud.Pages
{
    /// <summary>
    /// System-wide FPS / latency tweaks. Two cards: "Recommended" (safe, clean wins) and
    /// "Advanced" (real but situational, flagged, off by default). Each row is a real
    /// registry-backed toggle that reads its true state back from Windows and shows a plain
    /// status line when on. Admin-only tweaks trigger a single scoped UAC prompt.
    /// </summary>
    public sealed class FpsTweaksPage : GlowPage
    {
        private readonly SystemTweakService _service;
        private bool _syncing;

        private static readonly Font LabelFont = new(Theme.FontFamily, 10f, FontStyle.Bold);
        private static readonly Font DescFont = new(Theme.FontFamily, 8.5f);
        private static readonly Font StatusFont = new(Theme.FontFamily, 8f, FontStyle.Bold);

        public FpsTweaksPage(SystemTweakService service)
        {
            _service = service;

            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            AutoScroll = true;
            Font = new Font(Theme.FontFamily, 9.5f);
            Padding = new Padding(40, 32, 40, 32);

            const int width = 640;
            int y = 40;

            y = BuildCard(y, width, "RECOMMENDED",
                "Safe, reversible tweaks that give real FPS and lower input lag.",
                _service.All.Where(t => t.Tier == TweakTier.Safe).ToList());

            BuildCard(y, width, "ADVANCED",
                "Real but situational - only helps on some PCs. Turn on one at a time and test.",
                _service.All.Where(t => t.Tier == TweakTier.Advanced).ToList());
        }

        private int BuildCard(int top, int width, string title, string subtitle, IReadOnlyList<ISystemTweak> tweaks)
        {
            const int rowH = 66;
            int headerH = 60;
            var card = new CardPanel
            {
                Location = new Point(40, top),
                Size = new Size(width, headerH + tweaks.Count * rowH + 12)
            };
            card.Controls.Add(UiHelpers.Caption(title, 18, 16, 300));
            card.Controls.Add(new Label
            {
                Text = subtitle,
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = DescFont,
                Location = new Point(18, 34),
                Size = new Size(width - 36, 18)
            });

            int y = headerH;
            foreach (var tweak in tweaks)
            {
                AddRow(card, tweak, y, width);
                y += rowH;
            }

            Controls.Add(card);
            return top + card.Height + 20;
        }

        private void AddRow(CardPanel card, ISystemTweak tweak, int y, int width)
        {
            card.Controls.Add(new Label
            {
                Text = tweak.Label + (tweak.RequiresAdmin ? "   (admin)" : ""),
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Font = LabelFont,
                Location = new Point(18, y),
                Size = new Size(width - 90, 20)
            });
            card.Controls.Add(new Label
            {
                Text = tweak.Description,
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = DescFont,
                Location = new Point(18, y + 20),
                Size = new Size(width - 90, 30)
            });

            var status = new Label
            {
                Text = tweak.IsApplied() ? StatusLine(tweak) : "",
                ForeColor = Theme.Accent,
                BackColor = Color.Transparent,
                Font = StatusFont,
                Location = new Point(18, y + 44),
                Size = new Size(width - 90, 16)
            };
            card.Controls.Add(status);

            var toggle = new ToggleSwitch
            {
                Location = new Point(width - 62, y + 6),
                Checked = tweak.IsApplied()
            };
            toggle.CheckedChanged += (s, e) =>
            {
                if (_syncing) return;
                var result = _service.Toggle(tweak.Id, toggle.Checked);

                if (result == null && toggle.Checked)
                {
                    // Failed or the user declined UAC - snap the toggle back.
                    _syncing = true;
                    toggle.Checked = false;
                    _syncing = false;
                }

                // Re-read the real state so the row always reflects the machine.
                _syncing = true;
                toggle.Checked = tweak.IsApplied();
                _syncing = false;
                status.Text = tweak.IsApplied() ? StatusLine(tweak) : "";
                Invalidate();
            };
            card.Controls.Add(toggle);
        }

        private static string StatusLine(ISystemTweak tweak) =>
            tweak is RegistryTweak r ? "✓ " + r.AppliedStatus : "✓ Applied";
    }
}
