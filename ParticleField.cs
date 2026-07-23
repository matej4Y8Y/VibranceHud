using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace VibranceHud
{
    /// <summary>
    /// An animated purple "plexus": glowing nodes drift slowly around the window (bouncing
    /// softly off the edges so they float), and thin lines web between any two nodes closer
    /// than a threshold - the lines fade with distance, so the web constantly forms and
    /// dissolves. Recreates the reference network animation procedurally. Pure drawing; the
    /// hosting window owns the timer and decides when to run it.
    /// </summary>
    public sealed class ParticleField
    {
        private struct Node
        {
            public float X, Y, Vx, Vy, Phase, Size;
            public bool Big;
            public Color Color;
        }

        private readonly struct Segment
        {
            public readonly float X1, Y1, X2, Y2;
            public readonly int Alpha;
            public Segment(float x1, float y1, float x2, float y2, int a) { X1 = x1; Y1 = y1; X2 = x2; Y2 = y2; Alpha = a; }
        }

        private static readonly Color Violet = Color.FromArgb(167, 139, 250);
        private static readonly Color Magenta = Color.FromArgb(232, 96, 214);
        private static readonly Color LineColor = Color.FromArgb(150, 130, 240);

        private readonly Node[] _nodes;
        private readonly List<Segment> _segments = new();
        private readonly Random _rng = new();
        private int _w, _h;
        private double _t;

        public ParticleField(int count) => _nodes = new Node[count];

        private float Threshold => Math.Clamp(Math.Min(_w, _h) * 0.26f, 120f, 220f);

        public void Resize(int w, int h)
        {
            bool first = _w == 0 && _h == 0;
            _w = Math.Max(1, w);
            _h = Math.Max(1, h);
            if (first)
            {
                for (int i = 0; i < _nodes.Length; i++)
                    Spawn(ref _nodes[i]);
                BuildSegments(); // so the web is present on the very first paint
            }
        }

        private void Spawn(ref Node n)
        {
            n.X = (float)(_rng.NextDouble() * _w);
            n.Y = (float)(_rng.NextDouble() * _h);
            double ang = _rng.NextDouble() * Math.PI * 2;
            float speed = 8f + (float)_rng.NextDouble() * 15f; // slow float (px/s)
            n.Vx = (float)Math.Cos(ang) * speed;
            n.Vy = (float)Math.Sin(ang) * speed;
            n.Phase = (float)(_rng.NextDouble() * Math.PI * 2);
            n.Big = _rng.NextDouble() < 0.4;
            n.Size = n.Big ? 3f + (float)_rng.NextDouble() * 2f : 1.8f + (float)_rng.NextDouble() * 1.2f;
            n.Color = _rng.NextDouble() < 0.5 ? Violet : Magenta;
        }

        public void Update(double dtSeconds)
        {
            _t += dtSeconds;
            float dt = (float)dtSeconds;

            for (int i = 0; i < _nodes.Length; i++)
            {
                ref var n = ref _nodes[i];
                n.X += n.Vx * dt;
                n.Y += n.Vy * dt;
                if (n.X < 0) { n.X = 0; n.Vx = -n.Vx; }
                else if (n.X > _w) { n.X = _w; n.Vx = -n.Vx; }
                if (n.Y < 0) { n.Y = 0; n.Vy = -n.Vy; }
                else if (n.Y > _h) { n.Y = _h; n.Vy = -n.Vy; }
            }

            BuildSegments();
        }

        // Rebuild the web once per frame; the surfaces then just draw these segments.
        private void BuildSegments()
        {
            _segments.Clear();
            float thr = Threshold, thr2 = thr * thr;
            for (int i = 0; i < _nodes.Length; i++)
            {
                for (int j = i + 1; j < _nodes.Length; j++)
                {
                    float dx = _nodes[i].X - _nodes[j].X, dy = _nodes[i].Y - _nodes[j].Y;
                    float d2 = dx * dx + dy * dy;
                    if (d2 >= thr2) continue;
                    float d = (float)Math.Sqrt(d2);
                    int a = (int)((1f - d / thr) * 95f);
                    if (a <= 3) continue;
                    _segments.Add(new Segment(_nodes[i].X, _nodes[i].Y, _nodes[j].X, _nodes[j].Y, a));
                }
            }
        }

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

            using (var pen = new Pen(LineColor, 1f))
            {
                foreach (var s in _segments)
                {
                    pen.Color = Color.FromArgb(s.Alpha, LineColor);
                    g.DrawLine(pen, s.X1, s.Y1, s.X2, s.Y2);
                }
            }

            using var brush = new SolidBrush(Color.White);
            foreach (var n in _nodes)
            {
                float twinkle = 0.7f + 0.3f * (float)Math.Sin(_t * 2 + n.Phase);
                int alpha = (int)(twinkle * 230);

                if (n.Big)
                {
                    brush.Color = Color.FromArgb(Math.Min(60, alpha / 3), n.Color);
                    float hs = n.Size * 4.5f;
                    g.FillEllipse(brush, n.X - hs / 2, n.Y - hs / 2, hs, hs);
                }

                brush.Color = Color.FromArgb(Math.Min(240, alpha), n.Color);
                g.FillEllipse(brush, n.X - n.Size / 2, n.Y - n.Size / 2, n.Size, n.Size);
            }
        }
    }
}
