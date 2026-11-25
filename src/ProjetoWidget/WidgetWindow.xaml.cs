using System.Windows;
using System.Windows.Input;

namespace ProjetoWidget;

/// <summary>A single widget on the desktop: a glass card that can be dragged around.</summary>
public partial class WidgetWindow : Window
{
    /// <summary>Space around the card inside the window, used by the drop shadow.</summary>
    public const double ShadowMargin = 16;

    private Point? _grab; // where on the window the mouse grabbed it, while dragging

    public WidgetWindow(WidgetOption widget)
    {
        InitializeComponent();
        Widget = widget;
        Title = widget.Name;
        Host.Content = widget.Id switch
        {
            "clock" => new Widgets.ClockView(),
            "calendar" => new Widgets.CalendarView(),
            "cpu-ram" => new Widgets.CpuRamView(),
            "weather" => new Widgets.WeatherView(),
            "music" => new Widgets.MusicView(),
            _ => null,
        };
        // Music has buttons; clicks elsewhere on it still drag the widget.
        Host.IsHitTestVisible = Host.Content is Widgets.MusicView;
    }

    public WidgetOption Widget { get; }

    /// <summary>Cards of the other widgets on screen, which this one may touch but not cover.</summary>
    public Func<IEnumerable<Rect>> Obstacles { get; set; } = () => [];

    /// <summary>Raised after the user finishes dragging the widget to a new spot.</summary>
    public event EventHandler? Moved;

    /// <summary>The visible glass card, in screen coordinates (without the shadow margin).</summary>
    public Rect Card => CardAt(Left, Top, Width, Height);

    public static Rect CardAt(double left, double top, double width, double height) =>
        new(left + ShadowMargin, top + ShadowMargin, width - 2 * ShadowMargin, height - 2 * ShadowMargin);

    // ---------- Dragging that never lets cards overlap ----------

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _grab = e.GetPosition(this);
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void Card_MouseMove(object sender, MouseEventArgs e)
    {
        if (_grab is not { } grab) return;
        var p = e.GetPosition(this);
        var (left, top) = Place(Left + p.X - grab.X, Top + p.Y - grab.Y);
        Left = left;
        Top = top;
    }

    private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_grab is null) return;
        _grab = null;
        ((UIElement)sender).ReleaseMouseCapture();
        Moved?.Invoke(this, EventArgs.Empty);
    }

    private void Card_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_grab is null) return;
        _grab = null;
        Moved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Where the window can go when asked for (left, top): if the card would cover another one,
    /// it slides out the shortest way so the edges just touch. If that isn't possible, it stays put.
    /// </summary>
    private (double Left, double Top) Place(double left, double top)
    {
        var card = CardAt(left, top, Width, Height);
        return Resolve(card, Obstacles().ToList()) is { } free
            ? (free.Left - ShadowMargin, free.Top - ShadowMargin)
            : (Left, Top);
    }

    /// <summary>Pushes <paramref name="card"/> out of any obstacle it overlaps. Null if it can't be freed.</summary>
    public static Rect? Resolve(Rect card, IReadOnlyList<Rect> obstacles)
    {
        for (var pass = 0; pass < 8; pass++)
        {
            var moved = false;
            foreach (var o in obstacles)
            {
                if (!Overlaps(card, o)) continue;

                // Four ways out; take the shortest.
                var pushes = new[]
                {
                    new Vector(o.Left - card.Right, 0),
                    new Vector(o.Right - card.Left, 0),
                    new Vector(0, o.Top - card.Bottom),
                    new Vector(0, o.Bottom - card.Top),
                };
                var push = pushes.MinBy(v => v.Length);
                card.Offset(push);
                moved = true;
            }
            if (!moved) return card;
        }
        return obstacles.Any(o => Overlaps(card, o)) ? null : card;
    }

    /// <summary>True if the two cards cover each other. Touching edges is fine.</summary>
    public static bool Overlaps(Rect a, Rect b)
    {
        const double tolerance = 0.5;
        return a.Left < b.Right - tolerance && a.Right > b.Left + tolerance &&
               a.Top < b.Bottom - tolerance && a.Bottom > b.Top + tolerance;
    }
}
