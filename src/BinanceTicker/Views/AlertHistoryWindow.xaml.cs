using System.Windows;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Services;

namespace BinanceTicker.Views;

public partial class AlertHistoryWindow : Window
{
    public AlertHistoryViewModel ViewModel { get; }
    public AlertHistoryWindow(AlertHistoryViewModel viewModel)
    {
        InitializeComponent(); ViewModel = viewModel; DataContext = viewModel;
        MaxWidth = SystemParameters.WorkArea.Width; MaxHeight = SystemParameters.WorkArea.Height;
    }
    protected override void OnSourceInitialized(EventArgs e) { base.OnSourceInitialized(e); RefreshTheme(); }
    public void RefreshTheme() => ThemeService.RefreshWindowFrame(this);
}
