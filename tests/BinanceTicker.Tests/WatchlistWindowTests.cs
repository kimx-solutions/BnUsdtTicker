using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Services;
using BinanceTicker.Views;
namespace BinanceTicker.Tests;

internal static class WatchlistWindowTests
{
    public static void Verify(Application app)
    {
        var s=WatchlistEditorTests.Groups();var vm=new TickerViewModel();vm.Configure(s);
        vm.Update(new("BTCUSDT",12,0,DateTime.UtcNow));vm.SetStatus(ConnectionStatus.Connected);
        var window=new TickerWindow { DataContext=vm,ShowActivated=false };
        try
        {
            window.Show();window.UpdateLayout();
            var selector=Descendants<ComboBox>(window).Single(b=>AutomationProperties.GetName(b)=="自選分組");
            Assert.False(TickerWindowManager.IsDragSource(selector));Assert.Equal("長期",((WatchlistGroup)selector.SelectedItem).Name);
            Assert.Contains(Descendants<TextBlock>(selector),b=>b.Text=="長期");
            selector.SelectedItem=vm.Watchlists[1];app.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.DataBind);Assert.Equal("b",vm.ActiveWatchlistId);
            var expander=Descendants<Expander>(window).Single();expander.IsExpanded=true;window.UpdateLayout();
            Assert.Contains(Descendants<TextBlock>(window),b=>b.Text=="全部持倉");Assert.Contains(Descendants<TextBlock>(window),b=>b.Text=="本組全部成員");
            MarketVisualizationWindowTests.Render(window,"watchlists-dark.png");
            ThemeService.Apply(ColorTheme.Light);MarketVisualizationWindowTests.Render(window,"watchlists-light.png");
            s.Watchlists![1].Members=Enumerable.Range(0,40).Select(i=>new WatchlistMember { Symbol=$"COIN{i}USDT",Order=i }).ToList();
            vm.Configure(s);vm.SelectWatchlist("b");window.Width=360;window.Height=260;window.UpdateLayout();
            var scroll=Descendants<ScrollViewer>(window).First(v=>v.ScrollableHeight>0);Assert.True(scroll.ViewportHeight>0);
            scroll.ScrollToEnd();window.UpdateLayout();Assert.True(scroll.VerticalOffset>0);
            Assert.InRange(selector.TranslatePoint(new Point(0,selector.ActualHeight),window).Y,1,260);
            Assert.InRange(((FrameworkElement)window.FindName("ThemeToggleButton")).TranslatePoint(new Point(0,28),window).Y,1,260);
            MarketVisualizationWindowTests.Render(window,"watchlists-small-long-light.png");
            expander.IsExpanded=false;ThemeService.Apply(ColorTheme.Dark);MarketVisualizationWindowTests.Render(window,"watchlists-small-long-dark.png");
        }
        finally { window.Close();ThemeService.Apply(ColorTheme.Dark); }
        var editor=new SettingsViewModel(WatchlistEditorTests.Groups(),new WatchlistEditorTests.Validation());
        var saved=0;var settingsWindow=new SettingsWindow(editor,_=> { saved++;return Task.FromResult(true); });
        try
        {
            settingsWindow.Show();settingsWindow.UpdateLayout();
            Assert.Contains(Descendants<ComboBox>(settingsWindow),b=>AutomationProperties.GetName(b)=="編輯分組");
            Assert.Contains(Descendants<TextBlock>(settingsWindow),b=>b.Text==SettingsViewModel.DeleteWatchlistExplanation);
            Assert.Contains(Descendants<TextBox>(settingsWindow),b=>AutomationProperties.GetName(b)=="BTCUSDT 持有數量");
            editor.RemoveWatchlistCommand.Execute(null);settingsWindow.UpdateLayout();
            Assert.False(Descendants<Button>(settingsWindow).Single(b=>Equals(b.Content,"刪除分組")).IsEnabled);
            editor.Holdings.Single(h=>h.Symbol=="BTCUSDT").QuantityText="3";
            MarketVisualizationWindowTests.Render(settingsWindow,"watchlist-settings-dark.png");
            Descendants<TextBox>(settingsWindow).Single(b=>AutomationProperties.GetName(b)=="BTCUSDT 持有數量").BringIntoView();
            settingsWindow.UpdateLayout();MarketVisualizationWindowTests.Render(settingsWindow,"holdings-settings-dark.png");
            ThemeService.Apply(ColorTheme.Light);MarketVisualizationWindowTests.Render(settingsWindow,"watchlist-settings-light.png");
            settingsWindow.Height=400;settingsWindow.Width=400;settingsWindow.UpdateLayout();
            var save=Descendants<Button>(settingsWindow).Single(b=>Equals(b.Content,"儲存"));
            Assert.InRange(save.TranslatePoint(new Point(0,save.ActualHeight),settingsWindow).Y,1,400);
            MarketVisualizationWindowTests.Render(settingsWindow,"watchlist-settings-small.png");
            MarketVisualizationWindowTests.Invoke(Descendants<Button>(settingsWindow).Single(b=>Equals(b.Content,"取消")),app);Assert.Equal(0,saved);
        }
        finally { settingsWindow.Close();ThemeService.Apply(ColorTheme.Dark); }
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject => MarketVisualizationWindowTests.Descendants<T>(root);
}
