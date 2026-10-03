<img src="docs/images/app-icon.png" alt="Binance USDT Desktop Ticker 圖示" width="128" height="128">

# Binance USDT Desktop Ticker

**工作時，偶爾看一眼行情。把價格放在桌面的小角落，留給自己欣賞。**

寫程式、整理報表或專心處理工作時，想知道關注的幣種現在多少錢，
不用再打開網頁、切換分頁。Binance USDT Desktop Ticker 用一個小巧的
Windows 桌面視窗，顯示你選擇的幣種價格與 24 小時漲跌幅。

拖到喜歡的角落，讓它安靜陪著你；想看時瞄一眼，想專心時收回系統匣。
**不需要 Binance 帳號，不需要 API Key，也不需要提供個人資訊。**

## 下載

[**前往 GitHub Releases 下載**](https://github.com/kimx-solutions/BnUsdtTicker/releases)

版本發佈後，在 Release 的 **Assets** 下載 `BinanceTicker-v版本號-win-x64.zip`。
發佈包適用於 Windows x64，內含 .NET 執行環境，不需要另外安裝 .NET。

1. 下載 ZIP，完整解壓縮到你喜歡的資料夾。
2. 執行 `BinanceTicker.exe`，開始查看行情。
3. 日後更新時，先從系統匣結束舊版，再解壓縮並開啟新版；本機設定會保留。

同一頁亦提供 `.zip.sha256` 校驗檔，可用來確認下載檔案的 SHA256 是否相符。

## 使用說明

開啟後，預設顯示 BTC、ETH、ENA 對 USDT 的最新價格與 24 小時漲跌幅。
拖曳視窗的空白處或報價列，就能把它放到桌面的小角落；最後位置會自動記住。

### 兩種觀看方式

| 模式 | 行為 | 適合的使用方式 |
| --- | --- | --- |
| **Fix** | 保持在其他視窗上方，切換程式仍顯示 | 放在角落，工作時偶爾瞄一眼 |
| **Float** | 點系統匣叫出，點到其他程式時自動隱藏 | 想看時叫出，看完繼續工作 |

右鍵點擊右下角系統匣（System Tray）的**程式圖示**，即可切換模式。
圖示可能收在系統匣的隱藏圖示選單中。
報價視窗右下角用圖示表示模式與連線狀態，滑鼠移上去可查看文字說明。
連線圖示為綠色時表示已連線，黃色表示連線中，紅色表示斷線。

### 選擇你想看的幣種

1. 點報價視窗右上角的齒輪，或在系統匣選單選擇「設定」。
2. 輸入 `BTC`、`ETH`、`NEAR` 等代號並按「新增」，也可以直接輸入 `BTCUSDT`。
3. 勾選要顯示的幣種；選取一列後，可使用「上移」、「下移」或「刪除」。
4. 按「儲存」套用；按「取消」放棄這次編輯。

新增時會向 Binance 驗證是否存在可交易的 USDT 現貨交易對。
設定也能調整透明度、緊湊列高、24 小時漲跌幅欄位，以及啟動時是否顯示視窗。

### 隱藏、叫回與結束

- **暫時隱藏**：點報價視窗的 X 或按 `Alt+F4`；程式仍在系統匣常駐。
- **重新叫出**：左鍵點系統匣的程式圖示，或右鍵選「顯示報價」。
- **結束程式**：右鍵點系統匣的程式圖示，選擇「結束程式」。

網路中斷時，視窗會保留最後收到的報價並自動嘗試重新連線。
滑鼠移到報價列可查看最後更新時間；斷線期間顯示的價格是最後一次收到的資料。

## 安全與隱私

**看行情，只需要公開價格。你的帳號、資產與個人資料，都不需要交給這個工具。**

- **不需要 API Key**：直接讀取 Binance 公開市場報價，無需建立或貼上金鑰。
- **不要求個人資訊**：沒有註冊或登入流程，不收集、不使用姓名、Email、電話等個人資料。
- **不存取你的帳戶**：不讀取 Binance 帳戶、資產、交易紀錄，也不提供下單或轉帳功能。
- **沒有使用追蹤**：程式沒有廣告、分析追蹤或遙測上傳功能。
- **偏好保存在本機**：監控幣種、視窗位置、顯示模式與透明度儲存在你的電腦，設定檔不會上傳。
- **行情連線使用加密傳輸**：透過 HTTPS / WSS 直接向 Binance 取得公開行情。

查價時會向 Binance 傳送交易對代號；Binance 也會收到一般網路連線資訊，例如 IP 位址。
程式不會因此要求或傳送你的 Binance 登入資料或 API Key。
原始碼可在[本專案](https://github.com/kimx-solutions/BnUsdtTicker)檢視。

## 本機設定

設定自動儲存於 `%LOCALAPPDATA%\BinanceTicker\settings.json`。
更換程式資料夾或更新版本時，偏好設定仍會保留。
若設定檔損壞，程式會先備份為 `settings.json.bak`，再恢復預設值。
顯示器配置改變時，會嘗試把螢幕外的報價視窗移回主螢幕工作區。

可參考 [settings.example.json](settings.example.json)；手動編輯設定檔前，請先結束程式。

<details>
<summary>給開發者：執行、建置、發佈與測試</summary>

## 從原始碼執行

專案使用 .NET 10 / WPF / MVVM，需要 Windows 與 .NET 10 SDK：

```powershell
dotnet run --project src/BinanceTicker
```

價格 ≥1000 顯示 2 位、≥1 顯示 4 位、≥0.01 顯示 5 位，其餘顯示 8 位小數。
初始資料使用 REST `/api/v3/ticker/24hr`；即時資料使用 combined
`<symbol>@ticker` WebSocket，約每秒更新。[Binance 官方串流文件](https://developers.binance.com/docs/binance-spot-api-docs/web-socket-streams)。
斷線依 1、2、5、10、30 秒重試，收到行情後重設延遲。
REST/驗證有 10 秒逾時；停用全部幣種時不建立行情連線。

## 建置與驗證

```powershell
dotnet build --configuration Release
dotnet test --configuration Release
dotnet publish src/BinanceTicker --configuration Release --runtime win-x64 --self-contained true -o artifacts/publish
```

執行 `artifacts/publish/BinanceTicker.exe`。自包含版本不需要另外安裝 .NET runtime。
CI 在 Windows 上執行建置、測試及發佈封裝。

## GitHub Release

先將包含 `.github/workflows/release.yml` 的程式碼推送到 GitHub，再建立版本標籤：

```powershell
git tag v1.0.0
git push origin v1.0.0
```

`Release Windows app` workflow 會檢出該標籤的程式碼，依序還原、建置、測試、
發佈 Windows x64 自包含版本，再建立 GitHub Release 與自動產生的版本說明。
版本號會寫入程式組件。`v1.0.0-beta.1` 等帶後綴的版本會標示為 Pre-release。
一般 branch push/PR 使用原本 CI，版本 tag 使用 Release workflow。

Release 附件：

- `BinanceTicker-v1.0.0-win-x64.zip`：完整程式、.NET runtime、README 與設定範例。
- `BinanceTicker-v1.0.0-win-x64.zip.sha256`：ZIP 的 SHA256 校驗值。

下載並完整解壓縮 ZIP，執行 `BinanceTicker.exe`，無需安裝 .NET。
Workflow 使用 GitHub 自動提供的 `GITHUB_TOKEN`，不需另外設定 PAT。

亦可在 Actions → **Release Windows app** → **Run workflow** 輸入已推送的
版本標籤。手動執行入口需要 workflow 存在於預設分支；請選擇包含此 workflow
的分支。流程仍會建置輸入標籤的程式碼，標籤必須存在。
重新執行時，已存在的附件會保留。若 ZIP 已上傳但校驗檔缺少，會下載原本的
ZIP 產生相符校驗檔；若只剩校驗檔，會先核對重建 ZIP，確認一致才補上。
上次失敗留下的草稿會在附件齊全後正式發佈。
測試結果與 ZIP 也會保存在該次 Actions run 的 artifacts 中。
發佈採用 [GitHub CLI 的 Release 指令](https://cli.github.com/manual/gh_release_create)。

## 測試與實機驗收

測試使用受控 HTTP/WebSocket 資料，無需 Binance 網路，包括 JSON 損壞備份、
價格精度、幣種管理、串流分段、重連延遲及 WPF 視窗生命週期。
WPF smoke test 使用 STA 執行緒建立實際視窗並短暫顯示，檢查 X 隱藏、
模式切換、失焦處理、位置修正及 XAML 載入。

實機驗收：

1. 開啟程式，確認真實價格持續更新；斷網/復網確認狀態與重連。
2. Fix 切到另一程式仍顯示；Float 點另一程式即隱藏；Tray 可叫回。
3. 拖曳、隱藏、重開程式，確認位置保存；設定新增/排序/停用/取消正常。
4. 將視窗放到不同 DPI 的外接螢幕，拔除螢幕或重新啟動，確認視窗回到可見工作區。
5. Tray 結束後確認程序退出與圖示移除。

V1 僅報價，不含帳戶、下單、K 線或歷史資料。

</details>
