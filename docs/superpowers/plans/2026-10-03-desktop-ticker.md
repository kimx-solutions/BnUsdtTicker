# Desktop Ticker Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement issue #1's complete V1 Windows desktop ticker.
**Architecture:** A .NET 10 Core library owns testable market/settings logic.
WPF binds its view models and centralizes window/tray lifetime in services.
**Tech Stack:** C#, WPF, System.Text.Json, HttpClient, ClientWebSocket, NotifyIcon, xUnit.
**Spec:** ../specs/2026-10-03-desktop-ticker-design.md

## Global Constraints

- Windows, .NET 10, WPF, MVVM; no API keys, trading, database or history.
- JSON location: `%LOCALAPPDATA%\BinanceTicker\settings.json`.
- Retry delays: 1, 2, 5, 10, 30 seconds, capped at 30; reset after success.
- X hides the ticker; Tray Exit cancels networking, disposes the icon, then shuts down.

## Review Focus

- Malformed/null settings: recover without losing a corrupt-file backup.
- Offline startup/REST errors: the ticker and settings remain usable.
- All symbols disabled: empty state with no socket connection.
- Changing symbols during reconnect: cancel old work and reject stale updates.
- Removed/negative-coordinate/mixed-DPI monitors: recover to a visible work area.

### Task 1: Core data and persistence

Files: `src/BinanceTicker.Core/Models/*`, `Services/SettingsService.cs`,
`Services/SymbolNormalizer.cs`, `Services/PriceFormatter.cs`, tests in
`tests/BinanceTicker.Tests/CoreTests.cs`.
Interfaces: `SettingsService.Load()/Save(AppSettings)`,
`SymbolNormalizer.Normalize(string)`, `PriceFormatter.Format(decimal)`.

- [x] Write failing tests for normalization, precision boundaries and JSON recovery/roundtrip.
- [x] Implement models, observable base, atomic storage and normalization.
- [x] Run `dotnet test`; verify the tests pass.

### Task 2: Market transport

Files: `Services/BinanceService.cs`, `Services/BinanceWebSocketService.cs`,
`Services/MarketFeed.cs`, transport tests.
Interfaces: `IBinanceService.IsValidSymbolAsync()/GetPricesAsync()`,
`BinanceWebSocketService.RunAsync(symbols, onPrice, onStatus, cancellationToken)`.

- [x] Write failing tests for REST payloads, invalid pairs, fragmented streaming,
  disconnect/retry/reset and prompt cancellation.
- [x] Implement REST and cancellable reconnecting socket transport.
- [x] Run `dotnet test`; verify the tests pass without public network access.

### Task 3: MVVM and settings management

Files: `ViewModels/TickerViewModel.cs`, `SettingsViewModel.cs`, commands;
tests for add/remove/reorder/toggle, duplicates and edit cancellation.
Interfaces: ticker `Configure/Update/SetStatus`; settings `CreateSettings()`
and observable command properties.

- [x] Write failing tests for enabled ordering, price update notification and settings actions.
- [x] Implement view models and commands; retain old prices while disconnected.
- [x] Run the full test suite.

### Task 4: Windows application and delivery

Files: `src/BinanceTicker/App.xaml*`, `Views/*`, `Services/*`, `README.md`,
`.github/workflows/build.yml`, Windows lifecycle tests.
Interfaces: `TickerWindowManager.Show/Hide/Toggle/SetMode/SavePosition`,
`TrayIconService` callbacks; app coordinates settings and feed replacement.

- [x] Write lifecycle/position recovery checks, then implement WPF bindings, tray,
  monitor recovery and shutdown ownership.
- [x] Build Release; run suite and WPF smoke checks.
- [x] Document run/publish/settings and manual physical-monitor checks.
- [x] Review the final implementation against issue #1 and report verified limits.

## Execution decisions

- User supplied a detailed implementation issue and requested implementation;
  execute inline without another approval handoff.
- Work in the clean provided checkout on `codex/issue-1-desktop-ticker` so the
  implementation remains available in the user's workspace.
- No remote push, issue comments or merge is part of this request.
