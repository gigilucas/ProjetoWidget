using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProjetoWidget.Services;

namespace ProjetoWidget.Widgets;

public partial class MusicSettingsView : UserControl
{
    private bool _ready;

    public MusicSettingsView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await MusicService.DetectAsync();
            var selected = MusicService.Normalize(Settings.Current.MusicApp);

            // Always the same three apps, installed or not
            AppList.ItemsSource = MusicService.Catalog
                .Select(a => new AppChoice(a.Id, a.Name, a.Status, a.Color, a.Name[..1], a.Id == selected))
                .ToList();
            _ready = true;
        };
    }

    private void App_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready || sender is not RadioButton { Tag: string id }) return;
        Settings.Current.MusicApp = id;
        Settings.Current.Save();
    }

    public sealed record AppChoice(string Id, string Name, string Status, string Hex, string Initial, bool IsSelected)
    {
        public Brush IconBrush { get; } = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(Hex));
    }
}
