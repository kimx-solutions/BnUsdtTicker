# 換幣比較 Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan inline. Steps use checkbox syntax for tracking.

**Goal:** 手動記錄 A → B，查看現在換回 A 的幣數差額、USDT 差額與相對報酬。

**Architecture:** AppSettings 保存輸入，純計算服務產生比較結果。獨立 ViewModel 保存行情快取與未儲存編輯，WPF 視窗接到 App 的即時行情與共用設定保存流程。

**Tech Stack:** C# / .NET 10 / WPF / xUnit，無新外部相依。

**Spec:** docs/superpowers/specs/2026-10-09-swap-comparison-design.md

## Global Constraints

- 比較基準為繼續持有原幣；不改動共用持倉，不加總不同情境。
- 實際到帳數量，顯示未扣除換回費用的估算。
- 正 decimal 數量，兩個不同且有效的 USDT 現貨交易對，時間不可未來。
- 超過 60 秒或非 Connected 標示最後報價，無效報價不得污染快取。
- 深／淺色、Fix／Float、視窗重用、主窗隱藏仍更新。
- 舊設定保持相容；損壞比較資料保留備份並阻止靜默覆寫。

## Review Focus

- 極小正值乘積捨入為零不得造成除零例外（Task 1）。
- 舊編輯視窗儲存不得覆蓋新比較／最新警示（Task 3）。
- 無新報價仍須更新過期狀態（Task 2/3）。
- 非法 JSON 記錄的 decimal／時間型別不得重置其他設定（Task 1）。
- 網路驗證或保存失敗／視窗關閉仍保留已保存基準（Task 2/3）。

### Task 1: 比較模型、計算與設定保存

**Files:** Core/Models/SwapComparisonSetting.cs、SwapComparisonResult.cs；Core/Services/SwapComparisonCalculator.cs、SwapComparisonSettingConverter.cs；修改 AppSettings.cs、SettingsService.cs、MarketDemand.cs、PriceAlertService.cs；新增 SwapComparisonCalculatorTests.cs、SwapComparisonSettingsTests.cs。

**Interfaces:** SwapComparisonCalculator.Value(setting, fromQuote, toQuote, status, now) → SwapComparisonResult；AppSettings.SwapComparisons；SwapComparisonSetting.Copy()/Validate(now)；SettingsService.LoadWarning。

復原整合：PriceAlertService.ApplySettingsAsync 在既有交易鎖內呼叫 SettingsService.SaveWithComparisonRecovery(updated, previous)；
只允許明確修正／移除損壞紀錄的保存，並完整保留剩餘無效紀錄的 raw JSON。
一般 Save 仍拒絕包含損壞紀錄的設定。

- [x] RED：測試 100/20、5/30 → 120/+20/+100/+20%，5/20 → 80/−20/−100/−20%，5/25 → 100/0/0/0；缺／無效／未來行情、停用、60 秒邊界、溢位與極小值。
- [x] RED：讀取舊 JSON，設定往返、深複製、需求去重；單筆錯誤型別隔離、原檔備份、保存保護與明確移除解除。
- [x] 執行 targeted dotnet test，確認缺少功能失敗。
- [x] 實作正 decimal 計算，顯示結果全有或全無；錯誤紀錄保留 raw JSON 與穩定 ID。
- [x] 執行 targeted test，所有新增案例通過。

### Task 2: 紀錄與編輯 ViewModel

**Files:** Core/ViewModels/SwapComparisonViewModel.cs、SwapComparisonRowViewModel.cs；新增 SwapComparisonViewModelTests.cs。

**Interfaces:** constructor(settings, IBinanceService, Func<IReadOnlyList<SwapComparisonSetting>,Task<bool>> save, TimeProvider?)；Configure(settings)；Update(quote)；SetStatus(status)；Refresh()；SaveAsync/DeleteAsync/ToggleAsync；New/Edit/Cancel commands。

- [x] RED：新增與編輯保存、取消不改基準、同方向多筆、日期排序；無效數量／同幣／未來時間／無效交易對不保存。
- [x] RED：以受控行情與時間驗證任一邊更新、倒序／未來／零價不污染快取，無新報價仍過期；未保存輸入在錯誤／重新 Configure 後保留。
- [x] 執行 targeted test，確認缺少功能失敗。
- [x] 實作獨立編輯草稿與行情快取；保存成功才更新紀錄，失敗保持草稿；busy 時禁止再次變更。
- [x] 執行 targeted test，所有新增案例通過。

### Task 3: WPF 視窗、入口與 App 整合

**Files:** Views/SwapComparisonWindow.xaml(.cs)、Services/SwapComparisonWindowManager.cs、App.Swaps.cs；修改 App.xaml.cs、TrayIconService.cs、TickerWindow.xaml(.cs)；新增 SwapComparisonWindowTests.cs，接到現有 STA WindowTests；README.md 與 docs/verification.md。

**Interfaces:** 單一 WindowManager Show()/Close()/SetMode()/RefreshTheme()；App.SaveSwapComparisonsAsync(records) 從當前 settings.Copy() 合併，透過 alerts.ApplySettingsAsync 保存並保留最新提醒；ApplySettingsAsync 保留最新比較資料。

- [x] RED：STA 真實視窗檢查入口、編輯 binding、視窗重用、模式／主題、窄視窗及渲染；App 保存合併與失敗不變更設定。
- [x] 執行 targeted test，確認缺少功能失敗。
- [x] 實作清單、結果與編輯區、保存／取消／刪除／啟停；使用動態主題資源與 AutomationProperties。
- [x] 連接行情與狀態、每秒過期刷新、需求集合、模式／主題與退出生命週期；啟用紀錄即使未在分組中也接收行情。
- [x] 完整測試與 Release build 使用 --artifacts-path artifacts/swap-build，預期全部通過；檢視深淺／窄視窗截圖，補使用說明與驗證紀錄。
- [x] 最後獨立審查並修正重要發現；保留功能分支供使用者查看，不擅自合併或發佈。
