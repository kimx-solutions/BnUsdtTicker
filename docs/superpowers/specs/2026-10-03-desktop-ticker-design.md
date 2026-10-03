# Binance USDT Desktop Ticker

Source: https://github.com/kimx-solutions/BnUsdtTicker/issues/1

Implement the issue's V1 as a Windows .NET 10 WPF application using MVVM.
The tray owns application lifetime. Closing the borderless draggable ticker
hides it; only the tray Exit action shuts down. Fix stays topmost; Float hides
on deactivation. Settings edit a copy and apply only after a successful save.

The platform-independent Core library owns settings models, atomic JSON storage,
Binance REST validation/initial snapshots, combined WebSocket ticker streams,
bounded retry delays, formatting, and view models. The WPF application owns
dispatcher delivery, tray icon disposal, window lifecycle, and monitor recovery.
Use public market endpoints without credentials. Defaults: BTC, ETH, ENA;
Fix mode, opacity 0.95, compact rows, show 24h change, show on startup.

Settings live at `%LOCALAPPDATA%\BinanceTicker\settings.json`. Corrupt JSON is
copied to `.bak` before replacing it with defaults. Unknown symbols fail
validation; timeouts do not freeze the UI. Restarting subscriptions cancels and
awaits the old feed before starting the new one. No network is needed to run tests.
On disconnected monitors or display changes, recover an inaccessible window to
the primary work area using native pixel coordinates (including mixed DPI).

Verification: deterministic unit/integration tests for persistence, invalid
inputs, REST requests, fragmented socket messages, retry and cancellation,
quote ordering/notifications, settings management, and screen recovery;
Release build and a Windows WPF lifecycle smoke test. Manual tray/focus/drag
and mixed-monitor checks are documented for physical desktop verification.
