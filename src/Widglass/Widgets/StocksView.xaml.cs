using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Widglass.Services;

namespace Widglass.Widgets;

/// <summary>
/// Stocks widget: 1 stock as a square with a big chart, or 2-3 stocks as a wide rectangle with a row each.
/// Prices refresh every few seconds and flash green or red when they change.
/// </summary>
public partial class StocksView : UserControl
{
    private static readonly Color UpColor = Color.FromRgb(0x2E, 0xCC, 0x71);
    private static readonly Color DownColor = Color.FromRgb(0xFF, 0x4D, 0x6D);
    private static readonly Brush UpBrush = Frozen(UpColor);
    private static readonly Brush DownBrush = Frozen(DownColor);
    private static readonly Brush ClosedBrush = Frozen(Color.FromRgb(0x9A, 0x98, 0xB0));

    /// <summary>How often prices refresh while a market is open, and while all are closed.</summary>
    private static readonly TimeSpan LiveInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ClosedInterval = TimeSpan.FromSeconds(30);

    private readonly DispatcherTimer _timer = new() { Interval = LiveInterval };
    private readonly Dictionary<string, double> _lastPrice = [];
    private readonly List<RowParts> _rows = [];
    private string _layoutKey = "";
    private bool _loading;

    public StocksView()
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
        // Square for 1 stock, wide for 2-3
        (Window.GetWindow(this) as WidgetWindow)?.SetWide(Settings.Current.ActiveStocks.Count > 1);
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_loading) return;
        _loading = true;
        var picks = Settings.Current.ActiveStocks;
        try
        {
            // All stocks at once
            var tasks = picks.Select(async p =>
            {
                try { return await StockService.GetQuoteAsync(p.Symbol); }
                catch (Exception) { return null; }
            }).ToList();
            var quotes = await Task.WhenAll(tasks);

            if (quotes.All(q => q is null))
            {
                ShowMessage("No data right now", "Check the internet connection, or pick other stocks in settings.");
                _timer.Interval = TimeSpan.FromSeconds(30);
                return;
            }

            if (picks.Count == 1)
                ShowSingle(quotes[0]!);
            else
                ShowList(picks, quotes);

            _timer.Interval = quotes.Any(q => q?.MarketOpen == true) ? LiveInterval : ClosedInterval;
        }
        finally
        {
            _loading = false;
        }
    }

    // ---------- One stock ----------

    private void ShowSingle(StockQuote q)
    {
        SymbolText.Text = q.Symbol;
        NameText.Text = q.Name;
        MarketText.Text = q.MarketOpen ? "Live" : "Closed";
        MarketDot.Fill = q.MarketOpen ? UpBrush : ClosedBrush;

        PriceText.Text = StockService.FormatPrice(q.Price, q.Currency);
        ChangeText.Text = ChangeLabel(q);
        ChangeText.Foreground = q.Change >= 0 ? UpBrush : DownBrush;
        Flash(PriceFlash, q);

        Chart.Baseline = q.ChangeBase;
        Chart.Points = q.Points;

        ListView.Visibility = Visibility.Collapsed;
        MessageView.Visibility = Visibility.Collapsed;
        SingleView.Visibility = Visibility.Visible;
    }

    // ---------- Two or three stocks ----------

    private sealed record RowParts(TextBlock Symbol, TextBlock Name, StockChart Chart, Border PriceFlash, TextBlock Price, Border ChangePill, TextBlock Change);

    private void ShowList(IReadOnlyList<StockPick> picks, StockQuote?[] quotes)
    {
        var key = string.Join(",", picks.Select(p => p.Symbol));
        if (key != _layoutKey)
            BuildRows(picks.Count);
        _layoutKey = key;

        var anyOpen = false;
        for (var i = 0; i < picks.Count; i++)
        {
            var r = _rows[i];
            var q = quotes[i];
            r.Symbol.Text = q?.Symbol ?? picks[i].Symbol;
            r.Name.Text = q?.Name ?? picks[i].Name;
            if (q is null)
            {
                r.Price.Text = "—";
                r.Change.Text = "no data";
                r.ChangePill.Background = ClosedBrush;
                r.Chart.Points = null;
                continue;
            }
            anyOpen |= q.MarketOpen;
            r.Price.Text = StockService.FormatPrice(q.Price, q.Currency);
            r.Change.Text = string.Format(CultureInfo.InvariantCulture, "{0}{1:0.00}%", q.Change >= 0 ? "+" : "−", Math.Abs(q.ChangePercent));
            r.ChangePill.Background = q.Change >= 0 ? UpBrush : DownBrush;
            r.Chart.Baseline = q.ChangeBase;
            r.Chart.Points = q.Points;
            Flash(r.PriceFlash, q);
        }

        ListMarketText.Text = anyOpen ? "Live" : "Closed";
        ListMarketDot.Fill = anyOpen ? UpBrush : ClosedBrush;

        SingleView.Visibility = Visibility.Collapsed;
        MessageView.Visibility = Visibility.Collapsed;
        ListView.Visibility = Visibility.Visible;
    }

    /// <summary>One row per stock: symbol and name, mini chart, price and change pill.</summary>
    private void BuildRows(int count)
    {
        Rows.Children.Clear();
        Rows.RowDefinitions.Clear();
        _rows.Clear();
        for (var i = 0; i < count; i++)
        {
            Rows.RowDefinitions.Add(new RowDefinition());

            var row = new Grid { VerticalAlignment = VerticalAlignment.Center };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) });

            var symbol = new TextBlock { FontSize = 15, FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis };
            var name = new TextBlock { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
            name.SetResourceReference(TextBlock.ForegroundProperty, "InkSoftBrush");
            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 8, 0) };
            left.Children.Add(symbol);
            left.Children.Add(name);

            var chart = new StockChart { Height = count == 3 ? 34 : 50, Margin = new Thickness(0, 0, 12, 0) };
            chart.SetResourceReference(StockChart.GuideBrushProperty, "InkSoftBrush");
            Grid.SetColumn(chart, 1);

            var price = new TextBlock { FontSize = 15, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right };
            var priceFlash = new Border { Child = price, CornerRadius = new CornerRadius(6), Padding = new Thickness(4, 0, 4, 0), Margin = new Thickness(0, 0, -4, 0), HorizontalAlignment = HorizontalAlignment.Right, Background = Brushes.Transparent };
            var change = new TextBlock { FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
            var pill = new Border { Child = change, CornerRadius = new CornerRadius(7), Padding = new Thickness(7, 1, 7, 2), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 3, 0, 0) };
            var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            right.Children.Add(priceFlash);
            right.Children.Add(pill);
            Grid.SetColumn(right, 2);

            row.Children.Add(left);
            row.Children.Add(chart);
            row.Children.Add(right);

            // Thin line between rows
            var cell = new Grid();
            cell.Children.Add(row);
            if (i > 0)
            {
                var divider = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Top };
                divider.SetResourceReference(Border.BackgroundProperty, "WidgetDividerBrush");
                cell.Children.Add(divider);
            }
            Grid.SetRow(cell, i);
            Rows.Children.Add(cell);
            _rows.Add(new RowParts(symbol, name, chart, priceFlash, price, pill, change));
        }
    }

    // ---------- Helpers ----------

    /// <summary>Briefly tints the price green (up) or red (down) when it changes, then fades.</summary>
    private void Flash(Border target, StockQuote q)
    {
        if (_lastPrice.TryGetValue(q.Symbol, out var previous) && Math.Abs(previous - q.Price) > 1e-9)
        {
            var color = q.Price > previous ? UpColor : DownColor;
            var brush = new SolidColorBrush(Color.FromArgb(0x66, color.R, color.G, color.B));
            target.Background = brush;
            brush.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(Color.FromArgb(0, color.R, color.G, color.B), TimeSpan.FromSeconds(1.4))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                });
        }
        _lastPrice[q.Symbol] = q.Price;
    }

    private static string ChangeLabel(StockQuote q)
    {
        var sign = q.Change >= 0 ? "+" : "−";
        return string.Format(CultureInfo.InvariantCulture, "{0}{1:0.00}  {0}{2:0.00}%", sign, Math.Abs(q.Change), Math.Abs(q.ChangePercent));
    }

    private void ShowMessage(string title, string text)
    {
        MessageTitle.Text = title;
        MessageText.Text = text;
        SingleView.Visibility = Visibility.Collapsed;
        ListView.Visibility = Visibility.Collapsed;
        MessageView.Visibility = Visibility.Visible;
    }

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
