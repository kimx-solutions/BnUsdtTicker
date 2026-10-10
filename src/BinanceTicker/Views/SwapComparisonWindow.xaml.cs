using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Services;
namespace BinanceTicker.Views;

public partial class SwapComparisonWindow : Window
{
    public SwapComparisonViewModel ViewModel { get; }
    internal bool ClosingForExit { get; set; }
    private SwapComparisonEditorWindow? editor;
    private SwapSettlementWindow? settlementWindow;
    public SwapComparisonWindow(SwapComparisonViewModel viewModel)
    {
        InitializeComponent();ViewModel=viewModel;DataContext=viewModel;
        MaxWidth=SystemParameters.WorkArea.Width;MaxHeight=SystemParameters.WorkArea.Height;
        Width=Math.Min(Width,MaxWidth);Height=Math.Min(Height,MaxHeight);
        ViewModel.PropertyChanged+=Changed;
        Closed+=(_,_)=>{ViewModel.PropertyChanged-=Changed;ViewModel.Cancel();ViewModel.CancelSettlement();};
        RefreshButtons();
    }
    protected override void OnSourceInitialized(EventArgs e) { base.OnSourceInitialized(e);RefreshTheme(); }
    protected override void OnClosing(CancelEventArgs e)
    {
        if(ViewModel.IsBusy && !ClosingForExit)e.Cancel=true;
        if(!e.Cancel && editor is not null) { editor.ClosingForOwner=true;editor.Close(); }
        if(!e.Cancel && settlementWindow is not null) { settlementWindow.ClosingForOwner=true;settlementWindow.Close(); }
        base.OnClosing(e);
    }
    public void RefreshTheme()
    {
        ThemeService.RefreshWindowFrame(this);
        if(editor is not null)ThemeService.RefreshWindowFrame(editor);
        if(settlementWindow is not null)ThemeService.RefreshWindowFrame(settlementWindow);
    }
    private void Changed(object? sender,PropertyChangedEventArgs e)
    {
        RefreshButtons();
        if(e.PropertyName==nameof(ViewModel.IsEditing))
        {
            if(ViewModel.IsEditing && editor is null)
            {
                editor=new SwapComparisonEditorWindow(ViewModel) { Owner=this,Topmost=Topmost };
                editor.Closed+=(_,_)=> { editor=null;IsEnabled=true;Activate(); };
                IsEnabled=false;editor.Show();
            }
            else if(!ViewModel.IsEditing && editor is not null)editor.Close();
        }
        if(e.PropertyName==nameof(ViewModel.IsSettling))
        {
            if(ViewModel.IsSettling && settlementWindow is null)
            {
                settlementWindow=new(ViewModel) { Owner=this,Topmost=Topmost };
                settlementWindow.Closed+=(_,_)=> { settlementWindow=null;IsEnabled=true;ViewModel.CancelSettlement();Activate(); };
                IsEnabled=false;settlementWindow.Show();
            }
            else if(!ViewModel.IsSettling && settlementWindow is not null)settlementWindow.Close();
        }
        if(e.PropertyName==nameof(ViewModel.Selected) && ViewModel.Selected is not null)
            RecordsGrid.ScrollIntoView(ViewModel.Selected);
    }
    private void RecordDoubleClicked(object sender,MouseButtonEventArgs e)
    {
        var source=e.OriginalSource as DependencyObject;
        while(source is not null && source is not DataGridRow)source=VisualTreeHelper.GetParent(source);
        if(source is DataGridRow && ViewModel.EditCommand.CanExecute(null))ViewModel.EditCommand.Execute(null);
    }
    private void RecordsKeyDown(object sender,KeyEventArgs e)
    {
        if(e.Key is Key.Enter or Key.F2 && ViewModel.EditCommand.CanExecute(null))
        { ViewModel.EditCommand.Execute(null);e.Handled=true; }
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
