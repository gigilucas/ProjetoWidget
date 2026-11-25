using WinForms = System.Windows.Forms;

namespace ProjetoWidget;

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
        var open = new WinForms.ToolStripMenuItem("Open ProjetoWidget", null, (_, _) => menu.ShowMenu())
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
        var quit = new WinForms.ToolStripMenuItem("Quit", null, (_, _) => menu.Quit());

        var contextMenu = new WinForms.ContextMenuStrip();
        contextMenu.Items.AddRange([open, _startWithWindows, new WinForms.ToolStripSeparator(), quit]);

        _icon = new WinForms.NotifyIcon
        {
            Text = "ProjetoWidget",
            Icon = LoadIcon(),
            ContextMenuStrip = contextMenu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
                menu.ShowMenu();
        };
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
