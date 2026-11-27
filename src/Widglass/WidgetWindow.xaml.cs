using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Widglass;

/// <summary>A single widget on the desktop: a glass card that can be dragged around.</summary>
public partial class WidgetWindow : Window
{
    /// <summary>Space around the card inside the window, used by the drop shadow.</summary>
    public const double ShadowMargin = 16;

    /// <summary>Window width of a square widget (220 px card) and of a wide one (two squares and the gap between them).</summary>
    public const double SquareWidth = 252;
    public const double WideWidth = 488;

    private Point? _grab; // where on the window the mouse grabbed it, while dragging

    /// <param name="instance">Which copy of the widget, from 0 (only Photos has more than one: up to 5 frames).</param>
    public WidgetWindow(WidgetOption widget, int instance = 0)
    {
        InitializeComponent();
        Widget = widget;
        Instance = instance;
        Title = instance == 0 ? widget.Name : $"{widget.Name} {instance + 1}";
        Host.Content = widget.Id switch
        {
            "clock" => new Widgets.ClockView(),
            "calendar" => new Widgets.CalendarView(),
            "cpu-ram" => new Widgets.CpuRamView(),
            "weather" => new Widgets.WeatherView(),
            "music" => new Widgets.MusicView(),
            "stocks" => new Widgets.StocksView(),
            "currency" => new Widgets.CurrencyView(),
            "photos" => new Widgets.PhotosView(instance),
            _ => null,
        };
        // Music has buttons; clicks elsewhere on it still drag the widget.
        Host.IsHitTestVisible = Host.Content is Widgets.MusicView;

        // Some widgets can be a wide rectangle instead of a square
        if (IsWide(widget.Id, instance))
            Width = WideWidth;

        // Photos fill the whole card, just inside the glass rim
        if (widget.Id == "photos")
            Host.Margin = new Thickness(1.5);

        // Stuck to the desktop: behind all windows, still there on Win+D.
        DesktopPin.Attach(this);
    }

    public WidgetOption Widget { get; }

    /// <summary>Which copy of the widget this is, from 0.</summary>
    public int Instance { get; }

    /// <summary>Name under which this window's spot is saved: "photos", "photos-2", ...</summary>
    public string Key => KeyFor(Widget.Id, Instance);

    public static string KeyFor(string id, int instance) => instance == 0 ? id : $"{id}-{instance + 1}";

    /// <summary>Stocks with 2-3 stocks, and photo frames set to wide.</summary>
    private static bool IsWide(string id, int instance) => id switch
    {
        "stocks" => Settings.Current.ActiveStocks.Count > 1,
        "photos" => Settings.Current.Frame(instance).Wide,
        _ => false,
    };

    /// <summary>Cards of the other widgets on screen, which this one may touch but not cover.</summary>
    public Func<IEnumerable<Rect>> Obstacles { get; set; } = () => [];

    /// <summary>Raised after the user finishes dragging the widget to a new spot.</summary>
    public event EventHandler? Moved;

    /// <summary>
    /// Switches between square and wide. The widget grows from where it is, then slides if needed
    /// so it stays whole on the screen and doesn't cover another widget.
    /// </summary>
    public void SetWide(bool wide)
    {
        var width = wide ? WideWidth : SquareWidth;
        if (Math.Abs(Width - width) < 0.5) return;
        Width = width;
        (Left, Top) = Place(Left, Top);
        Moved?.Invoke(this, EventArgs.Empty);
    }

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
    /// Where the window can go when asked for (left, top):
    /// the card stays whole inside one screen (the one under its center, above the taskbar),
    /// and if it would cover another widget it slides out the shortest way so the edges just touch.
    /// If that isn't possible, it stays put.
    /// </summary>
    private (double Left, double Top) Place(double left, double top)
    {
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var card = CardAt(left, top, Width, Height);
        var screen = ScreenAreaAt(Center(card), scale);
        return Resolve(ClampInto(card, screen), Obstacles().ToList(), screen) is { } free
            ? (free.Left - ShadowMargin, free.Top - ShadowMargin)
            : (Left, Top);
    }

    public static Point Center(Rect r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);

    /// <summary>Moves the card the least so it fits entirely inside <paramref name="area"/>.</summary>
    public static Rect ClampInto(Rect card, Rect area)
    {
        card.X = Math.Clamp(card.X, area.Left, Math.Max(area.Left, area.Right - card.Width));
        card.Y = Math.Clamp(card.Y, area.Top, Math.Max(area.Top, area.Bottom - card.Height));
        return card;
    }

    /// <summary>Work area (screen minus taskbar) of the monitor at or nearest to a point, in WPF units.</summary>
    public static Rect ScreenAreaAt(Point point, double scale)
    {
        var monitor = MonitorFromPoint(new POINT { X = (int)(point.X * scale), Y = (int)(point.Y * scale) }, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
            return SystemParameters.WorkArea;
        var w = info.rcWork;
        return new Rect(w.Left / scale, w.Top / scale, (w.Right - w.Left) / scale, (w.Bottom - w.Top) / scale);
    }

    /// <summary>
    /// Pushes <paramref name="card"/> out of any obstacle it overlaps, never leaving <paramref name="bounds"/>.
    /// Null if it can't be freed.
    /// </summary>
    public static Rect? Resolve(Rect card, IReadOnlyList<Rect> obstacles, Rect? bounds = null)
    {
        for (var pass = 0; pass < 8; pass++)
        {
            var moved = false;
            foreach (var o in obstacles)
            {
                if (!Overlaps(card, o)) continue;

                // Four ways out; take the shortest one that stays on the screen.
                var pushes = new[]
                {
                    new Vector(o.Left - card.Right, 0),
                    new Vector(o.Right - card.Left, 0),
                    new Vector(0, o.Top - card.Bottom),
                    new Vector(0, o.Bottom - card.Top),
                };
                var options = pushes
                    .Select(p => { var c = card; c.Offset(p); return (Push: p, Card: c); })
                    .Where(x => bounds is not { } b || Inside(x.Card, b))
                    .ToList();
                if (options.Count == 0) return null;
                card = options.MinBy(x => x.Push.Length).Card;
                moved = true;
            }
            if (!moved) return card;
        }
        return obstacles.Any(o => Overlaps(card, o)) ? null : card;
    }

    private static bool Inside(Rect card, Rect area)
    {
        const double tolerance = 0.5;
        return card.Left >= area.Left - tolerance && card.Right <= area.Right + tolerance &&
               card.Top >= area.Top - tolerance && card.Bottom <= area.Bottom + tolerance;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    /// <summary>True if the two cards cover each other. Touching edges is fine.</summary>
    public static bool Overlaps(Rect a, Rect b)
    {
        const double tolerance = 0.5;
        return a.Left < b.Right - tolerance && a.Right > b.Left + tolerance &&
               a.Top < b.Bottom - tolerance && a.Bottom > b.Top + tolerance;
    }
}
