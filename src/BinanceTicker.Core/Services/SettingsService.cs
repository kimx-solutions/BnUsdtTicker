using System.Text.Json;
using System.Text.Json.Serialization;
using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    public string FilePath { get; }
    public string? LoadWarning { get; private set; }

    public SettingsService(string? filePath = null) => FilePath = filePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BinanceTicker", "settings.json");

    public AppSettings Load()
    {
        LoadWarning = null;
        if (!File.Exists(FilePath)) return new();
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options)
                ?? throw new JsonException("Empty settings.");
            Normalize(settings);
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            File.Copy(FilePath, FilePath + ".bak", true);
            var defaults = new AppSettings();
            Save(defaults);
            LoadWarning = "設定檔損壞，已備份至 settings.json.bak 並還原預設值。";
            return defaults;
        }
    }

    public void Save(AppSettings settings)
    {
        var copy = settings.Copy();
        Normalize(copy);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
        var tempPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(copy, Options));
            File.Move(tempPath, FilePath, true);
        }
        finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
    }

    private static void Normalize(AppSettings settings)
    {
        if (settings.Window is null || settings.Ui is null || settings.Symbols is null ||
            settings.Symbols.Any(s => s is null) || !Enum.IsDefined(settings.Mode) || !Enum.IsDefined(settings.Ui.Theme))
            throw new JsonException("Invalid settings structure.");
        settings.Window.Left = double.IsFinite(settings.Window.Left) ? settings.Window.Left : 100;
        settings.Window.Top = double.IsFinite(settings.Window.Top) ? settings.Window.Top : 100;
        settings.Window.Opacity = double.IsFinite(settings.Window.Opacity) ? Math.Clamp(settings.Window.Opacity, 0.2, 1) : 0.95;
        var symbols = new HashSet<string>(StringComparer.Ordinal);
        settings.Symbols = settings.Symbols.OrderBy(s => s.Order).Where(s =>
        {
            s.Symbol = SymbolNormalizer.Normalize(s.Symbol);
            return symbols.Add(s.Symbol);
        }).ToList();
        for (var i = 0; i < settings.Symbols.Count; i++) settings.Symbols[i].Order = i + 1;
    }
}
