using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ProjetoWidget.Services;

namespace ProjetoWidget.Widgets;

/// <summary>Weather widget for this PC's location, refreshed every 15 minutes.</summary>
public partial class WeatherView : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(15) };

    public WeatherView()
    {
        InitializeComponent();
        _timer.Tick += async (_, _) => await RefreshAsync(force: true);
        Loaded += async (_, _) =>
        {
            _timer.Start();
            await RefreshAsync(force: false);
        };
        Unloaded += (_, _) => _timer.Stop();
    }

    private async Task RefreshAsync(bool force)
    {
        try
        {
            Show(await WeatherService.GetAsync(force));
        }
        catch (LocationOffException)
        {
            ShowMessage("Location is off", "Turn on location in Windows to see the weather here.");
        }
        catch (Exception)
        {
            ShowMessage("No weather right now", "Check the internet connection. Trying again soon.");
        }
    }

    private void Show(WeatherReport r)
    {
        PlaceText.Text = r.Place;
        TempText.Text = $"{Math.Round(r.Temp):0}°";
        Icon.Kind = WeatherCodes.Icon(r.Code, r.IsDay);
        ConditionText.Text = WeatherCodes.Describe(r.Code);
        HighLowText.Text = $"H:{Math.Round(r.High):0}°  L:{Math.Round(r.Low):0}°";

        Days.Children.Clear();
        foreach (var d in r.Days)
        {
            var cell = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var label = new TextBlock { Text = d.Label, FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center };
            var low = new TextBlock
            {
                Text = $"{Math.Round(d.Low):0}°", FontSize = 10.5,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, -2, 0, 0),
            };
            // Soft text that follows light/dark mode
            label.SetResourceReference(TextBlock.ForegroundProperty, "InkSoftBrush");
            low.SetResourceReference(TextBlock.ForegroundProperty, "InkSoftBrush");

            cell.Children.Add(label);
            cell.Children.Add(new TextBlock
            {
                Text = $"{Math.Round(d.High):0}°", FontSize = 12.5, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            cell.Children.Add(low);
            Days.Children.Add(cell);
        }

        MessageView.Visibility = Visibility.Collapsed;
        ForecastView.Visibility = Visibility.Visible;
    }

    private void ShowMessage(string title, string text)
    {
        MessageTitle.Text = title;
        MessageText.Text = text;
        ForecastView.Visibility = Visibility.Collapsed;
        MessageView.Visibility = Visibility.Visible;
    }
}
