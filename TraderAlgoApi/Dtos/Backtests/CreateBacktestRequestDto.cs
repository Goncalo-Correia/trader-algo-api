using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Backtests;

public sealed record CreateBacktestRequestDto(
    [property: JsonPropertyName("symbol")]         string SymbolCode,
    [property: JsonPropertyName("interval")]       string IntervalCode,
    [property: JsonPropertyName("from")]           DateTimeOffset From,
    [property: JsonPropertyName("to")]             DateTimeOffset To,
    [property: JsonPropertyName("initialBalance")] decimal InitialBalance);
