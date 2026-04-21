using System.Globalization;
using System.Text.Json;

namespace TraderAlgoApi.Services.Binance;

internal sealed record BinanceStreamKline(
    string Symbol,
    string Interval,
    long EventTime,
    long Time,
    DateTimeOffset OpenTime,
    DateTimeOffset CloseTime,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume,
    decimal QuoteAssetVolume,
    int NumberOfTrades,
    decimal TakerBuyBaseAssetVolume,
    decimal TakerBuyQuoteAssetVolume,
    bool IsClosed)
{
    internal static BinanceStreamKline? FromJson(byte[] message)
    {
        using var document = JsonDocument.Parse(message);
        var root = document.RootElement;

        if (!root.TryGetProperty("k", out var kline))
            return null;

        var openTime = kline.GetProperty("t").GetInt64();
        var closeTime = kline.GetProperty("T").GetInt64();

        return new BinanceStreamKline(
            Symbol: kline.GetProperty("s").GetString() ?? string.Empty,
            Interval: kline.GetProperty("i").GetString() ?? string.Empty,
            EventTime: root.GetProperty("E").GetInt64(),
            Time: DateTimeOffset.FromUnixTimeMilliseconds(openTime).ToUnixTimeSeconds(),
            OpenTime: DateTimeOffset.FromUnixTimeMilliseconds(openTime),
            CloseTime: DateTimeOffset.FromUnixTimeMilliseconds(closeTime),
            Open: GetDecimal(kline, "o"),
            High: GetDecimal(kline, "h"),
            Low: GetDecimal(kline, "l"),
            Close: GetDecimal(kline, "c"),
            Volume: GetDecimal(kline, "v"),
            QuoteAssetVolume: GetDecimal(kline, "q"),
            NumberOfTrades: kline.GetProperty("n").GetInt32(),
            TakerBuyBaseAssetVolume: GetDecimal(kline, "V"),
            TakerBuyQuoteAssetVolume: GetDecimal(kline, "Q"),
            IsClosed: kline.GetProperty("x").GetBoolean());
    }

    private static decimal GetDecimal(JsonElement element, string propertyName) =>
        decimal.Parse(
            element.GetProperty(propertyName).GetString() ?? "0",
            NumberStyles.Number,
            CultureInfo.InvariantCulture);
}
