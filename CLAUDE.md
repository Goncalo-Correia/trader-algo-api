# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

ASP.NET Core (.NET 10) backend for algorithmic crypto trading. It ingests live/historical Binance
market data, computes indicators, runs strategy-driven trade bots and backtests, and streams to an
Angular frontend over WebSockets. Optional sidecars: **Kronos** (candle forecasting) and an **ML
policy** service (PPO model entries). The `README.md` is the authoritative functional spec — read it
for domain behavior (strategies, trade lifecycle, backtests, ML training/promotion, sync jobs, auth,
env vars). This file covers build mechanics and code structure the README omits.

## Commands

The project lives in the `TraderAlgoApi/` subdirectory — run `dotnet` commands from there.

```bash
cd TraderAlgoApi
dotnet build
dotnet run                     # serves Swagger at https://localhost:7096/swagger
dotnet ef migrations add <Name>
dotnet ef database update      # apply migrations (81+ exist; the DB schema is migration-driven)
```

- **`ApiKey` must be set** (user-secrets locally) or the app fails to start. Also set
  `ConnectionStrings:Supabase`. See README → Running locally / Environment variables.
- **No test project exists and the owner does not want tests** — do not add a test framework or
  write tests unless explicitly asked. There is also no linter/formatter configured: `dotnet build`
  (and running the app) is the only verification gate.
- Deployed on Render via the root `Dockerfile` (binds Kestrel to `:10000`).

## Architecture patterns

Standard layering: **Controllers → scoped services (`I*Service` + impl) → EF Core**. DTOs are
`record`s under `Dtos/`, grouped by feature. Everything is registered explicitly in
[Program.cs](TraderAlgoApi/Program.cs) — that file is the fastest map of the whole system (feeds,
domain services, hosted services, sidecar HttpClients).

**Real-time backbone (two singleton event buses).** `PriceFeed` (every tick) and `ClosedCandleFeed`
(every closed candle, post-indicator) decouple producers from consumers. Background `IHostedService`s
subscribe: `TradeMonitorService` (SL/TP + limit fills off ticks), `TradeBotMonitorService` (bot
evaluation off closed candles), `DataCollectorTimer` (nightly backfill), `SyncJobWorker` (drains the
in-process background-job queue one at a time). WebSocket streams also subscribe to these feeds.

**WebSockets are separate from controllers.** REST is attribute-routed controllers under
`Controllers/`; live streams are minimal-API `MapGet` handlers in
[WebSockets/WebSocketEndpoints.cs](TraderAlgoApi/WebSockets/WebSocketEndpoints.cs), each delegating to
a `*StreamService`. They're `.ExcludeFromDescription()`'d (hidden from Swagger). WS handshakes can't
send the `X-Api-Key` header, so they authenticate with a short-lived single-use `?ticket=` minted by
`POST /api/auth/ws-ticket` ([WebSocketTicketService](TraderAlgoApi/Infrastructure/WebSocketTicketService.cs));
legacy `?apiKey=` is still accepted as a fallback.

**Backtest compute is a single-flight background job, not the socket.** `BacktestStreamService`
still owns the compute/replay split, but computation runs via
[BacktestJobRunner](TraderAlgoApi/Services/Backtests/BacktestJobRunner.cs) (singleton) on a task tied
to the app lifetime — started at most once per backtest id regardless of how many clients attach, so
a client disconnect stops only that client's replay, never the run. `ComputeAsync(backtestId, ct)` is
the detached entrypoint (own `DbContext`); the socket handler awaits the shared job then replays the
persisted run. A run interrupted by host shutdown stays `Running` and resumes from persisted progress.

**Market clock (two implementations — mind the difference).** Session-hours logic exists in two
places: `NyseSessionService` (stateless singleton) backs the `/api/session/*` aggregate endpoints and
honours a **holiday calendar**; `BacktestSimulationEngine.IsWithinNySession` (pure static) enforces
the bots' `isNySessionOnly` entry gate and only excludes **weekends** (no holidays). Both the live
`TradeBotMonitorService` and the backtest engine gate entries through the *same* logic — the backtest
via `BacktestSimulationEngine.CanEnterToday`, the live monitor by reusing `IsWithinNySession` plus a
per-day `DailyProfitGoal`/`MaxLossesPerDay` check that mirrors it — so the two modes gate entries
identically (entries only; exits/opposite-signal closes still run outside the session or after a daily
limit). The two session implementations are not equivalent (holidays); reconcile them rather than
adding a third if you touch this.

**`maxCandlesPerTrade` is a per-trade candle-age exit enforced in both modes.** The backtest
(`BacktestStreamService`, via `BacktestSimulationEngine`'s `openTradeCandles` counter) and the live
`TradeBotMonitorService` (`MaybeForceCloseMaxCandlesAsync`) both force-close an open trade at market
once it has spanned that many candles, checked *before* the entry/opposite-signal evaluation so a
max-candles exit wins over a same-candle opposite signal. It applies to every strategy (not just
`MlPolicy`, which is trained against the same horizon in the ML env). Live counts candle age with the
`KlineData.OpenTime > Trade.OpenedAt` convention shared with `candlesSinceLastTradeClosed`; keep the
two modes' cap behavior in sync if you touch either.

**DbContext registered twice, deliberately.** Both `AddDbContext` (scoped, for request work) and
`AddDbContextFactory` (for long-lived WebSocket streams and background jobs that must not hold a
request-scoped context open). In background/stream code, create a fresh context per unit of work via
the factory. A second context, `MlflowDbContext`, is read-only over the MLflow tracking schema. All
three share one `ConfigureDb` built from `BuildSupabaseConnectionString` ([Program.cs](TraderAlgoApi/Program.cs)):
it forces `Pooling=true`, disables GSS encryption (`GssEncryptionMode.Disable` — the runtime image
installs `libgssapi-krb5-2` so Npgsql can negotiate it off), caps the local pool at
`Database:MaxPoolSize` (default 5, well below Supabase's small session-pool cap so background workers
queue locally instead of exhausting Supabase slots), short idle-pool pruning via
`Database:ConnectionIdleLifetimeSeconds`/`Database:ConnectionPruningIntervalSeconds`, optional
`Database:MinPoolSize`, and enables
`EnableRetryOnFailure` for transient Postgres faults. Background subscribers that touch the DB at
startup (e.g. `BinanceKlineStreamingService`) additionally wrap their initial load in their own
retry-with-backoff loop rather than crashing the host if the DB is briefly unreachable.

Because `EnableRetryOnFailure` installs `NpgsqlRetryingExecutionStrategy`, a bare
`Database.BeginTransactionAsync` throws (`does not support user-initiated transactions`): the
strategy must own the retry boundary so it can re-run the whole transactional unit on a transient
fault. **Any multi-statement transaction must run inside
`dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async ct => { … })`** with the transaction
opened inside the delegate (see `BacktestService.CreateAsync`/`DeleteAsync`,
`TradeService.CloseAsync`/`EvaluatePriceAsync`). Construct any change-tracked entities *inside* the
delegate so a retry doesn't double-save stale ones; the guarded `ExecuteUpdate`/`ExecuteDelete`
writes are naturally retry-safe.

**Some tables are written by the ML sidecar, not this app.** The `training_*` telemetry tables
(`Models/Telemetry/`, mapped in `ApplicationDbContext` and created by a migration so the schema is
tracked) are **written by the Python `trader-algo-ml` sidecar** — treat them as an external read
model and don't add telemetry-producing write paths from this app. This app only **reads** them
(e.g. `MlPerformanceController` under `/api/ml`; `training_decisions` is served from
[Data/TrainingDecisionsQueryExtensions.cs](TraderAlgoApi/Data/TrainingDecisionsQueryExtensions.cs)
instead of proxying the sidecar — the REST endpoint `GET /api/ml/training-runs/{id}/decisions`
streams the stored JSON verbatim via `GetTrainingDecisionLogPayloadAsync` (these logs can be tens
of MB, so it skips the DTO round-trip), while `GetTrainingDecisionLogAsync` still deserializes the
same payload for the WebSocket replay) and **deletes** rows as cleanup when a training run is
removed (`MlController` DELETE `training-runs/{id}`).

**`MlPolicy` columns are not all sent to `/train`.** The `/train` request
([Dtos/Ml/MlTrainRequest.cs](TraderAlgoApi/Dtos/Ml/MlTrainRequest.cs), built by
`MlController.BuildTrainRequest`) is a strict subset of the policy: symbol/interval + the
high-level `validationScheme` (forwarded as `validation_scheme`; see below) + the
risk/environment params (`totalTimesteps`, `initialBalance`, `maxCandlesPerTrade`, `dailyProfit`,
`dailyDrawdownLimit`, `slippage`, `fee`, `riskPerTrade`). ML **sizing is sidecar-driven**
(`BacktestSimulationEngine.MlPositionSize`): the `/decide` response's optional `quantity` — the
sidecar's regime-selected size in ATR-regime mode (policies trained with `risk_per_trade`) — wins
verbatim when present; otherwise it falls back to `riskPerTrade`-only volatility targeting
(`riskPerTrade / stopDistance`), then to the bot's fixed quantity. See the "`/decide` `quantity`"
note below. ML bots run **no breakeven
ratchet**, so the legacy `quantity`/`breakeven`/`breakevenStop` columns are **retired**: they're not
in the policy API DTOs ([MlPolicyDtos.cs](TraderAlgoApi/Dtos/Ml/MlPolicyDtos.cs)) or the `/decide`
request ([MlDecideRequest.cs](TraderAlgoApi/Dtos/Ml/MlDecideRequest.cs)), `MlPoliciesController.Apply`
no longer writes them, and `TradeBotService.ApplyPolicyRisk` no longer copies them to the bound bot
(it sets `Quantity = 0`, `Breakeven`/`BreakevenStop = null`). These three columns have now been
**dropped** from `ml_policies` (migration `DropMlPolicyRetiredQuantityBreakevenColumns`) and removed
from [MlPolicy.cs](TraderAlgoApi/Models/MlPolicy.cs) — the `ml_policies` table was emptied first, so
the drop lost no data. (The `Quantity`/`Breakeven`/`BreakevenStop` columns on `trade_bots` are
unrelated and remain.) `riskPerTrade` is `decimal?` on the
policy and **optional** on the wire (`MlTrainRequest.RiskPerTrade` is `decimal?`), so
`BuildTrainRequest` forwards it as-is: an unset policy sends `null` (not `0`), which lets the
sidecar's `RISK_PER_TRADE` env fallback take effect — the sidecar rejects `risk_per_trade <= 0`, so
"unset" must stay `null`/omitted rather than being coerced to `0`. A bound ML bot left without
`riskPerTrade` still sizes to `0` (no position) in live execution. Keep the DTO in lockstep with the
sidecar's train contract; don't assume a new policy column is forwarded to training unless you add it
to `MlTrainRequest`.

`validationScheme` is deliberately a plain **lowercase string** (`single`/`block`), not a
C# enum: the global `JsonStringEnumConverter` has no naming policy, so an enum would serialize
PascalCase and break the sidecar's `validation_scheme` contract. The allow-list, normalization
(null/blank → `single`, trim+lowercase, retired `sliding` → `block`), and `IsValid` live in
[Models/ValidationSchemes.cs](TraderAlgoApi/Models/ValidationSchemes.cs); `MlPoliciesController`
validates create/update (invalid → 400) and persists the normalized value, and `BuildTrainRequest`
re-normalizes on the way out so even legacy rows send a valid scheme. Only the high-level choice is
modelled — fold counts, window sizes, embargo bars, and promotion thresholds stay engine-owned in
Python; don't add columns/DTO fields for them.

**The `/decide` response's `quantity` drives ATR-regime sizing (both modes).** In ATR-regime mode
(policies trained with `risk_per_trade`) the sidecar picks the stop/TP/size from the entry ATR's
volatility regime and returns the chosen order size as `quantity`
([MlDecideResponse.cs](TraderAlgoApi/Dtos/Ml/MlDecideResponse.cs), wire `quantity`, `decimal?`). The
stop/TP formulas are unchanged — the engine still computes `stop = slAtrMult × ATR-at-entry` and
`tp = tpRMult × stop`; in regime mode the sidecar sets `sl_bracket`/`tp_bracket` to `null` and returns
`sl_atr_mult = risk_per_trade / ATR` so `slAtrMult × ATR` reproduces the fixed `risk_per_trade` stop.
`MlPositionSize(fallbackQuantity, riskPerTrade, stopDistance, decideQuantity)` applies `decideQuantity`
verbatim when non-null (it must **not** be re-derived from `riskPerTrade / stop`, which would ignore
the regime downsizing), else the legacy `risk/stop` volatility target, else the fixed fallback. Both
the live monitor (`TradeBotSignalService` carries it on `MlBracket` → `TradeBotMonitorService`) and the
backtest (`BacktestStreamService` carries it on `MlDecision`) call `/decide` per candle and thread the
field through the same method, so the two modes size identically. Legacy menu-mode policies return
`quantity = null` and keep the old sizing. The regime thresholds/quantities/TP-multiples are
model-internal (ML-side env), **not** on the `/train` wire.

**Enum ↔ lookup-table dual representation (important).** Every lookup (TradeSide, TradeStatus,
TradingStrategy, SyncJobType, etc.) exists as both:
- a `Models/Enums/*.cs` C# enum, and
- a `Models/Lookups/*.cs` EF entity (a `<name>` table seeded via `HasData` in `OnModelCreating`).

**Lookup-table row IDs are kept identical to the enum integer values.** Entities persist the `*Id`
int/FK column; a `[NotMapped]` `*Enum` property (e.g. `Trade.SideEnum`) is the single home for the
`int ↔ enum` cast. **Prefer the `*Enum` property over raw casts in C# — but it is NOT usable inside
EF LINQ queries** (filter on the `*Id` column instead, e.g. `.Where(t => t.StatusId == (int)TradeStatus.Active)`).
When adding a lookup value, add it to *both* the enum and the `HasData` seed with matching IDs, then
create a migration.

**Market data providers are abstracted** behind `IMarketDataProvider` (currently Binance only),
wired in [Infrastructure/MarketDataServiceCollectionExtensions.cs](TraderAlgoApi/Infrastructure/MarketDataServiceCollectionExtensions.cs)
via `AddMarketDataProviders`. Keep new provider code behind that abstraction.

**Cross-cutting infrastructure** ([Infrastructure/](TraderAlgoApi/Infrastructure)):
`ApiKeyAuthentication` (middleware enforcing the key on all endpoints except `/health`; REST via
`X-Api-Key`, WS via a single-use `?ticket=` with legacy `?apiKey=` fallback; Swagger is gated by a
Basic-auth variant), `GlobalExceptionHandler` (RFC 7807 ProblemDetails for everything uncaught —
return typed errors, don't hand-roll error bodies), `DatabaseHealthCheck`. Outbound named HTTP
clients (Binance, Kronos) get retry/backoff/timeout/circuit-breaker via
[HttpResilienceExtensions](TraderAlgoApi/Infrastructure/HttpResilienceExtensions.cs); the ML Policy
client gets a plain timeout only (its `/train` POST is non-idempotent, so it must not be retried).

**Indicators** (`SimpleMovingAverage`, `RelativeStrengthIndex`, `Macd`, `Atr`) are one-to-one side
tables keyed on `KlineData.Id` (cascade delete), recomputed incrementally whenever a candle is
inserted/updated. Each has its own `I*Service`; `IIndicatorSyncService` recomputes them together.

## Conventions

- Enums serialize as **strings** over JSON (`JsonStringEnumConverter`, configured globally).
- Decimals use `[Precision(28, 10)]` for prices/quantities/PnL.
- `sealed` classes, nullable reference types and implicit usings are on.
- Migrations reflect a live production DB — **avoid destructive migrations** (dropping/renaming
  columns or tables that hold data); prefer additive changes.
