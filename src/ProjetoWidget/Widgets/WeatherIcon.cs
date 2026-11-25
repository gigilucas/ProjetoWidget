using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using ProjetoWidget.Services;

namespace ProjetoWidget.Widgets;

/// <summary>Soft 3D-looking weather icon (sun, moon, clouds, rain...) drawn in a 100x80 box.</summary>
public sealed class WeatherIcon : Viewbox
{
    private readonly Canvas _canvas = new() { Width = 100, Height = 80 };

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(WeatherIconKind), typeof(WeatherIcon),
        new PropertyMetadata(WeatherIconKind.PartlyCloudyDay, (d, _) => ((WeatherIcon)d).Draw()));

    public WeatherIcon()
    {
        Child = _canvas;
        Draw();
    }

    public WeatherIconKind Kind
    {
        get => (WeatherIconKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    private void Draw()
    {
        _canvas.Children.Clear();
        switch (Kind)
        {
            case WeatherIconKind.Sun:
                Add(Sun(22, 8, 58));
                break;
            case WeatherIconKind.Moon:
                Add(Moon(26, 8, 50));
                break;
            case WeatherIconKind.PartlyCloudyDay:
                Add(Sun(46, 2, 46));
                Add(Cloud(6, 30, 1.0));
                break;
            case WeatherIconKind.PartlyCloudyNight:
                Add(Moon(50, 2, 40));
                Add(Cloud(6, 30, 1.0));
                break;
            case WeatherIconKind.Cloud:
                Add(Cloud(30, 14, 0.75, dim: true));
                Add(Cloud(6, 30, 1.0));
                break;
            case WeatherIconKind.Rain:
                Add(Cloud(10, 14, 1.0));
                foreach (var x in new[] { 30.0, 48, 66 })
                    Add(RainDrop(x, 62));
                break;
            case WeatherIconKind.Snow:
                Add(Cloud(10, 14, 1.0));
                foreach (var (x, y) in new[] { (30.0, 64.0), (48.0, 70.0), (66.0, 64.0) })
                    Add(Flake(x, y));
                break;
            case WeatherIconKind.Storm:
                Add(Cloud(10, 10, 1.0, dim: true));
                Add(Bolt(42, 44));
                break;
            case WeatherIconKind.Fog:
                Add(Cloud(10, 8, 1.0, dim: true));
                foreach (var (y, w) in new[] { (58.0, 70.0), (68.0, 56.0) })
                    Add(new Rectangle
                    {
                        Width = w, Height = 5, RadiusX = 2.5, RadiusY = 2.5,
                        Fill = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                    }, 15 + (70 - w) / 2, y);
                break;
        }
    }

    private void Add(UIElement e, double left = 0, double top = 0)
    {
        if (left != 0) Canvas.SetLeft(e, left);
        if (top != 0) Canvas.SetTop(e, top);
        _canvas.Children.Add(e);
    }

    private static UIElement Sun(double x, double y, double size)
    {
        var sun = new Ellipse
        {
            Width = size, Height = size,
            Fill = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.35, 0.3), Center = new Point(0.4, 0.35), RadiusX = 0.7, RadiusY = 0.7,
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0xFF, 0xF3, 0xC4), 0),
                    new GradientStop(Color.FromRgb(0xFF, 0xB1, 0x3D), 0.55),
                    new GradientStop(Color.FromRgb(0xFF, 0x8A, 0x3D), 1),
                },
            },
            Effect = new DropShadowEffect { Color = Color.FromRgb(0xFF, 0xB1, 0x3D), BlurRadius = 22, ShadowDepth = 0, Opacity = 0.85 },
        };
        Canvas.SetLeft(sun, x);
        Canvas.SetTop(sun, y);
        return sun;
    }

    private static UIElement Moon(double x, double y, double size)
    {
        var moon = new Path
        {
            Data = new CombinedGeometry(GeometryCombineMode.Exclude,
                new EllipseGeometry(new Rect(0, 0, size, size)),
                new EllipseGeometry(new Rect(size * 0.32, -size * 0.12, size * 0.9, size * 0.9))),
            Fill = new LinearGradientBrush(Color.FromRgb(0xFF, 0xF6, 0xD6), Color.FromRgb(0xC9, 0xB8, 0xFF), new Point(0, 0), new Point(1, 1)),
            Effect = new DropShadowEffect { Color = Color.FromRgb(0x8B, 0x5C, 0xFF), BlurRadius = 18, ShadowDepth = 0, Opacity = 0.55 },
        };
        Canvas.SetLeft(moon, x);
        Canvas.SetTop(moon, y);
        return moon;
    }

    /// <summary>Cloud made of a rounded base and two puffs, drawn as one shape.</summary>
    private static UIElement Cloud(double x, double y, double scale, bool dim = false)
    {
        var shape = new GeometryGroup { FillRule = FillRule.Nonzero };
        shape.Children.Add(new RectangleGeometry(new Rect(0, 22, 80, 34), 17, 17));
        shape.Children.Add(new EllipseGeometry(new Rect(12, 4, 40, 40)));
        shape.Children.Add(new EllipseGeometry(new Rect(38, 14, 30, 30)));
        shape.Transform = new ScaleTransform(scale, scale);

        var top = dim ? Color.FromRgb(0xEE, 0xEF, 0xF8) : Colors.White;
        var bottom = dim ? Color.FromRgb(0xC4, 0xC8, 0xE0) : Color.FromRgb(0xDD, 0xE0, 0xF2);
        var cloud = new Path
        {
            Data = shape,
            Fill = new LinearGradientBrush(top, bottom, 90),
            Effect = new DropShadowEffect { Color = Color.FromRgb(0x3C, 0x28, 0x82), BlurRadius = 14, ShadowDepth = 4, Direction = 270, Opacity = 0.22 },
        };
        Canvas.SetLeft(cloud, x);
        Canvas.SetTop(cloud, y);
        return cloud;
    }

    private static UIElement RainDrop(double x, double y)
    {
        var drop = new Path
        {
            Data = Geometry.Parse("M4,0 C4,0 8,6 8,9 A4,4 0 0 1 0,9 C0,6 4,0 4,0 Z"),
            Fill = new LinearGradientBrush(Color.FromRgb(0x3F, 0xB8, 0xFF), Color.FromRgb(0x4F, 0x7C, 0xFF), 90),
        };
        Canvas.SetLeft(drop, x);
        Canvas.SetTop(drop, y);
        return drop;
    }

    private static UIElement Flake(double x, double y)
    {
        var flake = new Ellipse
        {
            Width = 8, Height = 8, Fill = Brushes.White,
            Effect = new DropShadowEffect { Color = Color.FromRgb(0x4F, 0x7C, 0xFF), BlurRadius = 6, ShadowDepth = 0, Opacity = 0.6 },
        };
        Canvas.SetLeft(flake, x);
        Canvas.SetTop(flake, y);
        return flake;
    }

    private static UIElement Bolt(double x, double y)
    {
        var bolt = new Path
        {
            Data = Geometry.Parse("M10,0 L0,16 L8,16 L4,30 L18,11 L10,11 L14,0 Z"),
            Fill = new LinearGradientBrush(Color.FromRgb(0xFF, 0xD9, 0x5C), Color.FromRgb(0xFF, 0xB1, 0x3D), 90),
        };
        Canvas.SetLeft(bolt, x);
        Canvas.SetTop(bolt, y);
        return bolt;
    }
}
