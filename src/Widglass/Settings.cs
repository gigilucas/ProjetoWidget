using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Widglass;

public enum ClockStyle { Analog, Digital }

/// <summary>User settings, stored in %AppData%\Widglass\settings.json.</summary>
public sealed class Settings
{
    /// <summary>Raised when a widget option changes, so open widgets can update right away.</summary>
    public static event Action? Changed;

    private ClockStyle _clockStyle = ClockStyle.Analog;
    private string _musicApp = "";

    /// <summary>Menu and widgets in dark mode.</summary>
    public bool DarkMode { get; set; }

    /// <summary>Open the app (in the tray, with its widgets) when Windows starts. On by default.</summary>
    public bool StartWithWindows { get; set; } = true;

    /// <summary>Install new versions without asking (otherwise the app just tells you). Off by default.</summary>
    public bool AutoInstallUpdates { get; set; }

    /// <summary>Music widget: the preferred app (Apple Music, YouTube Music or Spotify).</summary>
    public string MusicApp
    {
        get => _musicApp;
        set
        {
            if (_musicApp == value) return;
            _musicApp = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Clock widget: analog face with three hands, or digital numbers.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ClockStyle ClockStyle
    {
        get => _clockStyle;
        set
        {
            if (_clockStyle == value) return;
            _clockStyle = value;
            Changed?.Invoke();
        }
    }

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Widglass", "settings.json");

    /// <summary>Where settings lived before the app was renamed to Widglass.</summary>
    private static readonly string OldFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ProjetoWidget", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // Must come after FilePath and OldFilePath: static fields are initialized in the order they are written.
    /// <summary>The one settings object shared by the whole app.</summary>
    public static Settings Current { get; } = Load();

    /// <summary>Which widgets are switched on, by widget id.</summary>
    public Dictionary<string, bool> Widgets { get; set; } = [];

    /// <summary>Where each widget sits on the screen (left, top), by widget id.</summary>
    public Dictionary<string, double[]> Positions { get; set; } = [];

    public bool IsOn(string id) => Widgets.TryGetValue(id, out var on) && on;

    public void SetOn(string id, bool on) => Widgets[id] = on;

    public bool TryGetPosition(string id, out double left, out double top)
    {
        left = top = 0;
        if (!Positions.TryGetValue(id, out var p) || p.Length != 2)
            return false;
        (left, top) = (p[0], p[1]);
        return true;
    }

    public void SetPosition(string id, double left, double top) => Positions[id] = [left, top];

    public static Settings Load()
    {
        try
        {
            // First run after the rename: bring the old settings over.
            if (!File.Exists(FilePath) && File.Exists(OldFilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.Copy(OldFilePath, FilePath);
            }

            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch (Exception)
        {
            // A broken settings file should never stop the app from opening.
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception)
        {
            // Not being able to save is not fatal; the toggles still work for this session.
        }
    }
}
