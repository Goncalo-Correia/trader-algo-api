using TraderAlgoApi.Data;
using TraderAlgoApi.Services.Indicators;
using TraderAlgoApi.Services.MarketData.Alpaca;

namespace TraderAlgoApi.Services.DataCollector;

public sealed class AlpacaDataCollectorService(
    ApplicationDbContext dbContext,
    AlpacaMarketDataProvider provider,
    IIndicatorSyncService indicatorSyncService)
    : DataCollectorServiceBase(dbContext, provider, indicatorSyncService),
      IAlpacaDataCollectorService;
