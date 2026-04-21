namespace TraderAlgoApi.Services.Charts;

public interface IChartsService
{
    string NormalizeSymbol(string? symbol);
    string NormalizeInterval(string? interval);
}
