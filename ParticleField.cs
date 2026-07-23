using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace VibranceHud
{
    /// <summary>
    /// An animated purple/magenta particle field that emanates from the centre and fades
    /// out toward the edges - recreating the reference video's look procedurally, so it
    /// stays sharp at any size and costs almost nothing. Pure drawing; the hosting control
    /// owns the timer and decides when to run it.
    /// </summary>
    public sealed class ParticleField
    {
        private struct Particle
        {
            public float Ang, Rad, Speed, Bright, Phase, Size;
            public bool Node;
            public Color Color;
        }

        private static readonly Color Violet = Color.FromArgb(167, 139, 250);
        private static readonly Color Magenta = Color.FromArgb(232, 96, 214);

        private readonly Particle[] _ps;
        private readonly Random _rng = new();
        private int _w, _h;
        private double _t;

        public ParticleField(int count) => _ps = new Particle[count];

        private float MaxR => (float)Math.Sqrt(_w * (double)_w + _h * (double)_h) * 0.55f;

        public void Resize(int w, int h)
        {
            bool first = _w == 0 && _h == 0;
            _w = Math.Max(1, w);
            _h = Math.Max(1, h);
            if (first)
                for (int i = 0; i < _ps.Length; i++)
                    Spawn(ref _ps[i], anywhere: true);
        }

        private void Spawn(ref Particle p, bool anywhere)
        {
            p.Ang = (float)(_rng.NextDouble() * Math.PI * 2);
            // Bias toward the centre (dense core, sparse rim) via a power curve.
            p.Rad = anywhere
                ? (float)(MaxR * Math.Pow(_rng.NextDouble(), 1.7))
                : (float)(MaxR * 0.05 * _rng.NextDouble());
            p.Speed = 5f + (float)_rng.NextDouble() * 12f; // slow outward drift (px/s)
            p.Bright = 0.35f + (float)_rng.NextDouble() * 0.65f;
            p.Phase = (float)(_rng.NextDouble() * Math.PI * 2);
            p.Node = _rng.NextDouble() < 0.12; // ~12% are bright, haloed "nodes"
            p.Size = p.Node ? 3f + (float)_rng.NextDouble() * 2.5f : 1.5f + (float)_rng.NextDouble() * 1.5f;
            p.Color = _rng.NextDouble() < 0.5 ? Violet : Magenta;
        }

        public void Update(double dtSeconds)
        {
            _t += dtSeconds;
            float maxR = MaxR;
            for (int i = 0; i < _ps.Length; i++)
            {
                _ps[i].Rad += _ps[i].Speed * (float)dtSeconds;
                if (_ps[i].Rad > maxR)
                    Spawn(ref _ps[i], anywhere: false); // recycle back near the centre
            }
        }

        /// <summary>Paints the field into a surface whose top-left sits at (offsetX,
        /// offsetY) in window coordinates, so several surfaces share one continuous field
        /// centred on the window.</summary>
        public void Paint(Graphics g, int offsetX, int offsetY)
        {
            var state = g.Save();
            g.TranslateTransform(-offsetX, -offsetY);
            PaintCore(g);
            g.Restore(state);
        }

        private void PaintCore(Graphics g)
        {
            if (_w <= 1 || _h <= 1) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float cx = _w / 2f, cy = _h / 2f, maxR = MaxR;

            // Soft central bloom.
            using (var bloomPath = new GraphicsPath())
            {
                float rw = maxR * 1.6f;
                var bounds = new RectangleF(cx - rw, cy - rw * 0.62f, rw * 2, rw * 1.24f);
                bloomPath.AddEllipse(bounds);
                using var bloom = new PathGradientBrush(bloomPath)
                {
                    CenterColor = Color.FromArgb(38, Violet),
                    SurroundColors = new[] { Color.FromArgb(0, Violet) }
                };
                g.FillPath(bloom, bloomPath);
            }

            using var brush = new SolidBrush(Color.White);
            foreach (var p in _ps)
            {
                float rf = 1f - p.Rad / maxR; // brighter near the centre
                if (rf <= 0) continue;

                float x = cx + (float)Math.Cos(p.Ang) * p.Rad;
                float y = cy + (float)Math.Sin(p.Ang) * p.Rad;
                float twinkle = 0.65f + 0.35f * (float)Math.Sin(_t * 2 + p.Phase);
                int alpha = (int)(p.Bright * rf * twinkle * 205);
                if (alpha <= 3) continue;

                if (p.Node)
                {
                    brush.Color = Color.FromArgb(Math.Min(55, alpha / 3), p.Color);
                    float hs = p.Size * 4.5f;
                    g.FillEllipse(brush, x - hs / 2, y - hs / 2, hs, hs);
                }

                brush.Color = Color.FromArgb(Math.Min(235, alpha), p.Color);
                g.FillEllipse(brush, x - p.Size / 2, y - p.Size / 2, p.Size, p.Size);
            }
        }
    }
}
