using System.Windows;
using System.Windows.Controls;

namespace BinanceTicker.Views;

public partial class TickerWindow : Window
{
    public event Action? SettingsRequested;
    public event Action? ThemeRequested;
    public event Action<string>? PriceAlertRequested;
    public event Action<string>? MarketDetailsRequested;
    private void MarketDetailsClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: string symbol }) MarketDetailsRequested?.Invoke(symbol);
    }
    public TickerWindow() => InitializeComponent();
    private void SettingsClicked(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();
    private void ThemeClicked(object sender, RoutedEventArgs e) => ThemeRequested?.Invoke();
    private void HideClicked(object sender, RoutedEventArgs e) => Close();
    private void PriceAlertClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string symbol }) PriceAlertRequested?.Invoke(symbol);
    }
}
