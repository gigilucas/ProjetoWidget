using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Widglass.Services;

namespace Widglass.Widgets;

public partial class StocksSettingsView : UserControl
{
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly bool _ready;
    private int _searchId;
    private int _selectedSlot;

    public StocksSettingsView()
    {
        InitializeComponent();
        (Settings.Current.StockCount switch { 2 => Count2, 3 => Count3, _ => Count1 }).IsChecked = true;
        ShowSlots();
        _debounce.Tick += async (_, _) => { _debounce.Stop(); await SearchAsync(SearchBox.Text); };
        _ready = true;
    }

    /// <summary>A row in the list of chosen stocks.</summary>
    public sealed record SlotItem(int Index, string Symbol, string Name, bool IsSelected)
    {
        public string Number => (Index + 1).ToString();
    }

    private void ShowSlots()
    {
        var count = Settings.Current.StockCount;
        if (_selectedSlot >= count) _selectedSlot = 0;
        Slots.ItemsSource = Settings.Current.Stocks.Take(count)
            .Select((s, i) => new SlotItem(i, s.Symbol, s.Name, i == _selectedSlot))
            .ToList();
        CountHint.Text = count == 1
            ? "A square widget with a big chart."
            : $"A wide widget with a row for each stock. Click a stock below to change it.";
    }

    private void Count_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        Settings.Current.StockCount = Count3.IsChecked == true ? 3 : Count2.IsChecked == true ? 2 : 1;
        Settings.Current.Save();
        ShowSlots();
    }

    private void Slot_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: int index })
            _selectedSlot = index;
        SearchBox.Focus();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        Placeholder.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _debounce.Stop();
        if (string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            Results.ItemsSource = null;
            SearchStatus.Visibility = Visibility.Collapsed;
            return;
        }
        _debounce.Start(); // search once the user pauses typing
    }

    private async Task SearchAsync(string query)
    {
        var id = ++_searchId;
        try
        {
            var matches = await StockService.SearchAsync(query);
            if (id != _searchId) return; // a newer search is on its way
            Results.ItemsSource = matches.Take(6).ToList();
            SearchStatus.Text = "Nothing found.";
            SearchStatus.Visibility = matches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception)
        {
            if (id != _searchId) return;
            Results.ItemsSource = null;
            SearchStatus.Text = "Couldn't search right now. Check the internet connection.";
            SearchStatus.Visibility = Visibility.Visible;
        }
    }

    private void Result_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: StockMatch match })
            Choose(match.Symbol, match.Name);
    }

    private void Pick_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && tag.Split('|', 2) is [var symbol, var name])
            Choose(symbol, name);
    }

    /// <summary>Puts the stock in the selected slot; the widget reloads right away.</summary>
    private void Choose(string symbol, string name)
    {
        Settings.Current.SetStock(_selectedSlot, symbol, name);
        Settings.Current.Save();
        SearchBox.Text = "";
        ShowSlots();
    }
}
