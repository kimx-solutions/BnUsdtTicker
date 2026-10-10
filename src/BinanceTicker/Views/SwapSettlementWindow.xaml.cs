using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Services;

namespace BinanceTicker.Views;

public partial class SwapSettlementWindow : Window
{
    private readonly SwapComparisonViewModel viewModel;
    internal bool ClosingForOwner { get; set; }
    public SwapSettlementWindow(SwapComparisonViewModel viewModel)
    {
        InitializeComponent();this.viewModel=viewModel;DataContext=viewModel.SettlementEditor;
        MaxWidth=SystemParameters.WorkArea.Width;MaxHeight=SystemParameters.WorkArea.Height;
        Width=Math.Min(Width,MaxWidth);Height=Math.Min(Height,MaxHeight);
        Loaded+=(_,_)=>ActualReturnInput.Focus();
    }
    protected override void OnSourceInitialized(EventArgs e)
    { base.OnSourceInitialized(e);ThemeService.RefreshWindowFrame(this); }
    protected override void OnClosing(CancelEventArgs e)
    { if(viewModel.IsBusy && viewModel.IsSettling && !ClosingForOwner)e.Cancel=true;base.OnClosing(e); }
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if(e.Key==Key.Escape) { viewModel.CancelSettlement();e.Handled=true; }
        base.OnPreviewKeyDown(e);
    }
}
