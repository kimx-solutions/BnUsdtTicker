# Issue #6 Market Visualization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在桌面報價加入 1h／24h 迷你曲線、個別幣種行情詳情及 Binance 現貨入口，共用可供 #7 使用的一分鐘行情資料。

**Architecture:** Core 負責行情模型、REST／stream 解析、共用快取、歷史載入協調及畫面資料。WPF 以原生繪圖顯示走勢；App 管理資料工作、畫面需求、Dispatcher、設定及視窗生命週期。ticker 與 K 線共用 combined 連線，歷史載入不阻塞即時報價。

**Tech Stack:** .NET 10、Windows WPF、C#、MVVM、HttpClient、ClientWebSocket、System.Text.Json、xUnit；不新增產品依賴。

**Spec:** [正式規格](../specs/2026-10-04-issue-6-market-visualization-design.md)

## Global Constraints

- 主畫面維持目前 390 DIP 寬度；曲線高度約 32 DIP，預設顯示走勢、選擇 1h。
- 數值使用 decimal；Binance Unix 毫秒轉為 UTC，畫面才轉為本機時區。
- 歷史間隔 1m；REST 每頁最多 1000 根；保留近 24 小時與邊界，硬上限 1500 根。
- 最多兩個歷史請求同時進行；起始請求至少間隔 250 毫秒；暫時失敗最多再重試三次，延遲 1、2、5 秒。
- HTTP 429／418 共用暫停且不能被手動重試繞過；程式結束取消並等待所有相關工作。
- 圖形最多每秒更新一次，每圖最多約 300 點；資料缺口不以直線連接；超過 60 秒未更新標示逾期。
- ticker 詳細欄位可缺省；缺值不當作零，舊設定與單次提醒行為相容。
- 分組、持倉、進階提醒與視窗可調整大小仍由 #7／#8／#9 處理。

## Review Focus

1. 伺服器回傳重複／亂序頁或頁游標不前進：停止錯誤分頁，不形成無限請求，也不宣稱完整歷史；由 Task 2 驗證。
2. 尚未收盤 K 線時間在未來、遲到的未收盤事件或超出區間資料：不畫到未來、不還原已收盤資料；由 Task 3 驗證。
3. 停用幣種與晚回來的 HTTP／舊 feed 同時發生：忽略過期回應，不恢復已移除資料；由 Task 4 驗證。
4. 一般設定保持開啟期間，使用者在主畫面切換走勢偏好：只覆蓋明確編輯的欄位；由 Task 1／7 驗證。
5. 新互動入口與 Float 失焦、拖曳及關閉交錯：按鈕不觸發移動，詳情仍可使用，關閉後不持有訂閱；由 Task 6／7 驗證。

## 檔案責任

- `Models/CandlePrice.cs`、`Services/CandleParser.cs`：一分鐘行情的資料與解析。
- `Services/BinanceHistoryService.cs`、`HistoryRequestScheduler.cs`：歷史分頁及共用請求限流／重試。
- `Services/CandleCache.cs`、`HistoryCoordinator.cs`：共用資料與可取消的載入工作。
- `Models/SparklineSeries.cs`、`Services/SparklineProjection.cs`：與 WPF 無關的走勢區間、缺口及裁減。
- 既有 ticker／settings 模型與 ViewModel：詳細欄位、顯示偏好與列資料。
- `ViewModels/MarketDetailsViewModel.cs`、`Services/BinanceTradeLink.cs`：詳情操作與固定來源連結。
- `Controls/SparklineControl.cs`、`Views/MarketDetailsWindow.xaml(.cs)`：原生 WPF 顯示。
- `Services/MarketDetailsWindowManager.cs`、`BrowserLauncher.cs`：視窗復用與系統瀏覽器邊界。
- 既有 `App.xaml.cs`：串接上述元件；不把歷史演算法、繪圖或 HTTP 重試放進 App。

Core 路徑均位於 `src/BinanceTicker.Core`，WPF 路徑位於 `src/BinanceTicker`。測試位於 `tests/BinanceTicker.Tests`。

### Task 1：行情詳細欄位、偏好相容與現貨連結

**Files:** Modify `Models/TickerPrice.cs`、`Models/AppSettings.cs`、`Services/TickerParser.cs`、`Services/SettingsService.cs`、`ViewModels/SettingsViewModel.cs`；Create `Services/BinanceTradeLink.cs`；Test `MarketVisualizationSettingsTests.cs`、`TickerDetailsTests.cs`。

**Interfaces:** `TickerPrice` 保留原本建構參數，增加可缺省的 `decimal? HighPrice24h, LowPrice24h, Volume24h, QuoteVolume24h`；`UiSettings.ShowSparkline: bool = true`、`SparklineRange: string = "1h"`；`SettingsViewModel.PreserveUneditedSparklinePreferences(AppSettings edited, AppSettings current): void`；`BinanceTradeLink.Create(string symbol): Uri`。

- [ ] Write tests `ParsesRestAndStreamDetailsWithoutBreakingOldPayloads`、`BadOptionalFieldsDoNotDiscardValidQuote`、`OldAndUnknownRangesPreserveExistingSettings`、`StaleEditorPreservesUneditedPreferenceFields`、`TradeLinkUsesValidatedSpotSymbol`。精確驗證 `h/l/v/q` 與 REST 欄位、缺值 null、range 回到 `1h`、兩個偏好各自的編輯旗標。

```csharp
Assert.Equal("https://www.binance.com/en/trade/BTC_USDT?type=spot", BinanceTradeLink.Create("BTCUSDT").AbsoluteUri);
Assert.Throws<ArgumentException>(() => BinanceTradeLink.Create("https://example.com"));
Assert.True(new AppSettings().Ui.ShowSparkline);
Assert.Equal("1h", new AppSettings().Ui.SparklineRange);
```

- [ ] Run `dotnet test --configuration Release --filter "FullyQualifiedName~TickerDetailsTests|FullyQualifiedName~MarketVisualizationSettingsTests"`，確認缺少行為的測試失敗。
- [ ] Implement interfaces，詳細欄位無效時回傳 null；range 採 string 並在 Normalize 回復 `1h`，避免 enum 反序列化錯誤丟棄整份設定；偏好 setter 分別追蹤實際編輯，Copy 保留新欄位。
- [ ] Run 同一命令及 `dotnet test --configuration Release --filter "FullyQualifiedName~CoreTests|FullyQualifiedName~ViewModelTests"`，預期全部通過。
- [ ] Commit `feat: add market details and compatible sparkline preferences`。

### Task 2：可取消、限流且正確分頁的歷史行情

**Files:** Create `Models/CandlePrice.cs`、`Services/CandleParser.cs`、`Services/BinanceHistoryService.cs`、`Services/HistoryRequestScheduler.cs`；Test `CandleParserTests.cs`、`HistoryRestTests.cs`、`HistoryRequestSchedulerTests.cs`、`TestTimeProvider.cs`。

**Interfaces:** `CandlePrice(string Symbol, DateTimeOffset OpenTime, DateTimeOffset CloseTime, decimal Open, decimal High, decimal Low, decimal Close, decimal Volume, bool IsClosed, DateTimeOffset UpdatedAt)`；`CandleParser.ParseRest(JsonElement data, string symbol, DateTimeOffset asOf): CandlePrice`；`ParseStream(JsonElement data): CandlePrice`；`IBinanceHistoryService.GetCandlesAsync(string symbol, DateTimeOffset start, DateTimeOffset end, CancellationToken token): Task<IReadOnlyList<CandlePrice>>`；`HistoryRequestScheduler(HttpClient client, TimeProvider? timeProvider = null).GetAsync(Uri uri, CancellationToken token): Task<HttpResponseMessage>`。`BinanceHistoryService` 接收共用 scheduler。`TestTimeProvider(DateTimeOffset start)` 實作可控 `GetUtcNow()`／`CreateTimer()` 與 `Advance(TimeSpan)`。

- [ ] Write tests `Loads1442CandlesAcrossTwoFixedRangePages`、`RejectsNonAdvancingOrWrongOrderPage`、`CancelsBetweenPages`、`OptionalCurrentCandleIsNotClosedEarly`、`SchedulerLimitsConcurrencyAndStartSpacing`、`RetriesOnlyTransientFailures`、`RateLimitPauseAlsoAppliesToNewAndManualRequests`。使用受控 HttpMessageHandler 與可控時間；驗證原始查詢參數及回應，不呼叫真實網路。

```csharp
Assert.Equal(1442, result.Count);
Assert.Equal(TimeSpan.FromMinutes(1), result[1].OpenTime - result[0].OpenTime);
Assert.Equal(2, handler.MaximumConcurrentRequests);
Assert.All(handler.StartGaps, gap => Assert.True(gap >= TimeSpan.FromMilliseconds(250)));
Assert.Equal(new[] { 1d, 2d, 5d }, retryDelays.Select(d => d.TotalSeconds));
```

- [ ] Run `dotnet test --configuration Release --filter "FullyQualifiedName~CandleParserTests|FullyQualifiedName~HistoryRestTests|FullyQualifiedName~HistoryRequestSchedulerTests"`，確認分頁、限流及取消需求尚未滿足。
- [ ] Implement models、解析、scheduler 與 REST。固定 end/asOf，游標按分鐘前進；驗證 tuple、OHLC、時間與幣種。每個實際 HTTP GET 共用並行 gate 與開始間隔；retry 使用新的請求，dispose 失敗 response，保留最後失敗資訊；429／418 無 Retry-After 時共用暫停至少 60 秒。
- [ ] Run 同一命令，預期所有測試通過，取消／限流測試不依靠真實等待。
- [ ] Commit `feat: load paginated candles with shared request throttling`。

### Task 3：共用快取與時間正確的走勢投影

**Files:** Create `Services/CandleCache.cs`、`Models/SparklineSeries.cs`、`Services/SparklineProjection.cs`；Test `CandleCacheTests.cs`、`SparklineProjectionTests.cs`。

**Interfaces:** `CandleCache.Configure(IReadOnlyCollection<string> symbols): void`；`Merge(CandlePrice candle, DateTimeOffset now): bool`；`GetSnapshot(string symbol, DateTimeOffset now): IReadOnlyList<CandlePrice>`。只接受 Configure 內幣種，回傳隔離的唯讀快照。`SparklinePoint(DateTimeOffset Time, decimal Price)`、`SparklineSegment(IReadOnlyList<SparklinePoint> Points)`、`SparklineSeries(IReadOnlyList<SparklineSegment> Segments, DateTimeOffset Start, DateTimeOffset End, bool IsComplete, bool IsPositive)`；`SparklineProjection.Create(IReadOnlyList<CandlePrice> candles, DateTimeOffset end, TimeSpan range, int maxPoints = 300): SparklineSeries`。

- [ ] Write tests `NewerStreamWinsOverLateRest`、`ClosedCandleNeverReopens`、`CacheKeepsBoundaryAndAtMost1500`、`UnknownAndRemovedSymbolsStayAbsent`、`RangeUsesActualTimesAndDoesNotStretchPartialData`、`GapsProduceSeparateSegments`、`DownsamplingRetainsEndpointsAndExtrema`、`FutureLiveTimestampDoesNotDrawPastEnd`、`FlatSeriesAndZeroPricesRemainValid`。

```csharp
Assert.Equal(1500, cappedSnapshot.Count);
Assert.Empty(cache.GetSnapshot("REMOVEDUSDT", now));
Assert.Equal(now - TimeSpan.FromHours(24), series.Start);
Assert.Equal(now, series.End);
Assert.Equal(2, gappedSeries.Segments.Count);
Assert.True(series.Segments.Sum(s => s.Points.Count) <= 300);
```

- [ ] Run `dotnet test --configuration Release --filter "FullyQualifiedName~CandleCacheTests|FullyQualifiedName~SparklineProjectionTests"`，確認競態及投影需求尚未滿足。
- [ ] Implement 開盤時間有序快取、版本與收盤狀態比較、保留邊界及裁切。投影依 close/event 時間；只在相鄰分鐘連續時銜接左界，缺口拆段；按時間區段保留首尾／高低價，總點數不超過上限。
- [ ] Run 同一命令，預期全部通過；確認讀取快照不能修改快取內部資料。
- [ ] Commit `feat: share candle history and project gap-aware sparklines`。

### Task 4：混合串流與歷史載入生命週期

**Files:** Modify `Services/BinanceWebSocketService.cs`、`Services/MarketFeed.cs`；Create `Models/HistoryLoadState.cs`、`Services/HistoryCoordinator.cs`；Test `MarketTests.cs`、`CandleStreamTests.cs`、`HistoryCoordinatorTests.cs`。

**Interfaces:** 保留現有 RunAsync 參數並在末端新增選用 `Action<CandlePrice>? onCandle = null`；callback 存在時訂閱 ticker＋kline_1m，否則保持原本 ticker-only 行為。`HistoryLoadStatus { NotRequested, Loading, Loaded, Failed }`；`HistoryLoadState(HistoryLoadStatus Status, string? Error, DateTimeOffset? LastLoadedAt)`。`HistoryCoordinator(IBinanceHistoryService history, CandleCache cache, TimeProvider? timeProvider = null)` 提供 `ConfigureAsync(IReadOnlyCollection<string> symbols): Task`、`SetDemand(IReadOnlyCollection<string> symbols): void`、`EnsureLoadedAsync(string symbol, bool refresh = false): Task`、`GetState(string symbol): HistoryLoadState`、`OnConnectionStatus(ConnectionStatus status): void`、`StopAsync(): Task` 及 `event Action<string>? Changed`。

- [ ] Write tests `RoutesFragmentedTickerAndCandleMessages`、`CombinedStreamsStayWithin1024Subscriptions`、`ConcurrentDemandUsesOneHistoryTask`、`ReconnectFillsGapsOnlyForDemandedSymbols`、`LateGenerationCannotRecreateRemovedSymbol`、`ClosingOneViewKeepsSharedWorkAlive`、`StopCancelsAndDrainsOutstandingWork`。驗證第 513 個幣種可分到下一個 socket，並聚合各 socket 狀態。
- [ ] Run `dotnet test --configuration Release --filter "FullyQualifiedName~MarketTests|FullyQualifiedName~CandleStreamTests|FullyQualifiedName~HistoryCoordinatorTests"`，確認新增路由與生命週期測試失敗。
- [ ] Implement 混合事件解析與訂閱分批：雙 stream 每批最多 512 幣種；ticker-only 每批最多 1024；連線狀態優先任一斷線、其次任一連線中，全部已連線才顯示 Connected。Coordinator 以 feed 世代及每幣種共用任務去重；重連捕捉固定範圍補抓，取消並 await 舊工作後才重新 Configure。可恢復的歷史失敗由 coordinator 記錄 Failed 狀態，供 UI 讀取，不透過 async void 造成未處理例外。
- [ ] Run 同一命令，預期全部通過；排程回呼在鎖外發送，確保 UI／重連回呼不造成死鎖。
- [ ] Commit `feat: coordinate mixed market streams and history recovery`。

### Task 5：報價列與詳情的畫面資料

**Files:** Modify `ViewModels/TickerViewModel.cs`；Create `ViewModels/MarketDetailsViewModel.cs`；Test `MarketVisualizationViewModelTests.cs`、`MarketDetailsViewModelTests.cs`。

**Interfaces:** Row 新增 `HighPriceText`、`LowPriceText`、`VolumeText`、`QuoteVolumeText`、`SparklineSeries? Series`、`QuoteStatusText`、`HistoryStatusText`，提供 `UpdateHistory(IReadOnlyList<CandlePrice> candles, HistoryLoadState state, ConnectionStatus status, string range, DateTimeOffset now): void`。Ticker 新增 `ShowSparkline`、`SparklineRange`、`SelectHourCommand`、`SelectDayCommand`、`ToggleSparklineCommand` 與 `event Action? SparklinePreferencesChanged`。`MarketDetailsViewModel(TickerRowViewModel row, Func<Task> retry, Action<Uri> openBrowser)` 以 `Row` 共用資料，提供 `RetryCommand`、`OpenTradeCommand` 與 `Error`；實作 IDisposable 釋放訂閱。

- [ ] Write tests `RangeSwitchReusesRowsAndSnapshots`、`HistoryFailureDoesNotDiscardQuote`、`StaleQuoteAndHistoryStatusesAreIndependent`、`SelectedRangeTrendIsIndependentOf24hChange`、`DetailsShareRowAndFormatUnits`、`BrowserFailureLeavesDetailsUsable`。可控 now 精確驗證超過 60 秒逾期，不用 Thread.Sleep。

```csharp
Assert.Same(originalRow, ticker.Prices.Single(p => p.Symbol == "BTCUSDT"));
Assert.Contains("USDT", details.Row.QuoteVolumeText);
Assert.Contains("資料", details.Row.HistoryStatusText);
Assert.NotEmpty(details.Error);
Assert.Equal(originalPrice, details.Row.Price);
```

- [ ] Run `dotnet test --configuration Release --filter "FullyQualifiedName~MarketVisualizationViewModelTests|FullyQualifiedName~MarketDetailsViewModelTests"`，確認偏好／狀態／詳情行為尚未滿足。
- [ ] Implement row／Ticker 及 details ViewModel；保留現有數值排序及警示狀態；只以有效 ticker 更新詳細欄位，不引入新的 HTTP 呼叫。
- [ ] Run 同一命令及既有 ViewModelTests，預期全部通過。
- [ ] Commit `feat: expose sparkline and market detail view models`。

### Task 6：WPF 走勢控制項與詳情介面

**Files:** Create `Controls/SparklineControl.cs`、`Views/MarketDetailsWindow.xaml`、`Views/MarketDetailsWindow.xaml.cs`；Modify `Views/TickerWindow.xaml(.cs)`、`Views/SettingsWindow.xaml`、`Themes/Dark.xaml`、`Themes/Light.xaml`；Test `MarketVisualizationWindowTests.cs`。

**Interfaces:** `SparklineControl.Series` dependency property 接收 Task 3 的 SparklineSeries，原生 OnRender 按完整時間範圍映射且不跨段連線。`TickerWindow.MarketDetailsRequested: event Action<string>?`、`MarketDetailsWindow(MarketDetailsViewModel viewModel)`、`RefreshTheme(): void`。詳情與主視窗均 binding 同一 row，不自行訂閱網路。

- [ ] Write STA tests `ToolbarControlsRangeAndVisibility`、`SymbolButtonRoutesDetailsWithoutTriggeringDrag`、`DarkLightAndLongListsRenderReadableGraphs`、`DetailsShowUnitsAndFailureStates`、`ZeroSizeAndFlatGraphRenderWithoutException`。以 ButtonAutomationPeer 觸發實際 controls，RenderTargetBitmap 儲存受控資料預覽。
- [ ] Run `dotnet test --configuration Release --filter "FullyQualifiedName~MarketVisualizationWindowTests"`，確認實際 UI 控制或 rendering 需求尚未滿足。
- [ ] Implement 圖形及 XAML；32 DIP 曲線、狀態文字、range／visibility、可及性名稱及明確詳情入口；使用目前 resources 配色，保留 390 DIP 主畫面及既有捲動／拖曳排除規則。
- [ ] Run 同一命令；逐一檢視 dark／light、1h／24h、hidden、long-list、partial／disconnected、details 預覽，修正裁切及文字重疊。
- [ ] Commit `feat: render sparklines and per-symbol market details`。

### Task 7：整合、生命週期與交付驗證

**Files:** Create `Services/MarketDetailsWindowManager.cs`、`Services/BrowserLauncher.cs`；Modify `App.xaml.cs`、`README.md`、`settings.example.json`、`docs/verification.md`；Test `MarketVisualizationIntegrationTests.cs`、既有 `WindowTests.cs`。

**Interfaces:** `IBrowserLauncher.Open(Uri uri): void` 的 production adapter 使用預設瀏覽器。`MarketDetailsWindowManager(Func<string, MarketDetailsViewModel?> createViewModel)` 提供 `Show(string symbol): void`、`CloseUnavailable(IReadOnlyCollection<string> enabledSymbols): void`、`RefreshTheme(): void`、`CloseAll(): void`、`OpenSymbols: IReadOnlyCollection<string>` 與 `event Action? OpenSymbolsChanged`；視窗 Closed 時釋放詳情 ViewModel 並通知需求變化。App 擁有一份 CandleCache／HistoryCoordinator；事件經 Dispatcher 及 feed 世代檢查交付，1 秒 DispatcherTimer 更新可見圖形及 60 秒資料狀態。

- [ ] Write integration tests `RepeatedDetailsOpenReusesWindowAndDemand`、`FloatHidesTickerButKeepsDetailsUsable`、`SettingsRemovalClosesDetailsAndIgnoresLateResponses`、`SettingsSaveMergesOnlyUntouchedSparklinePreferences`、`HiddenGraphsDoNotInitializeHistoryWithoutDetails`、`ExitDrainsHistoryBeforeDisposingNetworkAndTray`。只用受控服務與暫存檔案，不開真實瀏覽器或使用者設定。
- [ ] Run `dotnet test --configuration Release --filter "FullyQualifiedName~MarketVisualizationIntegrationTests|FullyQualifiedName~WindowTests"`，確認 App 尚未串接的新需求失敗。
- [ ] Implement manager／browser／App 串接：先啟動主 feed，再背景建立歷史需求；所有圖形需求由啟用幣種與開啟詳情去重。隱藏畫面停止重繪；關閉／停用清理畫面訂閱，退出先 cancel／await 所有工作再 dispose。更新 README、設定範例與實機驗收文件。
- [ ] Run `dotnet build --configuration Release`、`dotnet test --configuration Release`，預期 0 build errors／warnings、全部測試通過；新增改動若導致失敗則修復後只重跑必要及最終完整檢查。
- [ ] Run `dotnet publish src/BinanceTicker --configuration Release --runtime win-x64 --self-contained true -o artifacts/issue-6-publish`。少量讀取 Binance 公開 REST／stream 驗證格式；確認 UI 預覽及自包含輸出，記錄原生滑鼠／焦點／瀏覽器等實機未驗證項目。
- [ ] Commit `feat: integrate market visualization lifecycle and document usage`。

## 計畫自我審閱與執行方式

- 規格 1–3 的介面與功能由 Task 5–7 完成；4 的格式與資料來源由 Task 1–2／4 完成。
- 規格 5–7 的資料共用、競態、限流與生命週期由 Task 2–4／7 完成；8 的圖形由 Task 3／6–7 完成；9 的設定與外部入口由 Task 1／5／7 完成；10 由各 task 與 Task 7 最終檢查完成。
- 七個 task 的公共簽章一致；Review Focus 五種情況均有對應受控測試。不以新增功能已完成或先前 87 個測試代表這份計畫已驗收。
- 建議採用 **Native**：由目前 agent 在本聊天逐步實作，再做整體獨立審查。這些任務依賴同一組快取及生命週期介面，沿同一上下文實作較容易保持一致。計畫待使用者審閱並選擇執行方式；本文件尚不代表已開始功能實作。
