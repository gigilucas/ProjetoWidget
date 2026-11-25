using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using ProjetoWidget.Services;

namespace ProjetoWidget.Widgets;

public partial class WeatherSettingsView : UserControl
{
    public WeatherSettingsView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await CheckLocationAsync();
    }

    private async Task CheckLocationAsync()
    {
        try
        {
            var report = await WeatherService.GetAsync();
            LocationStatus.Text = $"Now: {report.Place}";
            OpenLocationSettings.Visibility = Visibility.Collapsed;
        }
        catch (LocationOffException)
        {
            LocationStatus.Text = "Location is off in Windows. Turn it on so the weather can follow this PC.";
            OpenLocationSettings.Visibility = Visibility.Visible;
        }
        catch (Exception)
        {
            LocationStatus.Text = "Couldn't get the weather right now.";
            OpenLocationSettings.Visibility = Visibility.Collapsed;
        }
    }

    private void OpenLocationSettings_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("ms-settings:privacy-location") { UseShellExecute = true });
}
