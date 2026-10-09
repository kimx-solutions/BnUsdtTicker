using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Services;
using BinanceTicker.Views;
namespace BinanceTicker.Tests;

internal static class SwapComparisonWindowTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Verify(App app)
    {
        var record=SwapComparisonCalculatorTests.Example();record.SwappedAt=DateTimeOffset.UtcNow.AddDays(-1);
        using var vm=new SwapComparisonViewModel(new(){SwapComparisons=[record]},new NoNetwork(),_=>Task.FromResult(true));
        var now=DateTime.UtcNow;vm.SetStatus(ConnectionStatus.Connected);
        vm.Update(new("NEARUSDT",5,0,now));vm.Update(new("QNTUSDT",30,0,now));
        var manager=new SwapComparisonWindowManager(vm,DisplayMode.Fix);
        var window=manager.Show();
        try
        {
            Assert.True(window.Topmost);window.WindowState=WindowState.Minimized;
            Assert.Same(window,manager.Show());Assert.Equal(WindowState.Normal,window.WindowState);
            window.UpdateLayout();Assert.Contains(Descendants<TextBlock>(window),t=>t.Text.Contains("+20.00%"));
            var title=Descendants<TextBlock>(window).Single(t=>t.Text=="100 NEAR → 20 QNT");
            Assert.Equal(Color.FromRgb(0xE4,0xF3,0xFF),((SolidColorBrush)title.Foreground).Color);
            var disclosure=Descendants<Expander>(window).Single(e=>Equals(e.Header,"計算方式"));
            Assert.False(disclosure.IsExpanded);disclosure.IsExpanded=true;window.UpdateLayout();
            var formula=Descendants<TextBlock>(window).Single(t=>t.Text.StartsWith("可換回 NEAR ="));
            Assert.Equal(Color.FromRgb(0x90,0xAD,0xC5),((SolidColorBrush)formula.Foreground).Color);
            disclosure.IsExpanded=false;window.UpdateLayout();
            Render(window,"swap-dark.png");
            var ticker=new TickerWindow();var called=false;ticker.SwapComparisonsRequested+=()=>called=true;
            try
            {
                ticker.Show();ticker.UpdateLayout();
                Descendants<Button>(ticker).Single(b=>AutomationProperties.GetName(b)=="換幣比較").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(called);
            }
            finally { ticker.Close(); }
            vm.Selected=vm.Records[0];vm.Edit();window.UpdateLayout();
            var quantity=Descendants<TextBox>(window).Single(b=>AutomationProperties.GetName(b)=="實際換入數量");
            quantity.Text="15";Assert.Equal("15",vm.ToQuantityText);
            vm.Cancel();Assert.Equal(20,vm.Records[0].Setting.ToQuantity);
            vm.Edit();window.UpdateLayout();quantity.Text="15";
            var saveButton=Descendants<Button>(window).Single(b=>Equals(b.Content,"儲存紀錄"));
            ((IInvokeProvider)new ButtonAutomationPeer(saveButton).GetPattern(PatternInterface.Invoke)).Invoke();
            app.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Assert.False(vm.IsEditing);Assert.Equal(15,vm.Records[0].Setting.ToQuantity);
            Assert.Equal(90,vm.Records[0].Result.ReturnQuantity);
            var comparisonScroll=Descendants<ScrollViewer>(window).Single(s=>s.Content is StackPanel);
            Assert.Equal(0,comparisonScroll.VerticalOffset);
            vm.SetStatus(ConnectionStatus.Disconnected);Render(window,"swap-disconnected.png");
            Assert.Contains(Descendants<TextBlock>(window),t=>t.Text=="使用最後報價");
            manager.SetMode(DisplayMode.Float);Assert.False(window.Topmost);
            ThemeService.Apply(ColorTheme.Light);manager.RefreshTheme();Render(window,"swap-light.png");
            var sample=record.Copy();sample.FromQuantity=1183;sample.ToQuantity=22.47m;
            vm.Configure(new(){SwapComparisons=[sample]});vm.SetStatus(ConnectionStatus.Connected);
            vm.Update(new("NEARUSDT",4.792m,0,DateTime.UtcNow));vm.Update(new("QNTUSDT",238.53m,0,DateTime.UtcNow));
            ThemeService.Apply(ColorTheme.Dark);manager.RefreshTheme();Render(window,"swap-polished-example-dark.png");
            ThemeService.Apply(ColorTheme.Light);manager.RefreshTheme();Render(window,"swap-polished-example-light.png");
            window.Width=480;window.Height=500;Render(window,"swap-polished-example-narrow.png");
            window.Width=480;window.Height=500;vm.New();
            app.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Render(window,"swap-narrow-editor.png");
            var fromInput=Descendants<TextBox>(window).Single(b=>AutomationProperties.GetName(b)=="換出幣種");
            Assert.InRange(fromInput.TranslatePoint(new Point(0,0),window).Y,0,window.ActualHeight);
            Assert.True(Descendants<Button>(window).Single(b=>Equals(b.Content,"取消編輯")).IsVisible);
        }
        finally { manager.Close();ThemeService.Apply(ColorTheme.Dark); }
        VerifyAppSave(app);
    }
    private static void VerifyAppSave(App app)
    {
        var directory=Path.Combine(Path.GetTempPath(),"swap-app-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        var field=typeof(App).GetField("settings",Private)!;var previousSettings=field.GetValue(app);
        var alertField=typeof(App).GetField("alerts",Private)!;var previousAlerts=alertField.GetValue(app);
        var subscriptions=typeof(App).GetField("subscribedSymbols",Private)!;var previousSubscriptions=subscriptions.GetValue(app);
        var viewModelField=typeof(App).GetField("swapComparisons",Private)!;var previousVm=viewModelField.GetValue(app);
        try
        {
            var settings=new AppSettings { Holdings=[new(){Symbol="NEARUSDT",Quantity=10,AverageCost=3}] };
            settings.Symbols[0].Alert.UpperPrice=1;settings.Symbols[0].Alert.UpperTriggered=true;
            field.SetValue(app,settings);
            var store=new SettingsService(Path.Combine(directory,"settings.json"));
            var alerts=new PriceAlertService(settings,store,new AlertHistoryService(Path.Combine(directory,"history.json")),new NoNotification());
            alertField.SetValue(app,alerts);
            var record=SwapComparisonCalculatorTests.Example();record.SwappedAt=DateTimeOffset.UtcNow.AddDays(-1);
            using var vm=new SwapComparisonViewModel(settings,new NoNetwork(),_=>Task.FromResult(true));
            viewModelField.SetValue(app,vm);
            subscriptions.SetValue(app,new[]{"BTCUSDT","ENAUSDT","ETHUSDT","NEARUSDT","QNTUSDT"});
            var save=typeof(App).GetMethod("SaveSwapComparisonsAsync",Private)!;
            Assert.True(((Task<bool>)save.Invoke(app,[new List<SwapComparisonSetting>{record}])!).GetAwaiter().GetResult());
            var saved=store.Load();Assert.Single(saved.SwapComparisons);Assert.Equal(10,saved.Holdings[0].Quantity);
            Assert.True(saved.Symbols.Single(s=>s.Symbol=="BTCUSDT").Alert.UpperTriggered);
            var ticker=(TickerViewModel)typeof(App).GetField("ticker",Private)!.GetValue(app)!;
            Assert.Null(ticker.GetRow("QNTUSDT"));
            typeof(App).GetMethod("UpdatePriceAsync",Private)!.Invoke(app,[new TickerPrice("NEARUSDT",5,0,DateTime.UtcNow)]);
            typeof(App).GetMethod("UpdatePriceAsync",Private)!.Invoke(app,[new TickerPrice("QNTUSDT",30,0,DateTime.UtcNow)]);
            Assert.Equal(120,vm.Records[0].Result.ReturnQuantity);
            var oldEditor=new AppSettings();
            typeof(App).GetMethod("PreserveSwapComparisons",Private)!.Invoke(app,[oldEditor]);
            Assert.Single(oldEditor.SwapComparisons);
            Assert.Equal(20,oldEditor.SwapComparisons[0].ToQuantity);
            // A failed storage transaction must retain the previously saved baseline and live subscriptions.
            File.Delete(store.FilePath);Directory.CreateDirectory(store.FilePath);
            record.ToQuantity=99;
            Assert.False(((Task<bool>)save.Invoke(app,[new List<SwapComparisonSetting>{record}])!).GetAwaiter().GetResult());
            var current=(AppSettings)field.GetValue(app)!;Assert.Equal(20,current.SwapComparisons[0].ToQuantity);
        }
        finally
        {
            field.SetValue(app,previousSettings);alertField.SetValue(app,previousAlerts);
            subscriptions.SetValue(app,previousSubscriptions);viewModelField.SetValue(app,previousVm);
            Directory.Delete(directory,true);
        }
    }
    private sealed class NoNotification:INotificationService { public void Show(AlertHistoryEntry entry){} }
    private sealed class NoNetwork:IBinanceService
    {
        public Task<bool> IsValidSymbolAsync(string symbol,CancellationToken token)=>Task.FromResult(true);
        public Task<IReadOnlyList<TickerPrice>> GetPricesAsync(IReadOnlyList<string> symbols,CancellationToken token)=>Task.FromResult<IReadOnlyList<TickerPrice>>([]);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var child=VisualTreeHelper.GetChild(parent,i);if(child is T match)yield return match;
            foreach(var descendant in Descendants<T>(child))yield return descendant;
        }
    }
    private static void Render(Window window,string name)
    {
        window.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);
        bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root.Parent is not null && !File.Exists(Path.Combine(root.FullName,"BinanceTicker.sln")))root=root.Parent;
        var path=Path.Combine(root.FullName,"artifacts","swap-previews",name);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream=File.Create(path);encoder.Save(stream);
    }
}
