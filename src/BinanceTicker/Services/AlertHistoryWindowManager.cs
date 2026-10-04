using System.Windows;
using BinanceTicker.Core.Services;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Views;

namespace BinanceTicker.Services;

public sealed class AlertHistoryWindowManager(AlertHistoryService history)
{
    private AlertHistoryWindow? window;
    public AlertHistoryWindow Show()
    {
        if (window is null)
        {
            var vm = new AlertHistoryViewModel(history); vm.Reload();
            window = new(vm); window.Closed += (_, _) => window = null;
        }
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Show(); window.Activate(); return window;
    }
    public void Refresh() => window?.ViewModel.RefreshAfterSubmission();
    public void RefreshTheme() => window?.RefreshTheme();
    public void Close() => window?.Close();
}
