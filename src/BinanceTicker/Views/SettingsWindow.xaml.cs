using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.ViewModels;

namespace BinanceTicker.Views;

public partial class SettingsWindow : Window
{
    private readonly Func<AppSettings, Task<bool>> save;
    private bool saving;
    public SettingsViewModel ViewModel { get; }
    public SettingsWindow(SettingsViewModel viewModel, Func<AppSettings, Task<bool>> save)
    {
        InitializeComponent();
        ViewModel = viewModel; DataContext = viewModel; this.save = save;
        Closed += (_, _) => ViewModel.Dispose();
        // Explicit primary placement matches WorkArea. CenterScreen can choose the mouse's monitor.
        var area = SystemParameters.WorkArea;
        WindowStartupLocation = WindowStartupLocation.Manual;
        MaxHeight = area.Height;
        MaxWidth = area.Width;
        Height = Math.Min(Height, area.Height);
        Width = Math.Min(Width, area.Width);
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + (area.Height - Height) / 2;
    }
    private void SelectSymbolRow(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        var list = (ListBox)sender;
        if (e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement(list, source) is ListBoxItem item)
            item.IsSelected = true;
    }
    private void CancelClicked(object sender, RoutedEventArgs e) => Close();
    private async void SaveClicked(object sender, RoutedEventArgs e)
    {
        if (saving || !ViewModel.CanSave) return;
        saving = true; IsEnabled = false;
        try { if (await save(ViewModel.CreateSettings())) Close(); }
        finally { saving = false; IsEnabled = true; }
    }
}
