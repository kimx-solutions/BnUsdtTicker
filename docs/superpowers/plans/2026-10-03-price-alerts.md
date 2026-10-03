# Price Alerts Implementation Plan

**Goal:** Implement GitHub issue #3: independent one-shot upper/lower alerts, persistent state, manual reset, Windows notifications and JSON history.

**Architecture:** Keep alert evaluation in Core with an injectable notification boundary. Serialize checks, resets and settings replacement with one service gate; use atomic JSON replacement. Run application mutations on the WPF dispatcher, including existing position/theme saves. Use the existing NotifyIcon Windows notification API without extra dependencies.

**Tech Stack:** .NET 10, WPF, System.Text.Json, xUnit.

**Spec:** https://github.com/kimx-solutions/BnUsdtTicker/issues/3

## Constraints and decisions

- Inclusive comparisons: price >= upper, price <= lower. Each condition stays triggered until explicitly reset, including after threshold edits.
- Empty thresholds disable that condition; configured values must be positive decimals. Do not require lower < upper: conditions are independent.
- Evaluate initial REST quotes as well as WebSocket quotes. Ignore disabled/unknown symbols and invalid prices.
- Stage resets in the settings editor and apply on Save; Cancel discards all edits.
- Preserve live triggered states when applying an older settings draft, except for explicitly requested resets.
- Persist state and history before submitting notifications. Roll back when notification submission fails; do not notify if persistence fails. Windows controls visibility of submitted notifications.
- Separate files cannot provide an atomic transaction with the Windows shell. Sudden process termination during submission is outside the normal-operation delivery guarantee; do not claim proof of on-screen delivery.

## Task 1: Persistence and evaluation

Files: Models/PriceAlertSettings.cs, Models/AlertHistoryEntry.cs, Models/SymbolSetting.cs; Services/PriceAlertService.cs, Services/AlertHistoryService.cs, Services/AtomicJsonFile.cs, Services/SettingsService.cs; tests/PriceAlertTests.cs.

- [x] Write and run a failing settings round-trip test using the issue's JSON fixture.
- [x] Add deep-copied alert settings with backward-compatible defaults and validation.
- [x] Write failing tests for inclusive upper/lower triggers, independent state, repeated/concurrent checks, restart, per-side/all reset, disabled symbols and failed persistence/notification.
- [x] Implement INotificationService.Show(AlertHistoryEntry), IPriceAlertService.CheckAsync/ResetAsync, durable history and service gating.
- [x] Verify filtered tests and the whole suite.

## Task 2: Settings editor and ticker status

Files: ViewModels/PriceAlertEditorViewModel.cs, SettingsViewModel.cs, TickerViewModel.cs; Views/SettingsWindow.xaml, SettingsWindow.xaml.cs, TickerWindow.xaml; tests/PriceAlertEditorTests.cs, WindowTests.cs.

- [x] Write failing tests for threshold validation, per-side/all resets, cancellation, all-symbol drafts, live state merge and ticker alert state.
- [x] Add a selected-symbol editor with threshold inputs, status text and reset commands. Validate every draft before Save.
- [x] Add a small alert indicator to affected ticker rows.
- [x] Exercise real WPF input/reset bindings, dark/light themes, scrolling and narrow settings layout.

## Task 3: Runtime integration and verification

Files: App.xaml.cs, Services/NotificationService.cs, TrayIconService.cs, README.md, settings.example.json, docs/verification.md.

- [x] Route accepted quotes to the service and refresh live editor/ticker states; await pending checks before exit.
- [x] Apply drafts through the service gate and preserve reset intent and latest triggered states.
- [x] Add Windows notification submission with symbol, side, actual price and target.
- [x] Document editing/reset behavior, files and notification submission limitations.
- [x] Run Release build, whole test suite and self-contained Windows publish; review diff and rendered settings screenshots.

## Review focus

Old JSON without alert fields; settings open while alerts fire; failure before notification submission; repeated quotes from reconnects; shutdown while a check is pending. Each gets behavioral coverage in the owning task.

## Execution record

- Baseline: 49/49 Release tests pass. Working tree clean on codex/issue-1-desktop-ticker.
- User explicitly requested implementation of the linked, fully specified issue. Execute in the current checkout on a dedicated issue branch and keep changes local for review.
- Task 1 complete: settings round-trip RED/GREEN; independent triggers, high-frequency updates, restart/reset and persistence/submission failure coverage GREEN.
- Task 2 complete: settings draft preservation, parsing/reset and WPF control presence observed RED/GREEN; editing, live state refresh, visible validation, ticker indicator and both themes verified.
- Task 3 complete: App quote handler and shutdown drain covered, notification prices retain decimal precision, documentation updated and Windows x64 publish verified.
- Final review: independent reviewer found incomplete rollback (P2) and pending-shutdown test gap (P3). Both fixed and tested. Failed rollback is recovered before any further operation; notification resources remain alive until queued price work completes.
- Ruling: service guarantees concurrency within one application instance. Cross-process ownership would require a separate single-instance lifecycle change; do not introduce that unrelated change here.
- Final verification: Release build 0 warnings/errors; full suite 83/83; self-contained publish succeeds. Windows banner visibility and sudden power loss remain documented manual/architectural limits.
