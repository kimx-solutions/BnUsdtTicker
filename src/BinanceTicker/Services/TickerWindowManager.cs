using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace BinanceTicker.Services;

public sealed class TickerWindowManager : IDisposable
{
    private readonly Window window;
    private readonly Action save;
    private AppSettings settings;
    private bool exiting;
    private bool placing;
    public TickerWindowManager(Window window, AppSettings settings, Action save)
    {
        this.window = window; this.settings = settings; this.save = save;
        window.Left = settings.Window.Left; window.Top = settings.Window.Top;
        Apply(settings);
        window.Closing += OnClosing; window.Deactivated += OnDeactivated;
        window.PreviewMouseLeftButtonDown += OnDrag; window.SizeChanged += OnSizeChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
    }
    public void Apply(AppSettings value) { settings = value; window.Opacity = settings.Window.Opacity; SetMode(settings.Mode); }
    public void Show() { window.Show(); EnsureVisible(); window.Activate(); }
    public void Hide() => window.Hide();
    public void Toggle() { if (window.IsVisible) Hide(); else Show(); }
    public void SetMode(DisplayMode mode) { settings.Mode = mode; window.Topmost = mode == DisplayMode.Fix; window.ShowInTaskbar = true; }
    public void SavePosition() { settings.Window.Left = window.Left; settings.Window.Top = window.Top; save(); }
    public void CloseForExit() { exiting = true; window.Close(); }
    private void OnClosing(object? sender, CancelEventArgs e) { if (exiting) return; e.Cancel = true; SavePosition(); Hide(); }
    private void OnDeactivated(object? sender, EventArgs e) { if (settings.Mode == DisplayMode.Float) Hide(); }
    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject target) return;
        for (var current = target; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is ButtonBase or ScrollBar) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;
        window.DragMove(); SavePosition(); e.Handled = true;
    }
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) { if (window.IsVisible) EnsureVisible(); }
    private void OnDisplayChanged(object? sender, EventArgs e) => window.Dispatcher.BeginInvoke(() => { if (!exiting) EnsureVisible(); });
    public void EnsureVisible()
    {
        if (placing) return;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        placing = true;
        try
        {
            var currentArea = Forms.Screen.FromHandle(handle).WorkingArea;
            var scale = Math.Max(1, GetDpiForWindow(handle)) / 96d;
            window.MaxHeight = Math.Min(640, currentArea.Height / scale);
            window.MaxWidth = currentArea.Width / scale;
            window.UpdateLayout();
            if (!GetWindowRect(handle, out var rect)) return;
            var areas = Forms.Screen.AllScreens.Select(s => s.WorkingArea).Select(r => new ScreenRect(r.X, r.Y, r.Width, r.Height));
            if (WindowPlacement.IsVisible(new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top), areas)) return;
            var primary = (Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0]).WorkingArea;
            var x = primary.Left + Math.Max(0, (primary.Width - (rect.Right - rect.Left)) / 2);
            var y = primary.Top + Math.Max(0, Math.Min(60, primary.Height - (rect.Bottom - rect.Top)));
            // Both work area and window rectangle use physical pixels under PerMonitorV2.
            SetWindowPos(handle, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010);
            SavePosition();
        }
        finally { placing = false; }
    }
    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        window.Closing -= OnClosing; window.Deactivated -= OnDeactivated;
        window.PreviewMouseLeftButtonDown -= OnDrag; window.SizeChanged -= OnSizeChanged;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
}
