using TraderAlgoApi.Models.Enums;

namespace TraderAlgoApi.Services.MarketData;

/// <summary>
/// Resolves the correct IMarketDataProvider implementation for a given SymbolProvider.
/// Binance is injected via the IBinanceMarketDataService (which also implements IMarketDataProvider).
/// </summary>
public sealed class MarketDataProviderFactory(
    IMarketDataProvider binanceProvider) : IMarketDataProviderFactory
{
    public IMarketDataProvider GetProvider(SymbolProvider provider) => provider switch
    {
        SymbolProvider.Binance => binanceProvider,
        _                      => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
    };
}
