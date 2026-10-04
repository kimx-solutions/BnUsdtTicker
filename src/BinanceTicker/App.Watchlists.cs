using System.Text.Json;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
namespace BinanceTicker;

public partial class App
{
    private Action<AppSettings>? watchlistPreferenceWriter=null;
    private string[] subscribedSymbols=[];
    private void PersistWatchlistPreferences()
    {
        if(exiting)return;
        var updated=settings.Copy();ticker.WriteWatchlistPreferences(updated);
        try
        {
            (watchlistPreferenceWriter ?? settingsService.Save)(updated);
            settings.Watchlists=updated.Watchlists;settings.ActiveWatchlistId=updated.ActiveWatchlistId;
            settings.Ui=updated.Ui;ticker.WatchlistError="";
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { ticker.Configure(settings);ticker.WatchlistError="分組／顯示設定儲存失敗："+ex.Message; }
        UpdateHistoryDemand();RefreshMarketGraphs();
    }
    private bool MarketDemandChanged(AppSettings updated) => !subscribedSymbols.SequenceEqual(MarketDemand.Quotes(updated));
}
