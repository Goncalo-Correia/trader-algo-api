using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Charts;
using TraderAlgoApi.Services.Indicators;
using TraderAlgoApi.Services.MarketData;
using TraderAlgoApi.Services.PriceFeeds;

namespace TraderAlgoApi.Services.Charts;

/// <summary>
/// Streams live candle data to frontend WebSocket clients by subscribing to the
/// shared PriceFeed and ClosedCandleFeed singletons.  Works for both Binance and
/// Alpaca symbols without any provider-specific logic.
/// </summary>
public sealed class LiveChartDataService(
    PriceFeed                  priceFeed,
    ClosedCandleFeed           closedCandleFeed,
    CandleAggregator           aggregator,
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ISimpleMovingAverageService smaService,
    IRsiService                rsiService,
    IMacdService               macdService) : ILiveChartDataService
{
    private const int WindowSize = 200;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task StreamCandlesAsync(
        HttpContext context,
        string? symbol   = null,
        string? interval = null,
        CancellationToken cancellationToken = default) =>
        StreamAsync(context, symbol, interval, withIndicators: false, cancellationToken);

    public Task StreamCandlesWithIndicatorsAsync(
        HttpContext context,
        string? symbol   = null,
        string? interval = null,
        CancellationToken cancellationToken = default) =>
        StreamAsync(context, symbol, interval, withIndicators: true, cancellationToken);

    private async Task StreamAsync(
        HttpContext context,
        string? symbol,
        string? interval,
        bool withIndicators,
        CancellationToken cancellationToken)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            await context.Response.WriteAsync(
                "This endpoint requires a WebSocket connection.", cancellationToken);
            return;
        }

        string streamSymbol;
        string streamInterval;
        List<decimal>? closes = null;

        // Use a short-lived context only for the up-front reads, then release it before the
        // (potentially very long) streaming loop — which talks only to the in-memory feeds.
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var (resolvedSymbol, resolvedInterval, valid) =
                await ResolveParamsAsync(dbContext, symbol, interval, cancellationToken);

            if (!valid)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                var validCodes = await dbContext.Intervals
                    .Where(i => i.IsActive)
                    .OrderBy(i => i.Duration)
                    .Select(i => i.Code)
                    .ToListAsync(cancellationToken);
                await context.Response.WriteAsync(
                    $"Interval must be one of: {string.Join(", ", validCodes)}.", cancellationToken);
                return;
            }

            streamSymbol   = resolvedSymbol;
            streamInterval = resolvedInterval;

            if (withIndicators)
                closes = await LoadRecentClosesAsync(dbContext, streamSymbol, streamInterval, WindowSize, cancellationToken);
        }

        using var clientSocket = await context.WebSockets.AcceptWebSocketAsync();

        await StreamFromFeedsAsync(
            clientSocket, streamSymbol, streamInterval, withIndicators, closes, cancellationToken);
    }

    // ── Core streaming loop ───────────────────────────────────────────────────

    private async Task StreamFromFeedsAsync(
        WebSocket         clientSocket,
        string            symbol,
        string            intervalCode,
        bool              withIndicators,
        List<decimal>?    closes,
        CancellationToken ct)
    {
        // Subscribe to price ticks.
        void OnTick(string tickSymbol, decimal price)
        {
            if (tickSymbol != symbol) return;
            aggregator.OnTick(symbol, intervalCode, price, DateTimeOffset.UtcNow);
        }

        // Subscribe to closed candles to keep the rolling close window current.
        void OnClosed(ClosedCandleEvent e)
        {
            if (e.Symbol != symbol || e.Interval != intervalCode) return;
            if (withIndicators && closes is not null)
            {
                closes.Add(e.Close);
                if (closes.Count > WindowSize)
                    closes.RemoveAt(0);
            }
        }

        priceFeed.TickReceived    += OnTick;
        closedCandleFeed.CandleClosed += OnClosed;

        try
        {
            while (!ct.IsCancellationRequested &&
                   clientSocket.State == WebSocketState.Open)
            {
                // Wait for the next tick (via a polling loop over the aggregator).
                // This avoids a complex async-event-to-channel bridge while keeping
                // the send rate reasonable for the frontend.
                await Task.Delay(250, ct);

                var partial = aggregator.GetPartial(symbol, intervalCode);
                if (partial is null) continue;

                byte[] payload;

                if (withIndicators && closes is not null && closes.Count > 0)
                {
                    var lastIdx  = closes.Count - 1;
                    var (sma20, sma100)              = smaService.Compute(closes, lastIdx);
                    var rsiValues                     = rsiService.ComputeAll(closes);
                    var rsi                           = rsiValues[lastIdx];
                    var rsiSmooth                     = rsiService.ComputeSmooth(rsiValues, lastIdx);
                    var divergence                    = rsiService.DetectDivergence(closes, rsiValues, lastIdx);
                    var macdValues                    = macdService.ComputeAll(closes);
                    var (macdLine, signalLine, histogram) = macdValues[lastIdx];

                    payload = JsonSerializer.SerializeToUtf8Bytes(
                        new CandleWithIndicatorsResponseDto(
                            partial.OpenTime.ToUnixTimeSeconds(),
                            partial.Open, partial.High, partial.Low, partial.Close,
                            partial.Volume,
                            TakerBuyBaseAssetVolume:  0m,
                            TakerSellBaseAssetVolume: 0m,
                            sma20, sma100,
                            rsi, rsiSmooth, divergence,
                            macdLine, signalLine, histogram),
                        JsonOptions);
                }
                else
                {
                    payload = JsonSerializer.SerializeToUtf8Bytes(
                        new CandleResponseDto(
                            partial.OpenTime.ToUnixTimeSeconds(),
                            partial.Open, partial.High, partial.Low, partial.Close,
                            partial.Volume,
                            BuyVolume:  0m,
                            SellVolume: 0m),
                        JsonOptions);
                }

                await clientSocket.SendAsync(
                    payload, WebSocketMessageType.Text, endOfMessage: true, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (WebSocketException ex)
        {
            // Client disconnected — not an error.
            _ = ex;
        }
        finally
        {
            priceFeed.TickReceived        -= OnTick;
            closedCandleFeed.CandleClosed -= OnClosed;

            if (clientSocket.State == WebSocketState.Open)
            {
                await clientSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "Stream ended.", CancellationToken.None);
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<(string Symbol, string Interval, bool Valid)> ResolveParamsAsync(
        ApplicationDbContext dbContext, string? symbol, string? interval, CancellationToken ct)
    {
        var s = string.IsNullOrWhiteSpace(symbol)
            ? await dbContext.Symbols.Where(x => x.IsDefault).Select(x => x.Code).FirstOrDefaultAsync(ct) ?? string.Empty
            : symbol;

        var i = string.IsNullOrWhiteSpace(interval)
            ? await dbContext.Intervals.Where(x => x.IsDefault).Select(x => x.Code).FirstOrDefaultAsync(ct) ?? string.Empty
            : interval;

        var valid = await dbContext.Intervals.AnyAsync(x => x.IsActive && x.Code == i, ct);

        return (s, i, valid);
    }

    private static async Task<List<decimal>> LoadRecentClosesAsync(
        ApplicationDbContext dbContext, string symbol, string intervalCode, int count, CancellationToken ct)
    {
        var closes = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.Symbol.Code   == symbol
                     && k.Interval.Code == intervalCode)
            .OrderByDescending(k => k.OpenTime)
            .Take(count)
            .OrderBy(k => k.OpenTime)
            .Select(k => k.Close)
            .ToListAsync(ct);

        return closes;
    }
}
