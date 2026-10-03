namespace BinanceTicker.Core.Models;

public enum DisplayMode { Fix, Float }
public enum ColorTheme { Dark, Light }
public enum ConnectionStatus { Connecting, Connected, Disconnected }

public sealed class AppSettings
{
    public DisplayMode Mode { get; set; } = DisplayMode.Fix;
    public bool ShowOnStartup { get; set; } = true;
    public WindowSettings Window { get; set; } = new();
    public UiSettings Ui { get; set; } = new();
    public List<SymbolSetting> Symbols { get; set; } =
    [
        new() { Symbol = "BTCUSDT", Order = 1 },
        new() { Symbol = "ETHUSDT", Order = 2 },
        new() { Symbol = "ENAUSDT", Order = 3 }
    ];

    public AppSettings Copy() => new()
    {
        Mode = Mode, ShowOnStartup = ShowOnStartup,
        Window = new() { Left = Window.Left, Top = Window.Top, Opacity = Window.Opacity },
        Ui = new() { CompactMode = Ui.CompactMode, ShowChangePercent = Ui.ShowChangePercent, Theme = Ui.Theme },
        Symbols = Symbols.Select(s => s.Copy()).ToList()
    };
}

public sealed class WindowSettings
{
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public double Opacity { get; set; } = 0.95;
}

public sealed class UiSettings
{
    public ColorTheme Theme { get; set; } = ColorTheme.Dark;
    public bool ShowChangePercent { get; set; } = true;
    public bool CompactMode { get; set; } = true;
}
