using TraderAlgoApi.Data;
using TraderAlgoApi.Services.Binance;
using TraderAlgoApi.Services.Indicators;

namespace TraderAlgoApi.Services.DataCollector;

public sealed class BinanceDataCollectorService(
    ApplicationDbContext dbContext,
    BinanceMarketDataService provider,
    IIndicatorSyncService indicatorSyncService)
    : DataCollectorServiceBase(dbContext, provider, indicatorSyncService),
      IBinanceDataCollectorService;
