# TraderAlgoAPI

ASP.NET Core backend for algorithmic trading. It ingests live and historical market data
(Binance crypto + Alpaca equities), computes indicators, runs strategy-driven trade bots and
backtests, and streams everything to an Angular frontend over WebSockets. It can also call out
to **Kronos** for AI candle forecasting and an **ML policy** sidecar for model-driven entries.

**Stack:** C# · .NET 10 · ASP.NET Core · EF Core · PostgreSQL (Supabase)

## Contents

- [Architecture](#architecture)
- [How it works](#how-it-works)
- [Trading strategies](#trading-strategies)
- [Trade bots](#trade-bots)
- [Backtests](#backtests)
- [API reference](#api-reference)
- [Running locally](#running-locally)
- [Kronos forecasting](#kronos-forecasting)

---

## Architecture

| Layer | Technology | Hosting |
|---|---|---|
| Frontend | Angular | Vercel |
| Backend API | C# / .NET 10 | Render |
| Database | PostgreSQL | Supabase |
| Forecast service (optional) | FastAPI + Kronos | Modal.com |

Market data flows in from two providers behind a common `IMarketDataProvider` abstraction:

- **Binance** — crypto (e.g. `BTCUSDT`), 24/7 WebSocket stream.
- **Alpaca** — US equities (e.g. `SPY`), streamed during market hours, polled for higher intervals.

---

## How it works

All real-time work is decoupled through two singleton event buses:

- **`PriceFeed`** — every price tick.
- **`ClosedCandleFeed`** — every closed candle (after indicators are computed).

Producers publish to these buses; consumers subscribe. The main pipelines:

| Pipeline | Trigger | What it does |
|---|---|---|
| **Live ingest** | Provider WebSocket frame | Publishes ticks; upserts closed candles, computes SMA/RSI/MACD, forwards to `ClosedCandleFeed`. Auto-reconnects on drop. |
| **Price monitor** | `PriceFeed` tick | Fills pending limit orders and triggers SL/TP on live trades. |
| **Bot evaluation** | `ClosedCandleFeed` candle | Evaluates each enabled bot's strategy and opens/closes trades. |
| **Trade events** | Any service | Pub/sub bus pushing per-account trade events to WebSocket clients. |
| **Live charts** | Client WebSocket | Streams candles (optionally enriched with indicators) to the frontend. |
| **Backtest stream** | Client WebSocket | Replays historical candles through a strategy (see [Backtests](#backtests)). |
| **Daily collection** | Midnight UTC timer | Full upsert of every active `symbol × interval`, recomputing indicators. |

Indicators (SMA20/SMA100, RSI(14) + smoothed, MACD) are stored alongside each candle and
recomputed incrementally whenever a candle is inserted or updated.

---

## Trading strategies

Each strategy decides `shouldEnterLong` / `shouldEnterShort` from the latest candles and their
indicators. **All rules in a strategy must be true to enter.** A fifth strategy, **ML Policy**,
delegates the entry decision to an external model sidecar.

### SMA — trend retest
Fast **SMA20** vs slow **SMA100**.

| Rule | Long | Short |
|---|---|---|
| Trend filter | SMA20 > SMA100 | SMA20 < SMA100 |
| Retest wick | candle wick touches SMA20 | candle wick touches SMA20 |
| Retest close | close above SMA20 | close below SMA20 |
| Last 3 closes | all above their SMA20 | all below their SMA20 |

### RSI — momentum reversal
**RSI(14)** vs a smoothed RSI signal line.

| Rule | Long | Short |
|---|---|---|
| Oversold / overbought | RSI < 30 | RSI > 70 |
| Momentum confirm | RSI above smoothed RSI | RSI below smoothed RSI |

### MACD — momentum exhaustion
Enters as momentum fades, **before** a full crossover.

| Rule | Long | Short |
|---|---|---|
| Line relationship | MACD below signal | MACD above signal |
| Histogram side | below zero | above zero |
| Histogram direction | rising toward zero | falling toward zero |

### SMA MACD — trend + momentum
SMA sets directional bias; MACD confirms timing.

| Rule | Long | Short |
|---|---|---|
| Trend filter | SMA20 above SMA100 | SMA20 below SMA100 |
| Price location | close above SMA20 | close below SMA20 |
| MACD line | above zero | below zero |
| Histogram side / direction | below zero & rising | above zero & falling |

---

## Trade bots

A trade bot watches one `symbol × interval` and trades a strategy automatically against a linked
**trading account**. `TradeBotMonitorService` evaluates every enabled bot on each candle close.

Key fields: `tradingStrategyId`, `symbol`/`interval`, `quantity`, `stopLoss`/`takeProfit`,
`breakeven` + `breakevenStop`, `fee`, `isNySessionOnly`, `dailyProfitGoal`, `maxLossesPerDay`,
`maxCandlesPerTrade`, `isEnabled`.

Rules: only **one active trade per bot** (new signals are ignored while a position is open); an
opposite-direction signal closes the current trade; enabling a bot that already has an active
trade on its account is rejected to avoid double entry.

---

## Backtests

A backtest replays historical candles through a strategy and records every simulated trade, the
equity curve, drawdowns, and aggregate PnL — without touching a real account. Each backtest owns
a template trade bot that holds its strategy and risk settings.

**Lifecycle**

1. `POST /api/backtests` creates the record as `Pending`.
2. Connect to `WS /ws/charts/backtest?backtestId={id}` to start execution → `Running`.
   Add `&delay=true` to pace candles at 100 ms each for live visualisation.
3. Per candle the server checks SL/TP, the breakeven trigger, and the entry signal, then emits a
   `candle` frame. A `tradeBracketUpdate` frame is sent when breakeven moves the stop-loss.
4. When all candles are processed → `Completed` (socket closes normally). Disconnecting early →
   `Cancelled`; an error → `Failed`. The linked bot is disabled on any terminal status.

**Simulation rules**

- One trade open at a time.
- Stop-loss is checked before take-profit; if both hit in one candle, SL wins (conservative).
- `breakeven` moves the stop to entry (or to `breakevenStop`) once unrealised PnL hits the threshold.
- Any trade still open at the end of the range is force-closed at the last close.
- **Resumable:** reconnecting picks up from the last persisted progress instead of re-running everything.

---

## API reference

REST base path `/api`. Enums (side, status, strategy, etc.) serialize as strings.

| Area | Endpoints |
|---|---|
| **Trading accounts** | `POST/GET /trading-accounts` · `GET/PATCH/DELETE /trading-accounts/{id}` |
| **Trade bots** | `POST/GET /tradebots` · `GET/PATCH/DELETE /tradebots/{id}` · `POST /tradebots/{id}/enable` · `/disable` |
| **Backtests** | `POST/GET /backtests` · `GET/DELETE /backtests/{id}` |
| **Trades** | `POST /trades` · `POST /trades/{id}/stop` · `PATCH /trades/{id}` · `GET /trades/account/{id}/active` · `/history` · `GET /trades/backtest/{id}` |
| **Rules** | `GET /rules/{sma\|rsi\|macd}/evaluate?symbol=&interval=` |
| **Charts** | `GET /charts/candles?symbol=&interval=&lookback=` |
| **Symbols / Intervals** | `GET /symbols` · `GET /intervals` |
| **Data collector** | `POST /data-collector/{symbol}/{interval}` · `POST /data-collector/full-sync` |
| **Kronos** | `GET /kronos/...` (candle forecasts) |
| **Health** | `GET /health` (checks DB connectivity) |

**WebSocket endpoints**

| Endpoint | Purpose |
|---|---|
| `WS /ws/charts/candles?symbol=&interval=` | Live candles |
| `WS /ws/charts/candleswithindicators?symbol=&interval=` | Live candles + SMA/RSI/MACD |
| `WS /ws/charts/backtest?backtestId=&delay=` | Backtest replay |
| `WS /ws/tradebots/events?tradingAccountId=` | Live trade events |

Errors return RFC 7807 ProblemDetails: `400` invalid input, `404` not found, `409` conflict,
`503` upstream (Kronos/ML) unavailable.

---

## Running locally

Requires the **.NET 10 SDK** and a PostgreSQL database (Supabase or local).

1. Set secrets (the connection string and provider keys are **not** committed — `appsettings.json`
   holds placeholders only):

   ```bash
   cd TraderAlgoApi
   dotnet user-secrets set "ConnectionStrings:Supabase" "Host=...;Database=postgres;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true"
   dotnet user-secrets set "Alpaca:ApiKey" "<key>"
   dotnet user-secrets set "Alpaca:SecretKey" "<secret>"
   ```

2. Apply migrations and run:

   ```bash
   dotnet ef database update
   dotnet run
   ```

3. Open Swagger at `https://localhost:7096/swagger` (Development only).

Optional sidecars (base URLs configurable under `Kronos:` and `MlPolicy:`): the Kronos forecast
service and the ML policy service. The API runs without them — only the related endpoints/strategy
are affected.

---

## Kronos forecasting

[Kronos](https://github.com/shiyu-coder/Kronos) is an open-source foundation model for financial
time-series forecasting — a GPT-style autoregressive Transformer that predicts the next OHLCV
candle instead of the next word.

The API does **not** host Kronos directly. It calls a separate **Kronos Connector** (a FastAPI
wrapper around the model, recommended on Modal.com for serverless GPU) over HTTP, configured via
`Kronos:BaseUrl`. Keep the connector in its own repo with the upstream Kronos repo as a read-only
git submodule. The `GET /api/kronos/...` endpoints return forecast candles for charting.
