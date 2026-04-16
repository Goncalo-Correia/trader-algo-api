using System.Globalization;
using System.Text.Json;

namespace TraderAlgoApi.Services.Binance;

public sealed record BinanceKline(
    DateTimeOffset OpenTime,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume,
    DateTimeOffset CloseTime,
    decimal QuoteAssetVolume,
    int NumberOfTrades,
    decimal TakerBuyBaseAssetVolume,
    decimal TakerBuyQuoteAssetVolume)
{
    internal static BinanceKline FromJsonArray(JsonElement kline)
    {
        if (kline.ValueKind is not JsonValueKind.Array || kline.GetArrayLength() < 11)
        {
            throw new JsonException("Unexpected Binance kline payload.");
        }

        return new BinanceKline(
            OpenTime: DateTimeOffset.FromUnixTimeMilliseconds(kline[0].GetInt64()),
            Open: GetDecimal(kline[1]),
            High: GetDecimal(kline[2]),
            Low: GetDecimal(kline[3]),
            Close: GetDecimal(kline[4]),
            Volume: GetDecimal(kline[5]),
            CloseTime: DateTimeOffset.FromUnixTimeMilliseconds(kline[6].GetInt64()),
            QuoteAssetVolume: GetDecimal(kline[7]),
            NumberOfTrades: kline[8].GetInt32(),
            TakerBuyBaseAssetVolume: GetDecimal(kline[9]),
            TakerBuyQuoteAssetVolume: GetDecimal(kline[10]));
    }

    private static decimal GetDecimal(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String when decimal.TryParse(
                value.GetString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            JsonValueKind.Number => value.GetDecimal(),
            _ => throw new JsonException("Unexpected decimal value in Binance kline payload.")
        };
    }
}
