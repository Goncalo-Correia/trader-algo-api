namespace TraderAlgoApi.Services.Indicators;

public sealed class SimpleMovingAverageService : ISimpleMovingAverageService
{
    public (decimal? Sma20, decimal? Sma100) Compute(IReadOnlyList<decimal> closes, int index)
    {
        decimal? sma20 = null;
        if (index >= 19)
        {
            var sum = 0m;
            for (var i = index - 19; i <= index; i++)
                sum += closes[i];
            sma20 = sum / 20;
        }

        decimal? sma100 = null;
        if (index >= 99)
        {
            var sum = 0m;
            for (var i = index - 99; i <= index; i++)
                sum += closes[i];
            sma100 = sum / 100;
        }

        return (sma20, sma100);
    }
}
