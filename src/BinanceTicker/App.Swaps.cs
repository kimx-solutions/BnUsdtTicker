using System.Text.Json;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Services;
namespace BinanceTicker;

public partial class App
{
    private SwapComparisonViewModel? swapComparisons;
    private SwapComparisonWindowManager? swapWindow;
    private readonly SemaphoreSlim settingsMutationGate=new(1,1);
    private void InitializeSwapComparisons()
    {
        swapComparisons=new(settings,binance,SaveSwapComparisonsAsync);
        swapWindow=new(swapComparisons,settings.Mode);
    }
    private void OpenSwapComparisons() { if(!exiting)swapWindow?.Show(); }
    private void PreserveSwapComparisons(AppSettings edited) =>
        edited.SwapComparisons=settings.SwapComparisons.Select(s=>s.Copy()).ToList();
    private async Task<bool> SaveSwapComparisonsAsync(IReadOnlyList<SwapComparisonSetting> records)
    {
        await settingsMutationGate.WaitAsync();
        try
        {
            if(exiting)return false;
            var updated=settings.Copy();
            updated.SwapComparisons=records.Select(s=>s.Copy()).ToList();
            // Use the alert transaction gate to retain any alerts that changed during validation.
            await alerts.ApplySettingsAsync(updated,[],preserveAlerts:true);
            settings=updated;
            swapComparisons?.Configure(settings);
            ticker.Configure(settings);
            RefreshAlertWindows();
            marketDetails?.CloseUnavailable(Core.Services.MarketDemand.Quotes(settings));
            if(MarketDemandChanged(settings))await RestartFeedAsync();
            else { UpdateHistoryDemand();RefreshMarketGraphs(); }
            return true;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or
            InvalidOperationException or AggregateException or System.Security.SecurityException)
        {
            if(swapComparisons is not null)swapComparisons.Error="儲存失敗："+ex.Message;
            return false;
        }
        finally { settingsMutationGate.Release(); }
    }
    private async Task<bool> ApplySettingsAsync(AppSettings updated)
    {
        await settingsMutationGate.WaitAsync();
        try { return await ApplySettingsCoreAsync(updated); }
        finally { settingsMutationGate.Release(); }
    }
}
