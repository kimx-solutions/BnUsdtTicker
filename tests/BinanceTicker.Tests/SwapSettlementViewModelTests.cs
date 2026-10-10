using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using BinanceTicker.Core.ViewModels;

namespace BinanceTicker.Tests;

public sealed class SwapSettlementViewModelTests
{
    private static readonly DateTimeOffset Now=new(2026,10,9,10,0,0,TimeSpan.Zero);
    private static SwapComparisonViewModel Create(Func<IReadOnlyList<SwapComparisonSetting>,Task<bool>>? save=null,TestTimeProvider? clock=null)
    {
        var vm=new SwapComparisonViewModel(new(){SwapComparisons=[SwapComparisonCalculatorTests.Example()]},
            new NoNetwork(),save ?? (_=>Task.FromResult(true)),clock ?? new(Now));
        vm.SetStatus(ConnectionStatus.Connected);
        vm.Update(new("NEARUSDT",5,0,Now.UtcDateTime));vm.Update(new("QNTUSDT",30,0,Now.UtcDateTime));return vm;
    }

    [Fact]
    public async Task ActualPartialThenQuoteFullSettlementArchivesAndFreezesResults()
    {
        using var vm=Create();vm.BeginSettlement();var draft=vm.SettlementEditor!;
        draft.SettleAll=false;draft.QuantityText="5";draft.ActualReturnText="30";
        Assert.True(await vm.SaveSettlementAsync());
        Assert.Single(vm.ActiveRecords);Assert.Empty(vm.HistoryRecords);
        Assert.Equal(75,vm.Selected!.Setting.RemainingFromQuantity);
        Assert.Equal(15,vm.Selected.Setting.RemainingToQuantity);
        Assert.Equal(90,vm.Selected.Result.ReturnQuantity);
        Assert.False(vm.EditCommand.CanExecute(null));
        vm.Edit();Assert.False(vm.IsEditing);
        vm.BeginSettlement();vm.SettlementEditor!.UseMarketQuote=true;
        Assert.True(await vm.SaveSettlementAsync());
        Assert.Empty(vm.ActiveRecords);Assert.Single(vm.HistoryRecords);
        Assert.Equal(1,vm.ViewIndex);Assert.Same(vm.HistoryRecords[0],vm.Selected);
        Assert.Equal(120,vm.Selected!.Result.ReturnQuantity);
        Assert.Equal(100,vm.Selected.Result.UsdtDifference);
        Assert.Equal(2,vm.Selected.Setting.Settlements.Count);
        Assert.False(vm.Update(new("QNTUSDT",1,0,Now.UtcDateTime)));
        vm.SetStatus(ConnectionStatus.Disconnected);Assert.Equal(120,vm.Selected.Result.ReturnQuantity);
    }

    [Fact]
    public async Task QuoteConfirmationUsesLatestFreshPricesAndRejectsStaleOnes()
    {
        var clock=new TestTimeProvider(Now);using var vm=Create(clock:clock);vm.BeginSettlement();
        vm.SettlementEditor!.UseMarketQuote=true;clock.Advance(TimeSpan.FromSeconds(61));
        Assert.False(await vm.SaveSettlementAsync());Assert.Empty(vm.Selected!.Setting.Settlements);
        Assert.True(vm.IsSettling);Assert.NotEmpty(vm.SettlementEditor.Error);
        vm.Update(new("NEARUSDT",10,0,clock.GetUtcNow().UtcDateTime));
        vm.Update(new("QNTUSDT",30,0,clock.GetUtcNow().UtcDateTime));
        Assert.True(await vm.SaveSettlementAsync());Assert.Equal(60,vm.Selected!.Result.ReturnQuantity);
    }

    [Fact]
    public async Task InvalidQuantityAndStorageFailureRetainDraftAndOriginalRecord()
    {
        using var vm=Create(_=>Task.FromResult(false));vm.BeginSettlement();var draft=vm.SettlementEditor!;
        draft.SettleAll=false;draft.QuantityText="21";draft.ActualReturnText="120";
        Assert.False(await vm.SaveSettlementAsync());Assert.Empty(vm.Selected!.Setting.Settlements);
        draft.QuantityText="5";Assert.False(await vm.SaveSettlementAsync());
        Assert.Empty(vm.Selected.Setting.Settlements);Assert.True(vm.IsSettling);Assert.Equal("5",draft.QuantityText);
        vm.CancelSettlement();Assert.False(vm.IsSettling);Assert.Equal(20,vm.Selected.Setting.RemainingToQuantity);
    }

    [Fact]
    public async Task ActualSettlementWorksOfflineWithoutInventingUsdtValuation()
    {
        using var vm=Create();vm.SetStatus(ConnectionStatus.Disconnected);vm.BeginSettlement();
        vm.SettlementEditor!.ActualReturnText="110";vm.SettlementEditor.FromPriceText="";
        Assert.True(await vm.SaveSettlementAsync());Assert.Equal(10,vm.Selected!.Result.QuantityDifference);
        Assert.Equal(10,vm.Selected.Result.ReturnPercent);Assert.Null(vm.Selected.Result.UsdtDifference);
    }

    [Fact]
    public async Task FractionalSettlementsConsumeTheExactRemainderAndCopiesAreIndependent()
    {
        using var vm=Create();
        foreach(var quantity in new[]{"3","7","10"})
        {
            vm.BeginSettlement();var draft=vm.SettlementEditor!;draft.SettleAll=false;
            draft.QuantityText=quantity;draft.ActualReturnText=quantity;
            Assert.True(await vm.SaveSettlementAsync());
        }
        Assert.True(vm.Selected!.Setting.IsClosed);Assert.Equal(0,vm.Selected.Setting.RemainingFromQuantity);
        var copy=vm.Selected.Setting.Copy();copy.Settlements.Clear();
        Assert.Equal(3,vm.Selected.Setting.Settlements.Count);
    }

    private sealed class NoNetwork:IBinanceService
    {
        public Task<bool> IsValidSymbolAsync(string symbol,CancellationToken token)=>throw new InvalidOperationException("Settlement must not validate trading pairs.");
        public Task<IReadOnlyList<TickerPrice>> GetPricesAsync(IReadOnlyList<string> symbols,CancellationToken token)=>throw new InvalidOperationException();
    }

    [Fact]
    public async Task UnrepresentableSettlementValuationDoesNotArchiveOrSave()
    {
        using var vm=Create();vm.BeginSettlement();
        vm.SettlementEditor!.ActualReturnText="120";
        vm.SettlementEditor.FromPriceText="79228162514264337593543950335";
        Assert.False(await vm.SaveSettlementAsync());Assert.True(vm.IsSettling);
        Assert.Empty(vm.Selected!.Setting.Settlements);Assert.Empty(vm.HistoryRecords);
    }
}
