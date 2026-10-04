using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Reflection;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Services;
using BinanceTicker.Views;
namespace BinanceTicker.Tests;

internal static class WatchlistWindowTests
{
    public static void Verify(Application app)
    {
        VerifySettingsZOrder((App)app);
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
            vm.Configure(s.Copy());app.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);window.UpdateLayout();
            Assert.NotNull(selector.SelectedItem);Assert.Equal("長期",((WatchlistGroup)selector.SelectedItem).Name);
            Assert.Equal("長期",Assert.Single(Descendants<TextBlock>(selector)).Text);
            selector.SelectedItem=vm.Watchlists[1];app.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.DataBind);
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
            var tabs=Assert.Single(Descendants<TabControl>(settingsWindow));
            Assert.Equal(new[] { "一般設定","持倉成本" },tabs.Items.Cast<TabItem>().Select(t=>(string)t.Header));
            Assert.DoesNotContain(Descendants<TextBox>(settingsWindow),b=>AutomationProperties.GetName(b)=="BTCUSDT 持有數量");
            editor.RemoveWatchlistCommand.Execute(null);settingsWindow.UpdateLayout();
            Assert.False(Descendants<Button>(settingsWindow).Single(b=>Equals(b.Content,"刪除分組")).IsEnabled);
            editor.Holdings.Single(h=>h.Symbol=="BTCUSDT").QuantityText="3";
            MarketVisualizationWindowTests.Render(settingsWindow,"watchlist-settings-dark.png");
            tabs.SelectedIndex=1;settingsWindow.UpdateLayout();
            Assert.Contains(Descendants<TextBox>(settingsWindow),b=>AutomationProperties.GetName(b)=="BTCUSDT 持有數量");
            var quantity=Descendants<TextBox>(settingsWindow).Single(b=>AutomationProperties.GetName(b)=="BTCUSDT 持有數量");
            quantity.Text="4";quantity.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Assert.DoesNotContain(Descendants<ComboBox>(settingsWindow),b=>AutomationProperties.GetName(b)=="編輯分組");
            Descendants<TextBox>(settingsWindow).Single(b=>AutomationProperties.GetName(b)=="BTCUSDT 持有數量").BringIntoView();
            settingsWindow.UpdateLayout();MarketVisualizationWindowTests.Render(settingsWindow,"holdings-settings-dark.png");
            ThemeService.Apply(ColorTheme.Light);MarketVisualizationWindowTests.Render(settingsWindow,"watchlist-settings-light.png");
            settingsWindow.Height=400;settingsWindow.Width=400;settingsWindow.UpdateLayout();
            tabs.SelectedIndex=0;tabs.SelectedIndex=1;settingsWindow.UpdateLayout();
            Assert.Equal("4",editor.Holdings.Single(h=>h.Symbol=="BTCUSDT").QuantityText);
            var save=Descendants<Button>(settingsWindow).Single(b=>Equals(b.Content,"儲存"));
            Assert.InRange(save.TranslatePoint(new Point(0,save.ActualHeight),settingsWindow).Y,1,400);
            MarketVisualizationWindowTests.Render(settingsWindow,"watchlist-settings-small.png");
            MarketVisualizationWindowTests.Invoke(Descendants<Button>(settingsWindow).Single(b=>Equals(b.Content,"取消")),app);Assert.Equal(0,saved);
        }
        finally { settingsWindow.Close();ThemeService.Apply(ColorTheme.Dark); }
        VerifySaveFromHoldingsTab(app);
    }
    private static void VerifySaveFromHoldingsTab(Application app)
    {
        var original=WatchlistEditorTests.Groups();var draft=new SettingsViewModel(original,new WatchlistEditorTests.Validation());
        AppSettings? saved=null;
        var window=new SettingsWindow(draft,value=> { saved=value;return Task.FromResult(true); });
        try
        {
            window.Show();window.UpdateLayout();
            var name=Descendants<TextBox>(window).Single(b=>AutomationProperties.GetName(b)=="分組名稱");
            name.Text="已編輯分組";name.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();draft.RenameWatchlistCommand.Execute(null);
            Descendants<TabControl>(window).Single().SelectedIndex=1;window.UpdateLayout();
            var cost=Descendants<TextBox>(window).Single(b=>AutomationProperties.GetName(b)=="BTCUSDT 平均成本");
            cost.Text="15.25";cost.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            MarketVisualizationWindowTests.Invoke(Descendants<Button>(window).Single(b=>Equals(b.Content,"儲存")),app);
            Assert.NotNull(saved);Assert.Equal("已編輯分組",saved.Watchlists![0].Name);
            Assert.Equal(15.25m,saved.Holdings.Single(h=>h.Symbol=="BTCUSDT").AverageCost);
            Assert.Equal("長期",original.Watchlists![0].Name);Assert.False(window.IsVisible);
        }
        finally { window.Close(); }
    }
    private static void VerifySettingsZOrder(App app)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var settings=WatchlistEditorTests.Groups();var vm=new TickerViewModel();vm.Configure(settings);
        var owner=new TickerWindow { DataContext=vm,Topmost=true,ShowActivated=false };
        var oldWindow=typeof(App).GetField("tickerWindow",flags)!.GetValue(app);
        var oldSettings=typeof(App).GetField("settings",flags)!.GetValue(app);
        var oldBinance=typeof(App).GetField("binance",flags)!.GetValue(app);
        typeof(App).GetField("tickerWindow",flags)!.SetValue(app,owner);
        typeof(App).GetField("settings",flags)!.SetValue(app,settings);
        SettingsWindow? editor=null;
        try
        {
            // Opening settings from the tray before ever showing the ticker also needs to work.
            typeof(App).GetMethod("OpenSettings",flags)!.Invoke(app,null);
            editor=(SettingsWindow)typeof(App).GetField("settingsWindow",flags)!.GetValue(app)!;
            Assert.Same(owner,editor.Owner);Assert.True(editor.Topmost);Assert.False(owner.IsVisible);
            owner.Show();owner.Activate();app.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Assert.True(IsAbove(editor,owner),"Settings must stay above the FIX ticker even when the ticker is activated.");
            owner.Topmost=false;app.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.DataBind);
            Assert.False(editor.Topmost);Assert.True(IsAbove(editor,owner));
            owner.Hide();Assert.True(editor.IsVisible);
        }
        finally
        {
            editor?.Close();owner.Close();
            typeof(App).GetField("tickerWindow",flags)!.SetValue(app,oldWindow);
            typeof(App).GetField("settings",flags)!.SetValue(app,oldSettings);
            typeof(App).GetField("binance",flags)!.SetValue(app,oldBinance);
        }
    }
    private static bool IsAbove(Window higher,Window lower)
    {
        var upperHandle=new WindowInteropHelper(higher).Handle;var lowerHandle=new WindowInteropHelper(lower).Handle;
        for(var handle=GetTopWindow(IntPtr.Zero);handle!=IntPtr.Zero;handle=GetWindow(handle,2))
        { if(handle==upperHandle)return true;if(handle==lowerHandle)return false; }
        return false;
    }
    [DllImport("user32.dll")]private static extern IntPtr GetTopWindow(IntPtr window);
    [DllImport("user32.dll")]private static extern IntPtr GetWindow(IntPtr window,uint command);
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject => MarketVisualizationWindowTests.Descendants<T>(root);
}
