namespace BinanceTicker.Core.Models;

public sealed record AlertResetRequest(string Symbol, AlertType Type);
