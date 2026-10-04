using System.Text.Json;
using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.Services;

public static class WatchlistSettings
{
    public static WatchlistGroup Active(AppSettings settings) =>
        settings.Watchlists!.First(g => g.Id == settings.ActiveWatchlistId);

    public static void Normalize(AppSettings settings)
    {
        if (settings.Symbols is null || settings.Ui is null || settings.Holdings is null)
            throw new JsonException("Invalid watchlist settings.");
        settings.Watchlists ??= [new() { Id="default", Name="預設分組", Ui=settings.Ui.Copy(),
            Members=settings.Symbols.OrderBy(s=>s.Order).Select(s=>new WatchlistMember {
                Symbol=s.Symbol, Enabled=s.Enabled, Order=s.Order }).ToList() }];
        if (settings.Watchlists.Count == 0) throw new ArgumentException("至少須保留一個分組。");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var registry = settings.Symbols.Select(s=>SymbolNormalizer.Normalize(s.Symbol)).ToHashSet(StringComparer.Ordinal);
        foreach (var group in settings.Watchlists)
        {
            if (group is null || group.Members is null || group.Ui is null)
                throw new JsonException("Invalid watchlist structure.");
            group.Name = group.Name?.Trim() ?? "";
            if (group.Name.Length == 0 || !names.Add(group.Name)) throw new ArgumentException("分組名稱不可空白或重複。");
            if (string.IsNullOrWhiteSpace(group.Id) || !ids.Add(group.Id)) throw new ArgumentException("分組識別碼不可空白或重複。");
            if (group.SortColumn is { } sort && !Enum.IsDefined(sort)) group.SortColumn = null;
            if (group.Ui.SparklineRange is not ("1h" or "24h")) group.Ui.SparklineRange = "1h";
            var members = new HashSet<string>(StringComparer.Ordinal);
            group.Members = group.Members.OrderBy(m=>m?.Order).ToList();
            for (var i=0; i<group.Members.Count; i++)
            {
                var member = group.Members[i] ?? throw new JsonException("Invalid watchlist member.");
                member.Symbol=SymbolNormalizer.Normalize(member.Symbol); member.Order=i+1;
                if (!members.Add(member.Symbol)) throw new ArgumentException("同一分組不能重複加入幣種。");
                if (registry.Add(member.Symbol)) settings.Symbols.Add(new() { Symbol=member.Symbol, Order=settings.Symbols.Count+1 });
            }
        }
        var holdings = new HashSet<string>(StringComparer.Ordinal);
        foreach (var holding in settings.Holdings)
        {
            if (holding is null) throw new JsonException("Invalid holding.");
            holding.Symbol=SymbolNormalizer.Normalize(holding.Symbol);
            if (holding.Quantity < 0 || holding.AverageCost < 0) throw new ArgumentException("持有數量與平均成本不得為負數。");
            if (!holdings.Add(holding.Symbol)) throw new ArgumentException("每個幣種只能有一筆共用持倉。");
            if (registry.Add(holding.Symbol)) settings.Symbols.Add(new() { Symbol=holding.Symbol, Order=settings.Symbols.Count+1 });
        }
        if (!ids.Contains(settings.ActiveWatchlistId ?? "")) settings.ActiveWatchlistId=settings.Watchlists[0].Id;
    }
}
