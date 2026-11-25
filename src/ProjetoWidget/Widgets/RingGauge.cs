using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ProjetoWidget.Widgets;

/// <summary>Round progress ring with a gradient arc and the value in the middle.</summary>
public sealed class RingGauge : Grid
{
    private const double Thickness = 7;

    private readonly Path _arc;
    private readonly TextBlock _label;

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(RingGauge),
        new PropertyMetadata(0d, (d, _) => ((RingGauge)d).Redraw()));

    public RingGauge(Color from, Color to)
    {
        var track = new Ellipse
        {
            Stroke = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
            StrokeThickness = Thickness,
        };
        _arc = new Path
        {
            Stroke = new LinearGradientBrush(from, to, new Point(0, 0), new Point(1, 1)),
            StrokeThickness = Thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
        _label = new TextBlock
        {
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Children.Add(track);
        Children.Add(_arc);
        Children.Add(_label);
        SizeChanged += (_, _) => Redraw();
    }

    /// <summary>0-100.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private void Redraw()
    {
        var v = Math.Clamp(Value, 0, 100);
        _label.Text = $"{v:0}%";

        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;

        var r = (size - Thickness) / 2;
        var c = new Point(size / 2, size / 2);
        if (v <= 0.5)
        {
            _arc.Data = null;
            return;
        }
        // A full circle can't be one arc; stop just short of 360°.
        var angle = Math.Min(v, 99.9) / 100 * 360;
        var rad = (angle - 90) * Math.PI / 180;
        var start = new Point(c.X, c.Y - r);
        var end = new Point(c.X + r * Math.Cos(rad), c.Y + r * Math.Sin(rad));
        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment(end, new Size(r, r), 0, angle > 180, SweepDirection.Clockwise, true));
        _arc.Data = new PathGeometry([figure]);
    }
}
