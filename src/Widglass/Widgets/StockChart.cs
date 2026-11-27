using System.Windows;
using System.Windows.Media;
using Widglass.Services;

namespace Widglass.Widgets;

/// <summary>
/// Price line with a soft area under it, green when the price is up, red when down,
/// and a dashed line at the reference price (yesterday's close for "Today").
/// </summary>
public sealed class StockChart : FrameworkElement
{
    private static readonly Color Up = Color.FromRgb(0x2E, 0xCC, 0x71);
    private static readonly Color Down = Color.FromRgb(0xFF, 0x4D, 0x6D);

    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points), typeof(IReadOnlyList<PricePoint>), typeof(StockChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BaselineProperty = DependencyProperty.Register(
        nameof(Baseline), typeof(double), typeof(StockChart),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Color of the dashed reference line; set from the theme so it follows light/dark mode.</summary>
    public static readonly DependencyProperty GuideBrushProperty = DependencyProperty.Register(
        nameof(GuideBrush), typeof(Brush), typeof(StockChart),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<PricePoint>? Points
    {
        get => (IReadOnlyList<PricePoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public double Baseline
    {
        get => (double)GetValue(BaselineProperty);
        set => SetValue(BaselineProperty, value);
    }

    public Brush GuideBrush
    {
        get => (Brush)GetValue(GuideBrushProperty);
        set => SetValue(GuideBrushProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var points = Points;
        var w = ActualWidth;
        var h = ActualHeight;
        if (points is null || points.Count < 2 || w <= 0 || h <= 0)
            return;

        var min = points.Min(p => p.Price);
        var max = points.Max(p => p.Price);
        if (!double.IsNaN(Baseline))
        {
            min = Math.Min(min, Baseline);
            max = Math.Max(max, Baseline);
        }
        if (max - min < 1e-9) { max += 1; min -= 1; }
        var pad = (max - min) * 0.08;
        min -= pad;
        max += pad;

        double X(int i) => i * w / (points.Count - 1);
        double Y(double price) => h - (price - min) / (max - min) * h;

        var color = points[^1].Price >= (double.IsNaN(Baseline) ? points[0].Price : Baseline) ? Up : Down;

        // Line
        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(new Point(X(0), Y(points[0].Price)), false, false);
            for (var i = 1; i < points.Count; i++)
                ctx.LineTo(new Point(X(i), Y(points[i].Price)), true, true);
        }
        line.Freeze();

        // Area under the line
        var area = new StreamGeometry();
        using (var ctx = area.Open())
        {
            ctx.BeginFigure(new Point(X(0), h), true, true);
            for (var i = 0; i < points.Count; i++)
                ctx.LineTo(new Point(X(i), Y(points[i].Price)), true, true);
            ctx.LineTo(new Point(X(points.Count - 1), h), true, true);
        }
        area.Freeze();

        var fill = new LinearGradientBrush(Color.FromArgb(0x66, color.R, color.G, color.B),
            Color.FromArgb(0x00, color.R, color.G, color.B), 90);
        fill.Freeze();
        dc.DrawGeometry(fill, null, area);

        // Reference price (dashed)
        if (!double.IsNaN(Baseline))
        {
            var guide = new Pen(GuideBrush, 1) { DashStyle = new DashStyle([3, 3], 0) };
            guide.Freeze();
            var y = Math.Round(Y(Baseline)) + 0.5;
            dc.DrawLine(guide, new Point(0, y), new Point(w, y));
        }

        var pen = new Pen(new SolidColorBrush(color), 2) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        dc.DrawGeometry(null, pen, line);

        // Dot on the latest price
        var last = new Point(X(points.Count - 1), Y(points[^1].Price));
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0x55, color.R, color.G, color.B)), null, last, 5, 5);
        dc.DrawEllipse(new SolidColorBrush(color), null, last, 2.6, 2.6);
    }
}
