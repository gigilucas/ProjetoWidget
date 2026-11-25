using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ProjetoWidget.Widgets;

/// <summary>
/// Calendar widget. The date comes from this PC, shown as dd/mm/yyyy;
/// the week starts on the PC's first day of the week.
/// </summary>
public partial class CalendarView : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private DateTime _shownDay;

    public CalendarView()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => { if (DateTime.Today != _shownDay) Render(); }; // roll over at midnight
        Loaded += (_, _) => { Render(); _timer.Start(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    private void Render()
    {
        var today = DateTime.Today;
        _shownDay = today;

        DayText.Text = today.ToString("dddd", CultureInfo.InvariantCulture).ToUpperInvariant();
        DateText.Text = today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        MonthGrid.Children.Clear();
        var firstDayOfWeek = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        // Brush keys, so the text follows light/dark mode
        const string soft = "InkSoftBrush";
        const string ink = "InkBrush";

        // Weekday letters
        for (var i = 0; i < 7; i++)
        {
            var day = (DayOfWeek)(((int)firstDayOfWeek + i) % 7);
            MonthGrid.Children.Add(Cell(CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedDayName(day)[..1], soft, bold: true));
        }

        // Blank cells before the 1st
        var first = new DateTime(today.Year, today.Month, 1);
        var offset = ((int)first.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
        for (var i = 0; i < offset; i++)
            MonthGrid.Children.Add(new Border());

        // Days
        var days = DateTime.DaysInMonth(today.Year, today.Month);
        for (var d = 1; d <= days; d++)
        {
            var date = new DateTime(today.Year, today.Month, d);
            var weekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            MonthGrid.Children.Add(d == today.Day
                ? TodayCell(d)
                : Cell(d.ToString(CultureInfo.InvariantCulture), weekend ? soft : ink, bold: false));
        }

        // Keep 6 rows so the widget never changes height
        while (MonthGrid.Children.Count < 7 * 7)
            MonthGrid.Children.Add(new Border());
    }

    /// <param name="brushKey">Theme brush for the text, or null to keep it white.</param>
    private static TextBlock Cell(string text, string? brushKey, bool bold)
    {
        var cell = new TextBlock
        {
            Text = text,
            Foreground = Brushes.White,
            FontSize = 12,
            FontWeight = bold ? FontWeights.Bold : FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (brushKey is not null)
            cell.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        return cell;
    }

    private Grid TodayCell(int day)
    {
        var cell = new Grid();
        cell.Children.Add(new Ellipse
        {
            Width = 21, Height = 21,
            Fill = (Brush)FindResource("LogoGradient"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        cell.Children.Add(Cell(day.ToString(CultureInfo.InvariantCulture), null, bold: true));
        return cell;
    }
}
