namespace TraderAlgoApi.Services.MarketData.Alpaca;

internal static class AlpacaIntervalMapper
{
    /// <summary>Maps the app's interval code to Alpaca's timeframe parameter.</summary>
    internal static string ToAlpacaTimeframe(string intervalCode) => intervalCode switch
    {
        "1m"  => "1Min",
        "5m"  => "5Min",
        "15m" => "15Min",
        "1h"  => "1Hour",
        "4h"  => "4Hour",
        "1d"  => "1Day",
        _     => throw new ArgumentOutOfRangeException(nameof(intervalCode), intervalCode,
                     $"Interval code '{intervalCode}' has no Alpaca timeframe mapping.")
    };

    /// <summary>
    /// Returns true for intervals delivered via the Alpaca bars WebSocket stream
    /// (1-minute and daily). All others must be polled via REST.
    /// </summary>
    internal static bool IsStreamedByWebSocket(string intervalCode) =>
        intervalCode is "1m" or "1d";
}
