using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjetoWidget;

public partial class MainWindow : Window
{
    private const double CornerRadius = 34;

    private const double WidgetGap = 8;

    private readonly Settings _settings = Settings.Current;
    private readonly List<WidgetOption> _widgets;
    private readonly Dictionary<string, WidgetWindow> _openWidgets = [];
    private readonly bool _screenshotMode;
    private bool _shuttingDown;

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
        DarkModeToggle.IsChecked = _settings.DarkMode;
        UpdateSummary();

        // Dev aid: "ProjetoWidget.exe --screenshot out.png" saves an image of the window and exits.
        var args = Environment.GetCommandLineArgs();
        var i = Array.IndexOf(args, "--screenshot");
        _screenshotMode = i >= 0 && i + 1 < args.Length;

        // Dev aid: "--location 38.72,-9.14" uses these coordinates for the weather instead of the PC's location.
        var l = Array.IndexOf(args, "--location");
        if (l >= 0 && l + 1 < args.Length && args[l + 1].Split(',') is [var lat, var lon]
            && double.TryParse(lat, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var la)
            && double.TryParse(lon, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lo))
            Services.WeatherService.OverrideLocation(la, lo);

        // Dev aid: "--music-test powershell" lets the music widget follow a test media session.
        var m = Array.IndexOf(args, "--music-test");
        if (m >= 0 && m + 1 < args.Length)
            Services.MusicService.AddTestApp(args[m + 1]);

        // Dev aid: "--page calendar" (with --screenshot) captures that widget's settings view.
        var p = Array.IndexOf(args, "--page");
        if (_screenshotMode && p >= 0 && p + 1 < args.Length && _widgets.FirstOrDefault(w => w.Id == args[p + 1]) is { } page)
        {
            ShowSettingsFor(page);
            DetailPage.Visibility = Visibility.Visible;
            ListPage.Visibility = Visibility.Collapsed;
        }

        // Dev aid: "--widget clock" (with --screenshot) captures that widget instead of this window.
        var w = Array.IndexOf(args, "--widget");
        var widgetToCapture = _screenshotMode && w >= 0 && w + 1 < args.Length
            ? _widgets.FirstOrDefault(x => x.Id == args[w + 1])
            : null;

        if (_screenshotMode)
            ContentRendered += async (_, _) =>
            {
                Visual target = this;
                WidgetWindow? widgetWindow = null;
                if (widgetToCapture is not null)
                {
                    widgetWindow = new WidgetWindow(widgetToCapture) { Left = -4000, Top = -4000 };
                    widgetWindow.Show();
                    target = widgetWindow;
                }
                // let animations finish; widgets that load data (weather, CPU) get a few seconds
                await Task.Delay(widgetToCapture is null ? 600 : 10000);
                SaveScreenshot(target, args[i + 1]);
                widgetWindow?.Close();
                Close();
            };
        else
            Loaded += (_, _) =>
            {
                foreach (var widget in _widgets.Where(w => w.IsOn))
                    ShowWidget(widget);
            };

        // Closing the manager closes the whole app, widgets included.
        Closed += (_, _) =>
        {
            _shuttingDown = true; // keep the switches as they are, so the widgets come back next time
            Application.Current.Shutdown();
        };
    }

    private void ShowWidget(WidgetOption widget)
    {
        if (_openWidgets.ContainsKey(widget.Id))
            return;

        var window = new WidgetWindow(widget) { WindowStartupLocation = WindowStartupLocation.Manual };
        var (left, top) = _settings.TryGetPosition(widget.Id, out var savedLeft, out var savedTop)
            ? (savedLeft, savedTop)
            : DefaultPosition(_widgets.IndexOf(widget), window.Width, window.Height);

        // Widgets may touch but never cover each other.
        var others = () => _openWidgets.Values.Where(w => w != window).Select(w => w.Card);
        (window.Left, window.Top) = FreeSpot(left, top, window.Width, window.Height, others().ToList());
        window.Obstacles = others;

        window.Moved += (_, _) =>
        {
            _settings.SetPosition(widget.Id, window.Left, window.Top);
            _settings.Save();
        };
        // If the widget gets closed some other way (e.g. Alt+F4), switch it off too.
        window.Closed += (_, _) =>
        {
            _openWidgets.Remove(widget.Id);
            if (!_shuttingDown)
                widget.IsOn = false;
        };

        _openWidgets[widget.Id] = window;
        window.Show();
    }

    private void HideWidget(WidgetOption widget)
    {
        if (_openWidgets.Remove(widget.Id, out var window))
            window.Close();
    }

    /// <summary>
    /// The spot closest to (left, top) where a widget doesn't cover any other one:
    /// first by sliding it out, otherwise the first free place in a grid over the screen.
    /// </summary>
    private static (double Left, double Top) FreeSpot(double left, double top, double width, double height, IReadOnlyList<Rect> others)
    {
        var card = WidgetWindow.CardAt(left, top, width, height);
        if (WidgetWindow.Resolve(card, others) is { } free)
            return (free.Left - WidgetWindow.ShadowMargin, free.Top - WidgetWindow.ShadowMargin);

        var area = SystemParameters.WorkArea;
        for (var y = area.Top; y + height <= area.Bottom; y += card.Height)
            for (var x = area.Right - width; x >= area.Left; x -= card.Width)
                if (!others.Any(o => WidgetWindow.Overlaps(WidgetWindow.CardAt(x, y, width, height), o)))
                    return (x, y);
        return (left, top);
    }

    /// <summary>First launch: line the widgets up in a grid at the top-right of the screen.</summary>
    private static (double Left, double Top) DefaultPosition(int index, double width, double height)
    {
        var area = SystemParameters.WorkArea;
        int col = index / 3, row = index % 3;
        return (area.Right - (col + 1) * (width - WidgetGap), area.Top + row * (height - WidgetGap));
    }

    private static void SaveScreenshot(Visual target, string path)
    {
        var element = (FrameworkElement)target;
        var dpi = VisualTreeHelper.GetDpi(target);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)(element.ActualWidth * dpi.DpiScaleX), (int)(element.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(target);
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

        if (_screenshotMode)
            return;
        if (widget.IsOn)
            ShowWidget(widget);
        else
            HideWidget(widget);
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

    // ---------- Navigation between the list and a widget's settings ----------

    private void WidgetRow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Clicks on the on/off switch only toggle; anywhere else on the row opens the settings.
        if (IsInsideCheckBox(e.OriginalSource as DependencyObject))
            return;
        if (sender is FrameworkElement { DataContext: WidgetOption widget })
            OpenDetail(widget);
    }

    private static bool IsInsideCheckBox(DependencyObject? element)
    {
        for (; element is not null; element = VisualTreeHelper.GetParent(element))
            if (element is System.Windows.Controls.CheckBox)
                return true;
        return false;
    }

    private void OpenDetail(WidgetOption widget)
    {
        ShowSettingsFor(widget);
        SlidePages(from: ListPage, to: DetailPage, forward: true);
    }

    private void ShowSettingsFor(WidgetOption widget)
    {
        DetailPage.DataContext = widget;
        SettingsHost.Content = widget.Id switch
        {
            "clock" => new Widgets.ClockSettingsView(),
            "calendar" => new Widgets.CalendarSettingsView(),
            "cpu-ram" => new Widgets.CpuRamSettingsView(),
            "weather" => new Widgets.WeatherSettingsView(),
            "music" => new Widgets.MusicSettingsView(),
            _ => NothingToSet(),
        };
    }

    private static System.Windows.Controls.TextBlock NothingToSet()
    {
        var text = new System.Windows.Controls.TextBlock { Text = "Nothing to set yet.", FontSize = 15 };
        text.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "MenuInkSoft");
        return text;
    }

    private void DarkMode_Changed(object sender, RoutedEventArgs e)
    {
        var dark = DarkModeToggle.IsChecked == true;
        if (_settings.DarkMode == dark) return;
        _settings.DarkMode = dark;
        _settings.Save();
        App.ApplyTheme(dark);
    }

    private void Back_Click(object sender, RoutedEventArgs e) =>
        SlidePages(from: DetailPage, to: ListPage, forward: false);

    /// <summary>iOS-style push/pop: the new page slides in while the old one slides out and fades.</summary>
    private void SlidePages(FrameworkElement from, FrameworkElement to, bool forward)
    {
        var distance = Shell.ActualWidth;
        var duration = TimeSpan.FromMilliseconds(320);
        var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };

        to.Visibility = Visibility.Visible;
        Animate(to, forward ? distance : -distance * 0.3, 0, 0, 1);
        Animate(from, 0, forward ? -distance * 0.3 : distance, 1, 0, () => from.Visibility = Visibility.Collapsed);

        void Animate(FrameworkElement page, double x0, double x1, double o0, double o1, Action? done = null)
        {
            var slide = new System.Windows.Media.Animation.DoubleAnimation(x0, x1, duration) { EasingFunction = ease };
            var fade = new System.Windows.Media.Animation.DoubleAnimation(o0, o1, duration) { EasingFunction = ease };
            if (done is not null)
                slide.Completed += (_, _) => done();
            ((TranslateTransform)page.RenderTransform).BeginAnimation(TranslateTransform.XProperty, slide);
            page.BeginAnimation(OpacityProperty, fade);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
}
