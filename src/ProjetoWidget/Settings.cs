using System.IO;
using System.Text.Json;

namespace ProjetoWidget;

/// <summary>User settings, stored in %AppData%\ProjetoWidget\settings.json.</summary>
public sealed class Settings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ProjetoWidget", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Which widgets are switched on, by widget id.</summary>
    public Dictionary<string, bool> Widgets { get; set; } = [];

    public bool IsOn(string id) => Widgets.TryGetValue(id, out var on) && on;

    public void SetOn(string id, bool on) => Widgets[id] = on;

    public static Settings Load()
    {
        try
        {
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
