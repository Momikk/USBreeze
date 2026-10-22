using System.Windows;
using System.Windows.Media;
namespace USBreeze.App;

public sealed class SpeedChart : FrameworkElement
{
    public string Series { get; set; } = "Write";
    private readonly List<double> writes = [], reads = [];
    public void Clear() { writes.Clear(); reads.Clear(); InvalidateVisual(); }
    public void Add(string phase, double value) { var samples = phase == "Write" ? writes : reads; samples.Add(value); if (samples.Count > 600) samples.RemoveAt(0); InvalidateVisual(); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var w = ActualWidth; var h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(235, 240, 247)), 1);
        dc.DrawLine(gridPen, new Point(0, h - 1), new Point(w, h - 1));
        var max = Math.Max(1, writes.Concat(reads).DefaultIfEmpty(1).Max() * 1.15);
        void Draw(List<double> samples, Color color)
        {
            if (samples.Count == 0) return;
            var points = samples.Count == 1 ? new[] { samples[0], samples[0] } : samples.ToArray();
            var geometry = new StreamGeometry();
            using (var c = geometry.Open())
            {
                c.BeginFigure(new Point(0, h - points[0] / max * h), false, false);
                for (var i = 1; i < points.Length; i++) c.LineTo(new Point(w * i / (points.Length - 1), h - points[i] / max * h), true, false);
            }
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(color), 2.5), geometry);
        }
        if (writes.Count == 0 && reads.Count == 0)
            dc.DrawLine(new Pen(new SolidColorBrush(Series == "Write" ? Color.FromRgb(37, 99, 235) : Color.FromRgb(12, 146, 124)), 1.5), new Point(0, h - 5), new Point(w, h - 5));
        Draw(writes, Color.FromRgb(37, 99, 235)); Draw(reads, Color.FromRgb(12, 146, 124));
    }
}
