using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using BinanceTicker.Core.ViewModels;
namespace BinanceTicker.Tests;

public sealed class WatchlistEditorTests
{
    internal sealed class Validation : IBinanceService
    {
        public Task<bool> IsValidSymbolAsync(string symbol,CancellationToken token)=>Task.FromResult(true);
        public Task<IReadOnlyList<TickerPrice>> GetPricesAsync(IReadOnlyList<string> symbols,CancellationToken token)=>throw new NotSupportedException();
    }
    internal static AppSettings Groups()
    {
        var s=new AppSettings { Watchlists=[new() { Id="a",Name="長期",Members=[new() { Symbol="BTCUSDT",Order=1 },new() { Symbol="ETHUSDT",Order=2 }] },
            new() { Id="b",Name="短線",Members=[new() { Symbol="BTCUSDT",Order=1 }] }], ActiveWatchlistId="a",Holdings=[new() { Symbol="BTCUSDT",Quantity=2,AverageCost=10 }] };
        WatchlistSettings.Normalize(s);return s;
    }
    [Fact] public void GroupsKeepIndependentOrderAndVisibility()
    {
        using var vm=new SettingsViewModel(Groups(),new Validation());vm.SelectedSymbol=vm.Symbols[0];vm.MoveDownCommand.Execute(null);vm.Symbols[0].Enabled=false;
        vm.SelectedWatchlist=vm.Watchlists[1];Assert.Equal("BTCUSDT",Assert.Single(vm.Symbols).Symbol);Assert.True(vm.Symbols[0].Enabled);
        vm.SelectedWatchlist=vm.Watchlists[0];Assert.Equal("ETHUSDT",vm.Symbols[0].Symbol);Assert.False(vm.Symbols[0].Enabled);
        Assert.Equal("ETHUSDT",vm.CreateSettings().Watchlists![0].Members[0].Symbol);
    }
    [Fact] public void CrossGroupHoldingHasOneSharedEditor()
    {
        using var vm=new SettingsViewModel(Groups(),new Validation());var btc=vm.Holdings.Single(h=>h.Symbol=="BTCUSDT");btc.QuantityText="3";
        vm.SelectedWatchlist=vm.Watchlists[1];Assert.Same(btc,vm.Holdings.Single(h=>h.Symbol=="BTCUSDT"));Assert.Equal(3,Assert.Single(vm.CreateSettings().Holdings).Quantity);
    }
    [Fact] public void CancelDoesNotMutateOriginal()
    {
        var s=Groups();using(var vm=new SettingsViewModel(s,new Validation())) { vm.WatchlistName="改名";vm.RenameWatchlistCommand.Execute(null);vm.Holdings[0].QuantityText="5";vm.RemoveWatchlistCommand.Execute(null); }
        Assert.Equal(2,s.Watchlists!.Count);Assert.Equal("長期",s.Watchlists[0].Name);Assert.Equal(2,s.Holdings[0].Quantity);
    }
    [Fact] public void DeleteGroupKeepsHoldingsAndOtherMembers()
    {
        using var vm=new SettingsViewModel(Groups(),new Validation());vm.RemoveWatchlistCommand.Execute(null);var s=vm.CreateSettings();
        Assert.Equal("b",Assert.Single(s.Watchlists!).Id);Assert.Equal("BTCUSDT",Assert.Single(s.Watchlists![0].Members).Symbol);Assert.Single(s.Holdings);Assert.Equal("b",s.ActiveWatchlistId);
    }
    [Fact] public void LastGroupCannotBeRemoved()
    {
        using var vm=new SettingsViewModel(new(),new Validation());Assert.False(vm.RemoveWatchlistCommand.CanExecute(null));vm.RemoveWatchlistCommand.Execute(null);Assert.Single(vm.Watchlists);
    }
    [Fact] public void OrphanHoldingCanBeEditedAndCleared()
    {
        var s=Groups();s.Holdings.Add(new() { Symbol="SOLUSDT",Quantity=2 });WatchlistSettings.Normalize(s);
        using var vm=new SettingsViewModel(s,new Validation());var h=vm.Holdings.Single(h=>h.Symbol=="SOLUSDT");h.QuantityText="";h.AverageCostText="";
        Assert.DoesNotContain(vm.CreateSettings().Holdings,h=>h.Symbol=="SOLUSDT");Assert.Contains(vm.CreateSettings().Symbols,x=>x.Symbol=="SOLUSDT");
    }
    [Theory]
    [InlineData("-1","2")]
    [InlineData("abc","2")]
    [InlineData("1,000","2")]
    [InlineData("79228162514264337593543950336","2")]
    [InlineData("1","")]
    public void InvalidHoldingInputIsRejected(string quantity,string cost)
    {
        var h=new HoldingEditorViewModel("BTCUSDT",null);h.QuantityText=quantity;h.AverageCostText=cost;Assert.Throws<ArgumentException>(()=>h.CreateHolding());
    }
    [Fact] public void ZeroAndBlankAreDifferent()
    {
        var h=new HoldingEditorViewModel("BTCUSDT",null);Assert.Null(h.CreateHolding());h.QuantityText="0";h.AverageCostText="0";Assert.Equal(0,h.CreateHolding()!.Quantity);
    }
    [Fact] public void UneditedPreferencesPreserveLiveChanges()
    {
        var s=Groups();using var vm=new SettingsViewModel(s,new Validation());var current=s.Copy();current.ActiveWatchlistId="b";current.Watchlists![0].Ui.ShowSparkline=false;
        var edited=vm.CreateSettings();vm.PreserveUneditedWatchlistPreferences(edited,current);Assert.Equal("b",edited.ActiveWatchlistId);Assert.False(edited.Watchlists![0].Ui.ShowSparkline);
    }
    [Fact] public void RejectsEmptyAndDuplicateNames()
    {
        using var vm=new SettingsViewModel(Groups(),new Validation());vm.WatchlistName=" ";vm.AddWatchlistCommand.Execute(null);Assert.Equal(2,vm.Watchlists.Count);Assert.NotEmpty(vm.Error);
        vm.WatchlistName="短線";vm.RenameWatchlistCommand.Execute(null);Assert.Equal("長期",vm.Watchlists[0].Name);
    }
}
