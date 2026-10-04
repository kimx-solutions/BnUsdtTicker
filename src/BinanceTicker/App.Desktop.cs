using System.Windows;
using BinanceTicker.Services;

namespace BinanceTicker;

public partial class App
{
    private SingleInstanceService? instance;
    private GlobalHotkeyService? hotkeys;
    private DesktopPreferencesService? desktopPreferences;
    private readonly WindowsStartupService windowsStartup = new();

    private void InitializeDesktopPreferences(Window window)
    {
        hotkeys = new(new WindowsHotkeyPlatform(window), () => { if (!exiting) manager?.Toggle(); });
        desktopPreferences = new(hotkeys, windowsStartup);
        try
        {
            using var change = hotkeys.Prepare(settings.Hotkey);
            change.Commit();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { tray?.ShowWarning(ex.Message); }
        try
        {
            // Repair a moved executable when the user next launches it manually.
            if (settings.StartWithWindows || windowsStartup.Read() is not null)
                windowsStartup.Apply(settings.StartWithWindows);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Security.SecurityException)
        { tray?.ShowWarning("無法更新登入啟動：" + ex.Message); }
    }
}
