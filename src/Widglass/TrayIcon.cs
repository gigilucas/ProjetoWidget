using WinForms = System.Windows.Forms;

namespace Widglass;

/// <summary>
/// The app's icon in the notification area (the ^ arrow next to the clock).
/// Click it to open the menu; right-click for Open, Start with Windows and Quit.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ToolStripMenuItem _startWithWindows;

    public TrayIcon(MainWindow menu)
    {
        var open = new WinForms.ToolStripMenuItem("Open Widglass", null, (_, _) => menu.ShowMenu())
        {
            Font = new System.Drawing.Font(WinForms.Control.DefaultFont, System.Drawing.FontStyle.Bold),
        };
        _startWithWindows = new WinForms.ToolStripMenuItem("Start with Windows")
        {
            Checked = Settings.Current.StartWithWindows,
            CheckOnClick = true,
        };
        _startWithWindows.CheckedChanged += (_, _) => menu.SetStartWithWindows(_startWithWindows.Checked);
        menu.StartWithWindowsChanged += on => _startWithWindows.Checked = on;
        var checkUpdates = new WinForms.ToolStripMenuItem("Check for updates", null,
            async (_, _) => await menu.CheckForUpdatesAsync(userAsked: true));
        var quit = new WinForms.ToolStripMenuItem("Quit", null, (_, _) => menu.Quit());

        var contextMenu = new WinForms.ContextMenuStrip();
        contextMenu.Items.AddRange([open, _startWithWindows, checkUpdates, new WinForms.ToolStripSeparator(), quit]);

        _icon = new WinForms.NotifyIcon
        {
            Text = "Widglass",
            Icon = LoadIcon(),
            ContextMenuStrip = contextMenu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
                menu.ShowMenu();
        };
        _icon.BalloonTipClicked += (_, _) => _onNotificationClick?.Invoke();
    }

    private Action? _onNotificationClick;

    /// <summary>Shows a Windows notification from the tray icon; <paramref name="onClick"/> runs if it's clicked.</summary>
    public void Notify(string title, string text, Action? onClick = null)
    {
        _onNotificationClick = onClick;
        _icon.ShowBalloonTip(8000, title, text, WinForms.ToolTipIcon.Info);
    }

    private static System.Drawing.Icon LoadIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
        using var stream = resource!.Stream;
        // Pick the small size the notification area uses
        return new System.Drawing.Icon(stream, WinForms.SystemInformation.SmallIconSize);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
