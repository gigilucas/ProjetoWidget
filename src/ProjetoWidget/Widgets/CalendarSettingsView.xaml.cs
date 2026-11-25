using System.Globalization;
using System.Windows.Controls;

namespace ProjetoWidget.Widgets;

public partial class CalendarSettingsView : UserControl
{
    public CalendarSettingsView()
    {
        InitializeComponent();

        TodayText.Text = "Today: " + DateTime.Today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        WeekStartText.Text = CultureInfo.InvariantCulture.DateTimeFormat
            .GetDayName(CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);
    }
}
