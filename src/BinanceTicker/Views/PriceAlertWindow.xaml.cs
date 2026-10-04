using System.Windows;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Services;

namespace BinanceTicker.Views;

public partial class PriceAlertWindow : Window
{
    private readonly Func<PriceAlertSettings, IReadOnlyList<AlertResetRequest>, Task<bool>> save;
    private bool saving;
    public PriceAlertEditorViewModel ViewModel { get; }
    public event Action? HistoryRequested;

    public PriceAlertWindow(PriceAlertEditorViewModel viewModel,
        Func<PriceAlertSettings, IReadOnlyList<AlertResetRequest>, Task<bool>> save)
    {
        InitializeComponent();
        ViewModel = viewModel; DataContext = viewModel; this.save = save;
        Title = viewModel.Symbol + " 價格警示";
        var area = SystemParameters.WorkArea;
        MaxHeight = area.Height; MaxWidth = area.Width;
        Height = Math.Min(Height, area.Height); Width = Math.Min(Width, area.Width);
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + (area.Height - Height) / 2;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        RefreshTheme();
    }
    public void RefreshTheme() => ThemeService.RefreshWindowFrame(this);
    private void CancelClicked(object sender, RoutedEventArgs e) => Close();
    private void HistoryClicked(object sender, RoutedEventArgs e) => HistoryRequested?.Invoke();
    private async void SaveClicked(object sender, RoutedEventArgs e)
    {
        if (saving) return;
        saving = true; IsEnabled = false; ViewModel.Error = "";
        try { if (await save(ViewModel.CreateAlert(), ViewModel.GetAlertResets())) Close(); }
        catch (ArgumentException ex) { ViewModel.Error = ex.Message; }
        finally { saving = false; IsEnabled = true; }
    }
}
