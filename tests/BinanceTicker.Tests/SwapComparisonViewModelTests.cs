using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using BinanceTicker.Core.ViewModels;
namespace BinanceTicker.Tests;

public sealed class SwapComparisonViewModelTests
{
    private readonly DateTimeOffset now=new(2026,10,9,10,0,0,TimeSpan.Zero);
    private SwapComparisonViewModel Create(IBinanceService? binance=null,Func<IReadOnlyList<SwapComparisonSetting>,Task<bool>>? save=null,TestTimeProvider? clock=null)
        => new(new(),binance ?? new Validation(),save ?? (_=>Task.FromResult(true)),clock ?? new(now));

    [Fact]
    public async Task NewEditAndCancelNeverChangeTheSavedBaselineUntilSuccess()
    {
        IReadOnlyList<SwapComparisonSetting>? saved=null;
        var vm=Create(save:records=>{saved=records;return Task.FromResult(true);});
        vm.New(); Fill(vm);
        Assert.True(await vm.SaveAsync());Assert.Single(vm.Records);
        Assert.Equal("NEARUSDT",saved![0].FromSymbol);
        var id=saved[0].Id;
        vm.Selected=vm.Records[0];vm.Edit();vm.ToQuantityText="10";vm.Cancel();
        Assert.Equal(20,vm.Records[0].Setting.ToQuantity);
        vm.Edit();vm.ToQuantityText="15";Assert.True(await vm.SaveAsync());
        Assert.Equal(id,vm.Records[0].Setting.Id);Assert.Equal(15,vm.Records[0].Setting.ToQuantity);
        vm.New();Fill(vm);Assert.True(await vm.SaveAsync());Assert.Equal(2,vm.Records.Count);
    }

    [Theory]
    [InlineData("0","QNT","2026-10-08 18:00:00")]
    [InlineData("-1","QNT","2026-10-08 18:00:00")]
    [InlineData("1,000","QNT","2026-10-08 18:00:00")]
    [InlineData("abc","QNT","2026-10-08 18:00:00")]
    [InlineData("100","NEAR","2026-10-08 18:00:00")]
    [InlineData("100","QNT","2099-01-01 00:00:00")]
    public async Task InvalidInputsStayInTheDraftAndAreNotSaved(string quantity,string to,string date)
    {
        var vm=Create();vm.New();Fill(vm);vm.FromQuantityText=quantity;vm.ToSymbolText=to;vm.SwappedAtText=date;
        Assert.False(await vm.SaveAsync());Assert.Empty(vm.Records);Assert.NotEmpty(vm.Error);Assert.Equal(quantity,vm.FromQuantityText);
    }

    [Fact]
    public async Task ValidationAndStorageFailuresPreserveTheDraftAndRecords()
    {
        var vm=Create(new Validation(false));vm.New();Fill(vm);
        Assert.False(await vm.SaveAsync());Assert.Equal("20",vm.ToQuantityText);Assert.Empty(vm.Records);
        vm=Create(save:_=>throw new IOException("disk"));vm.New();Fill(vm);
        Assert.False(await vm.SaveAsync());Assert.NotEmpty(vm.Error);Assert.Empty(vm.Records);Assert.True(vm.IsEditing);
        vm=Create(new Validation(throws:true));vm.New();Fill(vm);
        Assert.False(await vm.SaveAsync());Assert.NotEmpty(vm.Error);Assert.Empty(vm.Records);
    }

    [Fact]
    public void UpdatesEitherSideRejectsBadQuotesAndExpiresWithoutAnotherQuote()
    {
        var clock=new TestTimeProvider(now);var setting=SwapComparisonCalculatorTests.Example();
        var vm=new SwapComparisonViewModel(new(){SwapComparisons=[setting]},new Validation(),_=>Task.FromResult(true),clock);
        vm.SetStatus(ConnectionStatus.Connected);
        vm.Update(new("NEARUSDT",5,0,now.UtcDateTime));
        vm.Update(new("QNTUSDT",30,0,now.UtcDateTime));
        Assert.Equal(120,vm.Records[0].Result.ReturnQuantity);
        Assert.False(vm.Update(new("QNTUSDT",50,0,now.AddSeconds(-1).UtcDateTime)));
        Assert.False(vm.Update(new("QNTUSDT",0,0,now.UtcDateTime)));
        Assert.False(vm.Update(new("QNTUSDT",50,0,now.AddSeconds(1).UtcDateTime)));
        Assert.Equal(120,vm.Records[0].Result.ReturnQuantity);
        clock.Advance(TimeSpan.FromSeconds(61));vm.Refresh();Assert.True(vm.Records[0].Result.IsStale);
        vm.Update(new("NEARUSDT",10,0,clock.GetUtcNow().UtcDateTime));
        vm.Update(new("QNTUSDT",30,0,clock.GetUtcNow().UtcDateTime));
        Assert.Equal(60,vm.Records[0].Result.ReturnQuantity);Assert.False(vm.Records[0].Result.IsStale);
    }

    [Fact]
    public async Task ToggleDeleteAndConfigureRetainUnrelatedDataAndUnsavedEdits()
    {
        var settings=new AppSettings{SwapComparisons=[SwapComparisonCalculatorTests.Example()]};
        var vm=new SwapComparisonViewModel(settings,new Validation(),_=>Task.FromResult(true),new TestTimeProvider(now));
        vm.Selected=vm.Records[0];Assert.True(await vm.ToggleAsync());
        Assert.False(vm.Records[0].Setting.Enabled);Assert.Null(vm.Records[0].Result.ReturnQuantity);
        vm.Edit();vm.NoteText="unsaved";vm.Configure(settings);
        Assert.Equal("unsaved",vm.NoteText);Assert.True(vm.IsEditing);
        vm.Cancel();vm.Selected=vm.Records[0];Assert.True(await vm.DeleteAsync());Assert.Empty(vm.Records);
        Assert.Equal(3,settings.Symbols.Count);Assert.Single(settings.SwapComparisons);
    }

    [Fact]
    public void TinyNegativeRoundedToZeroHasNoNegativeSign()
    {
        Assert.Equal("0.00%",SwapComparisonRowViewModel.Percent(-0.00001m));
        Assert.Equal("0.00",SwapComparisonRowViewModel.Signed(-0.000000000001m));
    }

    [Fact]
    public void DisplaySeparatesCoinPrecisionFromUsdtWhileRetainingExactTooltipValues()
    {
        var setting=SwapComparisonCalculatorTests.Example();
        setting.FromQuantity=1183;setting.ToQuantity=22.47m;
        var row=new SwapComparisonRowViewModel(setting);
        row.Refresh(new Dictionary<string,TickerPrice>
        {
            ["NEARUSDT"]=new("NEARUSDT",4.792m,0,now.UtcDateTime),
            ["QNTUSDT"]=new("QNTUSDT",238.53m,0,now.UtcDateTime)
        },ConnectionStatus.Connected,now);
        Assert.Equal("1,118.4827 NEAR",row.ReturnText);
        Assert.Equal("-64.5173 NEAR",row.QuantityDifferenceText);
        Assert.Equal("-309.17",row.UsdtDifferenceText);
        Assert.Equal("-5.45%",row.ReturnPercentText);
        Assert.Contains("1118.482700",row.ExactResultsText);
        Assert.Equal(-309.1669m,row.Result.UsdtDifference);
    }

    private static void Fill(SwapComparisonViewModel vm)
    {
        vm.FromSymbolText="near";vm.FromQuantityText="100";vm.ToSymbolText="QNT";
        vm.ToQuantityText="20";vm.SwappedAtText="2026-10-08 18:00:00";
    }
    private sealed class Validation(bool valid=true,bool throws=false):IBinanceService
    {
        public Task<bool> IsValidSymbolAsync(string symbol,CancellationToken token)
            => throws ? throw new HttpRequestException("network") : Task.FromResult(valid);
        public Task<IReadOnlyList<TickerPrice>> GetPricesAsync(IReadOnlyList<string> symbols,CancellationToken token)=>Task.FromResult<IReadOnlyList<TickerPrice>>([]);
    }
}
