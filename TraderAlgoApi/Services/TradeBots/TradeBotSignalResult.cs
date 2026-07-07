namespace TraderAlgoApi.Services.TradeBots;

public sealed record TradeBotSignalResult(TradeBotSignal Signal, string? Reason, MlBracket? Bracket = null);

/// <summary>
/// The ML policy's chosen entry bracket, carried alongside an EnterLong/EnterShort signal so the
/// monitor can size the stop (<see cref="SlAtrMult"/> × <see cref="AtrAtEntry"/>) and take-profit
/// (<see cref="TpRMult"/> × stop distance) and persist the ATR-at-entry for the breakeven ratchet.
/// </summary>
public sealed record MlBracket(decimal SlAtrMult, decimal TpRMult, decimal AtrAtEntry);
