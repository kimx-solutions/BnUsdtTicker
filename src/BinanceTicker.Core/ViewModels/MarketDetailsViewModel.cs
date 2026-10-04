using BinanceTicker.Core.Services;
namespace BinanceTicker.Core.ViewModels;

public sealed class MarketDetailsViewModel : ObservableObject, IDisposable
{
    private bool disposed;
    private string error = "";
    public TickerRowViewModel Row { get; }
    public string Error { get => error; private set => Set(ref error, value); }
    public AsyncCommand RetryCommand { get; }
    public RelayCommand OpenTradeCommand { get; }
    public MarketDetailsViewModel(TickerRowViewModel row, Func<Task> retry, Action<Uri> openBrowser)
    {
        Row = row;
        RetryCommand = new(async () =>
        {
            Error = "";
            try { await retry(); }
            catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
            { Error = "無法載入走勢資料，請稍後重試。"; }
        }, () => !disposed);
        OpenTradeCommand = new(() =>
        {
            Error = "";
            try { openBrowser(BinanceTradeLink.Create(Row.Symbol)); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or NotSupportedException)
            { Error = "無法開啟瀏覽器，請確認預設瀏覽器設定。"; }
        }, () => !disposed);
    }
    public void Dispose() { disposed = true; RetryCommand.Refresh(); OpenTradeCommand.Refresh(); }
}
