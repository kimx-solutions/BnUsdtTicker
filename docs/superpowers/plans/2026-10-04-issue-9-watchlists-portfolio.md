# Issue #9 Watchlists and Portfolio Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 支援可保存的自選分組與共用手動持倉，在報價窗顯示正確且不重複的損益摘要。

**Architecture:** 分組保存檢視，共用 Symbols 保存提醒，持倉按交易對保存。以純 decimal 計算服務、去重行情需求與全域報價快取連接既有 MVVM；設定編輯保持副本交易語意。

**Tech Stack:** .NET 10、WPF、System.Text.Json、xUnit；不新增產品依賴。

**Spec:** `docs/superpowers/specs/2026-10-04-issue-9-watchlists-portfolio-design.md`

## Global Constraints

- 資料僅保存本機，不增加帳戶登入、帳戶同步或下單。
- 沿用 WPF/MVVM、深淺主題、Fix/Float、價格提醒與歷史走勢。
- 使用 decimal；只在顯示時格式化與四捨五入，不提前截斷。
- 至少保留一個分組，最後一組不可刪除。
- 分組切換不重啟行情、不重設提醒；寫入失敗保留原選擇並提示。
- 長清單使用捲動區，固定操作項仍可用；最小視窗為 360 × 260 DIP。
- 行情需求取所有分組 enabled 成員、正數持倉及已啟用提醒所需幣種的聯集，按 symbol 去重。

## Review Focus

- 舊設定的空清單、停用幣種及已觸發短期提醒必須完整遷移；由 Task 1 固定測試。
- 正數孤立持倉與不在目前分組的提醒仍取得行情；由 Task 2、4 固定測試。
- 缺報價與 decimal 溢位不被計為零，也不可使訂閱回呼中止；由 Task 2 固定測試。
- 編輯期間主窗切換分組／偏好或獨立提醒觸發，儲存不得覆寫最新未編輯狀態；由 Task 3、4 固定測試。
- 取消分組刪除及持倉清除不能改動共用資料；最小視窗所有控制項可操作；由 Task 3、5 固定測試。

## Task 1: 設定模型、遷移與保存

**Files:** Create `src/BinanceTicker.Core/Models/WatchlistGroup.cs`, `src/BinanceTicker.Core/Models/HoldingSetting.cs`, `src/BinanceTicker.Core/Services/WatchlistSettings.cs`; modify `Models/AppSettings.cs`, `Services/SettingsService.cs`; create `tests/BinanceTicker.Tests/WatchlistSettingsTests.cs`.

**Interfaces:**
- `AppSettings.Watchlists : List<WatchlistGroup>?`, `ActiveWatchlistId : string?`, `Holdings : List<HoldingSetting>`；缺少 Watchlists 是舊資料遷移訊號。
- `WatchlistGroup` 包含 `Id`, `Name`, `Members : List<WatchlistMember>`, `Ui : UiSettings`, `SortColumn : TickerSortColumn?`, `SortDescending : bool`。
- `WatchlistMember` 包含 `Symbol : string`, `Enabled : bool`, `Order : int`，可通知啟用狀態變更。
- `HoldingSetting` 包含 `Symbol : string`, `Quantity : decimal`, `AverageCost : decimal`。
- `WatchlistSettings.Normalize(AppSettings settings) : void`；正規化／遷移並維持全域 symbol 登錄。`Active(AppSettings settings) : WatchlistGroup`；取得正規化後有效分組。
- AppSettings.Copy 必須深複製分組、Ui、成員、持倉，保留 null 的舊資料訊號。

- [x] **Step 1:** 寫 `LegacySettingsMigrateMembersAndAlerts`：原順序 ETH／BTC、BTC 停用、已觸發上限／短期提醒遷移後相同；寫 `EmptyLegacyListStaysEmpty`, `NewEmptyGroupIsNotRemigrated`, `CopiesAreIndependent`, `GroupsAndHoldingsRoundTrip`, `InvalidActiveIdFallsBack`。測試名稱重複／空白、同組重複 symbol、負持倉拒絕，以及跨組同 symbol 合法。
- [x] **Step 2:** Run `dotnet test --configuration Release --filter FullyQualifiedName~WatchlistSettingsTests`。Expected: FAIL，新增型別／屬性尚未存在。
- [x] **Step 3:** 實作上述模型及 Normalize；舊偏好複製至預設組，保留現有提醒策略與原子保存／損壞備份行為。
- [x] **Step 4:** Run 同 Step 2，Expected: PASS；Run `dotnet test --configuration Release --filter FullyQualifiedName~CoreTests`，Expected: PASS。
- [x] **Step 5:** Commit `feat: persist watchlists and shared holdings with legacy migration`。

## Task 2: 持倉計算與行情需求

**Files:** Create `Models/PortfolioValuation.cs`, `Services/PortfolioCalculator.cs`, `Services/MarketDemand.cs` in `src/BinanceTicker.Core`; create `tests/BinanceTicker.Tests/PortfolioCalculatorTests.cs`, `MarketDemandTests.cs`.

**Interfaces:**
- `PortfolioCalculator.Value(HoldingSetting holding, TickerPrice? quote, ConnectionStatus status, DateTimeOffset now) : HoldingValuation`。
- `HoldingValuation` 包含 `MarketValue`, `CostBasis`, `Profit`, `ProfitPercent : decimal?`，`MissingQuote`, `IsStale`, `Overflowed : bool`，`UpdatedAt : DateTime?`。
- `PortfolioCalculator.Summarize(IEnumerable<HoldingSetting> holdings, IReadOnlyDictionary<string,TickerPrice> quotes, ConnectionStatus status, DateTimeOffset now) : PortfolioSummary`。
- `PortfolioSummary` 包含 `MarketValue`, `CostBasis`, `Profit`, `ProfitPercent : decimal?`，`PartialMarketValue`, `PartialProfit : decimal`，`HoldingCount`, `MissingCount : int`, `IsStale : bool`；無正數持倉報價缺失／溢位才提供完整總額。
- `MarketDemand.Quotes(AppSettings settings) : string[]`；穩定排序的去重需求集合。啟用提醒須有任一上／下限或 Rise／Fall 門檻且全域 Symbols.Enabled=true。

- [x] **Step 1:** 寫 `ValuesDecimalProfitWithoutPrematureRounding`：數量 2、成本 10、價 12 → 市值 24、成本 20、損益 4、20%；負損益亦驗證。`ZeroCostDoesNotDivide`, `ZeroQuantityNeedsNoQuote`, `MissingQuoteIsNotZero`, `DisconnectedAndExpiredQuotesAreMarked`, `OverflowDoesNotThrow`（decimal.MaxValue × 2）。`SummaryUsesWeightedCostAndDeduplicates`：成本總額 40、損益 8 → 20%，同 symbol 不重複；`IncompleteSummaryReportsOnlySubtotal`：完整值／百分比 null 且 MissingCount=1；`ZeroAndUnsetHoldingsAreDistinct`。
- [x] **Step 2:** 寫 `DemandUnionsGroupsHoldingsAndAlerts`、`OrphanHoldingStillSubscribes`、`LegacyDisabledAlertDoesNotSubscribe`、`EmptyDemandIsEmpty`；Run `dotnet test --configuration Release --filter 'FullyQualifiedName~PortfolioCalculatorTests|FullyQualifiedName~MarketDemandTests'`。Expected: FAIL，服務尚未存在。
- [x] **Step 3:** 實作 decimal 純計算、nullable 資料與溢位處理；失效門檻固定 60 秒，摘要去重且缺報價不納入完整值。
- [x] **Step 4:** Run 同 Step 2，Expected: PASS。
- [x] **Step 5:** Commit `feat: calculate portfolio values and deduplicate market demand`。

## Task 3: 分組及共用持倉編輯交易

**Files:** Modify `src/BinanceTicker.Core/ViewModels/SettingsViewModel.cs`; create `ViewModels/HoldingEditorViewModel.cs`, `tests/BinanceTicker.Tests/WatchlistEditorTests.cs`。

**Interfaces:**
- SettingsViewModel 提供 `Watchlists : ObservableCollection<WatchlistGroup>`, `SelectedWatchlist : WatchlistGroup?`, `WatchlistName : string`, `Holdings : ObservableCollection<HoldingEditorViewModel>`；原 Symbols 編輯入口對應選取組成員。
- 命令 `AddWatchlistCommand`, `RenameWatchlistCommand`, `RemoveWatchlistCommand`；UI 必須先顯示刪除語意，ViewModel 拒絕刪除最後一組。
- HoldingEditorViewModel 提供 Symbol、QuantityText、AverageCostText；`CreateHolding() : HoldingSetting?` 解析 invariant decimal，兩欄空白為清除、只填一欄報錯、零可接受。
- `SettingsViewModel.CreateSettings() : AppSettings` 回傳編輯副本；`PreserveUneditedWatchlistPreferences(AppSettings edited, AppSettings current) : void` 合併未編輯選擇／偏好。

- [x] **Step 1:** 寫 `GroupsKeepIndependentOrderAndVisibility`, `CrossGroupHoldingHasOneSharedEditor`, `CancelDoesNotMutateOriginal`, `DeleteGroupKeepsHoldingsAndOtherMembers`, `LastGroupCannotBeRemoved`, `OrphanHoldingCanBeEditedAndCleared`。驗證 invalid／負數／溢位／千分位拒絕，quantity="0" 與空白不同。`UneditedPreferencesPreserveLiveChanges` 驗證編輯期間主窗新選擇與走勢偏好被保留。
- [x] **Step 2:** Run `dotnet test --configuration Release --filter FullyQualifiedName~WatchlistEditorTests`。Expected: FAIL，新編輯入口尚未存在。
- [x] **Step 3:** 實作以副本操作的新增／驗證／排序、分組 CRUD、持倉解析與偏好合併；保留既有 AddAsync 的取消及 Binance 驗證。
- [x] **Step 4:** Run 同 Step 2，Expected: PASS；Run `dotnet test --configuration Release --filter FullyQualifiedName~ViewModelTests`，Expected: PASS。
- [x] **Step 5:** Commit `feat: edit watchlists and shared holdings transactionally`。

## Task 4: 主窗 ViewModel 與行情生命週期

**Files:** Modify `src/BinanceTicker.Core/ViewModels/TickerViewModel.cs`, `src/BinanceTicker/App.xaml.cs`, `src/BinanceTicker.Core/Services/PriceAlertService.cs`（僅需要的門檻／合併）；可將 App 的分組協調拆入 `src/BinanceTicker/App.Watchlists.cs`；create `tests/BinanceTicker.Tests/WatchlistTickerTests.cs`, `WatchlistIntegrationTests.cs`。

**Interfaces:**
- TickerViewModel 使用全域 symbol→row 快取；`Update(TickerPrice price) : bool` 代表接受新的已訂閱報價，而非列是否可見。
- `SelectWatchlist(string id) : void` 更新可見列與分組偏好；暴露 Watchlists、ActiveWatchlistId、CurrentPortfolio、TotalPortfolio，`RefreshPortfolio(DateTimeOffset now) : void` 刷新報價年齡／摘要。
- 列提供持倉量、成本、市值、損益／百分比的顯示文字與資料狀態；切換後重用快取，保留各組獨立排序。
- `WatchlistPreferencesChanged` 事件由 App 處理持久化、失敗回復選取；不呼叫 RestartFeedAsync。行情集合變更由 MarketDemand.Quotes 比較後決定重啟。

- [x] **Step 1:** 寫 `HiddenGroupQuotesUpdatePortfolioAndAlerts`, `SwitchKeepsQuotesAndIndependentSort`, `SharedHoldingTotalsAreNotDoubled`, `HiddenMembersBelongToCurrentSummary`, `OlderQuotesAreRejectedGlobally`；現有排序行為與 row 重用不得退化。
- [x] **Step 2:** 寫 `GroupSwitchDoesNotRestartFeedOrResetAlerts`, `CostEditDoesNotRestartFeed`, `PersistenceFailureRestoresSelection`, `SettingsSavePreservesConcurrentAlertTrigger`, `PositiveOrphanHoldingReceivesQuotes`, `StaleStatusRefreshesWithoutVisibleSparkline`。Run `dotnet test --configuration Release --filter 'FullyQualifiedName~WatchlistTickerTests|FullyQualifiedName~WatchlistIntegrationTests'`。Expected: FAIL，新切換與全域更新未實作。
- [x] **Step 3:** 先更新快取再分別計算及提醒，解除 App.UpdatePriceAsync 目前的不可見列跳過問題；使用 Task 2 需求集合，歷史補載只針對可見列／詳情；更新 timer 確保摘要狀態不依走勢需求；保留提醒閘門、回復及舊報價拒絕。
- [x] **Step 4:** Run 同 Step 2，Expected: PASS；Run `dotnet test --configuration Release`，Expected: 全套 PASS。
- [x] **Step 5:** Commit `feat: switch watchlists without interrupting shared quotes and alerts`。

## Task 5: WPF 操作介面、文件與整體驗證

**Files:** Modify `src/BinanceTicker/Views/TickerWindow.xaml`, `TickerWindow.xaml.cs`, `SettingsWindow.xaml`, `SettingsWindow.xaml.cs`；必要時使用獨立 `Views/PortfolioEditorWindow.xaml` 與 `.xaml.cs` 保持一般設定可用；create `tests/BinanceTicker.Tests/WatchlistWindowTests.cs`; update `README.md`, `settings.example.json`, `docs/verification.md`。

**Interfaces:** 主窗 ComboBox 綁定分組且具有可及性名稱；可收合摘要顯示「本組全部成員」與「全部持倉」、未設定／部分小計／舊報價。設定介面透過 Task 3 編輯副本操作；刪除確認文字使用規格原文，儲存失敗停留編輯視窗。

- [x] **Step 1:** 寫 STA smoke `WindowLoadsWatchlistAndPortfolioBindings`, `SmallWindowKeepsControlsReachable`, `LongWatchlistScrolls`, `DeletionExplainsSharedDataPreservation`, `SaveAndCancelRespectEditorTransaction`；兩個主題均建立／短暫顯示實際 WPF 視窗，檢查 360×260 時捲動／固定控制項及收合摘要。
- [x] **Step 2:** Run `dotnet test --configuration Release --filter FullyQualifiedName~WatchlistWindowTests`。Expected: FAIL，新控制項及 binding 尚未存在。
- [x] **Step 3:** 實作 WPF UI，沿用既有樣式與拖曳排除，顯示成本單位及輸入格式；更新文件／設定範例為多組、共用持倉與遷移說明。
- [x] **Step 4:** Run 同 Step 2，Expected: PASS；輸出並檢視深淺主題、小視窗與長清單畫面，確認文字與操作可見。
- [x] **Step 5:** Run `dotnet build --configuration Release`、`dotnet test --configuration Release`、`dotnet publish src/BinanceTicker --configuration Release --runtime win-x64 --self-contained true -o artifacts/publish`、`git diff --check`。Expected: 全部 exit 0、tests 無失敗、publish 產生 BinanceTicker.exe。
- [x] **Step 6:** Commit `feat: expose watchlist and portfolio controls and document usage`。以獨立整體審查檢查規格與 Review Focus，必要修正使用 RED→GREEN 測試後再跑全套；不因完成本地實作自動合併或發佈。
