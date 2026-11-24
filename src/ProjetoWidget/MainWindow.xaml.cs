using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjetoWidget;

public partial class MainWindow : Window
{
    private const double CornerRadius = 34;

    private readonly Settings _settings = Settings.Load();
    private readonly List<WidgetOption> _widgets;

    public MainWindow()
    {
        InitializeComponent();

        _widgets =
        [
            new("clock",    "Clock",     "", "#3FB8FF", "#4F7CFF"),
            new("cpu-ram",  "CPU / RAM", "", "#8B5CFF", "#4F7CFF"),
            new("calendar", "Calendar",  "", "#FF5FA8", "#FFB13D"),
            new("weather",  "Weather",   "", "#FFB13D", "#FF8A3D"),
            new("music",    "Music",     "", "#4F7CFF", "#FF5FA8"),
        ];

        foreach (var widget in _widgets)
        {
            widget.IsOn = _settings.IsOn(widget.Id);
            widget.PropertyChanged += Widget_PropertyChanged;
        }

        WidgetList.ItemsSource = _widgets;
        UpdateSummary();

        // Dev aid: "ProjetoWidget.exe --screenshot out.png" saves an image of the window and exits.
        var args = Environment.GetCommandLineArgs();
        var i = Array.IndexOf(args, "--screenshot");
        if (i >= 0 && i + 1 < args.Length)
            ContentRendered += async (_, _) =>
            {
                await Task.Delay(400); // let the switch animations finish
                SaveScreenshot(args[i + 1]);
                Close();
            };
    }

    private void SaveScreenshot(string path)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)(ActualWidth * dpi.DpiScaleX), (int)(ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var file = System.IO.File.Create(path);
        encoder.Save(file);
    }

    private void Widget_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not WidgetOption widget || e.PropertyName != nameof(WidgetOption.IsOn))
            return;

        _settings.SetOn(widget.Id, widget.IsOn);
        _settings.Save();
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var on = _widgets.Count(w => w.IsOn);
        Summary.Text = $"{on} of {_widgets.Count} widgets on";
    }

    // Rounded corners for the whole window content
    private void Shell_SizeChanged(object sender, SizeChangedEventArgs e) =>
        Shell.Clip = new RectangleGeometry(new Rect(e.NewSize), CornerRadius, CornerRadius);

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
}
