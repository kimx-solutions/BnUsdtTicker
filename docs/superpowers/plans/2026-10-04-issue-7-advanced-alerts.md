# Issue #7 Advanced Alerts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans for native execution, or superpowers:subagent-driven-development if the user selects delegation. Steps use checkbox syntax for tracking.

**Goal:** 實作短期漲跌、獨立重複提醒與冷卻，以及相容既有檔案的提醒紀錄查詢。

**Architecture:** Core 的 QuoteWindow 專責有效即時報價序列，PriceAlertService 專責四種條件的序列化狀態機與保存／通知交易。保留舊欄位及既有呼叫入口，擴充獨立警示視窗並新增單一紀錄視窗；App 傳入來源、時間及連線生命週期。

**Tech Stack:** .NET 10、C#、WPF/MVVM、System.Text.Json、xUnit；不新增第三方套件。

**Spec:** `docs/superpowers/specs/2026-10-04-issue-7-advanced-alerts-design.md`

## Global Constraints

- 舊上下限與已提醒布林值保留；缺少新欄位時單次、0 分鐘冷卻、短期停用。
- 四個條件各自獨立；短期區間整數 1–60 分鐘，冷卻整數 0–1440 分鐘，門檻為正 decimal。
- 基準在 `行情時間 − 區間` 以前最多 5 秒；相鄰樣本間隔大於 5 秒重建序列。
- 即時事件距接收 UTC 時間最多正負 5 秒，拒絕相同或倒退時間；REST 不作短期樣本。
- 斷線、重連、重啟、feed 替換與停用後重新累積；已提醒重複條件等待有效未符合觀察。
- 冷卻期間突破消耗武裝，冷卻結束不補發；手動重設清除該條件已提醒及冷卻。
- 通知失敗還原完整狀態與紀錄，未完成還原前禁止新通知；程式結束等待待處理提醒。
- UI 使用繁體中文及既有深／淺色資源，支援鍵盤及窄視窗捲動。
- 不讀取使用者真實設定或用真實 Binance 報價進行測試。

## Review Focus

1. 開啟草稿後收到提醒，再存一般設定或警示，不得覆蓋新冷卻時間（Task 3、5）。
2. 只有某個幣種停止更新時，其他幣種仍有效；停更幣種不能用舊基準重新武裝（Task 2、6）。
3. 成功提醒後冷卻期間寫入失敗，不得消耗尚未保存的突破（Task 3）。
4. 舊紀錄和帶不同 UTC offset 的新紀錄混合時，按真正時刻排序，本機日期篩選含結束日（Task 4、6）。
5. 最小化紀錄視窗後再次開啟、主窗 Float 隱藏與結束程式，不能留下重複視窗或訂閱（Task 6）。

## Task 1: 設定及紀錄模型的相容性

**Files:**
- Modify: `src/BinanceTicker.Core/Models/PriceAlertSettings.cs`
- Create: `src/BinanceTicker.Core/Models/AlertConditionPolicy.cs`
- Create: `src/BinanceTicker.Core/Models/ShortTermAlertSettings.cs`
- Modify: `src/BinanceTicker.Core/Models/AlertHistoryEntry.cs`
- Modify: `src/BinanceTicker.Core/Services/SettingsService.cs`
- Modify: `src/BinanceTicker.Core/Services/AlertHistoryService.cs`
- Test: `tests/BinanceTicker.Tests/AdvancedAlertSettingsTests.cs`

**Interfaces:**
- `AlertStrategy { Once, Repeat }`；AlertType 保留 Upper、Lower，追加 Rise、Fall。
- `AlertConditionPolicy`：Strategy、CooldownMinutes、DateTimeOffset? LastTriggeredAt、bool Armed（預設 true），提供深複製 Copy。
- `ShortTermAlertSettings`：decimal? ThresholdPercent、int WindowMinutes（預設 5）、bool Triggered、AlertConditionPolicy Policy，提供 Copy。
- PriceAlertSettings 保留舊四欄，追加 UpperPolicy、LowerPolicy、Rise、Fall，Copy 深複製所有欄位。
- AlertHistoryEntry 的 TargetPrice 改 nullable，末尾追加 optional WindowMinutes、ThresholdPercent、BaselinePrice、BaselineAt、QuoteAt、ChangePercent；舊五參數呼叫仍編譯。

- [ ] 寫測試：舊 JSON 的 upperTriggered=true 讀回仍 true，策略 Once，Rise.ThresholdPercent=null；設定 Copy 修改短期及 Policy 不影響原件；非法區間 0/61、冷卻 -1/1441、零門檻遭拒。
- [ ] 執行 `dotnet test --configuration Release --filter AdvancedAlertSettingsTests`，確認新增行為尚未提供。
- [ ] 實作模型及 Normalize，依紀錄類型驗證必要欄位，保留舊 Upper／Lower 紀錄。
- [ ] 同一指令驗證 GREEN，並確認舊 CoreTests／PriceAlertTests 沒有相容性退化。
- [ ] Commit：`feat: extend alert models with compatible policies and change conditions`。

## Task 2: 有效報價與時間區間

**Files:**
- Create: `src/BinanceTicker.Core/Services/QuoteWindow.cs`
- Modify: `src/BinanceTicker.Core/Models/TickerPrice.cs`
- Modify: `src/BinanceTicker.Core/Services/TickerParser.cs`
- Test: `tests/BinanceTicker.Tests/QuoteWindowTests.cs`

**Interfaces:**
- TickerPrice 增加末尾 optional QuoteSource Source（Unknown／Rest／Stream）；parser 明確設定 Rest／Stream，舊測試建構子保留。
- `QuoteWindow.Add(TickerPrice quote, DateTimeOffset receivedAt, TimeSpan retention) : bool`。
- `QuoteWindow.Compare(string symbol, DateTimeOffset quoteAt, int windowMinutes) : QuoteComparison?`。
- `QuoteWindow.Clear(string? symbol = null) : void`；`QuoteComparison(decimal BaselinePrice, DateTimeOffset BaselineAt, decimal Price, DateTimeOffset QuoteAt, decimal ChangePercent)`。

- [ ] 寫測試：100→98 的完整五分鐘樣本結果 -2；目標時刻前 5 秒可比較，前 5 秒又 1ms 不可；較晚基準不得採用。
- [ ] 寫測試：每秒受控事件累積；5 秒樣本間隔可用、超過 5 秒重建；倒序／重複、REST、零價及接收時間偏差被拒；只清除停更幣種。
- [ ] 執行 `dotnet test --configuration Release --filter QuoteWindowTests` 觀察 RED。
- [ ] 實作各幣種排序序列、5 秒驗證、區間裁切及未四捨五入 decimal 比較；清除時不影響其他幣種。
- [ ] 同一指令 GREEN；加上既有 parser 測試驗證來源變更相容。
- [ ] Commit：`feat: calculate short-term changes from continuous live quotes`。

## Task 3: 四種提醒狀態機與交易保護

**Files:**
- Modify: `src/BinanceTicker.Core/Services/PriceAlertService.cs`
- Create: `src/BinanceTicker.Core/Services/AlertConditionEvaluator.cs`
- Test: `tests/BinanceTicker.Tests/AdvancedPriceAlertTests.cs`
- Modify: `tests/BinanceTicker.Tests/PriceAlertTests.cs`（只在共用 fixture 需要時調整）。

**Interfaces:**
- 保留 `CheckAsync(string symbol, decimal currentPrice)` 的舊上下限入口。
- IPriceAlertService 新增 `CheckQuoteAsync(TickerPrice quote)`、`OnConnectionStatusAsync(ConnectionStatus status)`；提供相容的 default interface body，既有測試替身不強制改寫。
- PriceAlertService 實作新的報價及連線入口，共用既有 gate、注入 clock 及 QuoteWindow。
- `AlertConditionEvaluator` 的純判斷接受 type、threshold、price／QuoteComparison 及 policy，返回符合狀態及待執行變更；持久化與通知仍由服務負責。
- ResetAsync、SaveAlertAsync、ApplySettingsAsync 保留簽名，支援四類 reset；狀態合併以 live 值為準。

- [ ] 寫單次上漲／下跌邊界及獨立四條件測試；保持首次上下限快照觸發。
- [ ] 寫重複測試：100 上限，101 通知，102 不通知，99 武裝，101 再通知；冷卻 2 分鐘內第二次突破消耗，結束時持續 101 不補發，再 99→101 才通知。
- [ ] 寫重啟、策略／參數修改、區間修改、回撥時鐘、並行更新及草稿保存不覆蓋 live 狀態測試。
- [ ] 寫 failure 測試：通知拋出、紀錄／設定唯讀、還原失敗與恢復；斷言完整持久狀態和紀錄，失敗突破可重試，成功後不重複；未符合重新武裝及冷卻消耗的寫入失敗需還原。
- [ ] 執行 `dotnet test --configuration Release --filter AdvancedPriceAlertTests` 逐組確認 RED。
- [ ] 實作狀態評估、序列化入口、完整快照還原、保守重連武裝及四種紀錄內容；避免把無效短期資料當作未符合。
- [ ] 同一指令 GREEN，執行完整 `dotnet test --configuration Release`。
- [ ] Commit：`feat: evaluate independent repeat alerts with durable cooldown state`。

## Task 4: 通知內容與紀錄查詢 ViewModel

**Files:**
- Modify: `src/BinanceTicker/Services/NotificationService.cs`
- Create: `src/BinanceTicker.Core/ViewModels/AlertHistoryViewModel.cs`
- Test: `tests/BinanceTicker.Tests/AdvancedNotificationTests.cs`
- Test: `tests/BinanceTicker.Tests/AlertHistoryViewModelTests.cs`

**Interfaces:**
- 保留 `NotificationService.Show(AlertHistoryEntry entry)`；Rise／Fall 使用區間、百分比、基準／實際價格與實際漲跌幅。
- `AlertHistoryViewModel(AlertHistoryService history)`，公開 SymbolFilter、AlertType? TypeFilter、DateTime? StartDate／EndDate、bool NewestFirst、Entries、Error、EmptyText、ReloadCommand。
- `Reload() : void`、`RefreshAfterSubmission() : void` 保留篩選；Entries 可使用呈現 row wrapper，排序依 TriggeredAt 實際時間並穩定。

- [ ] 寫測試：波動通知包含 5 分鐘、2% 門檻、100 基準、98 現價與 -2%；舊上下限通知內容仍有正確 side／target。
- [ ] 寫混合紀錄測試：已移除幣種可篩選、四類型、雙端日期、時區 offset 與穩定排序，結束日 23:59:59 包含、下一日排除。
- [ ] 寫讀取失敗保留上次資料、初始空檔與篩選空結果、非法日期提示及重載恢復測試。
- [ ] 執行相關 filter 觀察 RED，實作 ViewModel 及通知文字後驗證 GREEN。
- [ ] Commit：`feat: query alert history and format short-term notifications`。

## Task 5: 警示編輯介面

**Files:**
- Modify: `src/BinanceTicker.Core/ViewModels/PriceAlertEditorViewModel.cs`
- Create: `src/BinanceTicker.Core/ViewModels/AlertPolicyEditorViewModel.cs`
- Modify: `src/BinanceTicker.Core/ViewModels/TickerViewModel.cs`
- Modify: `src/BinanceTicker/Views/PriceAlertWindow.xaml`
- Modify: `src/BinanceTicker/Views/PriceAlertWindow.xaml.cs`
- Test: `tests/BinanceTicker.Tests/AdvancedAlertEditorTests.cs`
- Modify: `tests/BinanceTicker.Tests/PriceAlertEditorTests.cs`

**Interfaces:**
- 保留既有 UpperPriceText／LowerPriceText、CreateAlert、GetAlertResets、RefreshState 及 reset commands。
- 新 editor 提供 Rise／Fall 門檻／區間 text、四種 policy editor、兩個 reset command 及整幣種四條件 reset；policy editor 提供 Strategy、CooldownMinutesText 及狀態文字。
- PriceAlertWindow 增加 `HistoryRequested` 事件；修改草稿只有按儲存才生效。
- TickerRowViewModel.SetAlertState 納入四條件摘要；runtime 短期等待狀態透過 editor 狀態更新呈現。

- [ ] 寫測試：取消不改 live、留空停用、5 分鐘 2% 正確產生設定、非法區間／冷卻／門檻阻擋保存，四個 reset 獨立。
- [ ] 寫草稿期間成功提醒後 RefreshState／CreateAlert 不清除新執行狀態，鈴鐺涵蓋 Rise／Fall。
- [ ] 執行相關 filter 觀察 RED，實作 editor 與 XAML；保留既有控制項名稱供舊 WPF 測試使用。
- [ ] 驗證 GREEN，STA 視窗測試實際輸入、儲存／取消、切換策略、捲動及主題。
- [ ] Commit：`feat: edit advanced price alert conditions and policies`。

## Task 6: 紀錄視窗、App 整合及交付驗證

**Files:**
- Create: `src/BinanceTicker/Views/AlertHistoryWindow.xaml`、`.xaml.cs`
- Create: `src/BinanceTicker/Services/AlertHistoryWindowManager.cs`
- Modify: `src/BinanceTicker/Services/TrayIconService.cs`
- Modify: `src/BinanceTicker/App.xaml.cs`
- Test: `tests/BinanceTicker.Tests/AdvancedAlertIntegrationTests.cs`
- Test: `tests/BinanceTicker.Tests/AlertHistoryWindowTests.cs`
- Modify: `README.md`、`settings.example.json`、`docs/verification.md`

**Interfaces:**
- `AlertHistoryWindowManager.Show()`、`Refresh()`、`RefreshTheme()`、`Close()`，重用單一視窗並還原最小化。
- TrayIconService 建構子最後追加 optional Action? showHistory，保留舊呼叫相容。
- App 每次接受有效行情呼叫 CheckQuoteAsync；連線事件及 feed 停止／替換都由同一排程順序通知服務，禁止舊 generation 報價重新加入序列。
- 紀錄成功提交後通知已開啟紀錄視窗刷新；可使用 PriceAlertService 的成功提交 event，事件只在完整交易完成後發送，UI 更新錯誤不得當成通知提交失敗。

- [ ] 寫整合測試：主窗／走勢隱藏仍累積並觸發、REST 不暖機、斷線／重連重新等待、舊 generation 拒絕、單幣種停更不影響其他幣種。
- [ ] 寫 WPF 測試：tray／editor 入口、最小化後重用、四類紀錄顯示、篩選／排序、主題、讀取錯誤與關閉清除訂閱。
- [ ] 執行相關 filter 觀察 RED，實作視窗、manager、App 來源／時間／連線整合及生命週期。
- [ ] 驗證 GREEN，並實際 render／檢查深淺色視窗、窄視窗與空／錯誤狀態。
- [ ] 更新 README 比較公式、連續資料規則、策略／冷卻與重設、查詢入口及手動驗收；範例保留預設單次、新條件停用。
- [ ] 執行 `dotnet build --configuration Release`、`dotnet test --configuration Release`、`dotnet publish src/BinanceTicker --configuration Release --runtime win-x64 --self-contained true -o artifacts/issue-7-publish`。
- [ ] 在 docs/verification.md 記錄實際結果、測試數與原生 Windows 通知的手動驗收限制。
- [ ] Commit：`feat: integrate advanced alerts and history windows`。
- [ ] 執行全變更審查；依使用者選擇的執行方式採獨立 reviewer，修正實質問題後只重跑受影響與必要完整檢查。

## Plan self-review

- 規格 2–4 節由 Tasks 1–3 涵蓋；5.1 由 Task 5；5.2 由 Tasks 4、6；整合與驗證由 Task 6。
- 保留舊介面／測試替身，明確新增 quote source、波動比較及查詢 ViewModel；未引入新的第三方相依。
- 五項 Review Focus 均配置可觀察行為測試；無依原始碼文字或實作常數反推預期的測試。
- 狀態：使用者已選 Native；六項功能已實作並完成 Release build／176 項測試，發佈建置與獨立審查進行中。
