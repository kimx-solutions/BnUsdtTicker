using BinanceTicker.Core.Models;
using System.Text.Json;

namespace BinanceTicker.Core.Services;

public sealed class AlertHistoryService(string? filePath = null)
{
    public string FilePath { get; } = filePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BinanceTicker", "alerts-history.json");
    public IReadOnlyList<AlertHistoryEntry> Load()
    {
        lock (AtomicJsonFile.Gate(FilePath))
        {
            if (!File.Exists(FilePath)) return [];
            var entries = JsonSerializer.Deserialize<List<AlertHistoryEntry>>(File.ReadAllText(FilePath), AtomicJsonFile.Options)
                ?? throw new JsonException("Empty alert history.");
            if (entries.Any(e => e is null || string.IsNullOrWhiteSpace(e.Symbol) || !Enum.IsDefined(e.AlertType) ||
                e.TargetPrice <= 0 || e.TriggeredPrice <= 0 || e.TriggeredAt == default))
                throw new JsonException("Invalid alert history.");
            return entries;
        }
    }
    public void Save(IReadOnlyList<AlertHistoryEntry> entries) => AtomicJsonFile.Write(FilePath, entries);
}
