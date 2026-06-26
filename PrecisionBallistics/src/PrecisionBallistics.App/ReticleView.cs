using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PrecisionBallistics.Core;

namespace PrecisionBallistics.App
{
    /// <summary>
    /// An original "christmas-tree" holdover grid. It draws a central stadia with
    /// fine subdivisions and a tapering tree of wind branches, then plots the
    /// computed elevation/windage solution for each range as a labelled dot.
    /// The grid switches between MIL and MOA and between metric/imperial range
    /// labels. Scroll the mouse wheel to zoom (to count subdivisions), drag to
    /// pan, right-click to reset the view. This is a from-scratch visualisation,
    /// not a copy of any specific commercial reticle or database.
    /// </summary>
    public sealed class ReticleView : FrameworkElement
    {
        private TrajectorySolution? _solution;
        private bool _useMoa;
        private bool _useMetric = true;

        private double _zoom = 1.0;
        private double _panX = 0.0;
        private double _panY = 0.0;
        private bool _dragging;
        private Point _dragStart;
        private double _panStartX, _panStartY;

        private double _lastCx, _lastCy, _lastPx;
        private double? _curElev, _curWind;
        private string _holdText = "";

        private static readonly Brush BgBrush = new SolidColorBrush(Color.FromRgb(0x12, 0x17, 0x1C));
        private static readonly Brush GridBrush = new SolidColorBrush(Color.FromRgb(0x29, 0x33, 0x3C));
        private static readonly Brush GridBrightBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x47, 0x52));
        private static readonly Brush AxisBrush = new SolidColorBrush(Color.FromRgb(0x6A, 0x7C, 0x8A));
        private static readonly Brush DotBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xA0, 0x50));
        private static readonly Brush SubBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x68, 0x5A));
        private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(0xC9, 0xD3, 0xDA));
        private static readonly Brush MutedBrush = new SolidColorBrush(Color.FromRgb(0x86, 0x95, 0xA1));

        private Pen _faint = null!, _branch = null!, _axis = null!;

        static ReticleView()
        {
            BgBrush.Freeze(); GridBrush.Freeze(); GridBrightBrush.Freeze(); AxisBrush.Freeze();
            DotBrush.Freeze(); SubBrush.Freeze(); TextBrush.Freeze(); MutedBrush.Freeze();
        }

        public ReticleView()
        {
            Focusable = true;
            ClipToBounds = true;
            _faint = new Pen(GridBrush, 0.7); _faint.Freeze();
            _branch = new Pen(GridBrightBrush, 1.0); _branch.Freeze();
            _axis = new Pen(AxisBrush, 1.3); _axis.Freeze();
        }

        public void SetData(TrajectorySolution? solution, bool useMoa, bool useMetric)
        {
            _solution = solution;
            _useMoa = useMoa;
            _useMetric = useMetric;
            InvalidateVisual();
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            double factor = e.Delta > 0 ? 1.12 : 1.0 / 1.12;
            _zoom = Math.Clamp(_zoom * factor, 0.4, 10.0);
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            _dragging = true;
            _dragStart = e.GetPosition(this);
            _panStartX = _panX; _panStartY = _panY;
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var p = e.GetPosition(this);
            if (_dragging)
            {
                _panX = _panStartX + (p.X - _dragStart.X);
                _panY = _panStartY + (p.Y - _dragStart.Y);
                InvalidateVisual();
                return;
            }
            if (_lastPx > 0)
            {
                _curWind = (p.X - _lastCx) / _lastPx;
                _curElev = (p.Y - _lastCy) / _lastPx;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            var p = e.GetPosition(this);
            double moved = Math.Abs(p.X - _dragStart.X) + Math.Abs(p.Y - _dragStart.Y);
            _dragging = false;
            ReleaseMouseCapture();

            if (moved < 3 && _solution != null && _lastPx > 0)
            {
                double clickElev = (p.Y - _lastCy) / _lastPx;
                TrajectoryPoint? best = null;
                double bd = double.MaxValue;
                foreach (var pt in _solution.Points)
                {
                    double el = _useMoa ? pt.ElevationMoa : pt.ElevationMil;
                    if (el < 0) continue;
                    double d = Math.Abs(el - clickElev);
                    if (d < bd) { bd = d; best = pt; }
                }
                if (best != null)
                {
                    double rng = _useMetric ? best.RangeM : best.RangeYd;
                    double el = _useMoa ? best.ElevationMoa : best.ElevationMil;
                    double wd = _useMoa ? best.WindageMoa : best.WindageMil;
                    _holdText = $"≈ {rng:0} {(_useMetric ? "m" : "yd")}  ·  hold {el:0.0} {(_useMoa ? "MOA" : "MIL")} up, {wd:0.0} wind";
                }
                InvalidateVisual();
            }
            e.Handled = true;
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            _zoom = 1.0; _panX = 0; _panY = 0;
            InvalidateVisual();
            e.Handled = true;
        }

        private double Half(double e, double baseHalf, double slope, double cap)
            => Math.Min(cap, baseHalf + slope * e);

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 10 || h < 10) return;
            dc.DrawRectangle(BgBrush, null, new Rect(0, 0, w, h));

            double cx = w / 2.0 + _panX;
            double cy = h * 0.16 + _panY;

            double major = _useMoa ? 5.0 : 1.0;
            double minor = _useMoa ? 1.0 : 0.5;
            double fine = _useMoa ? 1.0 : 0.2;

            double baseHalf = _useMoa ? 2.0 : 0.5;
            double slope = _useMoa ? 0.45 : 0.55;
            double cap = _useMoa ? 18.0 : 6.0;

            // Determine elevation extent from the solution.
            double maxElev = major * 2;
            if (_solution != null)
                foreach (var p in _solution.Points)
                {
                    double e = _useMoa ? p.ElevationMoa : p.ElevationMil;
                    if (e > maxElev) maxElev = e;
                }
            maxElev = Math.Ceiling(maxElev / major) * major;

            double basePx = (h * 0.78) / (maxElev * 1.12);
            if (basePx <= 0 || double.IsInfinity(basePx)) basePx = 14;
            double px = basePx * _zoom;             // pixels per unit (mil or moa)
            _lastCx = cx; _lastCy = cy; _lastPx = px;

            double envMax = Half(maxElev, baseHalf, slope, cap);

            // Tree branches (every minor elevation step).
            for (double e = minor; e <= maxElev + 1e-9; e += minor)
            {
                double y = cy + e * px;
                if (y < -20) continue;
                if (y > h + 20) break;
                double hw = Half(e, baseHalf, slope, cap);
                bool isMajor = Math.Abs(e / major - Math.Round(e / major)) < 1e-6;
                dc.DrawLine(isMajor ? _branch : _faint,
                            new Point(cx - hw * px, y), new Point(cx + hw * px, y));

                for (double wnd = minor; wnd <= hw + 1e-9; wnd += minor)
                {
                    bool wMajor = Math.Abs(wnd / major - Math.Round(wnd / major)) < 1e-6;
                    double tick = wMajor ? 5 : 3;
                    double xl = cx - wnd * px, xr = cx + wnd * px;
                    dc.DrawLine(_faint, new Point(xl, y - tick), new Point(xl, y + tick));
                    dc.DrawLine(_faint, new Point(xr, y - tick), new Point(xr, y + tick));
                }

                if (isMajor)
                    DrawLabel(dc, e.ToString("0", CultureInfo.InvariantCulture),
                              cx + hw * px + 4, y - 7, 9, MutedBrush);
            }

            // Main axes.
            dc.DrawLine(_axis, new Point(cx, 0), new Point(cx, h));
            dc.DrawLine(_axis, new Point(0, cy), new Point(w, cy));

            // Central vertical stadia: fine ticks down the centreline.
            for (double e = fine; e <= maxElev + 1e-9; e += fine)
            {
                double y = cy + e * px;
                if (y > h) break;
                bool isMajor = Math.Abs(e / major - Math.Round(e / major)) < 1e-6;
                bool isMinor = Math.Abs(e / minor - Math.Round(e / minor)) < 1e-6;
                double tick = isMajor ? 9 : isMinor ? 6 : 3;
                dc.DrawLine(_axis, new Point(cx - tick, y), new Point(cx + tick, y));
            }

            // Horizontal centre ticks (wind reference).
            for (double wd = fine; wd <= envMax + 1e-9; wd += fine)
            {
                bool isMajor = Math.Abs(wd / major - Math.Round(wd / major)) < 1e-6;
                bool isMinor = Math.Abs(wd / minor - Math.Round(wd / minor)) < 1e-6;
                double tick = isMajor ? 9 : isMinor ? 6 : 3;
                double xl = cx - wd * px, xr = cx + wd * px;
                dc.DrawLine(_axis, new Point(xl, cy - tick), new Point(xl, cy + tick));
                dc.DrawLine(_axis, new Point(xr, cy - tick), new Point(xr, cy + tick));
                if (isMajor)
                    DrawLabel(dc, wd.ToString("0", CultureInfo.InvariantCulture),
                              cx + wd * px - 4, cy + 10, 9, MutedBrush);
            }

            // Plot the firing solution holdover dots.
            if (_solution != null)
            {
                foreach (var p in _solution.Points)
                {
                    double e = _useMoa ? p.ElevationMoa : p.ElevationMil;
                    double wd = _useMoa ? p.WindageMoa : p.WindageMil;
                    if (e < 0) continue;
                    double X = cx + wd * px, Y = cy + e * px;
                    if (Y > h || Y < 0) continue;
                    var brush = p.Supersonic ? DotBrush : SubBrush;
                    dc.DrawEllipse(brush, null, new Point(X, Y), 3.4, 3.4);
                    double rng = _useMetric ? p.RangeM : p.RangeYd;
                    DrawLabel(dc, rng.ToString("0", CultureInfo.InvariantCulture),
                              X + 6, Y - 8, 10, TextBrush);
                }
            }

            // Cursor crosshair + live readout.
            if (_curElev.HasValue && _curWind.HasValue)
            {
                double cxp = cx + _curWind.Value * px;
                double cyp = cy + _curElev.Value * px;
                var cpen = new Pen(DotBrush, 0.6);
                dc.DrawLine(cpen, new Point(cxp, cy), new Point(cxp, cyp));
                dc.DrawLine(cpen, new Point(cx, cyp), new Point(cxp, cyp));
            }

            // Legend.
            string unit = _useMoa ? "MOA" : "MIL";
            string rl = _useMetric ? "m" : "yd";
            DrawLabel(dc,
                $"holdover tree — {unit} grid · {rl} ranges · scroll = zoom ×{_zoom.ToString("0.0", CultureInfo.InvariantCulture)} · drag = pan · right-click = reset",
                8, 6, 11, TextBrush);

            if (_curElev.HasValue && _curWind.HasValue && _curElev.Value >= 0)
                DrawLabel(dc,
                    $"cursor: {_curElev.Value.ToString("0.0", CultureInfo.InvariantCulture)} {unit} up · {_curWind.Value.ToString("0.0", CultureInfo.InvariantCulture)} {unit} wind",
                    8, h - 36, 11, MutedBrush);

            if (_holdText.Length > 0)
                DrawLabel(dc, "click → " + _holdText, 8, h - 19, 11, DotBrush);
        }

        private void DrawLabel(DrawingContext dc, string text, double x, double y, double size, Brush brush)
        {
            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                size,
                brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(x, y));
        }
    }
}
