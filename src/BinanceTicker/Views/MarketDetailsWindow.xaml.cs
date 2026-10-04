using System.Windows;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Services;
namespace BinanceTicker.Views;

public partial class MarketDetailsWindow : Window
{
    public MarketDetailsViewModel ViewModel { get; }
    public MarketDetailsWindow(MarketDetailsViewModel viewModel)
    {
        InitializeComponent();
        ViewModel=viewModel; DataContext=viewModel; Title=viewModel.Row.Symbol+" 行情詳情";
        var area=SystemParameters.WorkArea;
        MaxWidth=area.Width; MaxHeight=area.Height;
        Width=Math.Min(Width,area.Width); Height=Math.Min(Height,area.Height);
        Left=area.Left+(area.Width-Width)/2; Top=area.Top+(area.Height-Height)/2;
    }
    protected override void OnSourceInitialized(EventArgs e) { base.OnSourceInitialized(e); RefreshTheme(); }
    public void RefreshTheme() => ThemeService.RefreshWindowFrame(this);
}
