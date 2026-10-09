using System.Windows;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Views;
namespace BinanceTicker.Services;

public sealed class SwapComparisonWindowManager(SwapComparisonViewModel viewModel,DisplayMode mode)
{
    private SwapComparisonWindow? window;
    private DisplayMode currentMode=mode;
    public SwapComparisonWindow Show()
    {
        if(window is null)
        {
            window=new(viewModel){Topmost=currentMode==DisplayMode.Fix};
            window.Closed+=(_,_)=>window=null;
        }
        if(window.WindowState==WindowState.Minimized)window.WindowState=WindowState.Normal;
        window.Show();window.Activate();return window;
    }
    public void SetMode(DisplayMode mode) { currentMode=mode;if(window is not null)window.Topmost=mode==DisplayMode.Fix; }
    public void RefreshTheme() => window?.RefreshTheme();
    public void Close()
    {
        if(window is null)return;
        window.ClosingForExit=true;window.Close();
    }
}
