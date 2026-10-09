using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using BinanceTicker.Core.Services;

namespace BinanceTicker.Views;

public partial class TickerWindow : Window
{
    public event Action? SettingsRequested;
    public event Action? SwapComparisonsRequested;
    private void SwapComparisonsClicked(object sender, RoutedEventArgs e) => SwapComparisonsRequested?.Invoke();
    public event Action? ThemeRequested;
    public event Action<string>? PriceAlertRequested;
    public event Action<string>? MarketDetailsRequested;
    private void MarketDetailsClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: string symbol }) MarketDetailsRequested?.Invoke(symbol);
    }
    public TickerWindow() => InitializeComponent();
    private void ResizeDragged(object sender, DragDeltaEventArgs e)
    {
        if (sender is not Thumb { Tag: string edge }) return;
        var bounds = WindowPlacement.Resize(new(Left, Top, Width, Height), edge,
            e.HorizontalChange, e.VerticalChange, MinWidth, MinHeight, MaxWidth, MaxHeight);
        Width = bounds.Width; Height = bounds.Height; Left = bounds.X; Top = bounds.Y;
        e.Handled = true;
    }
    private void SettingsClicked(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();
    private void ThemeClicked(object sender, RoutedEventArgs e) => ThemeRequested?.Invoke();
    private void HideClicked(object sender, RoutedEventArgs e) => Close();
    private void PriceAlertClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string symbol }) PriceAlertRequested?.Invoke(symbol);
    }
}
