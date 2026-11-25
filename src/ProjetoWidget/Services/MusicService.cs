using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Management.Deployment;
using Windows.Media.Control;

namespace ProjetoWidget.Services;

/// <summary>A music app the widget can follow.</summary>
public sealed class MusicApp
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Color { get; init; }
    /// <summary>Words that appear in the app's media session id (Windows "AppUserModelId").</summary>
    public required string[] SessionMatches { get; init; }
    public string? StorePackage { get; init; }
    public string? ExePath { get; init; }
    public string? ShortcutName { get; init; }
    /// <summary>Where to get the app when it isn't installed (Microsoft Store page or website).</summary>
    public required string GetUrl { get; init; }

    // Filled in by detection
    public bool IsInstalled { get; set; }
    public string? StoreAppId { get; set; }
    public string? ShortcutPath { get; set; }

    public string Status => IsInstalled ? "Installed" : "Not installed";

    public bool Matches(string sessionAppId) =>
        SessionMatches.Any(m => sessionAppId.Contains(m, StringComparison.OrdinalIgnoreCase));

    /// <summary>Opens the app, or the page to get it if it isn't installed.</summary>
    public void Launch()
    {
        try
        {
            if (StoreAppId is not null)
                Process.Start("explorer.exe", $"shell:AppsFolder\\{StoreAppId}");
            else if (ExePath is not null && File.Exists(ExePath))
                Process.Start(new ProcessStartInfo(ExePath) { UseShellExecute = true });
            else if (ShortcutPath is not null)
                Process.Start(new ProcessStartInfo(ShortcutPath) { UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo(GetUrl) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Nothing to do if Windows can't open it.
        }
    }
}

/// <summary>What is playing right now.</summary>
public sealed record NowPlaying(
    string Title, string Artist, ImageSource? Art, bool IsPlaying,
    TimeSpan Position, TimeSpan Duration, bool? Shuffle, MediaPlaybackAutoRepeatModeValue? Repeat,
    string SourceName, bool CanSeek, bool CanNext, bool CanPrevious);

public enum MediaPlaybackAutoRepeatModeValue { None, Track, List }

/// <summary>
/// Finds the music apps on this PC and reads/controls what they play through the
/// Windows media controls (the same ones the volume flyout uses). No logins needed.
/// </summary>
public static class MusicService
{
    /// <summary>
    /// The only apps the widget follows. Browsers (Edge, Chrome...) are never shown;
    /// YouTube Music counts only as the installed YouTube Music app.
    /// </summary>
    public static IReadOnlyList<MusicApp> Catalog => CatalogList;

    /// <summary>
    /// Dev aid ("--music-test FakePlayer" or "--music-test FakePlayer:Spotify"): also follow sessions
    /// whose id contains this text, shown under the given name.
    /// </summary>
    public static void AddTestApp(string spec)
    {
        var parts = spec.Split(':', 2);
        CatalogList.Add(new MusicApp
        {
            Id = "test", Name = parts.Length > 1 ? parts[1] : "Test", Color = "#8B5CFF",
            SessionMatches = [parts[0]], GetUrl = "about:blank", IsInstalled = true,
        });
    }

    private static readonly List<MusicApp> CatalogList =
    [
        new() { Id = "applemusic", Name = "Apple Music", Color = "#FA2D48", SessionMatches = ["AppleMusic"],
                StorePackage = "AppleInc.AppleMusicWin",
                GetUrl = "ms-windows-store://pdp/?productid=9PFHDD62MXS1" },
        new() { Id = "youtubemusic", Name = "YouTube Music", Color = "#FF0033",
                // id of the YouTube Music app installed from the browser
                SessionMatches = ["cinhimbnkkaeohfgghhklpknlkffjgod"],
                ShortcutName = "YouTube Music", GetUrl = "https://music.youtube.com" },
        new() { Id = "spotify", Name = "Spotify", Color = "#1DB954", SessionMatches = ["spotify"],
                StorePackage = "SpotifyAB.SpotifyMusic",
                ExePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Spotify\Spotify.exe"),
                GetUrl = "ms-windows-store://pdp/?productid=9NCBCSZSJRSB" },
    ];

    /// <summary>The chosen app id, or a sensible default if none (or an old one) is saved.</summary>
    public static string Normalize(string? id) =>
        Catalog.Any(a => a.Id == id) ? id! : (Catalog.FirstOrDefault(a => a.IsInstalled) ?? Catalog[0]).Id;

    private static Task? _detection;
    private static GlobalSystemMediaTransportControlsSessionManager? _manager;
    private static string? _artKey;
    private static ImageSource? _art;

    /// <summary>Checks which of the catalog apps are installed (runs once).</summary>
    public static Task DetectAsync() => _detection ??= Task.Run(Detect);

    private static void Detect()
    {
        // Store apps installed for this user
        var packages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var p in new PackageManager().FindPackagesForUser(string.Empty))
                packages.TryAdd(p.Id.Name, p.Id.FamilyName);
        }
        catch (Exception)
        {
            // Package list unavailable; desktop apps are still detected below.
        }

        var startMenus = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        };

        foreach (var app in Catalog)
        {
            if (app.StorePackage is not null && packages.TryGetValue(app.StorePackage, out var family))
            {
                app.IsInstalled = true;
                app.StoreAppId = FindStoreAppId(family);
            }
            if (app.ExePath is not null && File.Exists(app.ExePath))
                app.IsInstalled = true;
            if (app.ShortcutName is not null)
            {
                app.ShortcutPath = startMenus
                    .Where(Directory.Exists)
                    .SelectMany(d => SafeFiles(d, app.ShortcutName + "*.lnk"))
                    .FirstOrDefault();
                if (app.ShortcutPath is not null)
                    app.IsInstalled = true;
            }
        }
    }

    private static string FindStoreAppId(string family)
    {
        try
        {
            var package = new PackageManager().FindPackagesForUser(string.Empty, family).FirstOrDefault();
            var entry = package?.GetAppListEntriesAsync().AsTask().GetAwaiter().GetResult().FirstOrDefault();
            if (entry?.AppUserModelId is { Length: > 0 } id)
                return id;
        }
        catch (Exception)
        {
            // fall through to the usual id
        }
        return family + "!App";
    }

    private static IEnumerable<string> SafeFiles(string dir, string pattern)
    {
        try { return Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories).ToList(); }
        catch (Exception) { return []; }
    }

    public static MusicApp? FindApp(string id) => Catalog.FirstOrDefault(a => a.Id == id);

    /// <summary>
    /// Picks the session to show. Only Apple Music, YouTube Music and Spotify count.
    /// Whichever of them you open and play shows up automatically; the chosen app wins a tie.
    /// </summary>
    private static async Task<GlobalSystemMediaTransportControlsSession?> GetSessionAsync(string appId)
    {
        _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

        var sessions = _manager.GetSessions()
            .Where(s => Catalog.Any(a => a.Matches(s.SourceAppUserModelId)))
            .ToList();
        if (sessions.Count == 0) return null;

        var chosen = FindApp(appId);
        bool IsChosen(GlobalSystemMediaTransportControlsSession s) => chosen?.Matches(s.SourceAppUserModelId) == true;
        static bool IsPlaying(GlobalSystemMediaTransportControlsSession s) =>
            s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

        return sessions.FirstOrDefault(s => IsChosen(s) && IsPlaying(s))
               ?? sessions.FirstOrDefault(IsPlaying)
               ?? sessions.FirstOrDefault(IsChosen)
               ?? sessions[0];
    }

    /// <summary>What the chosen app is playing, or null if nothing.</summary>
    public static async Task<NowPlaying?> GetNowPlayingAsync(string appId)
    {
        var session = await GetSessionAsync(appId);
        if (session is null) return null;

        var props = await session.TryGetMediaPropertiesAsync();
        if (props is null || string.IsNullOrWhiteSpace(props.Title)) return null;

        var info = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();
        var playing = info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

        // Position moves on while playing; Windows only reports it now and then.
        var position = timeline.Position - timeline.StartTime;
        if (playing && timeline.LastUpdatedTime > DateTimeOffset.MinValue)
            position += DateTimeOffset.Now - timeline.LastUpdatedTime;
        var duration = timeline.EndTime - timeline.StartTime;
        if (duration > TimeSpan.Zero && position > duration) position = duration;
        if (position < TimeSpan.Zero) position = TimeSpan.Zero;

        // Album art: only reload when the song changes
        var key = $"{props.Title}|{props.Artist}|{props.AlbumTitle}";
        if (key != _artKey)
        {
            _artKey = key;
            _art = await LoadArtAsync(props.Thumbnail);
        }

        var repeat = info?.AutoRepeatMode switch
        {
            Windows.Media.MediaPlaybackAutoRepeatMode.Track => MediaPlaybackAutoRepeatModeValue.Track,
            Windows.Media.MediaPlaybackAutoRepeatMode.List => MediaPlaybackAutoRepeatModeValue.List,
            Windows.Media.MediaPlaybackAutoRepeatMode.None => MediaPlaybackAutoRepeatModeValue.None,
            _ => (MediaPlaybackAutoRepeatModeValue?)null,
        };

        var source = SourceName(session.SourceAppUserModelId);
        var controls = info?.Controls;
        return new NowPlaying(props.Title, props.Artist ?? "", _art, playing, position, duration,
            info?.IsShuffleActive, repeat, source,
            CanSeek: controls?.IsPlaybackPositionEnabled == true && duration > TimeSpan.Zero,
            CanNext: controls?.IsNextEnabled != false,
            CanPrevious: controls?.IsPreviousEnabled != false);
    }

    /// <summary>Jumps to a point in the current song.</summary>
    public static async Task<bool> SeekAsync(string appId, TimeSpan position)
    {
        if (await GetSessionAsync(appId) is not { } s) return false;
        var start = s.GetTimelineProperties().StartTime;
        return await s.TryChangePlaybackPositionAsync((start + position).Ticks);
    }

    /// <summary>Name for the chip under the song.</summary>
    private static string SourceName(string sessionAppId) =>
        Catalog.FirstOrDefault(a => a.Matches(sessionAppId))?.Name ?? "Now Playing";

    private static async Task<ImageSource?> LoadArtAsync(Windows.Storage.Streams.IRandomAccessStreamReference? thumbnail)
    {
        if (thumbnail is null) return null;
        try
        {
            using var stream = await thumbnail.OpenReadAsync();
            using var net = stream.AsStream();
            var memory = new MemoryStream();
            await net.CopyToAsync(memory);
            memory.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = memory;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task PlayPauseAsync(string appId)
    {
        if (await GetSessionAsync(appId) is { } s) await s.TryTogglePlayPauseAsync();
    }

    public static async Task NextAsync(string appId)
    {
        if (await GetSessionAsync(appId) is { } s) await s.TrySkipNextAsync();
    }

    public static async Task PreviousAsync(string appId)
    {
        if (await GetSessionAsync(appId) is { } s) await s.TrySkipPreviousAsync();
    }

    public static async Task ToggleShuffleAsync(string appId)
    {
        if (await GetSessionAsync(appId) is { } s && s.GetPlaybackInfo()?.IsShuffleActive is { } on)
            await s.TryChangeShuffleActiveAsync(!on);
    }

    /// <summary>Off → whole list → one song → off.</summary>
    public static async Task CycleRepeatAsync(string appId)
    {
        if (await GetSessionAsync(appId) is not { } s || s.GetPlaybackInfo()?.AutoRepeatMode is not { } mode) return;
        var next = mode switch
        {
            Windows.Media.MediaPlaybackAutoRepeatMode.None => Windows.Media.MediaPlaybackAutoRepeatMode.List,
            Windows.Media.MediaPlaybackAutoRepeatMode.List => Windows.Media.MediaPlaybackAutoRepeatMode.Track,
            _ => Windows.Media.MediaPlaybackAutoRepeatMode.None,
        };
        await s.TryChangeAutoRepeatModeAsync(next);
    }
}
