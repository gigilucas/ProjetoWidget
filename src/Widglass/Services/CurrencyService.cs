using System.Globalization;

namespace Widglass.Services;

/// <summary>A currency the converter offers.</summary>
public sealed record CurrencyInfo(string Code, string Name, string Symbol, bool IsCrypto = false);

/// <summary>Live exchange rate between two currencies, with today's chart.</summary>
public sealed record ExchangeRate(
    string From, string To, double Rate, double ChangeBase, IReadOnlyList<PricePoint> Points, bool MarketOpen)
{
    public double Change => Rate - ChangeBase;
    public double ChangePercent => ChangeBase == 0 ? 0 : Change / ChangeBase * 100;
}

/// <summary>
/// Exchange rates from Yahoo Finance's public chart data (the same live source as the Stocks widget).
/// Currencies trade around the clock on weekdays; crypto every day.
/// </summary>
public static class CurrencyService
{
    public static readonly IReadOnlyList<CurrencyInfo> All =
    [
        new("EUR", "Euro", "€"),
        new("USD", "US Dollar", "$"),
        new("GBP", "British Pound", "£"),
        new("BRL", "Brazilian Real", "R$"),
        new("CHF", "Swiss Franc", "CHF "),
        new("JPY", "Japanese Yen", "¥"),
        new("CAD", "Canadian Dollar", "CA$"),
        new("AUD", "Australian Dollar", "A$"),
        new("CNY", "Chinese Yuan", "CN¥"),
        new("INR", "Indian Rupee", "₹"),
        new("MXN", "Mexican Peso", "MX$"),
        new("AOA", "Angolan Kwanza", "Kz "),
        new("BTC", "Bitcoin", "₿", IsCrypto: true),
        new("ETH", "Ethereum", "Ξ", IsCrypto: true),
    ];

    public static CurrencyInfo Info(string code) =>
        All.FirstOrDefault(c => c.Code == code) ?? new CurrencyInfo(code, code, code + " ");

    /// <summary>How much one <paramref name="from"/> is worth in <paramref name="to"/>, live.</summary>
    public static async Task<ExchangeRate> GetRateAsync(string from, string to)
    {
        if (from == to)
            return new ExchangeRate(from, to, 1, 1, [], true);

        // Straight pair, the other way round (inverted), or through the US dollar
        if (await TryPairAsync(from, to) is { } rate)
            return rate;
        if (from != "USD" && to != "USD" &&
            await TryPairAsync(from, "USD") is { } toUsd && await TryPairAsync("USD", to) is { } fromUsd)
            return Cross(toUsd, fromUsd);
        throw new InvalidOperationException($"No rate for {from} to {to}.");
    }

    private static async Task<ExchangeRate?> TryPairAsync(string from, string to)
    {
        var fromCrypto = Info(from).IsCrypto;
        var toCrypto = Info(to).IsCrypto;
        // Yahoo symbols: "EURUSD=X" for money, "BTC-EUR" for crypto
        var direct = fromCrypto ? $"{from}-{to}" : toCrypto ? null : $"{from}{to}=X";
        var inverse = toCrypto ? $"{to}-{from}" : fromCrypto ? null : $"{to}{from}=X";

        if (direct is not null && await TryQuoteAsync(direct) is { } q)
            return new ExchangeRate(from, to, q.Price, q.ChangeBase, q.Points, q.MarketOpen);
        if (inverse is not null && await TryQuoteAsync(inverse) is { } r && r.Price > 0)
            return new ExchangeRate(from, to, 1 / r.Price, r.ChangeBase > 0 ? 1 / r.ChangeBase : 0,
                r.Points.Where(p => p.Price > 0).Select(p => p with { Price = 1 / p.Price }).ToList(), r.MarketOpen);
        return null;
    }

    private static async Task<StockQuote?> TryQuoteAsync(string symbol)
    {
        try
        {
            var q = await StockService.GetQuoteAsync(symbol);
            return q.Price > 0 ? q : null;
        }
        catch (Exception)
        {
            return null; // pair not listed
        }
    }

    /// <summary>A→USD times USD→B; the chart follows A's points, using B's rate at each moment.</summary>
    private static ExchangeRate Cross(ExchangeRate a, ExchangeRate b)
    {
        var points = new List<PricePoint>();
        var j = 0;
        foreach (var p in a.Points)
        {
            while (j + 1 < b.Points.Count && b.Points[j + 1].Time <= p.Time) j++;
            var other = b.Points.Count > 0 ? b.Points[j].Price : b.Rate;
            points.Add(p with { Price = p.Price * other });
        }
        return new ExchangeRate(a.From, b.To, a.Rate * b.Rate, a.ChangeBase * b.ChangeBase, points, a.MarketOpen || b.MarketOpen);
    }

    /// <summary>"€100.00", "R$548.12", "¥15,820", "₿0.00182".</summary>
    public static string FormatMoney(double value, string code)
    {
        var info = Info(code);
        var abs = Math.Abs(value);
        var format = code == "JPY" && abs >= 1 ? "N0"
            : abs >= 1 || abs == 0 ? "N2"
            : abs >= 0.01 ? "N4"
            : "0.########";
        return info.Symbol + value.ToString(format, CultureInfo.InvariantCulture);
    }

    /// <summary>A rate with enough digits to see it move: 1.0942, 101.75, 0.00001826.</summary>
    public static string FormatRate(double rate)
    {
        var abs = Math.Abs(rate);
        var format = abs >= 1000 ? "N2" : abs >= 100 ? "N3" : abs >= 0.01 ? "N4" : "0.########";
        return rate.ToString(format, CultureInfo.InvariantCulture);
    }
}
