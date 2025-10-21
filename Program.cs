using System;
using System.Drawing;
using System.Windows.Forms;

namespace LabWork
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new PlotForm());
        }
    }

    public class PlotForm : Form
    {
        // Function domain and step (private fields with underscore)
        private readonly double _xMin = 2.5;
        private readonly double _xMax = 9.0;
        private readonly double _dx = 0.8;

    // layout constants
    private const int PaddingInside = 40;
    private const float PointRadius = 2.5f;
    private const double Eps = 1e-12;

    // cached resources
    private readonly Font _labelFont = new Font("Segoe UI", 9);

        // UI: ComboBox for line style
        private readonly ComboBox _lineStyleCombo;
        private System.Drawing.Drawing2D.DashStyle _currentDashStyle = System.Drawing.Drawing2D.DashStyle.Solid;

        public PlotForm()
        {
            Text = "Plot: y = (1.5x - ln(2x)) / (3x + 1)";
            MinimumSize = new Size(400, 300);
            DoubleBuffered = true; // reduce flicker
            BackColor = Color.White;

            // ComboBox for line style
            _lineStyleCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Left = 10,
                Top = 10,
                Width = 160,
                Font = _labelFont,
            };
            _lineStyleCombo.Items.AddRange(new object[]
            {
                "Суцільна",
                "Штрихова",
                "Пунктирна",
                "Штрих-пунктир",
                "Штрих-пунктир-пунктир"
            });
            _lineStyleCombo.SelectedIndex = 0;
            _lineStyleCombo.SelectedIndexChanged += (s, e) =>
            {
                switch (_lineStyleCombo.SelectedIndex)
                {
                    case 0: _currentDashStyle = System.Drawing.Drawing2D.DashStyle.Solid; break;
                    case 1: _currentDashStyle = System.Drawing.Drawing2D.DashStyle.Dash; break;
                    case 2: _currentDashStyle = System.Drawing.Drawing2D.DashStyle.Dot; break;
                    case 3: _currentDashStyle = System.Drawing.Drawing2D.DashStyle.DashDot; break;
                    case 4: _currentDashStyle = System.Drawing.Drawing2D.DashStyle.DashDotDot; break;
                    default: _currentDashStyle = System.Drawing.Drawing2D.DashStyle.Solid; break;
                }
                Invalidate();
            };
            Controls.Add(_lineStyleCombo);

            // Invalidate on resize so OnPaint will be called
            this.Resize += (s, e) => Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            DrawPlot(e.Graphics);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        private void DrawPlot(Graphics g)
        {
            var client = this.ClientRectangle;
            if (client.Width <= 0 || client.Height <= 0) return;

            // Adjust plot area to leave space for ComboBox
            int topPad = PaddingInside;
            if (_lineStyleCombo.Visible)
            {
                topPad = Math.Max(PaddingInside, _lineStyleCombo.Bottom + 10);
            }

            // Compute points
            var points = ComputeFunctionPoints();

            // If no points, set sensible default and don't attempt to plot lines
            if (points.Count == 0)
            {
                double defaultMin = -1.0, defaultMax = 1.0;
                Rectangle plotAreaEmpty = new Rectangle(client.Left + PaddingInside, client.Top + PaddingInside, Math.Max(10, client.Width - 2 * PaddingInside), Math.Max(10, client.Height - 2 * PaddingInside));
                DrawAxes(g, plotAreaEmpty, _xMin, _xMax, defaultMin, defaultMax);
                return;
            }

            // Find y range
            double yMin = double.PositiveInfinity, yMax = double.NegativeInfinity;
            foreach (var pt in points)
            {
                if (pt.Y < yMin) yMin = pt.Y;
                if (pt.Y > yMax) yMax = pt.Y;
            }
            if (!(double.IsFinite(yMin) && double.IsFinite(yMax)) || Math.Abs(yMax - yMin) < Eps)
            {
                // sensible default if degenerate
                yMin = yMin - 1.0;
                yMax = yMax + 1.0;
                if (!double.IsFinite(yMin) || !double.IsFinite(yMax))
                {
                    yMin = -1.0; yMax = 1.0;
                }
            }

            // Padding inside client area
            Rectangle plotArea = new Rectangle(client.Left + PaddingInside, client.Top + topPad, Math.Max(10, client.Width - 2 * PaddingInside), Math.Max(10, client.Height - topPad - PaddingInside));

            // Draw axes and grid
            DrawAxes(g, plotArea, _xMin, _xMax, yMin, yMax);

            // Transform points to screen
            PointF[] screenPts = new PointF[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                screenPts[i] = ToScreen(points[i], plotArea, _xMin, _xMax, yMin, yMax);
            }

            using (var pen = new Pen(Color.Blue, 2f))
            {
                pen.DashStyle = _currentDashStyle;
                if (screenPts.Length >= 2)
                {
                    g.DrawLines(pen, screenPts);
                }
                else if (screenPts.Length == 1)
                {
                    g.DrawEllipse(pen, screenPts[0].X - PointRadius, screenPts[0].Y - PointRadius, PointRadius * 2, PointRadius * 2);
                }
            }

            // Draw points
            using (var brush = new SolidBrush(Color.Red))
            {
                foreach (var p in screenPts)
                {
                    g.FillEllipse(brush, p.X - PointRadius, p.Y - PointRadius, PointRadius * 2, PointRadius * 2);
                }
            }
        }

        private void DrawAxes(Graphics g, Rectangle area, double xMin, double xMax, double yMin, double yMax)
        {
                using (var thinPen = new Pen(Color.Gray, 1f))
            using (var axisPen = new Pen(Color.Black, 1.5f))
            using (var brush = new SolidBrush(Color.Black))
            {
                // Draw border
                g.DrawRectangle(thinPen, area);

                // Choose ticks for x and y
                double xRange = xMax - xMin;
                double yRange = yMax - yMin;

                int xTicks = Math.Max(2, (int)Math.Ceiling(xRange));
                int yTicks = Math.Max(2, (int)Math.Ceiling(yRange));
                // cap ticks to avoid overcrowding
                yTicks = Math.Min(yTicks, 20);
                xTicks = Math.Min(xTicks, 20);

                // X ticks
                for (int i = 0; i <= xTicks; i++)
                {
                    double xv = xMin + i * xRange / xTicks;
                    var p = ToScreen(new PointD(xv, 0), area, xMin, xMax, yMin, yMax);
                    g.DrawLine(thinPen, p.X, area.Top, p.X, area.Bottom);
                    string label = xv.ToString("0.##");
                    var sz = g.MeasureString(label, _labelFont);
                    g.DrawString(label, _labelFont, brush, p.X - sz.Width / 2, area.Bottom + 2);
                }

                // Y ticks
                for (int i = 0; i <= yTicks; i++)
                {
                    double yv = yMin + i * yRange / yTicks;
                    var p = ToScreen(new PointD(0, yv), area, xMin, xMax, yMin, yMax);
                    g.DrawLine(thinPen, area.Left, p.Y, area.Right, p.Y);
                    string label = yv.ToString("0.##");
                    var sz = g.MeasureString(label, _labelFont);
                    g.DrawString(label, _labelFont, brush, area.Left - sz.Width - 4, p.Y - sz.Height / 2);
                }

                // Draw X and Y axis lines at 0 if visible
                if (yMin <= 0 && yMax >= 0)
                {
                    var p0 = ToScreen(new PointD(0, 0), area, xMin, xMax, yMin, yMax);
                    g.DrawLine(axisPen, area.Left, p0.Y, area.Right, p0.Y);
                }
                if (xMin <= 0 && xMax >= 0)
                {
                    var p0 = ToScreen(new PointD(0, 0), area, xMin, xMax, yMin, yMax);
                    g.DrawLine(axisPen, p0.X, area.Top, p0.X, area.Bottom);
                }

                // Axis labels: place 'x' near right end of x-axis (or bottom-right) and 'y' near top of y-axis (or top-left)
                string xLabel = "x";
                string yLabel = "y";
                SizeF xLabelSize = g.MeasureString(xLabel, _labelFont);
                SizeF yLabelSize = g.MeasureString(yLabel, _labelFont);

                // X label position
                PointF xLabelPos;
                if (yMin <= 0 && yMax >= 0)
                {
                    var p0 = ToScreen(new PointD(xMax, 0), area, xMin, xMax, yMin, yMax);
                    xLabelPos = new PointF(Math.Min(area.Right - xLabelSize.Width, p0.X - xLabelSize.Width / 2), Math.Min(area.Bottom + 4, area.Bottom + 4));
                }
                else
                {
                    xLabelPos = new PointF(area.Right - xLabelSize.Width - 4, area.Bottom + 4);
                }

                // Y label position
                PointF yLabelPos;
                if (xMin <= 0 && xMax >= 0)
                {
                    var p0 = ToScreen(new PointD(0, yMax), area, xMin, xMax, yMin, yMax);
                    yLabelPos = new PointF(Math.Max(area.Left + 4, area.Left + 4), Math.Max(area.Top + 2, p0.Y - yLabelSize.Height / 2));
                }
                else
                {
                    yLabelPos = new PointF(area.Left + 4, area.Top + 2);
                }

                using (var labelBrush = new SolidBrush(Color.Black))
                {
                    g.DrawString(xLabel, _labelFont, labelBrush, xLabelPos);
                    g.DrawString(yLabel, _labelFont, labelBrush, yLabelPos);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _labelFont?.Dispose();
            }
            base.Dispose(disposing);
        }

        private PointF ToScreen(PointD pt, Rectangle area, double xMin, double xMax, double yMin, double yMax)
        {
            double xRange = xMax - xMin;
            double yRange = yMax - yMin;
            // Protect against zero ranges
            float sx = area.Left + (float)((xRange == 0) ? area.Width / 2.0 : (pt.X - xMin) / xRange * area.Width);
            // invert y
            float sy = (float)(area.Bottom - ((yRange == 0) ? area.Height / 2.0 : (pt.Y - yMin) / yRange * area.Height));
            return new PointF(sx, sy);
        }

        private System.Collections.Generic.List<PointD> ComputeFunctionPoints()
        {
            var list = new System.Collections.Generic.List<PointD>();
            if (_dx <= 0) return list; // invalid step

            // compute number of steps robustly using floor with eps to avoid overshoot
            int n = (int)Math.Floor(((_xMax - _xMin) / _dx) + Eps);
            if (n < 0) return list;
            for (int i = 0; i <= n; i++)
            {
                double x = _xMin + i * _dx;
                // clamp possible floating rounding overshoot
                if (x < _xMin - Eps) x = _xMin;
                if (x > _xMax + Eps) x = _xMax;

                // domain checks
                if (2 * x <= 0) continue; // ln domain
                double denom = 3 * x + 1;
                if (Math.Abs(denom) < 1e-12) continue; // avoid division by zero (use slightly looser eps)

                double y = (1.5 * x - Math.Log(2 * x)) / denom;
                if (double.IsNaN(y) || double.IsInfinity(y)) continue;
                list.Add(new PointD(x, y));
            }
            // Ensure last point at xMax is included (in case rounding skipped it)
            if (list.Count == 0 || Math.Abs(list[list.Count - 1].X - _xMax) > Eps)
            {
                double x = _xMax;
                if (2 * x > 0)
                {
                    double denom = 3 * x + 1;
                    if (Math.Abs(denom) >= 1e-12)
                    {
                        double y = (1.5 * x - Math.Log(2 * x)) / denom;
                        if (!double.IsNaN(y) && !double.IsInfinity(y))
                            list.Add(new PointD(x, y));
                    }
                }
            }
            return list;
        }
    }

    // Small helper struct used to keep double precision points
    public readonly struct PointD
    {
        public double X { get; }
        public double Y { get; }
        public PointD(double x, double y) { X = x; Y = y; }
    }
}
