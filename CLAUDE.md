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
  write tests unless explicitly asked.
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

**DbContext registered twice, deliberately.** Both `AddDbContext` (scoped, for request work) and
`AddDbContextFactory` (for long-lived WebSocket streams and background jobs that must not hold a
request-scoped context open). In background/stream code, create a fresh context per unit of work via
the factory. A second context, `MlflowDbContext`, is read-only over the MLflow tracking schema.

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

**Indicators** (`SimpleMovingAverage`, `RelativeStrengthIndex`, `Macd`) are one-to-one side tables
keyed on `KlineData.Id` (cascade delete), recomputed incrementally whenever a candle is inserted/updated.

## Conventions

- Enums serialize as **strings** over JSON (`JsonStringEnumConverter`, configured globally).
- Decimals use `[Precision(28, 10)]` for prices/quantities/PnL.
- `sealed` classes, nullable reference types and implicit usings are on.
- Migrations reflect a live production DB — **avoid destructive migrations** (dropping/renaming
  columns or tables that hold data); prefer additive changes.
