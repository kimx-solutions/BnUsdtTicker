namespace BinanceTicker.Core.Models;

public enum DisplayMode { Fix, Float }
public enum ColorTheme { Dark, Light }
public enum ConnectionStatus { Connecting, Connected, Disconnected }

public sealed class AppSettings
{
    public DisplayMode Mode { get; set; } = DisplayMode.Fix;
    public bool ShowOnStartup { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public HotkeySettings Hotkey { get; set; } = new();
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
        Mode = Mode, ShowOnStartup = ShowOnStartup, StartWithWindows = StartWithWindows,
        Hotkey = new() { Enabled = Hotkey.Enabled, Gesture = Hotkey.Gesture },
        Window = new() { Left = Window.Left, Top = Window.Top, Width = Window.Width, Height = Window.Height, Opacity = Window.Opacity },
        Ui = new() { CompactMode = Ui.CompactMode, ShowChangePercent = Ui.ShowChangePercent, Theme = Ui.Theme,
            ShowSparkline = Ui.ShowSparkline, SparklineRange = Ui.SparklineRange },
        Symbols = Symbols.Select(s => s.Copy()).ToList()
    };
}

public sealed class WindowSettings
{
    public const double DefaultWidth = 390, DefaultHeight = 360, MinimumWidth = 360, MinimumHeight = 260;
    public double Width { get; set; } = DefaultWidth;
    public double Height { get; set; } = DefaultHeight;
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public double Opacity { get; set; } = 0.95;
}

public sealed class HotkeySettings
{
    public bool Enabled { get; set; }
    public string Gesture { get; set; } = "Ctrl+Alt+T";
}

public sealed class UiSettings
{
    public bool ShowSparkline { get; set; } = true;
    public string SparklineRange { get; set; } = "1h";
    public ColorTheme Theme { get; set; } = ColorTheme.Dark;
    public bool ShowChangePercent { get; set; } = true;
    public bool CompactMode { get; set; } = true;
}
