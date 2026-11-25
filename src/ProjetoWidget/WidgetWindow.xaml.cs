using System.Windows;
using System.Windows.Input;

namespace ProjetoWidget;

/// <summary>A single widget on the desktop. Empty glass card for now.</summary>
public partial class WidgetWindow : Window
{
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

    /// <summary>Raised after the user finishes dragging the widget to a new spot.</summary>
    public event EventHandler? Moved;

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
            return;

        DragMove(); // returns when the mouse button is released
        Moved?.Invoke(this, EventArgs.Empty);
    }
}
