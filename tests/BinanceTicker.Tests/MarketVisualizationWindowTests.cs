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

// Called by the one STA Application owned by WindowTests.
internal static class MarketVisualizationWindowTests
{
    public static void Verify(Application app)
    {
        var now = CandleFixtures.Now;
        var vm = new TickerViewModel(); vm.Configure(new());
        var candles = Enumerable.Range(0, 1442).Select(i => CandleFixtures.Minute(now.AddMinutes(i-1442), 100 + (decimal)Math.Sin(i/15d)*10)).ToArray();
        foreach (var row in vm.Prices)
        {
            row.Update(new(row.Symbol, 100, -2, now.UtcDateTime, 110, 90, 12, 1200));
            row.UpdateHistory(candles, new(HistoryLoadStatus.Loaded), ConnectionStatus.Connected, "1h", now);
        }
        var window = new TickerWindow { DataContext = vm, ShowActivated=false };
        try
        {
            Render(window, "market-dark-1h.png");
            var graphType=typeof(TickerWindow).Assembly.GetType("BinanceTicker.Controls.SparklineControl"); Assert.NotNull(graphType);
            var compactGraph=Descendants<FrameworkElement>(window).First(element=>element.GetType()==graphType);
            Assert.Equal(vm.Prices[0].GetType().GetProperty("SparklineToolTip")?.GetValue(vm.Prices[0]),compactGraph.ToolTip);
            Assert.Same(compactGraph,VisualTreeHelper.HitTest(compactGraph,new Point(compactGraph.ActualWidth/2,1))?.VisualHit);
            var day = Buttons(window).SingleOrDefault(b => ReferenceEquals(b.Command, vm.SelectDayCommand));
            Assert.NotNull(day); Invoke(day, app); Assert.Equal("24h", vm.SparklineRange);
            foreach (var row in vm.Prices) row.UpdateHistory(candles,new(HistoryLoadStatus.Loaded),ConnectionStatus.Connected,vm.SparklineRange,now);
            Render(window,"market-dark-24h.png");
            var symbol = Buttons(window).Single(b => AutomationProperties.GetName(b) == "BTC 行情詳情");
            var requested = ""; window.GetType().GetEvent("MarketDetailsRequested")!.AddEventHandler(window,(Action<string>)(s=>requested=s));
            Invoke(symbol,app); Assert.Equal("BTCUSDT",requested);
            Assert.IsAssignableFrom<System.Windows.Controls.Primitives.ButtonBase>(symbol);
            Invoke(Buttons(window).Single(b=>ReferenceEquals(b.Command,vm.ToggleSparklineCommand)),app);
            Assert.False(vm.ShowSparkline); Render(window,"market-hidden.png");
            Invoke(Buttons(window).Single(b=>ReferenceEquals(b.Command,vm.ToggleSparklineCommand)),app);
            ThemeService.Apply(ColorTheme.Light); Render(window,"market-light-24h.png");
            using var detailsVm = new MarketDetailsViewModel(vm.Prices[0],()=>Task.CompletedTask,_=>{});
            var detailsType = typeof(TickerWindow).Assembly.GetType("BinanceTicker.Views.MarketDetailsWindow"); Assert.NotNull(detailsType);
            var details = (Window)Activator.CreateInstance(detailsType,detailsVm)!;
            try
            {
                detailsVm.Row.UpdateHistory(candles.TakeLast(20).ToArray(),new(HistoryLoadStatus.Failed),ConnectionStatus.Disconnected,"24h",now);
                Render(details,"market-details-failed-light.png");
                var texts = Descendants<TextBlock>(details).Select(t=>t.Text).ToArray();
                Assert.Contains(detailsVm.Row.VolumeText,texts); Assert.Contains(detailsVm.Row.QuoteVolumeText,texts);
                Assert.Contains(detailsVm.Row.HistoryStatusText,texts);
                Assert.Contains((string)detailsVm.Row.GetType().GetProperty("HistoryUpdatedText")!.GetValue(detailsVm.Row)!,texts);
                ThemeService.Apply(ColorTheme.Dark); Render(details,"market-details-failed-dark.png");
            }
            finally { details.Close(); }
            var many = new AppSettings { Symbols=Enumerable.Range(0,25).Select(i=>new SymbolSetting { Symbol=$"COIN{i}USDT", Enabled=true, Order=i }).ToList() };
            vm.Configure(many);
            vm.SelectDayCommand.Execute(null);
            foreach(var row in vm.Prices) { row.Update(new(row.Symbol,100,1,now.UtcDateTime)); row.UpdateHistory(candles.TakeLast(20).ToArray(),new(HistoryLoadStatus.Loaded),ConnectionStatus.Disconnected,"24h",now); }
            Render(window,"market-long-partial-disconnected.png"); Assert.InRange(window.ActualHeight,100,640);
            var type = typeof(TickerWindow).Assembly.GetType("BinanceTicker.Controls.SparklineControl"); Assert.NotNull(type);
            var graph = (FrameworkElement)Activator.CreateInstance(type)!;
            type.GetProperty("Series")!.SetValue(graph,SparklineProjection.Create([CandleFixtures.Minute(now.AddMinutes(-2),0),CandleFixtures.Minute(now.AddMinutes(-1),0)],now,TimeSpan.FromHours(1)));
            graph.Measure(new Size(0,0)); graph.Arrange(new Rect(0,0,0,0));
            graph.Measure(new Size(200,32)); graph.Arrange(new Rect(0,0,200,32));
            new RenderTargetBitmap(200,32,96,96,PixelFormats.Pbgra32).Render(graph);
        }
        finally { window.Close(); ThemeService.Apply(ColorTheme.Dark); }
    }
    internal static void Invoke(Button button,Application app)
    {
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        app.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }
    internal static IEnumerable<Button> Buttons(DependencyObject root) => Descendants<Button>(root);
    internal static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {
            var child=VisualTreeHelper.GetChild(root,i); if(child is T match) yield return match;
            foreach(var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    internal static void Render(Window window,string file)
    {
        window.Show(); window.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);
        bitmap.Render(window);
        if(Environment.GetEnvironmentVariable("TICKER_TEST_ARTIFACTS") is { } directory)
        {
            Directory.CreateDirectory(directory);
            var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output=File.Create(Path.Combine(directory,file)); encoder.Save(output);
        }
    }
}
