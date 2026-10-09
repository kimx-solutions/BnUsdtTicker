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
    [JsonIgnore] public string? InvalidReason { get; set; }
    [JsonIgnore] public string? InvalidJson { get; set; }
    public SwapComparisonSetting Copy() => (SwapComparisonSetting)MemberwiseClone();
    public void Validate(DateTimeOffset now)
    {
        if (!string.IsNullOrEmpty(InvalidReason)) throw new ArgumentException("請先修正或刪除損壞的換幣紀錄。");
        if (string.IsNullOrWhiteSpace(Id)) throw new ArgumentException("換幣紀錄缺少識別碼。");
        FromSymbol=SymbolNormalizer.Normalize(FromSymbol);ToSymbol=SymbolNormalizer.Normalize(ToSymbol);
        if(FromSymbol==ToSymbol)throw new ArgumentException("換出與換入幣種不可相同。");
        if(FromQuantity<=0 || ToQuantity<=0)throw new ArgumentException("換出與實際換入數量必須大於零。");
        if(SwappedAt>now)throw new ArgumentException("換幣時間不可在未來。");
        Note ??= "";
    }
}
