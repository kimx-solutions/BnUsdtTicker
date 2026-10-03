# Verification — 2026-10-03

- `dotnet build --configuration Release`: success, zero warnings/errors.
- `dotnet test --configuration Release`: 39 passed, zero failed/skipped.
- `dotnet publish src/BinanceTicker --configuration Release --runtime win-x64 --self-contained true -o artifacts/publish`: success.
- Live Binance REST BTCUSDT snapshot and combined BTCUSDT WebSocket ticker received.
- Rendered WPF ticker and settings views inspected; deterministic preview prices are test data.
- Independent review findings fixed: checkbox row selection, constrained/high-DPI settings
  layout and monitor selection, malformed ticker recovery. Regression tests cover each.
- WPF STA smoke checks Fix/Float deactivation, X hides, exit closes, restored offscreen
  positions, persistence callbacks, XAML creation, tray creation/disposal and long watchlist scrolling.
- Networking checks cover fragmented frames, capped retry delays/reset, receive/retry cancellation,
  malformed REST fallback, valid trading pair checks and precision.

Physical mixed-DPI monitor unplug/replug and real mouse dragging/focus interactions remain
manual acceptance checks listed in README. Native event behavior is exercised deterministically
in tests; it does not substitute for physical monitor hardware verification.
