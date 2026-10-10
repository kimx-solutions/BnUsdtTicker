using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Services;

namespace BinanceTicker.Views;

public partial class SwapComparisonEditorWindow : Window
{
    private readonly SwapComparisonViewModel viewModel;
    internal bool ClosingForOwner { get; set; }

    public SwapComparisonEditorWindow(SwapComparisonViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel=viewModel;DataContext=viewModel;
        MaxWidth=SystemParameters.WorkArea.Width;MaxHeight=SystemParameters.WorkArea.Height;
        Width=Math.Min(Width,MaxWidth);Height=Math.Min(Height,MaxHeight);
        Loaded+=(_,_)=> { FromSymbolInput.Focus();FromSymbolInput.SelectAll(); };
        Closed+=(_,_)=> { if(viewModel.IsEditing)viewModel.Cancel(); };
    }

    protected override void OnSourceInitialized(EventArgs e)
    { base.OnSourceInitialized(e);ThemeService.RefreshWindowFrame(this); }

    protected override void OnClosing(CancelEventArgs e)
    {
        if(viewModel.IsBusy && viewModel.IsEditing && !ClosingForOwner)e.Cancel=true;
        base.OnClosing(e);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if(e.Key==Key.Escape)
        { if(viewModel.CancelCommand.CanExecute(null))viewModel.CancelCommand.Execute(null);e.Handled=true; }
        base.OnPreviewKeyDown(e);
    }
}
