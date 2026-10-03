using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;
using BinanceTicker.Core.Models;

namespace BinanceTicker.Services;

public static class ThemeService
{
    public static void Apply(ColorTheme theme)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var palette = new ResourceDictionary
        {
            Source = new Uri($"/BinanceTicker;component/Themes/{theme}.xaml", UriKind.Relative)
        };
        var previous = dictionaries.FirstOrDefault(d => d.Source?.OriginalString.Contains("Themes/", StringComparison.Ordinal) == true);
        if (previous is null) dictionaries.Add(palette);
        else dictionaries[dictionaries.IndexOf(previous)] = palette;
    }

    public static void RefreshWindowFrame(Window window)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var darkMode = ((SolidColorBrush)window.FindResource("SettingsBackground")).Color.R < 128 ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, 20, ref darkMode, sizeof(int));
        SetColor(34, "SettingsBorder"); SetColor(35, "SettingsBackground"); SetColor(36, "SettingsText");
        void SetColor(int attribute, string resourceKey)
        {
            var color = ((SolidColorBrush)window.FindResource(resourceKey)).Color;
            var colorRef = color.R | (color.G << 8) | (color.B << 16);
            _ = DwmSetWindowAttribute(handle, attribute, ref colorRef, sizeof(int));
        }
    }
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}
