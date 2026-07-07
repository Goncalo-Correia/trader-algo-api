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
a `*StreamService`. They're `.ExcludeFromDescription()`'d (hidden from Swagger).

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

**DbContext registered twice, deliberately.** Both `AddDbContext` (scoped, for request work) and
`AddDbContextFactory` (for long-lived WebSocket streams and background jobs that must not hold a
request-scoped context open). In background/stream code, create a fresh context per unit of work via
the factory. A second context, `MlflowDbContext`, is read-only over the MLflow tracking schema.

**Some tables are written by the ML sidecar, not this app.** The `training_*` telemetry tables
(`Models/Telemetry/`, mapped in `ApplicationDbContext` and created by a migration so the schema is
tracked) are **written by the Python `trader-algo-ml` sidecar** — treat them as an external read
model and don't add telemetry-producing write paths from this app. This app only **reads** them
(e.g. `MlPerformanceController` under `/api/ml`; `training_decisions` is served via
`GetTrainingDecisionLogAsync` in [Data/TrainingDecisionsQueryExtensions.cs](TraderAlgoApi/Data/TrainingDecisionsQueryExtensions.cs)
instead of proxying the sidecar) and **deletes** rows as cleanup when a training run is removed
(`MlController` DELETE `training-runs/{id}`).

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
`ApiKeyAuthentication` (middleware enforcing the key on all endpoints except `/health`; Swagger is
gated by a Basic-auth variant), `GlobalExceptionHandler` (RFC 7807 ProblemDetails for everything
uncaught — return typed errors, don't hand-roll error bodies), `DatabaseHealthCheck`.

**Indicators** (`SimpleMovingAverage`, `RelativeStrengthIndex`, `Macd`, `Atr`) are one-to-one side
tables keyed on `KlineData.Id` (cascade delete), recomputed incrementally whenever a candle is
inserted/updated. Each has its own `I*Service`; `IIndicatorSyncService` recomputes them together.

## Conventions

- Enums serialize as **strings** over JSON (`JsonStringEnumConverter`, configured globally).
- Decimals use `[Precision(28, 10)]` for prices/quantities/PnL.
- `sealed` classes, nullable reference types and implicit usings are on.
- Migrations reflect a live production DB — **avoid destructive migrations** (dropping/renaming
  columns or tables that hold data); prefer additive changes.
