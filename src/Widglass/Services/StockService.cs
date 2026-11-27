using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace Widglass.Services;

/// <summary>One point on the price chart.</summary>
public readonly record struct PricePoint(DateTimeOffset Time, double Price);

/// <summary>A stock (or ETF, index, crypto...) with its latest price and chart.</summary>
public sealed record StockQuote(
    string Symbol, string Name, string Currency, string Exchange,
    double Price, double ChangeBase, IReadOnlyList<PricePoint> Points,
    bool MarketOpen, DateTimeOffset UpdatedAt)
{
    public double Change => Price - ChangeBase;
    public double ChangePercent => ChangeBase == 0 ? 0 : Change / ChangeBase * 100;
}

/// <summary>A search result when picking a stock.</summary>
public sealed record StockMatch(string Symbol, string Name, string Exchange, string Kind);

/// <summary>
/// Stock prices from Yahoo Finance's public chart data (free, no account).
/// Prices are live while the market is open (some exchanges are a few minutes behind).
/// </summary>
public static class StockService
{
    private static readonly HttpClient Http = CreateHttp();

    /// <summary>Today's live price and a 1-minute chart of the day.</summary>
    public static async Task<StockQuote> GetQuoteAsync(string symbol)
    {
        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?range=1d&interval=1m";
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
        var result = doc.RootElement.GetProperty("chart").GetProperty("result")[0];
        var meta = result.GetProperty("meta");

        var points = new List<PricePoint>();
        if (result.TryGetProperty("timestamp", out var times))
        {
            var closes = result.GetProperty("indicators").GetProperty("quote")[0].GetProperty("close");
            for (var i = 0; i < times.GetArrayLength() && i < closes.GetArrayLength(); i++)
                if (closes[i].ValueKind == JsonValueKind.Number)
                    points.Add(new PricePoint(DateTimeOffset.FromUnixTimeSeconds(times[i].GetInt64()), closes[i].GetDouble()));
        }

        var price = Number(meta, "regularMarketPrice") ?? points.LastOrDefault().Price;
        // Change since yesterday's close, like Yahoo Finance shows it
        var changeBase = Number(meta, "chartPreviousClose") ?? Number(meta, "previousClose") ?? price;

        // The latest price can be newer than the last 1-minute point: add it so the line moves live
        if (meta.TryGetProperty("regularMarketTime", out var marketTime) && marketTime.ValueKind == JsonValueKind.Number)
        {
            var t = DateTimeOffset.FromUnixTimeSeconds(marketTime.GetInt64());
            if (points.Count == 0 || t > points[^1].Time)
                points.Add(new PricePoint(t, price));
            else if (t == points[^1].Time)
                points[^1] = new PricePoint(t, price);
        }

        var open = false;
        if (meta.TryGetProperty("currentTradingPeriod", out var period) && period.TryGetProperty("regular", out var regular))
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            open = now >= regular.GetProperty("start").GetInt64() && now < regular.GetProperty("end").GetInt64();
        }
        if (meta.TryGetProperty("instrumentType", out var type) && type.GetString() == "CRYPTOCURRENCY")
            open = true; // crypto trades all the time

        return new StockQuote(
            Text(meta, "symbol") ?? symbol,
            Text(meta, "shortName") ?? Text(meta, "longName") ?? symbol,
            Text(meta, "currency") ?? "",
            Text(meta, "fullExchangeName") ?? Text(meta, "exchangeName") ?? "",
            price, changeBase, points, open, DateTimeOffset.Now);
    }

    /// <summary>Stocks, ETFs, indexes and crypto matching a name or symbol (e.g. "apple", "TSLA", "bitcoin").</summary>
    public static async Task<IReadOnlyList<StockMatch>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var url = $"https://query2.finance.yahoo.com/v1/finance/search?q={Uri.EscapeDataString(query.Trim())}&quotesCount=8&newsCount=0";
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
        var matches = new List<StockMatch>();
        if (!doc.RootElement.TryGetProperty("quotes", out var quotes)) return matches;
        foreach (var q in quotes.EnumerateArray())
        {
            var kind = Text(q, "quoteType") ?? "";
            if (kind is not ("EQUITY" or "ETF" or "INDEX" or "CRYPTOCURRENCY" or "MUTUALFUND")) continue; // skip futures, options...
            var symbol = Text(q, "symbol");
            if (symbol is null) continue;
            var name = System.Text.RegularExpressions.Regex.Replace(Text(q, "shortname") ?? Text(q, "longname") ?? symbol, @"\s+", " ").Trim();
            matches.Add(new StockMatch(symbol, name, Text(q, "exchDisp") ?? "", KindLabel(kind)));
        }
        return matches;
    }

    private static string KindLabel(string kind) => kind switch
    {
        "EQUITY" => "Stock",
        "CRYPTOCURRENCY" => "Crypto",
        "INDEX" => "Index",
        "MUTUALFUND" => "Fund",
        _ => kind,
    };

    /// <summary>"$359.05", "€12.34", or "1,234.50 CHF".</summary>
    public static string FormatPrice(double value, string currency)
    {
        var digits = Math.Abs(value) >= 1000 ? 0 : Math.Abs(value) >= 1 ? 2 : 4;
        var number = value.ToString("N" + digits, CultureInfo.InvariantCulture);
        return currency switch
        {
            "USD" => "$" + number,
            "EUR" => "€" + number,
            "GBP" => "£" + number,
            "JPY" => "¥" + number,
            "" => number,
            _ => number + " " + currency,
        };
    }

    private static double? Number(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Widglass");
        return http;
    }
}
