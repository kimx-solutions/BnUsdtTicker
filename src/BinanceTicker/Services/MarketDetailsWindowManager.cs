using System.Windows;
using BinanceTicker.Core.Services;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Views;
namespace BinanceTicker.Services;

public sealed class MarketDetailsWindowManager(Func<string, MarketDetailsViewModel?> createViewModel)
{
    private readonly Dictionary<string,MarketDetailsWindow> windows=new(StringComparer.Ordinal);
    public IReadOnlyCollection<string> OpenSymbols => Array.AsReadOnly(windows.Keys.ToArray());
    public event Action? OpenSymbolsChanged;
    public void Show(string symbol)
    {
        symbol=SymbolNormalizer.Normalize(symbol);
        if(windows.TryGetValue(symbol,out var existing))
        {
            if(existing.WindowState==WindowState.Minimized) existing.WindowState=WindowState.Normal;
            existing.Show(); existing.Activate(); return;
        }
        var viewModel=createViewModel(symbol);
        if(viewModel is null) return;
        var window=new MarketDetailsWindow(viewModel);
        windows.Add(symbol,window);
        window.Closed+=(_,_)=>{windows.Remove(symbol);viewModel.Dispose();OpenSymbolsChanged?.Invoke();};
        window.Show();window.Activate();OpenSymbolsChanged?.Invoke();
    }
    public void CloseUnavailable(IReadOnlyCollection<string> enabledSymbols)
    {
        var available=enabledSymbols.ToHashSet(StringComparer.Ordinal);
        foreach(var symbol in windows.Keys.Where(s=>!available.Contains(s)).ToArray()) windows[symbol].Close();
    }
    public void RefreshTheme() {foreach(var window in windows.Values) window.RefreshTheme();}
    public void CloseAll() {foreach(var window in windows.Values.ToArray()) window.Close();}
}
