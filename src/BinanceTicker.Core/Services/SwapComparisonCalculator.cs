using BinanceTicker.Core.Models;
namespace BinanceTicker.Core.Services;

public static class SwapComparisonCalculator
{
    public static SwapComparisonResult Value(SwapComparisonSetting setting, TickerPrice? from, TickerPrice? to,
        ConnectionStatus status, DateTimeOffset now)
    {
        if(setting.InvalidReason is not null)return new(StateText:"紀錄損壞 · 請修正或刪除");
        if(!setting.Enabled)return new(StateText:"已停用");
        static bool Valid(TickerPrice? p,string symbol,DateTimeOffset at) => p is not null && p.Symbol==symbol &&
            p.Price>0 && new DateTimeOffset(p.UpdatedAt.ToUniversalTime())<=at;
        var missing=new List<string>();
        if(!Valid(from,setting.FromSymbol,now))missing.Add(Asset(setting.FromSymbol));
        if(!Valid(to,setting.ToSymbol,now))missing.Add(Asset(setting.ToSymbol));
        if(missing.Count>0)return new(StateText:"等待 "+string.Join("／",missing)+" 報價");
        var stale=status!=ConnectionStatus.Connected || now-new DateTimeOffset(from!.UpdatedAt.ToUniversalTime())>TimeSpan.FromSeconds(60) ||
            now-new DateTimeOffset(to!.UpdatedAt.ToUniversalTime())>TimeSpan.FromSeconds(60);
        try
        {
            if(setting.FromQuantity<=0 || setting.ToQuantity<=0)return new(StateText:"數量必須大於零");
            var original=setting.FromQuantity*from!.Price;
            var converted=setting.ToQuantity*to!.Price;
            // decimal can underflow to zero even when both operands are positive.
            if(original==0 || converted==0)return new(StateText:"超出可計算範圍");
            var returned=converted/from.Price;
            if(returned==0)return new(StateText:"超出可計算範圍");
            return new(original,converted,returned,returned-setting.FromQuantity,converted-original,
                (converted/original-1)*100,stale,stale ? "使用最後報價" : "即時報價");
        }
        catch(OverflowException) { return new(StateText:"超出可計算範圍"); }
    }
    public static string Asset(string symbol) => symbol.EndsWith("USDT",StringComparison.Ordinal) ? symbol[..^4] : symbol;
}
