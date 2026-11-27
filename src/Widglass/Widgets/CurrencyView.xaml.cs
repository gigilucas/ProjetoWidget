using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Widglass.Services;

namespace Widglass.Widgets;

/// <summary>
/// Currency converter: an amount in one currency converted to another at the live rate,
/// with today's chart of the rate. Refreshes every few seconds and flashes when the rate moves.
/// </summary>
public partial class CurrencyView : UserControl
{
    private static readonly Color UpColor = Color.FromRgb(0x2E, 0xCC, 0x71);
    private static readonly Color DownColor = Color.FromRgb(0xFF, 0x4D, 0x6D);
    private static readonly Brush UpBrush = Frozen(UpColor);
    private static readonly Brush DownBrush = Frozen(DownColor);
    private static readonly Brush ClosedBrush = Frozen(Color.FromRgb(0x9A, 0x98, 0xB0));

    private static readonly TimeSpan LiveInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ClosedInterval = TimeSpan.FromSeconds(60);

    private readonly DispatcherTimer _timer = new() { Interval = LiveInterval };
    private string _lastPair = "";
    private double _lastRate = double.NaN;
    private ExchangeRate? _rate;
    private bool _loading, _again;

    public CurrencyView()
    {
        InitializeComponent();
        Chart.SetResourceReference(StockChart.GuideBrushProperty, "InkSoftBrush");

        _timer.Tick += async (_, _) => await RefreshAsync();
        Loaded += async (_, _) =>
        {
            Settings.Changed += OnSettingsChanged;
            await RefreshAsync();
            _timer.Start();
        };
        Unloaded += (_, _) =>
        {
            Settings.Changed -= OnSettingsChanged;
            _timer.Stop();
        };
    }

    private async void OnSettingsChanged()
    {
        var s = Settings.Current;
        // Only the amount changed: no need to ask for the rate again
        if (_rate is { } r && r.From == s.CurrencyFrom && r.To == s.CurrencyTo)
            Show(r, s.CurrencyAmount);
        else
            await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_loading) { _again = true; return; } // run once more when this one ends
        _loading = true;
        var s = Settings.Current;
        var (from, to) = (s.CurrencyFrom, s.CurrencyTo);
        try
        {
            var rate = await CurrencyService.GetRateAsync(from, to);
            _rate = rate;
            Show(rate, Settings.Current.CurrencyAmount);
            _timer.Interval = rate.MarketOpen ? LiveInterval : ClosedInterval;
        }
        catch (Exception)
        {
            _rate = null;
            ShowMessage("No rate right now", "Check the internet connection, or pick other currencies in settings.");
            _timer.Interval = TimeSpan.FromSeconds(30);
        }
        finally
        {
            _loading = false;
        }
        if (_again)
        {
            _again = false;
            await RefreshAsync();
        }
    }

    private void Show(ExchangeRate r, double amount)
    {
        FromText.Text = r.From;
        ToText.Text = r.To;
        MarketText.Text = r.MarketOpen ? "Live" : "Closed";
        MarketDot.Fill = r.MarketOpen ? UpBrush : ClosedBrush;

        AmountText.Text = CurrencyService.FormatMoney(amount, r.From) + " =";
        ResultText.Text = CurrencyService.FormatMoney(amount * r.Rate, r.To);
        RateText.Text = $"1 {r.From} = {CurrencyService.FormatRate(r.Rate)} {r.To}";

        var showChange = r.Points.Count > 0;
        ChangeText.Visibility = showChange ? Visibility.Visible : Visibility.Collapsed;
        ChangeText.Text = string.Format(CultureInfo.InvariantCulture, "{0}{1:0.00}%", r.Change >= 0 ? "+" : "−", Math.Abs(r.ChangePercent));
        ChangeText.Foreground = r.Change >= 0 ? UpBrush : DownBrush;

        Chart.Baseline = r.ChangeBase;
        Chart.Points = r.Points;

        // Flash green/red when the rate of the same pair moves
        var pair = r.From + r.To;
        if (pair == _lastPair && !double.IsNaN(_lastRate) && Math.Abs(_lastRate - r.Rate) > 1e-12)
            Flash(r.Rate > _lastRate ? UpColor : DownColor);
        _lastPair = pair;
        _lastRate = r.Rate;

        MessageView.Visibility = Visibility.Collapsed;
        RateView.Visibility = Visibility.Visible;
    }

    private void Flash(Color color)
    {
        var brush = new SolidColorBrush(Color.FromArgb(0x66, color.R, color.G, color.B));
        ResultFlash.Background = brush;
        brush.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(Color.FromArgb(0, color.R, color.G, color.B), TimeSpan.FromSeconds(1.4))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            });
    }

    private void ShowMessage(string title, string text)
    {
        MessageTitle.Text = title;
        MessageText.Text = text;
        RateView.Visibility = Visibility.Collapsed;
        MessageView.Visibility = Visibility.Visible;
    }

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
