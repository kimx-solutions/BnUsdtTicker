using System.Text.Json;
using System.Text.Json.Serialization;
using BinanceTicker.Core.Models;
namespace BinanceTicker.Core.Services;

/// <summary>Isolates malformed comparison records without resetting unrelated settings.</summary>
public sealed class SwapComparisonSettingConverter : JsonConverter<SwapComparisonSetting>
{
    public override bool HandleNull => true;
    public override SwapComparisonSetting Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc=JsonDocument.ParseValue(ref reader);
        var value=doc.RootElement;
        try
        {
            var data=value.Deserialize<ComparisonData>(options) ?? throw new JsonException("Empty comparison.");
            var record=new SwapComparisonSetting
            {
                Id=data.Id,FromSymbol=data.FromSymbol,FromQuantity=data.FromQuantity,
                ToSymbol=data.ToSymbol,ToQuantity=data.ToQuantity,SwappedAt=data.SwappedAt,
                Note=data.Note ?? "",Enabled=data.Enabled,Settlements=data.Settlements ?? []
            };
            record.Validate(DateTimeOffset.UtcNow);
            return record;
        }
        catch(Exception ex) when(ex is JsonException or ArgumentException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            return new() { InvalidReason="紀錄欄位無效："+ex.Message, InvalidJson=value.GetRawText(), Enabled=false };
        }
    }
    public override void Write(Utf8JsonWriter writer, SwapComparisonSetting value, JsonSerializerOptions options)
    {
        if(value.InvalidJson is { } raw) { writer.WriteRawValue(raw);return; }
        JsonSerializer.Serialize(writer,new
        {
            value.Id,value.FromSymbol,value.FromQuantity,value.ToSymbol,value.ToQuantity,value.SwappedAt,value.Note,value.Enabled,value.Settlements
        },options);
    }
    private sealed class ComparisonData
    {
        public required string Id { get; set; }
        public required string FromSymbol { get; set; }
        public required decimal FromQuantity { get; set; }
        public required string ToSymbol { get; set; }
        public required decimal ToQuantity { get; set; }
        public required DateTimeOffset SwappedAt { get; set; }
        public string? Note { get; set; }
        public bool Enabled { get; set; }=true;
        public List<SwapSettlement>? Settlements { get; set; }
    }
}
