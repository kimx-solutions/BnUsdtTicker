using System.ComponentModel;
using System.Windows;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Services;
namespace BinanceTicker.Views;

public partial class SwapComparisonWindow : Window
{
    public SwapComparisonViewModel ViewModel { get; }
    internal bool ClosingForExit { get; set; }
    public SwapComparisonWindow(SwapComparisonViewModel viewModel)
    {
        InitializeComponent();ViewModel=viewModel;DataContext=viewModel;
        MaxWidth=SystemParameters.WorkArea.Width;MaxHeight=SystemParameters.WorkArea.Height;
        Width=Math.Min(Width,MaxWidth);Height=Math.Min(Height,MaxHeight);
        ViewModel.PropertyChanged+=Changed;
        Closed+=(_,_)=>{ViewModel.PropertyChanged-=Changed;ViewModel.Cancel();};
        RefreshButtons();
    }
    protected override void OnSourceInitialized(EventArgs e) { base.OnSourceInitialized(e);RefreshTheme(); }
    protected override void OnClosing(CancelEventArgs e)
    {
        if(ViewModel.IsBusy && !ClosingForExit)e.Cancel=true;
        base.OnClosing(e);
    }
    public void RefreshTheme() => ThemeService.RefreshWindowFrame(this);
    private void Changed(object? sender,PropertyChangedEventArgs e)
    {
        RefreshButtons();
        if(e.PropertyName==nameof(ViewModel.IsEditing))
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(()=>
            {
                if(ViewModel.IsEditing)EditorPanel.BringIntoView();
                else ComparisonScroll.ScrollToTop();
            }));
    }
    private void RefreshButtons() => DeleteButton.IsEnabled=ViewModel.DeleteCommand.CanExecute(null);
    private void DeleteClicked(object sender,RoutedEventArgs e)
    {
        if(ViewModel.DeleteCommand.CanExecute(null) &&
            MessageBox.Show(this,"刪除此換幣紀錄？不會修改持倉。","刪除換幣紀錄",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes)
            ViewModel.DeleteCommand.Execute(null);
    }
    private void RemoveInvalidClicked(object sender,RoutedEventArgs e)
    {
        if(ViewModel.RemoveInvalidCommand.CanExecute(null) &&
            MessageBox.Show(this,"移除所有損壞的換幣紀錄？原檔備份仍保留。","移除損壞紀錄",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes)
            ViewModel.RemoveInvalidCommand.Execute(null);
    }
}
