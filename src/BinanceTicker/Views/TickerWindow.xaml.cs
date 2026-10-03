using System.Windows;

namespace BinanceTicker.Views;

public partial class TickerWindow : Window
{
    public event Action? SettingsRequested;
    public event Action? ThemeRequested;
    public TickerWindow() => InitializeComponent();
    private void SettingsClicked(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();
    private void ThemeClicked(object sender, RoutedEventArgs e) => ThemeRequested?.Invoke();
    private void HideClicked(object sender, RoutedEventArgs e) => Close();
}
