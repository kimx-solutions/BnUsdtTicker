using System.Windows;

namespace BinanceTicker.Views;

public partial class TickerWindow : Window
{
    public event Action? SettingsRequested;
    public TickerWindow() => InitializeComponent();
    private void SettingsClicked(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();
    private void HideClicked(object sender, RoutedEventArgs e) => Close();
}
