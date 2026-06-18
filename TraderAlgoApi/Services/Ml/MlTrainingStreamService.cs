using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Data;
using TraderAlgoApi.Dtos.Charts;
using TraderAlgoApi.Dtos.Ml;
using TraderAlgoApi.Models;

namespace TraderAlgoApi.Services.Ml;

/// <summary>
/// Streams a trained model's decision process over a WebSocket. The OHLCV+indicator
/// candles come from the database (the source of truth); the per-candle model decisions
/// come from the Python service's persisted training decision log. The two are zipped by
/// candle open-time and emitted in order, mirroring the backtest replay stream.
/// </summary>
public sealed class MlTrainingStreamService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IMlConnectorService mlConnector,
    ILogger<MlTrainingStreamService> logger) : IMlTrainingStreamService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan CandleInterval = TimeSpan.FromMilliseconds(100);

    public async Task StreamAsync(
        HttpContext context,
        long trainingRunId,
        bool delay = false,
        CancellationToken cancellationToken = default)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            await context.Response.WriteAsync(
                "This endpoint requires a WebSocket connection.",
                cancellationToken);
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // The run record is the source of truth for the candle range (symbol/interval/from/to).
        var run = await dbContext.MlTrainingRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == trainingRunId, cancellationToken);
        if (run is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync($"Training run {trainingRunId} not found.", cancellationToken);
            return;
        }

        var decisionLog = await mlConnector.GetTrainingDecisionsAsync(trainingRunId, cancellationToken);
        if (decisionLog is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync(
                $"No training decision log for run {trainingRunId} yet. Wait for it to complete.",
                cancellationToken);
            return;
        }

        var candles = await dbContext.KlineData
            .AsNoTracking()
            .Where(k => k.SymbolId == run.SymbolId &&
                        k.IntervalId == run.IntervalId &&
                        k.OpenTime >= run.From &&
                        k.OpenTime <= run.To)
            .Include(k => k.SimpleMovingAverage)
            .Include(k => k.RelativeStrengthIndex)
            .Include(k => k.Macd)
            .OrderBy(k => k.OpenTime)
            .ToListAsync(cancellationToken);

        // Index decisions by candle open-time (unix seconds) for O(1) alignment.
        var decisionsByTime = decisionLog.Decisions
            .Where(d => d.OpenTime.HasValue)
            .GroupBy(d => d.OpenTime!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        using var clientSocket = await context.WebSockets.AcceptWebSocketAsync();

        using var disconnectCts = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disconnectCts.Token);
        var monitorTask = MonitorClientAsync(clientSocket, disconnectCts, linked.Token);

        try
        {
            foreach (var candle in candles)
            {
                var time = candle.OpenTime.ToUnixTimeSeconds();

                var candlePayload = JsonSerializer.SerializeToUtf8Bytes(
                    new MlTrainingStreamMessageDto<CandleWithIndicatorsResponseDto>("candle", ToDto(candle)),
                    JsonOptions);
                await clientSocket.SendAsync(
                    candlePayload, WebSocketMessageType.Text, endOfMessage: true, linked.Token);

                if (decisionsByTime.TryGetValue(time, out var decision))
                {
                    var decisionDto = new MlDecisionDto(
                        Time:       time,
                        Action:     decision.Action,
                        ActionName: decision.ActionName,
                        Confidence: decision.Confidence,
                        Probs:      decision.Probs,
                        Position:   decision.Position,
                        Balance:    decision.Balance);

                    var decisionPayload = JsonSerializer.SerializeToUtf8Bytes(
                        new MlTrainingStreamMessageDto<MlDecisionDto>("mlDecision", decisionDto),
                        JsonOptions);
                    await clientSocket.SendAsync(
                        decisionPayload, WebSocketMessageType.Text, endOfMessage: true, linked.Token);
                }

                if (delay)
                    await Task.Delay(CandleInterval, linked.Token);
            }

            if (clientSocket.State == WebSocketState.Open)
            {
                await clientSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Training replay completed.",
                    CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (disconnectCts.IsCancellationRequested)
        {
            logger.LogInformation("Training replay for run {RunId} cancelled: client disconnected", trainingRunId);
        }
        finally
        {
            await monitorTask;
        }
    }

    private static CandleWithIndicatorsResponseDto ToDto(KlineData k) =>
        new(
            k.OpenTime.ToUnixTimeSeconds(),
            k.Open, k.High, k.Low, k.Close, k.Volume,
            k.TakerBuyBaseAssetVolume,
            k.Volume - k.TakerBuyBaseAssetVolume,
            k.SimpleMovingAverage?.Sma20,
            k.SimpleMovingAverage?.Sma100,
            k.RelativeStrengthIndex?.Rsi,
            k.RelativeStrengthIndex?.RsiSmooth,
            k.RelativeStrengthIndex?.Divergence,
            k.Macd?.MacdLine,
            k.Macd?.SignalLine,
            k.Macd?.Histogram);

    private static async Task MonitorClientAsync(
        WebSocket socket,
        CancellationTokenSource disconnectCts,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[256];
        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await disconnectCts.CancelAsync();
                    break;
                }
            }
        }
        catch
        {
            await disconnectCts.CancelAsync();
        }
    }
}
