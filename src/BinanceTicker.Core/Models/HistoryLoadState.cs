namespace BinanceTicker.Core.Models;

public enum HistoryLoadStatus { NotRequested, Loading, Loaded, Failed }
public sealed record HistoryLoadState(HistoryLoadStatus Status, string? Error = null, DateTimeOffset? LastLoadedAt = null);
