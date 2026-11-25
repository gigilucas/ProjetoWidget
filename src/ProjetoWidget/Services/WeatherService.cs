using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Windows.Devices.Geolocation;

namespace ProjetoWidget.Services;

/// <summary>
/// Weather for this PC's location. Location comes from Windows location services;
/// the forecast from Open-Meteo (free, no account) and the place name from BigDataCloud.
/// Coordinates are rounded to 2 decimals (about 1 km) before they leave the PC.
/// </summary>
public static class WeatherService
{
    private static readonly HttpClient Http = CreateHttp();
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static WeatherReport? _cached;
    private static (double Lat, double Lon)? _override;

    /// <summary>Dev aid: use these coordinates instead of the PC's location.</summary>
    public static void OverrideLocation(double lat, double lon) => _override = (lat, lon);

    public static async Task<WeatherReport> GetAsync(bool force = false)
    {
        await Gate.WaitAsync();
        try
        {
            if (!force && _cached is { } c && DateTime.Now - c.UpdatedAt < CacheFor)
                return c;

            var (lat, lon) = await GetLocationAsync();
            lat = Math.Round(lat, 2);
            lon = Math.Round(lon, 2);

            var forecastTask = GetForecastAsync(lat, lon);
            var placeTask = GetPlaceNameAsync(lat, lon);
            var report = (await forecastTask) with { Place = await placeTask };
            _cached = report;
            return report;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<(double, double)> GetLocationAsync()
    {
        if (_override is { } o)
            return o;

        var access = await Geolocator.RequestAccessAsync();
        if (access != GeolocationAccessStatus.Allowed)
            throw new LocationOffException();

        var locator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
        var position = await locator.GetGeopositionAsync(TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(15));
        return (position.Coordinate.Point.Position.Latitude, position.Coordinate.Point.Position.Longitude);
    }

    private static async Task<WeatherReport> GetForecastAsync(double lat, double lon)
    {
        var url = string.Format(CultureInfo.InvariantCulture,
            "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}" +
            "&current=temperature_2m,weather_code,is_day" +
            "&daily=temperature_2m_max,temperature_2m_min,weather_code" +
            "&timezone=auto&forecast_days=5", lat, lon);

        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
        var root = doc.RootElement;

        var current = root.GetProperty("current");
        var temp = current.GetProperty("temperature_2m").GetDouble();
        var code = current.GetProperty("weather_code").GetInt32();
        var isDay = current.GetProperty("is_day").GetInt32() == 1;

        // Today plus the next four days
        var daily = root.GetProperty("daily");
        var dates = daily.GetProperty("time");
        var highs = daily.GetProperty("temperature_2m_max");
        var lows = daily.GetProperty("temperature_2m_min");
        var codes = daily.GetProperty("weather_code");
        var days = new List<DayForecast>();
        for (var i = 0; i < dates.GetArrayLength() && days.Count < 5; i++)
        {
            var date = DateTime.Parse(dates[i].GetString()!, CultureInfo.InvariantCulture);
            var label = i == 0 ? "Today" : date.ToString("ddd", CultureInfo.InvariantCulture);
            days.Add(new DayForecast(label, highs[i].GetDouble(), lows[i].GetDouble(), codes[i].GetInt32()));
        }

        return new WeatherReport("", temp, code, isDay, days[0].High, days[0].Low, days, DateTime.Now);
    }

    private static async Task<string> GetPlaceNameAsync(double lat, double lon)
    {
        try
        {
            var url = string.Format(CultureInfo.InvariantCulture,
                "https://api.bigdatacloud.net/data/reverse-geocode-client?latitude={0}&longitude={1}&localityLanguage=en", lat, lon);
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            foreach (var key in new[] { "city", "locality", "principalSubdivision", "countryName" })
                if (doc.RootElement.TryGetProperty(key, out var v) && v.GetString() is { Length: > 0 } name)
                    return name;
        }
        catch (Exception)
        {
            // the forecast still works without a place name
        }
        return "My Location";
    }

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ProjetoWidget/0.1");
        return http;
    }
}

/// <summary>Windows location is switched off (or this app isn't allowed to use it).</summary>
public sealed class LocationOffException() : Exception("Location is off in Windows.");

public sealed record DayForecast(string Label, double High, double Low, int Code);

public sealed record WeatherReport(
    string Place, double Temp, int Code, bool IsDay, double High, double Low,
    IReadOnlyList<DayForecast> Days, DateTime UpdatedAt);

/// <summary>WMO weather codes (used by Open-Meteo) to text and icon.</summary>
public static class WeatherCodes
{
    public static string Describe(int code) => code switch
    {
        0 => "Clear",
        1 => "Mostly Clear",
        2 => "Partly Cloudy",
        3 => "Cloudy",
        45 or 48 => "Fog",
        51 or 53 or 55 or 56 or 57 => "Drizzle",
        61 or 63 or 66 => "Rain",
        65 or 67 => "Heavy Rain",
        71 or 73 or 75 or 77 => "Snow",
        80 or 81 or 82 => "Showers",
        85 or 86 => "Snow Showers",
        95 or 96 or 99 => "Thunderstorm",
        _ => "—",
    };

    public static WeatherIconKind Icon(int code, bool isDay) => code switch
    {
        0 or 1 => isDay ? WeatherIconKind.Sun : WeatherIconKind.Moon,
        2 => isDay ? WeatherIconKind.PartlyCloudyDay : WeatherIconKind.PartlyCloudyNight,
        3 => WeatherIconKind.Cloud,
        45 or 48 => WeatherIconKind.Fog,
        >= 51 and <= 67 or >= 80 and <= 82 => WeatherIconKind.Rain,
        >= 71 and <= 77 or 85 or 86 => WeatherIconKind.Snow,
        >= 95 => WeatherIconKind.Storm,
        _ => WeatherIconKind.Cloud,
    };
}

public enum WeatherIconKind { Sun, Moon, PartlyCloudyDay, PartlyCloudyNight, Cloud, Rain, Snow, Storm, Fog }
