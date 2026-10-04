# Verification — 2026-10-03

## Issue #6 — Market visualization (2026-10-04)

- Follow-up: graph visibility now uses the bottom-right chart icon, immediately
  left of the theme switch. The top toolbar keeps only 1h/24h. Actual WPF controls
  verified both toggle directions; dark/light/hidden renders inspected in
  `artifacts/issue-6-icon-previews`. Updated self-contained executable:
  `artifacts/issue-6-icon-publish/BinanceTicker.exe`.

- Branch: `codex/issue-6-implementation`, based on `71c007e`.
- Added shared 1-minute history, 1h/24h sparklines, separate market details, and
  default-browser Binance spot links. Ticker width remains 390 DIP.
- Release build: zero warnings/errors. Release suite: 139 passed, zero failed/skipped.
- Self-contained Windows x64 output: `artifacts/issue-6-publish/BinanceTicker.exe`.
- Controlled tests cover pagination beyond 1000 bars, retries/shared 429/418 pause,
  request concurrency/spacing, immutable bounded snapshots, stream/REST races,
  finalized bars, fixed time axis, gaps, partial history, extrema-preserving reduction,
  mixed fragmented events, 513-symbol socket batching, reconnect demand, cancellation,
  removed-symbol generations, preferences merge, quote/history staleness and browser failures.
- Real WPF controls exercise range/visibility, symbol routing, independent reused
  details, disabled-symbol closure, Float deactivation, sticky actions and graceful
  shutdown waiting for both history and price-alert work before tray disposal.
- Dark/light, 1h/24h, hidden, long/partial/disconnected, and details failure renders
  inspected in `artifacts/issue-6-previews`. Preview prices are controlled test data.
- Live public Binance check: two BTCUSDT kline tuples each had 12 fields and 60000 ms
  spacing; ticker snapshot contained high/low/base volume/quote volume; the same
  combined socket delivered both `24hrTicker` and `kline` events.
- Implementation decisions: retain 24h plus a boundary (approximately 1442 bars,
  hard cap 1500), rather than filling a target of 1500 older bars; new visualization
  STA checks share the existing lifecycle test because WPF permits only one
  Application per process.
- One independent whole-branch review confirmed a slot-release/pause race and a
  hidden-view recovery gap; both were reproduced RED and fixed GREEN. Added short
  and >24h hidden outages, manual partial-history retry, and queued asynchronous
  rate-limit regressions. Coverage/freshness tooltip feedback was promoted from
  Minor to Important because users need actual data age/range separately from
  quote timestamps; formatting and actual WPF tooltip bindings/hit area were
  verified RED→GREEN. Full suite after the fix pass: 139/139. No deferred findings
  or declined-to-judge behaviors.

Physical mouse dragging/focus on real mixed-DPI monitors, OS display of notifications,
and opening the real default browser remain manual acceptance checks. Tests exercise
native window events and the browser boundary without opening a user's browser or
reading/writing their real settings.

## Issue #3 — Price alerts

- Follow-up: each ticker row has a bell that opens an independent per-symbol alert
  window; general settings no longer contain alert inputs. Real WPF controls verify
  symbol routing, validation, Save/Cancel/reset, reused dialogs and concurrent symbol
  dialogs. Service tests verify targeted persistence, failed saves, removed symbols
  and preservation of new thresholds/trigger state when a stale general settings
  window saves. Omitting that merge fails the regression (95000 expected, 85000 actual).
  Dark/light and small-window renders inspected in `artifacts/independent-alert-previews`.
  Independent review found no outstanding issues. Release suite: 87/87 passed.

- Follow-up: tray menu text is vertically centered against each full padded row.
  Raster checks verify Chinese/English text placement in dark/light themes and
  highlighted menus; the original offset failed the test before the fix. Full suite
  remains 83/83, and the self-contained executable was republished.

- Branch: `codex/issue-3-price-alerts`, based on `8f0456b`.
- Release build: zero warnings/errors. Full Release suite: 87 passed, zero failed/skipped.
- Windows x64 self-contained publish: `artifacts/issue-3-publish/BinanceTicker.exe`.
- Tests cover inclusive upper/lower limits, independent one-shot flags, 100 concurrent
  quotes, restart persistence, per-side/all resets, disabled/unconfigured symbols,
  actual/target prices and offset timestamps in JSON, old settings, deep-copy editing,
  invalid/per-symbol drafts, live-state merge and failed settings replacement.
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

## Issue #8 desktop convenience — 2026-10-04

- Implemented on `codex/issue-8-desktop-convenience`, based on main `18fa1c0`.
- Release build succeeded with zero warnings/errors; final complete test run on main base: 162 passed,
  zero failed/skipped. Windows x64 self-contained publish succeeded at
  `artifacts/issue-8-publish/BinanceTicker.exe`.
- Core tests cover missing/invalid sizes, settings copy isolation, round-trip sizes and desktop
  options, malformed hotkey recovery, supported/reserved key combinations, negative coordinates,
  undersized work areas, minimum-size anchored resizing, and quoted startup command limits.
- Service tests cover cancelled/rejected hotkey changes retaining old registration, pending
  candidate cleanup, startup rollback after cancelled settings save, isolated HKCU test-value
  creation/removal, and single-instance exclusion/restart. Tests do not enable the user's startup item.
- Single STA WPF smoke checks eight resize handlers, real visual hit testing at all grip centers,
  drag exclusion through template descendants, scrollbar exclusion, size restore/save/reset,
  Fix/Float size retention, scrolling with 50 symbols and fixed buttons in dark/light themes.
- Native Win32 test registers a shortcut on the real HWND, checks conflicting registration on a
  second HWND, posts WM_HOTKEY while hidden, verifies toggle to visible, and verifies release.
  This exercises registration/message routing, not physical key presses in another foreground app.
- App settings-write failure test verifies startup/hotkey rollback and preserves live dimensions
  despite a settings draft with stale bounds. No user settings or live Binance data are used.
- Preview artifacts: `artifacts/issue-8`, including small dark/light ticker and settings layouts.
  Normal minimum height was increased from the initial 220 to 260 DIP to fit a complete quote row.
- Independent review found no actionable code defects, but requested additional mouse-hit testing.
  Added visual hit tests and a failing corner-alpha regression, fixed grips with nonzero alpha so
  layered-window corners receive native input even at minimum opacity. Follow-up review found
  no remaining actionable findings.
- Native API choices follow Microsoft documentation:
  [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey),
  [Thumb resizing](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/how-to-resize-a-canvas-by-using-a-thumb),
  [Run keys](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys),
  [layered-window hit testing](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features).

Manual acceptance remains for physical edge/corner dragging and scrollbar interaction, mixed
100%/150%/200% DPI monitors and disconnects, a real foreground-app shortcut press, and actual
Windows sign-out/sign-in with startup enabled/disabled and a moved executable. README lists
these explicitly. Startup repairs a moved path on the next manual launch; a missing old executable
cannot launch itself. Startup registration and JSON settings are not atomic across process termination.
