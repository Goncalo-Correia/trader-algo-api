using TraderAlgoApi.Models.Enums;

namespace TraderAlgoApi.Services.MarketData;

public interface IMarketDataProviderFactory
{
    IMarketDataProvider GetProvider(SymbolProvider provider);
}
