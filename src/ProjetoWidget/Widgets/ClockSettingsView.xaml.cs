using System.Windows;
using System.Windows.Controls;

namespace ProjetoWidget.Widgets;

public partial class ClockSettingsView : UserControl
{
    private readonly bool _ready;

    public ClockSettingsView()
    {
        InitializeComponent();

        ZoneText.Text = TimeZoneInfo.Local.DisplayName;
        (Settings.Current.ClockStyle == ClockStyle.Analog ? AnalogOption : DigitalOption).IsChecked = true;
        UpdateHint();
        _ready = true;
    }

    private void Style_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        Settings.Current.ClockStyle = DigitalOption.IsChecked == true ? ClockStyle.Digital : ClockStyle.Analog;
        Settings.Current.Save();
        UpdateHint();
    }

    private void UpdateHint() => StyleHint.Text = Settings.Current.ClockStyle == ClockStyle.Analog
        ? "Clock face with hour, minute and second hands."
        : "Time in numbers, with seconds and the date.";
}
