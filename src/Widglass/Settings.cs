using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Widglass;

public enum ClockStyle { Analog, Digital }

/// <summary>One stock chosen for the Stocks widget.</summary>
public sealed class StockPick
{
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>One photo frame on the desktop: its photos and how they're shown.</summary>
public sealed class PhotoFrame
{
    /// <summary>Picked photo files and folders (every photo inside a folder is shown).</summary>
    public List<string> Sources { get; set; } = [];

    /// <summary>Seconds each photo stays on screen.</summary>
    public int Interval { get; set; } = 15;

    /// <summary>Random order instead of by name.</summary>
    public bool Shuffle { get; set; } = true;

    /// <summary>A wide rectangle (good for landscape photos) instead of a square.</summary>
    public bool Wide { get; set; }

    public void AddSources(IEnumerable<string> paths)
    {
        foreach (var path in paths)
            if (!Sources.Contains(path, StringComparer.OrdinalIgnoreCase))
                Sources.Add(path);
    }
}

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

    private int _stockCount = 1;

    /// <summary>
    /// Stocks widget: three slots (Yahoo Finance symbols, e.g. AAPL, TSLA, BTC-USD, ^GSPC).
    /// The widget shows the first <see cref="StockCount"/> of them.
    /// </summary>
    public List<StockPick> Stocks { get; set; } =
    [
        new() { Symbol = "AAPL", Name = "Apple Inc." },
        new() { Symbol = "MSFT", Name = "Microsoft Corporation" },
        new() { Symbol = "NVDA", Name = "NVIDIA Corporation" },
    ];

    /// <summary>Stocks widget: how many stocks to show, 1 to 3 (2 or 3 make the widget a wide rectangle).</summary>
    public int StockCount
    {
        get => _stockCount;
        set
        {
            value = Math.Clamp(value, 1, 3);
            if (_stockCount == value) return;
            _stockCount = value;
            Changed?.Invoke();
        }
    }

    /// <summary>The stocks the widget shows right now.</summary>
    [JsonIgnore] public IReadOnlyList<StockPick> ActiveStocks => Stocks.Where(s => s.Symbol.Length > 0).Take(StockCount).ToList();

    public void SetStock(int slot, string symbol, string name)
    {
        while (Stocks.Count < 3) Stocks.Add(new StockPick());
        Stocks[slot] = new StockPick { Symbol = symbol, Name = name };
        Changed?.Invoke();
    }

    // Older settings files had a single "StockSymbol"/"StockName": read them into the first slot.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StockSymbol { get => null; set { if (value is { Length: > 0 }) Stocks[0].Symbol = value; } }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StockName { get => null; set { if (value is { Length: > 0 }) Stocks[0].Name = value; } }

    // ---------- Currency widget ----------

    private string _currencyFrom = "EUR";
    private string _currencyTo = "USD";
    private double _currencyAmount = 100;

    /// <summary>Currency widget: convert from this currency (ISO code, or BTC/ETH)...</summary>
    public string CurrencyFrom
    {
        get => _currencyFrom;
        set { if (_currencyFrom == value) return; _currencyFrom = value; Changed?.Invoke(); }
    }

    /// <summary>...to this one.</summary>
    public string CurrencyTo
    {
        get => _currencyTo;
        set { if (_currencyTo == value) return; _currencyTo = value; Changed?.Invoke(); }
    }

    /// <summary>Currency widget: how much to convert.</summary>
    public double CurrencyAmount
    {
        get => _currencyAmount;
        set
        {
            value = Math.Clamp(value, 0, 1_000_000_000);
            if (_currencyAmount == value) return;
            _currencyAmount = value;
            Changed?.Invoke();
        }
    }

    public void SwapCurrencies()
    {
        (_currencyFrom, _currencyTo) = (_currencyTo, _currencyFrom);
        Changed?.Invoke();
    }

    // ---------- Photos widget ----------

    public const int MaxPhotoFrames = 5;

    private int _photoFrameCount = 1;

    /// <summary>Photos widget: each frame (1 to 5 on the desktop) has its own photos and options.</summary>
    public List<PhotoFrame> PhotoFrames { get; set; } = [new()];

    /// <summary>Photos widget: how many frames are on the desktop.</summary>
    public int PhotoFrameCount
    {
        get => _photoFrameCount;
        set
        {
            value = Math.Clamp(value, 1, MaxPhotoFrames);
            if (_photoFrameCount == value) return;
            _photoFrameCount = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Frame number <paramref name="index"/> (from 0), created if it doesn't exist yet.</summary>
    public PhotoFrame Frame(int index)
    {
        while (PhotoFrames.Count <= index) PhotoFrames.Add(new PhotoFrame());
        return PhotoFrames[index];
    }

    /// <summary>Changes one frame's options and lets the widgets know.</summary>
    public void UpdateFrame(int index, Action<PhotoFrame> change)
    {
        change(Frame(index));
        Changed?.Invoke();
    }

    // Before there were several frames, the options sat here: read them into the first frame.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? PhotoSources { get => null; set { if (value is not null) Frame(0).Sources = value; } }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PhotoInterval { get => null; set { if (value is { } v) Frame(0).Interval = v; } }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PhotoShuffle { get => null; set { if (value is { } v) Frame(0).Shuffle = v; } }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PhotoWide { get => null; set { if (value is { } v) Frame(0).Wide = v; } }

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
