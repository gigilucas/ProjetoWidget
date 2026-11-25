using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ProjetoWidget.Widgets;

/// <summary>Clock widget. Time comes from this PC (its clock and time zone).</summary>
public partial class ClockView : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };

    public ClockView()
    {
        InitializeComponent();
        DrawTicks();

        _timer.Tick += (_, _) => UpdateTime();
        Loaded += (_, _) =>
        {
            Settings.Changed += ApplyStyle;
            ApplyStyle();
            UpdateTime();
            _timer.Start();
        };
        Unloaded += (_, _) =>
        {
            Settings.Changed -= ApplyStyle;
            _timer.Stop();
        };
    }

    private void ApplyStyle()
    {
        var analog = Settings.Current.ClockStyle == ClockStyle.Analog;
        AnalogView.Visibility = analog ? Visibility.Visible : Visibility.Collapsed;
        DigitalView.Visibility = analog ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateTime()
    {
        var now = DateTime.Now; // local time of this PC

        HourHand.Angle = (now.Hour % 12) * 30 + now.Minute * 0.5;
        MinuteHand.Angle = now.Minute * 6 + now.Second * 0.1;
        SecondHand.Angle = now.Second * 6;

        // Digital follows the PC's 12/24-hour setting.
        var culture = CultureInfo.CurrentCulture;
        var uses12h = culture.DateTimeFormat.ShortTimePattern.Contains('t');
        TimeText.Text = now.ToString(uses12h ? "h:mm" : "HH:mm", culture);
        SecondsText.Text = uses12h ? now.ToString("tt", culture) : now.ToString("ss", culture);
        DayText.Text = now.ToString("dddd", CultureInfo.InvariantCulture);
        DateText.Text = now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    }

    private void DrawTicks()
    {
        for (var i = 0; i < 60; i++)
        {
            if (i % 15 == 0) continue; // numbers sit at 12, 3, 6 and 9
            var major = i % 5 == 0;
            var tick = new Line
            {
                X1 = 100, X2 = 100,
                Y1 = major ? 9 : 8, Y2 = major ? 19 : 13,
                Stroke = major ? (Brush)FindResource("InkBrush") : new SolidColorBrush(Color.FromArgb(0x59, 0x1C, 0x1B, 0x2E)),
                StrokeThickness = major ? 3 : 1.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                RenderTransform = new RotateTransform(i * 6, 100, 100),
            };
            Ticks.Children.Add(tick);
        }
    }
}
