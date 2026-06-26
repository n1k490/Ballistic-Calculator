using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PrecisionBallistics.Core;

namespace PrecisionBallistics.App
{
    /// <summary>
    /// A from-scratch line chart of the trajectory: elevation (come-up) on the
    /// left axis and retained velocity on the right axis, both versus range.
    /// </summary>
    public sealed class TrajectoryChart : FrameworkElement
    {
        private TrajectorySolution? _solution;
        private bool _useMoa;
        private bool _useMetric = true;

        private static readonly Brush BgBrush = new SolidColorBrush(Color.FromRgb(0x12, 0x17, 0x1C));
        private static readonly Brush GridBrush = new SolidColorBrush(Color.FromRgb(0x29, 0x33, 0x3C));
        private static readonly Brush AxisBrush = new SolidColorBrush(Color.FromRgb(0x6A, 0x7C, 0x8A));
        private static readonly Brush ElevBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xA0, 0x50));
        private static readonly Brush VelBrush = new SolidColorBrush(Color.FromRgb(0x5F, 0xA8, 0xD9));
        private static readonly Brush TransBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x68, 0x5A));
        private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(0xC9, 0xD3, 0xDA));

        static TrajectoryChart()
        {
            BgBrush.Freeze(); GridBrush.Freeze(); AxisBrush.Freeze();
            ElevBrush.Freeze(); VelBrush.Freeze(); TransBrush.Freeze(); TextBrush.Freeze();
        }

        public void SetData(TrajectorySolution? solution, bool useMoa, bool useMetric)
        {
            _solution = solution; _useMoa = useMoa; _useMetric = useMetric;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 20 || h < 20) return;
            dc.DrawRectangle(BgBrush, null, new Rect(0, 0, w, h));
            if (_solution == null || _solution.Points.Count < 2) return;

            double ml = 48, mr = 54, mt = 18, mb = 30;     // margins
            double pw = w - ml - mr, ph = h - mt - mb;

            double maxRange = 0, maxElev = 0, maxVel = 0;
            foreach (var p in _solution.Points)
            {
                double rng = _useMetric ? p.RangeM : p.RangeYd;
                double el = _useMoa ? p.ElevationMoa : p.ElevationMil;
                double vel = _useMetric ? p.VelocityMps : p.VelocityFps;
                if (rng > maxRange) maxRange = rng;
                if (el > maxElev) maxElev = el;
                if (vel > maxVel) maxVel = vel;
            }
            if (maxRange <= 0) return;
            maxElev = Math.Max(maxElev * 1.1, 1);
            maxVel = Math.Max(maxVel * 1.1, 1);

            var grid = new Pen(GridBrush, 0.6);
            var axis = new Pen(AxisBrush, 1.2);
            var elevPen = new Pen(ElevBrush, 2.0);
            var velPen = new Pen(VelBrush, 2.0);

            double X(double rng) => ml + pw * (rng / maxRange);
            double YE(double el) => mt + ph * (el / maxElev);          // elevation grows downward
            double YV(double vel) => mt + ph * (1 - vel / maxVel);     // velocity normal upward

            // Grid + range labels.
            int xticks = 6;
            for (int i = 0; i <= xticks; i++)
            {
                double rng = maxRange * i / xticks;
                double x = X(rng);
                dc.DrawLine(grid, new Point(x, mt), new Point(x, mt + ph));
                DrawText(dc, rng.ToString("0", CultureInfo.InvariantCulture), x - 10, mt + ph + 6, 9, TextBrush);
            }
            // Left axis (elevation) labels.
            int yticks = 5;
            for (int i = 0; i <= yticks; i++)
            {
                double el = maxElev * i / yticks;
                double y = YE(el);
                dc.DrawLine(grid, new Point(ml, y), new Point(ml + pw, y));
                DrawText(dc, el.ToString("0.0", CultureInfo.InvariantCulture), 6, y - 7, 9, ElevBrush);
                double vel = maxVel * (1 - (double)i / yticks);
                DrawText(dc, vel.ToString("0", CultureInfo.InvariantCulture), ml + pw + 6, y - 7, 9, VelBrush);
            }

            dc.DrawLine(axis, new Point(ml, mt), new Point(ml, mt + ph));
            dc.DrawLine(axis, new Point(ml, mt + ph), new Point(ml + pw, mt + ph));

            // Transonic marker.
            if (_solution.TransonicRangeM > 0)
            {
                double tr = _useMetric ? _solution.TransonicRangeM : Units.MToYard(_solution.TransonicRangeM);
                double x = X(tr);
                dc.DrawLine(new Pen(TransBrush, 1.0), new Point(x, mt), new Point(x, mt + ph));
                DrawText(dc, "transonic", x + 3, mt + 2, 9, TransBrush);
            }

            // Curves.
            var elevGeo = new StreamGeometry();
            var velGeo = new StreamGeometry();
            using (var ec = elevGeo.Open())
            using (var vc = velGeo.Open())
            {
                bool first = true;
                foreach (var p in _solution.Points)
                {
                    double rng = _useMetric ? p.RangeM : p.RangeYd;
                    double el = _useMoa ? p.ElevationMoa : p.ElevationMil;
                    double vel = _useMetric ? p.VelocityMps : p.VelocityFps;
                    var ep = new Point(X(rng), YE(Math.Max(el, 0)));
                    var vp = new Point(X(rng), YV(vel));
                    if (first) { ec.BeginFigure(ep, false, false); vc.BeginFigure(vp, false, false); first = false; }
                    else { ec.LineTo(ep, true, false); vc.LineTo(vp, true, false); }
                }
            }
            elevGeo.Freeze(); velGeo.Freeze();
            dc.DrawGeometry(null, velPen, velGeo);
            dc.DrawGeometry(null, elevPen, elevGeo);

            // Legend.
            string au = _useMoa ? "MOA" : "MIL";
            string vu = _useMetric ? "m/s" : "fps";
            string ru = _useMetric ? "m" : "yd";
            DrawText(dc, $"elevation ({au})", ml + 4, mt + 2, 10, ElevBrush);
            DrawText(dc, $"velocity ({vu})", ml + pw - 86, mt + 2, 10, VelBrush);
            DrawText(dc, $"range ({ru})", ml + pw / 2 - 24, mt + ph + 16, 10, TextBrush);
        }

        private void DrawText(DrawingContext dc, string text, double x, double y, double size, Brush brush)
        {
            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(x, y));
        }
    }
}
