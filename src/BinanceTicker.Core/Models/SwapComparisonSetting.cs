using System.Text.Json.Serialization;
using BinanceTicker.Core.Services;
namespace BinanceTicker.Core.Models;

[JsonConverter(typeof(SwapComparisonSettingConverter))]
public sealed class SwapComparisonSetting
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FromSymbol { get; set; } = "";
    public decimal FromQuantity { get; set; }
    public string ToSymbol { get; set; } = "";
    public decimal ToQuantity { get; set; }
    public DateTimeOffset SwappedAt { get; set; } = DateTimeOffset.Now;
    public string Note { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public List<SwapSettlement> Settlements { get; set; }=[];
    [JsonIgnore] public decimal RemainingToQuantity => ToQuantity-Settlements.Sum(s=>s.ToQuantity);
    [JsonIgnore] public decimal RemainingFromQuantity => FromQuantity-Settlements.Sum(s=>s.FromQuantity);
    [JsonIgnore] public bool IsClosed => InvalidReason is null && Settlements.Count>0 && RemainingToQuantity==0;
    [JsonIgnore] public string? InvalidReason { get; set; }
    [JsonIgnore] public string? InvalidJson { get; set; }
    public SwapComparisonSetting Copy()
    {
        var copy=(SwapComparisonSetting)MemberwiseClone();
        copy.Settlements=Settlements.ToList();return copy;
    }
    public void Validate(DateTimeOffset now)
    {
        if (!string.IsNullOrEmpty(InvalidReason)) throw new ArgumentException("請先修正或刪除損壞的換幣紀錄。");
        if (string.IsNullOrWhiteSpace(Id)) throw new ArgumentException("換幣紀錄缺少識別碼。");
        FromSymbol=SymbolNormalizer.Normalize(FromSymbol);ToSymbol=SymbolNormalizer.Normalize(ToSymbol);
        if(FromSymbol==ToSymbol)throw new ArgumentException("換出與換入幣種不可相同。");
        if(FromQuantity<=0 || ToQuantity<=0)throw new ArgumentException("換出與實際換入數量必須大於零。");
        if(SwappedAt>now)throw new ArgumentException("換幣時間不可在未來。");
        var remainingTo=ToQuantity;var remainingFrom=FromQuantity;var last=SwappedAt;
        decimal totalReturned=0,totalAllocated=0,totalFromValue=0,totalToValue=0;
        var ids=new HashSet<string>(StringComparer.Ordinal);
        try
        {
            foreach(var settlement in Settlements)
            {
                if(settlement is null || string.IsNullOrWhiteSpace(settlement.Id) || !ids.Add(settlement.Id) ||
                    !Enum.IsDefined(settlement.Mode) || settlement.ToQuantity<=0 || settlement.ToQuantity>remainingTo ||
                    settlement.ReturnedQuantity<=0 || settlement.SettledAt<last || settlement.SettledAt>now ||
                    settlement.FromPrice is <=0 || settlement.ToPrice is <=0)
                    throw new ArgumentException("結算紀錄的數量、時間或報價無效。");
                var allocated=settlement.ToQuantity==remainingTo ? remainingFrom : remainingFrom*(settlement.ToQuantity/remainingTo);
                if(allocated<=0 || settlement.FromQuantity!=allocated)
                    throw new ArgumentException("結算紀錄的原幣分攤數量無效。");
                if(settlement.Mode==SwapSettlementMode.MarketQuote &&
                    (settlement.FromPrice is null || settlement.ToPrice is null || settlement.FromQuoteAt is null || settlement.ToQuoteAt is null ||
                     settlement.FromQuoteAt>settlement.SettledAt || settlement.ToQuoteAt>settlement.SettledAt ||
                     settlement.SettledAt-settlement.FromQuoteAt>TimeSpan.FromSeconds(60) || settlement.SettledAt-settlement.ToQuoteAt>TimeSpan.FromSeconds(60) ||
                     settlement.ReturnedQuantity!=settlement.ToQuantity*settlement.ToPrice.Value/settlement.FromPrice.Value))
                    throw new ArgumentException("報價結算缺少有效的報價快照。");
                remainingTo-=settlement.ToQuantity;remainingFrom-=allocated;last=settlement.SettledAt;
                _=(settlement.ReturnedQuantity/allocated-1)*100;
                totalReturned+=settlement.ReturnedQuantity;totalAllocated+=allocated;
                _=(totalReturned/totalAllocated-1)*100;
                if(settlement.FromPrice is { } price)
                {
                    totalFromValue+=allocated*price;totalToValue+=settlement.ReturnedQuantity*price;
                    _=(settlement.ReturnedQuantity-allocated)*price;
                }
            }
        }
        catch(OverflowException ex) { throw new ArgumentException("結算數量或參考價超出可計算範圍。",ex); }
        Note ??= "";
    }
}
