using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Widglass.Services;

namespace Widglass.Widgets;

public partial class CurrencySettingsView : UserControl
{
    private readonly bool _ready;
    private bool _pickingTo; // which side the currency grid sets

    public CurrencySettingsView()
    {
        InitializeComponent();
        AmountBox.Text = Settings.Current.CurrencyAmount.ToString("0.########", CultureInfo.InvariantCulture);
        FromSide.IsChecked = true;
        Refresh();
        _ready = true;
    }

    /// <summary>One currency in the grid.</summary>
    public sealed record CurrencyItem(string Code, string Name, string Sign, bool IsCurrent, bool IsOther);

    private void Refresh()
    {
        var s = Settings.Current;
        var from = CurrencyService.Info(s.CurrencyFrom);
        var to = CurrencyService.Info(s.CurrencyTo);
        FromCode.Text = from.Code;
        FromName.Text = from.Name;
        ToCode.Text = to.Code;
        ToName.Text = to.Name;
        AmountSymbol.Text = from.Symbol.Trim();
        AmountBox.Padding = new Thickness(Math.Max(28, 18 + AmountSymbol.Text.Length * 9), 8, 10, 8);

        var current = _pickingTo ? to.Code : from.Code;
        var other = _pickingTo ? from.Code : to.Code;
        Currencies.ItemsSource = CurrencyService.All
            .Select(c => new CurrencyItem(c.Code, c.Name, c.Symbol.Trim(), c.Code == current, c.Code == other))
            .ToList();
        PickHint.Text = _pickingTo ? "Pick the currency to convert to:" : "Pick the currency to convert from:";
    }

    private void Side_Checked(object sender, RoutedEventArgs e)
    {
        _pickingTo = ToSide.IsChecked == true;
        if (_ready) Refresh();
    }

    private void Currency_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string code }) return;
        var s = Settings.Current;
        // Picking the other side's currency just swaps them
        if (code == (_pickingTo ? s.CurrencyFrom : s.CurrencyTo))
            s.SwapCurrencies();
        else if (_pickingTo)
            s.CurrencyTo = code;
        else
            s.CurrencyFrom = code;
        s.Save();
        Refresh();
    }

    private void Swap_Click(object sender, RoutedEventArgs e)
    {
        Settings.Current.SwapCurrencies();
        Settings.Current.Save();
        Refresh();
    }

    private void AmountBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        if (ParseAmount(AmountBox.Text) is { } amount)
        {
            AmountHint.Visibility = Visibility.Collapsed;
            Settings.Current.CurrencyAmount = amount;
            Settings.Current.Save();
        }
        else
        {
            AmountHint.Visibility = Visibility.Visible;
        }
    }

    /// <summary>Reads "100", "2.50", "2,50" or "1,000.50".</summary>
    public static double? ParseAmount(string text)
    {
        text = text.Replace(" ", "").Replace(" ", "");
        if (text.Length == 0) return null;
        if (text.Contains(',') && !text.Contains('.'))
            text = text.Replace(',', '.'); // decimal comma
        else
            text = text.Replace(",", ""); // thousands separators
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0 ? v : null;
    }
}
