# Issue #8 Desktop Convenience Implementation Plan

> 使用 superpowers:executing-plans 在此對話直接執行，依 TDD 完成每項。

**Goal:** 可調整且可還原的報價視窗、可設定全域快捷鍵及可選登入啟動。
**Architecture:** Core 管理設定、快捷鍵語法及幾何計算；WPF 管理縮放／工作區；獨立 Windows 服務管理快捷鍵、啟動項與单程序。
**Tech Stack:** .NET 10、WPF、Win32、HKCU Registry；不新增套件。
**Spec:** docs/superpowers/specs/2026-10-04-issue-8-desktop-convenience-design.md

## 任務

- [x] 先新增舊設定／尺寸／幾何／快捷鍵／啟動命令測試，執行確認失敗；實作 Core 設定、正規化與計算後執行通過。
- [x] 新增 Windows 服務測試，確認失敗；實作 RegisterHotKey 候選提交／取消、Run 註冊回復與 Mutex。
- [x] 擴充既有單一 STA WPF 測試，確認尺寸保存、縮放路由、固定控制項及重設；實作主窗 Thumb 與管理器。
- [x] 設定頁、App 啟動與設定提交接上服務，保留最新尺寸；驗證失敗回復及結束釋放。
- [x] 更新 README、設定範例與驗證紀錄；執行 Release build、完整測試、publish，檢查輸出畫面與 git diff。

## Review Focus

設定視窗開啟後尺寸變動；工作區小於最小尺寸；快捷鍵與設定檔儲存失敗；路徑包含空白及啟動註冊失敗；Float 縮放／捲動與關閉釋放。
