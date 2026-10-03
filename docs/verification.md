# Verification — 2026-10-03

## Issue #3 — Price alerts

- Follow-up: tray menu text is vertically centered against each full padded row.
  Raster checks verify Chinese/English text placement in dark/light themes and
  highlighted menus; the original offset failed the test before the fix. Full suite
  remains 83/83, and the self-contained executable was republished.

- Branch: `codex/issue-3-price-alerts`, based on `8f0456b`.
- Release build: zero warnings/errors. Full Release suite: 83 passed, zero failed/skipped.
- Windows x64 self-contained publish: `artifacts/issue-3-publish/BinanceTicker.exe`.
- Tests cover inclusive upper/lower limits, independent one-shot flags, 100 concurrent
  quotes, restart persistence, per-side/all resets, disabled/unconfigured symbols,
  actual/target prices and offset timestamps in JSON, old settings, deep-copy editing,
  invalid/all-symbol drafts, live-state merge and failed settings replacement.
- Persistence/submission failure tests verify no notification before successful writes,
  rollback after rejected submission, independent rollback attempts and recovery before
  retry when a history file becomes read-only during submission.
- Independent feature review identified incomplete rollback and missing pending-shutdown
  coverage. Both addressed: rollback regression observed RED then GREEN; omitting the
  shutdown drain makes the controlled WPF shutdown test fail before restoring GREEN.
- Real WPF controls exercise threshold input, validation visible beside Save, reset
  commands, alert row indicator, theme switching, scrolling and narrow settings layout.
- Runtime test passes quotes through the App handler and rejects stale reconnect quotes.
  Shutdown test queues an incomplete alert check and verifies the real NotifyIcon remains
  visible until completion, then is disposed.
- WPF tests suppress application startup using a test subclass; they use controlled
  services and temporary files without opening Binance or reading the user's settings.
- Rendered dark/light settings, ticker alert indicator and narrow layouts inspected in
  `artifacts/issue-3-previews`.

Native Windows notification visibility (including Do Not Disturb) remains a manual
acceptance check. Tests verify the production adapter's content and submission boundary,
not whether Windows displays a banner. Cross-process coordination and crash/power-loss
atomicity across two files and shell submission are outside the service's guarantee.

## Original desktop ticker verification

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
