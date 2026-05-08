using TraderAlgoApi.Models.Enums;
using TraderAlgoApi.Services.MarketData.Alpaca;

namespace TraderAlgoApi.Services.MarketData;

/// <summary>
/// Resolves the correct IMarketDataProvider implementation for a given SymbolProvider.
/// Binance is injected via the IBinanceMarketDataService (which also implements IMarketDataProvider).
/// </summary>
public sealed class MarketDataProviderFactory(
    IMarketDataProvider      binanceProvider,
    AlpacaMarketDataProvider alpacaProvider) : IMarketDataProviderFactory
{
    public IMarketDataProvider GetProvider(SymbolProvider provider) => provider switch
    {
        SymbolProvider.Binance => binanceProvider,
        SymbolProvider.Alpaca  => alpacaProvider,
        _                      => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
    };
}
