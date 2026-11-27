using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace Widglass.Services;

/// <summary>A newer Widglass published on GitHub Releases.</summary>
public sealed record UpdateInfo(Version Version, string DownloadUrl, long Size, string PageUrl)
{
    public string VersionText => UpdateService.Format(Version);
}

/// <summary>
/// Checks GitHub Releases for a newer Widglass, downloads it next to the running .exe,
/// swaps the files and restarts. Settings, widgets and positions are untouched
/// (they live in %AppData%\Widglass, not next to the .exe).
/// </summary>
public static class UpdateService
{
    private const string LatestReleaseApi = "https://api.github.com/repos/gigilucas/Widglass/releases/latest";
    public const string ReleasesPage = "https://github.com/gigilucas/Widglass/releases/latest";

    // Each release has the same .exe twice: "Widglass.exe" for people downloading it from the website,
    // and "Widglass-update.exe" for the app updating itself. GitHub counts downloads per file,
    // so the first number is real downloads and isn't inflated by updates.
    private const string UpdateAssetName = "Widglass-update.exe";
    private const string AssetName = "Widglass.exe"; // older releases only have this one

    // Static fields are initialized in the order they are written: CurrentVersion must come before Http,
    // which uses it for its User-Agent.
    public static Version CurrentVersion { get; } = ReadCurrentVersion();

    private static readonly HttpClient Http = CreateHttp();
    private static string? _feedOverride;

    /// <summary>Dev aid ("--update-feed feed.json"): read the "latest release" from a local file instead of GitHub.</summary>
    public static void OverrideFeed(string path) => _feedOverride = path;

    public static string Format(Version v) => $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}";

    private static Version ReadCurrentVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
    }

    /// <summary>The newer release, or null if this is already the latest (or there's no internet).</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        string json;
        if (_feedOverride is { } feed)
        {
            json = await File.ReadAllTextAsync(feed);
        }
        else
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await Http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            json = await response.Content.ReadAsStringAsync();
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest))
            return null;
        latest = new Version(latest.Major, latest.Minor, Math.Max(0, latest.Build));
        if (latest <= CurrentVersion)
            return null;

        var assets = root.GetProperty("assets").EnumerateArray()
            .Where(a => a.GetProperty("name").GetString() is { } n &&
                        (n.Equals(UpdateAssetName, StringComparison.OrdinalIgnoreCase) || n.Equals(AssetName, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(a => a.GetProperty("name").GetString()!.Equals(UpdateAssetName, StringComparison.OrdinalIgnoreCase) ? 0 : 1);
        foreach (var asset in assets)
        {
            var url = asset.GetProperty("browser_download_url").GetString();
            if (string.IsNullOrEmpty(url))
                continue;
            var size = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
            var page = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? ReleasesPage : ReleasesPage;
            return new UpdateInfo(latest, url, size, page);
        }
        return null; // release without the .exe yet (still building)
    }

    /// <summary>
    /// Downloads the new .exe, swaps it with the running one and starts it.
    /// The caller then quits; the new copy waits for that before starting up.
    /// </summary>
    public static async Task InstallAsync(UpdateInfo update, IProgress<double>? progress, bool showMenuAfter)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Unknown app location.");
        var newFile = exe + ".new";
        var oldFile = exe + ".old";

        // 1. Download next to the app (same folder, so the swap is a quick rename)
        await DownloadAsync(update, newFile, progress);
        if (update.Size > 0 && new FileInfo(newFile).Length != update.Size)
        {
            File.Delete(newFile);
            throw new IOException("The download was incomplete.");
        }

        // 2. Swap: a running .exe can be renamed, so move it aside and put the new one in its place
        TryDelete(oldFile);
        File.Move(exe, oldFile);
        try
        {
            File.Move(newFile, exe);
        }
        catch
        {
            File.Move(oldFile, exe); // put the old version back
            throw;
        }

        // 3. Start the new version; it waits until this one has quit
        var args = $"--wait-for {Environment.ProcessId} --updated" + (showMenuAfter ? "" : $" {StartupRegistration.BackgroundArg}");
        Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false });
    }

    private static async Task DownloadAsync(UpdateInfo update, string target, IProgress<double>? progress)
    {
        if (Uri.TryCreate(update.DownloadUrl, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            File.Copy(uri.LocalPath, target, overwrite: true); // dev/test feeds
            progress?.Report(1);
            return;
        }

        using var response = await Http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? update.Size;
        await using var source = await response.Content.ReadAsStreamAsync();
        await using var file = File.Create(target);
        var buffer = new byte[81920];
        long done = 0;
        int read;
        while ((read = await source.ReadAsync(buffer)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read));
            done += read;
            if (total > 0)
                progress?.Report((double)done / total);
        }
    }

    /// <summary>Removes the previous version left behind by an update (it can't be deleted while it runs).</summary>
    public static void CleanUpAfterUpdate()
    {
        if (Environment.ProcessPath is not { } exe) return;
        TryDelete(exe + ".old");
        TryDelete(exe + ".new");
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception) { /* still in use; next start */ }
    }

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Widglass/{Format(CurrentVersion)}");
        return http;
    }
}
