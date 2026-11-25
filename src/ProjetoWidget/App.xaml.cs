using System.Threading;
using System.Windows;

namespace ProjetoWidget;

/// <summary>
/// Starts the app: one copy at a time, an icon in the notification area, the widgets,
/// and the menu (unless it was started with Windows, then only the widgets and the icon).
/// </summary>
public partial class App : Application
{
    private const string InstanceName = "ProjetoWidget.SingleInstance";
    private const string ShowMenuSignal = "ProjetoWidget.ShowMenu";

    private Mutex? _instance;
    private EventWaitHandle? _showMenu;
    private TrayIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        ApplyTheme(ProjetoWidget.Settings.Current.DarkMode);
        base.OnStartup(e);

        var screenshot = e.Args.Contains("--screenshot");
        if (screenshot)
        {
            // Dev aid: render an image and exit; no tray icon, no single-instance check.
            new MainWindow().Show();
            return;
        }

        // Already running? Ask that copy to show its menu, and stop here.
        _instance = new Mutex(true, InstanceName, out var isFirst);
        _showMenu = new EventWaitHandle(false, EventResetMode.AutoReset, ShowMenuSignal);
        if (!isFirst)
        {
            _showMenu.Set();
            Shutdown();
            return;
        }

        var menu = new MainWindow();
        MainWindow = menu;
        _tray = new TrayIcon(menu);
        StartupRegistration.Apply(ProjetoWidget.Settings.Current.StartWithWindows); // keeps the .exe path current
        menu.StartWidgets();

        if (!e.Args.Contains(StartupRegistration.BackgroundArg))
            menu.ShowMenu();

        // A second launch (e.g. from a shortcut) opens this copy's menu.
        var waiter = new Thread(() =>
        {
            while (_showMenu.WaitOne())
                Dispatcher.BeginInvoke(menu.ShowMenu);
        }) { IsBackground = true, Name = "ShowMenuListener" };
        waiter.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
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
