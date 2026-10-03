# Binance USDT Desktop Ticker

Windows 常駐桌面報價工具，實作 [issue #1](https://github.com/kimx-solutions/BnUsdtTicker/issues/1)。
使用 .NET 10 / WPF / MVVM，讀取 Binance 公開現貨市場資料，不需要 API Key。

## 執行

需要 Windows 與 .NET 10 SDK：

```powershell
dotnet run --project src/BinanceTicker
```

預設顯示 BTC、ETH、ENA 的 USDT 最新價及 24h 漲跌幅。拖曳報價視窗可移動；
點右上角 X 或 Alt+F4 會隱藏視窗，程式仍持續常駐。左鍵點擊 Tray Icon 或
右鍵選「顯示報價」可重新叫出；Tray 選單的「結束程式」才會真正退出。

- **Fix Mode**：保持最上層，切換焦點仍顯示。
- **Float Mode**：失去焦點自動隱藏，再點 Tray Icon 叫出。
- **設定**：新增交易對、刪除、上移/下移、啟用/停用；可調整模式、透明度、
  緊湊列高、24h 欄位及啟動是否顯示。輸入 BTC 或 BTCUSDT 都可以，新增時
  會向 Binance 驗證可交易的 USDT 現貨交易對。取消不會套用編輯。

價格 ≥1000 顯示 2 位、≥1 顯示 4 位、≥0.01 顯示 5 位，其餘顯示 8 位小數。
初始資料使用 REST `/api/v3/ticker/24hr`；即時資料使用 combined
`<symbol>@ticker` WebSocket，約每秒更新。[Binance 官方串流文件](https://developers.binance.com/docs/binance-spot-api-docs/web-socket-streams)。
斷線保留最後報價並顯示狀態；依 1、2、5、10、30 秒重試，收到行情後重設延遲。
REST/驗證有 10 秒逾時；連線與接收都可取消。停用全部幣種時不建立行情連線。

## 設定檔

自動儲存於 `%LOCALAPPDATA%\BinanceTicker\settings.json`，範例見
[settings.example.json](settings.example.json)。修改檔案前先結束程式。
損壞的 JSON 會先備份為 `settings.json.bak`，再恢復預設設定。
設定採用暫存檔後原子替換；啟動與顯示器配置變更時，會將螢幕外的視窗移回
主螢幕工作區。視窗位置保存為 WPF 座標，可使用左側螢幕的負座標。

## 建置與驗證

```powershell
dotnet build --configuration Release
dotnet test --configuration Release
dotnet publish src/BinanceTicker --configuration Release --runtime win-x64 --self-contained true -o artifacts/publish
```

執行 `artifacts/publish/BinanceTicker.exe`。自包含版本不需要另外安裝 .NET runtime。
CI 在 Windows 上執行建置、測試及發佈封裝。

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
