# TraderAlgoAPI

ASP.NET Core backend for algorithmic trading. It ingests live and historical market data
(Binance crypto), computes indicators, runs strategy-driven trade bots and
backtests, and streams everything to an Angular frontend over WebSockets. It can also call out
to **Kronos** for AI candle forecasting and an **ML policy** sidecar for model-driven entries.

**Stack:** C# · .NET 10 · ASP.NET Core · EF Core · PostgreSQL (Supabase)

## Contents

- [Architecture](#architecture)
- [Core runtime](#core-runtime)
- [Market data & live charts](#market-data--live-charts)
- [Trading strategies](#trading-strategies)
- [Trades](#trades)
- [Trading accounts](#trading-accounts)
- [Trade bots](#trade-bots)
- [Backtests](#backtests)
- [ML policies & training](#ml-policies--training)
- [Kronos forecasting](#kronos-forecasting)
- [Background sync jobs](#background-sync-jobs)
- [API reference](#api-reference)
- [Logging](#logging)
- [Authentication](#authentication)
- [Environment variables](#environment-variables)
- [Running locally](#running-locally)

---

## Architecture

| Layer | Technology | Hosting |
|---|---|---|
| Frontend | Angular | Vercel |
| Backend API | C# / .NET 10 | Render |
| Database | PostgreSQL | Supabase |
| Forecast service (optional) | FastAPI + Kronos | Modal.com |
| ML policy service (optional) | FastAPI + PPO (stable-baselines3) | local / container |

Market data flows in from a single provider behind a common `IMarketDataProvider` abstraction
(kept provider-neutral so additional providers can be added later):

- **Binance** — crypto (e.g. `BTCUSDT`, `ETHUSDT`), 24/7 WebSocket stream.

```mermaid
flowchart LR
  Binance[(Binance)] --> Ingest
  Ingest[Live ingest] -->|ticks| PriceFeed((PriceFeed))
  Ingest -->|closed candles| ClosedFeed((ClosedCandleFeed))
  Ingest --> DB[(PostgreSQL)]
  PriceFeed --> TradeMon[Trade monitor SL/TP & limit fills]
  PriceFeed --> Charts[Live chart streams]
  ClosedFeed --> BotMon[Trade-bot evaluation]
  ClosedFeed --> Charts
  TradeMon --> DB
  BotMon --> DB
  Charts -->|WebSocket| UI[Angular UI]
  DB --> REST[REST API] --> UI
  REST -.HTTP.-> Kronos[Kronos sidecar]
  REST -.HTTP.-> ML[ML policy sidecar]
```

---

## Core runtime

All real-time work is decoupled through two singleton event buses:

- **`PriceFeed`** — every price tick.
- **`ClosedCandleFeed`** — every closed candle (after indicators are computed).

Producers publish to these buses; consumers subscribe. Background services bridge each feed into a
bounded `Channel` (`DropOldest`) so bursts never block ingestion. The main pipelines:

| Pipeline | Trigger | What it does |
|---|---|---|
| **Live ingest** | Provider WebSocket frame | Publishes ticks; upserts closed candles, computes SMA/RSI/MACD, forwards to `ClosedCandleFeed`. Auto-reconnects on drop. |
| **Price monitor** (`TradeMonitorService`) | `PriceFeed` tick | Fills pending limit orders and triggers SL/TP on active live trades. |
| **Bot evaluation** (`TradeBotMonitorService`) | `ClosedCandleFeed` candle | Evaluates each enabled bot's strategy and opens/closes trades. |
| **Trade events** (`ITradeEventPublisher`) | Any service | Pub/sub bus pushing per-account trade events to WebSocket clients. |
| **Live charts** (`LiveChartDataService`) | Client WebSocket | Streams candles (optionally enriched with indicators) to the frontend. |
| **Backtest stream** (`BacktestStreamService`) | Client WebSocket | Replays historical candles through a strategy (see [Backtests](#backtests)). |
| **Daily collection** (`DataCollectorTimer`) | Midnight UTC timer | Full upsert of every active `symbol × interval`, recomputing indicators. |

Indicators (SMA20/SMA100, RSI(14) + smoothed, MACD) are stored alongside each candle and
recomputed incrementally whenever a candle is inserted or updated.

---

## Market data & live charts

**Data.** Each `KlineData` row is one OHLCV candle for a `symbol × interval × openTime` (unique
index). Indicators live in one-to-one side tables (`SimpleMovingAverage`, `RelativeStrengthIndex`,
`Macd`) keyed on the candle id, so a candle and its indicators are written/recomputed together.

**Historical (REST).** `GET /api/charts/candles` and `GET /api/charts/candles/indicators` return the
last `lookback` candles for a symbol/interval straight from the database.
`GET /api/charts/candles/indicators/date-interval` returns the same indicator-enriched candles for an
explicit `from`/`to` date range instead of a lookback (the window spans `from` 00:00 to `to` 23:59 UTC).

**Live (WebSocket).** Two endpoints stream the **forming** candle:

| Endpoint | Payload |
|---|---|
| `WS /ws/charts/candles` | OHLCV only |
| `WS /ws/charts/candleswithindicators` | OHLCV + SMA20/100, RSI + smoothed + divergence, MACD |

Flow: the stream subscribes to `PriceFeed`; each tick is folded into the current bar by
`CandleAggregator`; every ~250 ms the latest partial bar is serialized and sent. For the indicator
variant a rolling window of the most recent 200 closes is kept in memory (seeded from the DB,
extended by `ClosedCandleFeed`) and indicators are computed on the fly.

```mermaid
sequenceDiagram
  participant UI
  participant Stream as LiveChartDataService
  participant PF as PriceFeed
  participant Agg as CandleAggregator
  UI->>Stream: WS connect (symbol, interval)
  Stream->>DB: seed 200 recent closes (indicator mode)
  loop every tick
    PF-->>Stream: tick(symbol, price)
    Stream->>Agg: fold tick into current bar
  end
  loop every ~250 ms
    Stream->>Agg: get partial bar
    Stream-->>UI: candle [+ indicators] frame
  end
```

---

## Trading strategies

Each strategy decides `shouldEnterLong` / `shouldEnterShort` from the latest candles and their
indicators. **All rules in a strategy must be true to enter.** A fifth strategy, **ML Policy**,
delegates the entry decision to an external model sidecar (see [ML policies](#ml-policies--training)).
Rules can be evaluated ad-hoc via `GET /api/rules/{sma|rsi|macd}/evaluate`.

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

## Trades

A **trade** is one position. It can belong to a live **trading account** (`tradingAccountId`) or to a
**backtest** (`backtestId`) — never both. Key fields: `side` (Buy/Sell), `orderType` (Market/Limit),
`status`, `quantity`, `requestedPrice`, `entryPrice`, `stopLoss`/`takeProfit`, `closedPrice`,
`closeReason`, `fee`, `pnl` (net of fee) and `accountPnl` (account balance after the trade).

**Lifecycle.** Market orders open `Active` immediately at the latest price; limit orders open
`Pending` and become `Active` when price reaches `requestedPrice`. SL/TP and limit fills are driven
by `TradeMonitorService`, which evaluates every `PriceFeed` tick (`EvaluatePriceAsync`). On close,
realized PnL (minus `fee`) is applied to the account's `currentBalance` and a trade event is
published.

```mermaid
stateDiagram-v2
  [*] --> Pending: limit order
  [*] --> Active: market order
  Pending --> Active: price hits requestedPrice
  Pending --> Cancelled: manual stop
  Active --> Closed: SL / TP hit
  Active --> Closed: manual stop / opposite bot signal
  Closed --> [*]
  Cancelled --> [*]
```

**Endpoints.** `POST /api/trades` (open), `POST /api/trades/{id}/stop` (close/cancel),
`PATCH /api/trades/{id}` (adjust SL/TP), `GET /api/trades/account/{id}/active` and `/history`,
`GET /api/trades/backtest/{id}`.

---

## Trading accounts

A **trading account** is a virtual balance that live trades and trade bots operate against. Fields:
`name`, `initialBalance`, `currentBalance`, `isActive`. `currentBalance` moves only when a trade
closes (realized PnL net of fees); unrealized PnL on open trades is computed on read, not persisted.

**Relationships.** One account → many trade bots and many trades. A bot must reference an active
account to run. Deleting an account cascades to its bots.

```mermaid
flowchart LR
  Account[Trading account<br/>currentBalance] --> Bot[Trade bot]
  Account --> Trades[Trades]
  Bot -->|opens / closes| Trades
  Trades -->|realized PnL on close| Account
```

**Endpoints.** `POST/GET /api/trading-accounts`, `GET/PATCH/DELETE /api/trading-accounts/{id}`.

---

## Trade bots

A trade bot watches one `symbol × interval` and trades a strategy automatically against a linked
trading account. `TradeBotMonitorService` evaluates every enabled bot on each candle close.

Key fields: `tradingStrategyId`, `mlPolicyId` (required when the strategy is **ML Policy**),
`symbol`/`interval`, `quantity`, `stopLoss`/`takeProfit`, `breakeven` + `breakevenStop`, `fee`,
`isNySessionOnly`, `delay`, `dailyProfitGoal`, `maxLossesPerDay`, `maxCandlesPerTrade`, `isEnabled`.

When `tradingStrategyId` is **ML Policy** the bot must reference an `mlPolicyId`: the policy's
symbol/interval must match the bot's, and its risk settings (SL/TP, breakeven, fee, daily limits)
are copied onto the bot. The entry signal then comes from the ML sidecar instead of an indicator
rule. Any other strategy rejects an `mlPolicyId`.

**Flow.** On each closed candle the monitor selects enabled bots for that symbol/interval, asks
`TradeBotSignalService` for a signal, and acts:

```mermaid
flowchart TD
  C[Closed candle] --> Sel{Enabled bots<br/>for symbol/interval}
  Sel --> Sig[Evaluate strategy signal]
  Sig -->|None| Skip[Ignore]
  Sig -->|Enter long/short| Pos{Open trade<br/>on account?}
  Pos -->|No| Open[Open market trade<br/>with bot SL/TP/fee]
  Pos -->|Yes, opposite side| Close[Close current trade<br/>reason = BotSignal]
  Pos -->|Yes, same side / pending| Ignore[Publish SignalIgnored]
```

Rules: only **one active trade per bot/account** (new same-direction signals are ignored while a
position is open); an opposite-direction signal closes the current trade; enabling a bot that
already has an active trade on its account is rejected to avoid double entry. Live trade events are
pushed to `WS /ws/tradebots/events?tradingAccountId=`.

---

## Backtests

A backtest replays historical candles through a strategy and records every simulated trade, the
equity curve, drawdowns, and aggregate PnL — without touching a real account. Each backtest owns
a template trade bot that holds its strategy and risk settings. The strategy can be any of the
indicator strategies or **ML Policy** — pass `mlPolicyId` and the risk settings are taken from the
policy (each candle's entry decision is fetched from the ML sidecar).

```mermaid
sequenceDiagram
  participant UI
  participant API
  participant Stream as BacktestStreamService
  UI->>API: POST /api/backtests (Pending)
  UI->>Stream: WS /ws/charts/backtest?backtestId=&delay=
  Note over Stream: status -> Running
  loop each candle
    Stream->>Stream: check SL/TP, breakeven, entry signal
    Stream-->>UI: candle frame
    Stream-->>UI: tradeBracketUpdate (on breakeven)
  end
  Note over Stream: all candles -> Completed (socket closes)
```

**Lifecycle.** `Pending → Running → Completed` (clean socket close). Disconnecting early →
`Cancelled`; an error → `Failed`. The linked bot is disabled on any terminal status.

**Simulation rules.**
- One trade open at a time.
- Stop-loss is checked before take-profit; if both hit in one candle, SL wins (conservative).
- `breakeven` moves the stop to entry (or to `breakevenStop`) once unrealised PnL hits the threshold.
- Any trade still open at the end of the range is force-closed at the last close.
- **Resumable:** reconnecting picks up from the last persisted progress instead of re-running everything.

---

## ML policies & training

The **ML Policy** strategy delegates entry decisions to an external PPO model served by the
[trader-algo-ml](https://github.com/Goncalo-Correia/trader-algo-ml) sidecar (base URL under
`MlPolicy:`). This API stores the training configuration, orchestrates training runs, and lets the
frontend replay a trained model's decision process the same way a backtest is streamed.

**Data model.**

```mermaid
erDiagram
  ml_policies ||--o{ ml_training_runs : "has runs"
  ml_policies { long Id; int SymbolId; int IntervalId; int TotalTimesteps; decimal riskParams; double tuningParams }
  ml_training_runs { long Id; long MlPolicyId; datetimeoffset From; datetimeoffset To; int StatusId; decimal FinalBalance; decimal FinalBalanceOos }
```

- **`ml_policies`** — a reusable config: symbol/interval + all PPO/risk hyperparameters
  (`totalTimesteps`, `initialBalance`, `quantity`, `takeProfit`, `stopLoss`, `breakeven`,
  `breakevenStop`, `fee`, `slippage`, `dailyProfit`, `dailyDrawdownLimit`, `maxCandlesPerTrade`,
  `maxTrailingDrawdown`) plus optional [tuning parameters](#optional-tuning-parameters). Live trade
  bots and backtests running the ML Policy strategy reference a policy by id (`mlPolicyId`); the
  policy id is also the model identifier sent to the sidecar.
- **`ml_training_runs`** — one execution of a policy over a date range (model/symbol/interval/params
  come from the policy); holds only run-specific state: dates, status, and final metrics. Metrics are
  recorded both **in-sample** (`finalBalance`, `pnlPct`) and **out-of-sample** (`finalBalanceOos`,
  `pnlPctOos`) — see [In-sample vs out-of-sample](#in-sample-vs-out-of-sample-metrics).

**Training flow.**

```mermaid
sequenceDiagram
  participant UI
  participant API
  participant ML as ML sidecar (Python)
  UI->>API: POST /api/ml/train { mlPolicyId, from, to }
  API->>DB: create ml_training_runs (Pending)
  API->>ML: POST /train (policy params + run id)
  API-->>UI: { trainingRunId }
  ML->>ML: train PPO (background)
  ML->>API: PATCH /training-runs/{id}/complete (in-sample + OOS metrics)
  API->>DB: Completed / Failed
  Note over ML: promotion is gated — a Completed run<br/>is not necessarily the served model
```

1. `POST /api/ml/train` takes `{ mlPolicyId, from, to }` (dates only — the controller stores `from`
   at **00:00** and `to` at **23:59** of the chosen days, and forwards them to the sidecar as
   **ISO-8601** `from_date`/`to_date`). It records a run as `Pending`, builds the Python request from
   the policy, and returns the `trainingRunId`.
2. The sidecar trains in the background and calls back to `PATCH /api/ml/training-runs/{id}/complete`,
   moving the run `Pending → Running → Completed` (or `Failed`) and recording final balance, PnL %,
   out-of-sample balance/PnL %, and trade count. Terminal states are final: a late or duplicate
   callback for an already `Completed`/`Failed` run is ignored so it cannot clobber the recorded result.
3. Each run's deterministic **decision log** is stored uniquely by `trainingRunId` (never
   overwritten), so re-running a policy preserves every run's history.
4. `POST /api/ml/retrain-all { from, to }` kicks off a run for **every** policy over a shared date
   range — used for the one-time retrain required after an observation-schema change (see below).

**Observation schema & retraining.** The model's observation vector is versioned. When the sidecar's
observation format changes, models trained on an older schema are **rejected at inference time** —
`POST /api/ml/decide` fails for that policy rather than serving garbage. After deploying a sidecar
release that changes the schema, retrain every policy (one `POST /api/ml/retrain-all`, or a `train`
per policy). During the migration window the bot path is fault-tolerant: a failed `/decide` is treated
as **hold** (no entry) and logged, so one un-retrained policy can never abort the whole bot batch.

<a id="in-sample-vs-out-of-sample-metrics"></a>
**In-sample vs out-of-sample metrics.** Each completed run reports two performance numbers:

| Metric | Fields | Meaning |
|---|---|---|
| In-sample | `finalBalance`, `pnlPct` | Full-range result — includes data the model trained on, so it is **optimistic**. |
| Out-of-sample (OOS) | `finalBalanceOos`, `pnlPctOos` | Result on the held-out tail — the **honest**, generalization-relevant number. `null` when the range was too small to hold out a validation tail. |

Prefer OOS when ranking or judging a model; treat in-sample as a reference only.

**Risk-aware promotion — `Completed` ≠ live.** Finishing a run does **not** make it the served model.
The sidecar promotes a candidate only if it beats the incumbent on a risk-adjusted basis: it must beat
the incumbent's OOS PnL by a margin when its OOS drawdown is no worse, otherwise it must beat the
incumbent's **Calmar ratio** (OOS PnL ÷ trailing drawdown). So a higher-PnL run may intentionally not
be promoted if it is much riskier. The currently-served model per policy is the source of truth and is
exposed by `GET /api/ml/served-models` — do not equate "training completed" with "now serving".

**Durable run status / orphan recovery.** Training runs are background tasks in the sidecar. If the
sidecar restarts mid-run, on startup it reconciles orphaned runs: any run still in flight is set to
`Failed` and a `Failed` callback is sent. The API therefore may receive a `Failed` completion for a run
it only ever saw as `Running`. `Failed` is terminal and the run can simply be retriggered.

<a id="optional-tuning-parameters"></a>
**Optional tuning parameters.** Policies expose the sidecar's PPO/reward tuning knobs. All are
**nullable** — when a value is null it is omitted from the `/train` request and the sidecar applies its
own default, so existing policies are unaffected.

| Group | Parameters (sidecar default) |
|---|---|
| Reward shaping | `entryCost` (0.05), `noTradeDayPenalty` (1.0), `streakBonusCoef` (0.1), `maxStreakBonus` (0.5), `maxPatienceRewardPerDay` (0.5) |
| Episode | `episodeDays` (5.0) |
| PPO | `learningRate` (0.0003), `nSteps` (2048, fresh only), `batchSize` (64, fresh only), `nEpochs` (10), `gamma` (0.99), `gaeLambda` (0.95), `clipRange` (0.2), `entCoef` (0.01) |
| OOS eval | `oosEvalEvery` (1 — higher = faster training) |

Risk hyperparameters are **absolute amounts**, consistent with backtests — not fractions.
`stopLoss`/`takeProfit`/`breakeven`/`breakevenStop` are price offsets from entry, `fee` is a flat
cash fee per round-trip, `slippage` is a flat price offset per fill, and `maxTrailingDrawdown` is a
cash drawdown from peak balance.

**Decision replay.** `WS /ws/ml/training?trainingRunId={id}` streams the run's candles (from the
database) zipped with the model's per-candle decisions — emitting `candle` and `mlDecision` frames —
so entry/hold choices and confidence can be visualised candle-by-candle.
`GET /api/ml/training-runs/{id}/decisions` returns the same log as a single payload (including
`oos_pnl_pct` / `oos_final_balance` when available). Deleting a run also removes its decision log from
the sidecar.

**MLflow tracking.** `GET /api/ml/training-runs/{id}/tracking` returns read-only MLflow metadata,
params, latest metrics, metric history, and a `rewardMetrics` dashboard linked by the MLflow param
`training_run_id`. The dashboard groups reward-based model checks into performance, stability,
learning quality, safety, baseline comparison, robustness, and reward integrity categories while
preserving the raw MLflow metrics. Training-run list/detail responses include a compact `tracking`
summary when MLflow data is available. Configure `Mlflow:TrackingUri` or the raw
`MLFLOW_TRACKING_URI` environment variable with the same PostgreSQL MLflow tracking URI used by
`trader-algo-ml`; the API converts `postgresql://...` MLflow URIs to Npgsql connection strings for
read-only queries.

**Inference.** `POST /api/ml/decide` fetches the latest candle+indicators and asks the sidecar for an
entry action — this is what the live `MlPolicy` strategy and ML backtests call while flat. The
decision is made on a **closed candle**; the live path enters at that candle's **close** to match the
training environment.

> **Unit contract (train/serve parity).** The decide payload must use the same units the training
> environment uses, otherwise the sidecar logs a skew warning and inference degrades:
> - `current_daily_drawdown` is a **fraction in `[0, 1]`** of the day-start balance — **not cash**.
> - `current_daily_pnl`, `last_trade_pnl`, `fee_rate`, `unrealized_pnl` are **absolute cash**.

**Served models.** `GET /api/ml/served-models` composes the sidecar's model registry with our
policies and returns **exactly one row per policy** (camelCase): `served` (false when nothing is
promoted yet), `servedTrainingRunId` (the run whose model is live — maps to `MlTrainingRun.id`),
`modelId`, `pnlPct`/`finalBalance`, `oosPnlPct`/`oosFinalBalance`, `nTrades`, `obsDim`,
`schemaVersion`, `runId` (MLflow), and `calibrated`. `calibrated` indicates whether the model's
`confidence` has been fitted to the realized win rate at that probability (when a run has enough
trades); otherwise `confidence` is the raw policy probability. This endpoint replaces the removed
model-registry endpoint that used to live at `GET /api/ml/models`.

---

## Kronos forecasting

[Kronos](https://github.com/shiyu-coder/Kronos) is an open-source foundation model for financial
time-series forecasting — a GPT-style autoregressive Transformer that predicts the next OHLCV
candle instead of the next word.

The API does **not** host Kronos directly. It calls a separate **Kronos Connector** (a FastAPI
wrapper around the model, recommended on Modal.com for serverless GPU) over HTTP, configured via
`Kronos:BaseUrl`.

```mermaid
flowchart LR
  UI -->|GET /api/kronos/...| API
  API -->|recent candles| Conn[Kronos connector]
  Conn --> Model[Kronos model]
  Model -->|forecast candles| Conn --> API --> UI
```

**Flow.** A `GET /api/kronos/{model}/{mode}` request loads recent candles for the symbol/interval,
posts them to the connector, and returns the predicted forward candles for charting. Models trade
context length for capacity — `kronos-mini` (up to 2048 candles), `kronos-small` / `kronos-base` (up
to 512). Each comes in two modes: **precise** (low temperature, multi-sample) and **diverse** (high
temperature, single sample). If the connector is down the endpoints return `503`.

---

## Background sync jobs

Full backfills are long — a full data-collection sweep since 2020 across every symbol×interval can run
for **days**. Running that inside an HTTP request fails on a hosted setup: the connection would have to
stay open for the whole run, and the work is tied to the request's cancellation token, so any client
disconnect aborts it. Instead these operations run as **background jobs**, decoupled from the request.

The four bulk endpoints — data-collector `full-sync`/`partial-sync` and indicator
`full-sync`/`partial-sync` — enqueue a job and return **`202 Accepted`** immediately with a `SyncJob`
record (and a `Location` header to its status). Poll `GET /api/jobs/{id}` for progress.

**Execution model.**

- A single `SyncJobWorker` hosted service drains an in-process queue, so jobs run **one at a time**
  and never overlap. The work runs on the application-lifetime token, not the request token.
- Each job's status is a durable `sync_jobs` row (`Pending → Running → Completed/Failed/Cancelled`)
  with `totalUnits`/`completedUnits` progress (counted in symbol×interval pairs) and a `message`.
- Enqueuing a sync whose type is already `Pending`/`Running` returns the existing job — the same sync
  can't be double-run.
- Each pair is processed in its own DI scope so the EF change tracker doesn't accumulate across a
  multi-day run, and a single failing pair is logged and skipped rather than aborting the whole job.
- **Errors are recorded per job.** Two kinds of failure are captured as `sync_job_errors` rows linked
  to the job (`syncJobId` FK, plus the `symbol`, `interval` and `candleOpenTime` they refer to):
  *per-candle* failures — data collection captures these at the candle level instead of throwing, so a
  bad candle (or a failed batch persist) is skipped, logged, and returned on the collector's result;
  and *whole-pair* failures — when an entire `symbol × interval` throws, the pair is logged, skipped
  (the job continues), and recorded with a null `candleOpenTime`. Every error counted in the job's
  `message` therefore has a matching row. `GET /api/jobs/{id}` returns the job together with these
  linked errors, so a run's failures can be inspected afterwards.
- **Resumable:** on startup the worker re-queues any job left `Pending`/`Running` by a previous
  process. Because both syncs are idempotent (collection skips existing candles; indicator sync
  upserts), an interrupted job simply resumes after a restart or redeploy.

> **Hosting note.** The worker lives inside the web service, so a multi-day job needs an instance that
> does not idle-stop (i.e. a paid Render tier, not the free instance which spins down after ~15 min of
> no traffic). It runs identically locally — hit the endpoint and poll `/api/jobs/{id}`.

The single-pair `POST /binance/data-collector/{symbol}/{interval}` endpoint stays synchronous for
targeted/diagnostic use; prefer `full-sync` for anything large.

---

## API reference

REST base path `/api`. Enums (side, status, strategy, etc.) serialize as strings. **All endpoints
require the API key** (`/health` is the only exception) — see [Authentication](#authentication).

| Area | Endpoints |
|---|---|
| **Trading accounts** | `POST/GET /trading-accounts` · `GET/PATCH/DELETE /trading-accounts/{id}` |
| **Trade bots** | `POST/GET /tradebots` · `GET/PATCH/DELETE /tradebots/{id}` · `POST /tradebots/{id}/enable` · `/disable` |
| **Backtests** | `POST/GET /backtests` · `GET/DELETE /backtests/{id}` |
| **Trades** | `POST /trades` · `POST /trades/{id}/stop` · `PATCH /trades/{id}` · `GET /trades/account/{id}/active` · `/history` · `GET /trades/backtest/{id}` |
| **ML policies** | `GET/POST /ml/policies` · `GET/PUT/DELETE /ml/policies/{id}` |
| **ML training** | `POST /ml/train` · `POST /ml/retrain-all` · `POST /ml/decide` · `GET /ml/served-models` · `GET /ml/training-runs[?mlPolicyId=]` · `GET/DELETE /ml/training-runs/{id}` · `GET /ml/training-runs/{id}/tracking` · `GET /ml/training-runs/{id}/decisions` · `PATCH /ml/training-runs/{id}/complete` |
| **Rules** | `GET /rules/{sma\|rsi\|macd}/evaluate?symbol=&interval=` |
| **Charts** | `GET /charts/candles?symbol=&interval=&lookback=` · `GET /charts/candles/indicators` · `GET /charts/candles/indicators/date-interval?from=&to=&symbol=&interval=` |
| **Symbols / Intervals** | `GET /symbols` · `GET /intervals` |
| **Data collector** | `POST /binance/data-collector/{symbol}/{interval}` (sync, single pair) · `POST /binance/data-collector/partial-sync` · `POST /binance/data-collector/full-sync` (both **async** → `202` + job) |
| **Indicators** | `POST /indicators/partial-sync` · `POST /indicators/full-sync` (both **async** → `202` + job) |
| **Jobs** | `GET /jobs/{id}` (sync-job status + linked collection errors) · `GET /jobs?take=` (recent jobs) |
| **Kronos** | `GET /kronos/{model}/{mode}?symbol=&interval=` (candle forecasts) |
| **Health** | `GET /health` (checks DB connectivity; **public — no key required**) |

**WebSocket endpoints**

| Endpoint | Purpose |
|---|---|
| `WS /ws/charts/candles?symbol=&interval=` | Live candles |
| `WS /ws/charts/candleswithindicators?symbol=&interval=` | Live candles + SMA/RSI/MACD |
| `WS /ws/charts/backtest?backtestId=&delay=` | Backtest replay |
| `WS /ws/ml/training?trainingRunId=&delay=` | ML training decision replay |
| `WS /ws/tradebots/events?tradingAccountId=` | Live trade events |

Errors return RFC 7807 ProblemDetails: `400` invalid input, `401` missing/invalid API key,
`404` not found, `409` conflict, `503` upstream (Kronos/ML) unavailable.

---

## Logging

Logging is config-driven (standard ASP.NET Core `ILogger`), so verbosity is controlled entirely
through configuration — no code changes or redeploys. The defaults live in the `Logging:LogLevel`
section of `appsettings.json`; override them per environment with environment variables using `__`
as the nesting separator. On a restart, the new levels take effect immediately.

Levels, most to least verbose: `Trace` → `Debug` → `Information` → `Warning` → `Error` → `Critical`
→ `None`. Categories are the class's namespace + name (the `T` in `ILogger<T>`); the most specific
match wins. `Logging__LogLevel__TraderAlgoApi=<level>` adjusts every app service at once, or target a
single feature with the categories below.

**Quiet down (high steady-state volume).** Set these to `Warning` to suppress routine chatter:

| Feature | Category |
|---|---|
| Data collection (nightly / full syncs) | `TraderAlgoApi.Services.DataCollector` |
| Live market ingest (per-candle storage) | `TraderAlgoApi.Services.Binance` |
| Indicator recompute (paired with every candle) | `TraderAlgoApi.Services.Indicators` |

**Turn up for debugging.** These are quiet by default but expose rich per-tick / per-candle
diagnostics at `Debug`:

| Feature | Category | What `Debug` reveals |
|---|---|---|
| Trades — SL/TP & limit fills | `TraderAlgoApi.Services.Trades` | Per-tick stop/target/limit evaluation |
| Trade bots | `TraderAlgoApi.Services.TradeBots` | Per-candle reason a bot did/didn't enter |
| ML decisions & training | `TraderAlgoApi.Services.Ml` | `/decide` and `/train` sidecar request payloads |
| Raw SQL (EF Core) | `Microsoft.EntityFrameworkCore.Database.Command` | Every SQL statement (very noisy) |

Examples:

```dotenv
Logging__LogLevel__Default=Warning                          # quiet everything to warnings+errors
Logging__LogLevel__TraderAlgoApi.Services.DataCollector=None  # silence the data collector entirely
Logging__LogLevel__TraderAlgoApi.Services.Trades=Debug      # trace SL/TP/limit fills
```

> **Swagger** is exposed in every environment by default and served at `/swagger`, but it is gated
> behind the API key (see [Authentication](#authentication)). Set `Swagger__Enabled=false` (config
> key `Swagger:Enabled`) to turn it off entirely without a redeploy.

---

## Authentication

Every endpoint requires the `ApiKey` (the health check at `/health` is the only exception, so
Render can probe it). How the key is presented depends on the caller:

| Caller | How to send the key |
|---|---|
| REST endpoints | `X-Api-Key: <key>` request header |
| WebSocket streams (`/ws/...`) | `?apiKey=<key>` query parameter (browsers can't set headers on a WS handshake) |
| Swagger UI / document (`/swagger`) | HTTP Basic auth — any username, the key as the **password** (the browser prompts on navigation) |

Inside the Swagger UI, click **Authorize** and paste the key to make the "Try it out" calls send
the `X-Api-Key` header.

> **CORS vs. host filtering.** When the frontend is deployed (e.g. to Vercel), add its origin to the
> CORS policy in `Program.cs` (`WithOrigins(...)`) — *not* to `AllowedHosts`. `AllowedHosts` filters
> the inbound `Host` header and should list the **API's** own hostname
> (`trader-algo-api.onrender.com`); CORS controls which **browser origins** may call it.

---

## Environment variables

Configuration is layered: `appsettings.json` holds the defaults, and any value can be overridden by
an environment variable. Nested keys use a **double underscore** (`__`) — e.g. the env var
`ConnectionStrings__Supabase` maps to the config key `ConnectionStrings:Supabase`. Secrets are never
committed; locally they live in user-secrets, in production they're set as environment variables
(e.g. on Render).

| Variable | Required | Default | Description & options |
|---|---|---|---|
| `ApiKey` | **Yes** | — | API key required on every endpoint (`X-Api-Key` header / `?apiKey=` for WebSockets / Basic-auth password for Swagger). The app fails to start if it's missing. Use a long random string. See [Authentication](#authentication). |
| `ConnectionStrings__Supabase` | **Yes** | — | Npgsql connection string for the main PostgreSQL database. Form: `Host=…;Port=5432;Database=postgres;Username=…;Password=…;SSL Mode=…`. **`SSL Mode`** options: `Disable`, `Allow`, `Prefer`, `Require` (encrypt, no cert validation — the default to use with Supabase), `VerifyCA`, `VerifyFull` (validate chain + hostname; needs Supabase's root CA via `Root Certificate=<path>`, since their pooler cert isn't in the public trust store). |
| `ASPNETCORE_ENVIRONMENT` | Recommended | `Production` | .NET hosting environment. `Production` enables HTTPS redirection (non-WebSocket requests) and skips dev-only connection-pool clearing; `Development` does the opposite and is more verbose. Defaults to `Production` when unset. |
| `Mlflow__TrackingUri` | For MLflow | falls back to `ConnectionStrings__Supabase` | Connection to the MLflow tracking database. Accepts a libpq URI (`postgresql://user:pass@host:5432/postgres?sslmode=require`) or an Npgsql keyword string. If unset, MLflow queries run against the main Supabase connection instead of the tracking schema. |
| `Mlflow__Enabled` | No | `true` | Enables the MLflow tracking features. Set `false` to disable. |
| `Swagger__Enabled` | No | `true` | Serves the (key-gated) Swagger UI and document at `/swagger`. Set `false` to remove it entirely in production. |
| `Logging__LogLevel__Default` | No | `Information` | Global log level. Options: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. |
| `Logging__LogLevel__<Category>` | No | varies | Per-namespace override, e.g. `Logging__LogLevel__Microsoft.AspNetCore=Warning` or `Logging__LogLevel__TraderAlgoApi.Services.Trades=Debug`. See [Logging](#logging). |
| `Binance__BaseUrl` | No | `https://api.binance.com` | Binance REST base URL (public market data — no key needed). |
| `Binance__WebSocketBaseUrl` | No | `wss://stream.binance.com:443` | Binance WebSocket stream base URL. |
| `Kronos__BaseUrl` | No | `http://host.docker.internal:8000` | Base URL of the optional Kronos forecast sidecar. The default only resolves when a sidecar runs on the same host; on Render leave it unset (Kronos endpoints stay inactive) or point it at a reachable URL. |
| `MlPolicy__BaseUrl` | No | `http://host.docker.internal:8766` | Base URL of the optional ML policy sidecar. Same caveat as `Kronos__BaseUrl`. |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | No | unset | Set to `true` behind a TLS-terminating proxy (like Render) so the app trusts `X-Forwarded-Proto`/`X-Forwarded-For`. Enable it if HTTPS redirection causes redirect loops. |

**Don't set on Render:** `Kestrel__Certificates__Development__Password` (local HTTPS dev cert only) or
a port/`ASPNETCORE_URLS` variable — the Dockerfile already binds Kestrel to `:10000`, which Render
expects.

---

## Running locally

Requires the **.NET 10 SDK** and a PostgreSQL database (Supabase or local).

1. Set secrets (the connection string is **not** committed — `appsettings.json` holds a placeholder
   only). Binance market data uses public endpoints, so no provider keys are required:

   ```bash
   cd TraderAlgoApi
   dotnet user-secrets set "ConnectionStrings:Supabase" "Host=...;Database=postgres;Username=...;Password=...;SSL Mode=Require"
   dotnet user-secrets set "Mlflow:TrackingUri" "postgresql://USER:PASSWORD@HOST:5432/postgres?sslmode=require"
   dotnet user-secrets set "ApiKey" "<a-long-random-string>"
   ```

   `ApiKey` is **required** — the app fails to start without it. In production set it as the
   `ApiKey` environment variable (e.g. on Render). See [Authentication](#authentication) for how
   callers present it.

   > **TLS to the database.** `SSL Mode=Require` encrypts the connection but does not validate the
   > server certificate — use it as the default, because Supabase's pooler cert is signed by a
   > private CA that isn't in the standard trust store, so `VerifyFull` fails with a certificate
   > error unless you supply that CA. To harden to full validation (chain + hostname), download
   > Supabase's root CA (*Project Settings → Database → SSL configuration*), make it available to the
   > app (e.g. `COPY` it into the Docker image), and use
   > `SSL Mode=VerifyFull;Root Certificate=/path/to/supabase-ca.crt`. Avoid
   > `Trust Server Certificate=true` — it's redundant with `Require` here and only adds noise.

   If the API runs in Docker, pass the MLflow tracking database through the container environment:

   ```bash
   MLFLOW_TRACKING_URI=postgresql://USER:PASSWORD@HOST:5432/postgres?sslmode=require
   ```

2. Apply migrations and run:

   ```bash
   dotnet ef database update
   dotnet run
   ```

3. Open Swagger at `https://localhost:7096/swagger`. The browser prompts for credentials — enter any
   username and your `ApiKey` as the password (see [Authentication](#authentication)). Enabled by
   default in every environment; see [Logging](#logging) to disable it.

Optional sidecars (base URLs configurable under `Kronos:` and `MlPolicy:`): the Kronos forecast
service and the ML policy service. The API runs without them — only the related endpoints/strategy
are affected. When the .NET API runs in a container and a sidecar runs on the host, point its base
URL at `http://host.docker.internal:<port>`.
