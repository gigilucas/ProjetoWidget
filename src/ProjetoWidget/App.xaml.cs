using System.Windows;

namespace ProjetoWidget;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ApplyTheme(ProjetoWidget.Settings.Current.DarkMode);
        base.OnStartup(e);
    }

    /// <summary>Switches the menu and the widgets between light and dark mode.</summary>
    public static void ApplyTheme(bool dark)
    {
        var themes = Current.Resources.MergedDictionaries;
        themes.Clear();
        themes.Add(new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{(dark ? "Dark" : "Light")}.xaml"),
        });
    }
}
