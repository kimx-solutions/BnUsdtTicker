using System.Text.Json;
using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.Services;

public sealed class SettingsService
{
    public string FilePath { get; }
    public string? LoadWarning { get; private set; }

    public SettingsService(string? filePath = null) => FilePath = filePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BinanceTicker", "settings.json");

    public AppSettings Load()
    {
        lock (AtomicJsonFile.Gate(FilePath)) return LoadCore();
    }

    private AppSettings LoadCore()
    {
        LoadWarning = null;
        if (!File.Exists(FilePath)) return new();
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), AtomicJsonFile.Options)
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
        lock (AtomicJsonFile.Gate(FilePath))
        {
            var copy = settings.Copy();
            Normalize(copy);
            AtomicJsonFile.Write(FilePath, copy);
        }
    }

    private static void Normalize(AppSettings settings)
    {
        if (settings.Window is null || settings.Ui is null || settings.Symbols is null ||
            settings.Symbols.Any(s => s is null) || !Enum.IsDefined(settings.Mode) || !Enum.IsDefined(settings.Ui.Theme))
            throw new JsonException("Invalid settings structure.");
        settings.Window.Left = double.IsFinite(settings.Window.Left) ? settings.Window.Left : 100;
        settings.Window.Width = double.IsFinite(settings.Window.Width) && settings.Window.Width >= WindowSettings.MinimumWidth ? settings.Window.Width : WindowSettings.DefaultWidth;
        settings.Window.Height = double.IsFinite(settings.Window.Height) && settings.Window.Height >= WindowSettings.MinimumHeight ? settings.Window.Height : WindowSettings.DefaultHeight;
        settings.Hotkey ??= new();
        try { settings.Hotkey.Gesture = HotkeyGesture.Parse(settings.Hotkey.Gesture).ToString(); }
        catch (ArgumentException) { settings.Hotkey = new(); }
        if (settings.Ui.SparklineRange is not ("1h" or "24h")) settings.Ui.SparklineRange = "1h";
        settings.Window.Top = double.IsFinite(settings.Window.Top) ? settings.Window.Top : 100;
        settings.Window.Opacity = double.IsFinite(settings.Window.Opacity) ? Math.Clamp(settings.Window.Opacity, 0.2, 1) : 0.95;
        var symbols = new HashSet<string>(StringComparer.Ordinal);
        settings.Symbols = settings.Symbols.OrderBy(s => s.Order).Where(s =>
        {
            s.Symbol = SymbolNormalizer.Normalize(s.Symbol);
            s.Alert ??= new();
            if (s.Alert.UpperPrice is <= 0 || s.Alert.LowerPrice is <= 0)
                throw new JsonException("Alert prices must be positive.");
            return symbols.Add(s.Symbol);
        }).ToList();
        for (var i = 0; i < settings.Symbols.Count; i++) settings.Symbols[i].Order = i + 1;
    }

}
