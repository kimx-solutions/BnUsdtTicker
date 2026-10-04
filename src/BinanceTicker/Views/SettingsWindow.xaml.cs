using BinanceTicker.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Interop;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.ViewModels;

namespace BinanceTicker.Views;

public partial class SettingsWindow : Window
{
    private readonly Func<AppSettings, Task<bool>> save;
    private bool saving;
    public SettingsViewModel ViewModel { get; }
    public SettingsWindow(SettingsViewModel viewModel, Func<AppSettings, Task<bool>> save, Window? owner = null)
    {
        InitializeComponent();
        ViewModel = viewModel; DataContext = viewModel; this.save = save;
        if (owner is not null)
        {
            // Tray settings can open before the ticker is first shown; create its hidden HWND for ownership.
            new WindowInteropHelper(owner).EnsureHandle();
            Owner = owner;
            SetBinding(TopmostProperty, new Binding(nameof(Topmost)) { Source = owner, Mode = BindingMode.OneWay });
        }
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
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        RefreshTheme();
    }
    public void RefreshTheme() => ThemeService.RefreshWindowFrame(this);

    private void SelectSymbolRow(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        var list = (ListBox)sender;
        if (e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement(list, source) is ListBoxItem item)
            item.IsSelected = true;
    }
    private void CancelClicked(object sender, RoutedEventArgs e) => Close();
    private void DeleteWatchlistClicked(object sender, RoutedEventArgs e)
    {
        if(!ViewModel.RemoveWatchlistCommand.CanExecute(null)) { ViewModel.Error="至少須保留一個分組。";return; }
        if(MessageBox.Show(this,SettingsViewModel.DeleteWatchlistExplanation,"刪除分組",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes)
            ViewModel.RemoveWatchlistCommand.Execute(null);
    }
    private async void SaveClicked(object sender, RoutedEventArgs e)
    {
        if (saving || !ViewModel.CanSave) return;
        saving = true; IsEnabled = false;
        try { if (await save(ViewModel.CreateSettings())) Close(); }
        catch (ArgumentException ex) { ViewModel.Error = ex.Message; }
        finally { saving = false; IsEnabled = true; }
    }
}
