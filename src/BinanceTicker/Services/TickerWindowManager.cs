using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace BinanceTicker.Services;

public sealed class TickerWindowManager : IDisposable
{
    private readonly Window window;
    private readonly Action save;
    private readonly DispatcherTimer boundsTimer;
    private AppSettings settings;
    private HwndSource? source;
    private bool exiting, placing, disposed;
    public TickerWindowManager(Window window, AppSettings settings, Action save)
    {
        this.window = window; this.settings = settings; this.save = save;
        window.Left = settings.Window.Left; window.Top = settings.Window.Top;
        window.SizeToContent = SizeToContent.Manual;
        window.Width = settings.Window.Width; window.Height = settings.Window.Height;
        Apply(settings);
        boundsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        boundsTimer.Tick += OnBoundsTimer;
        window.Closing += OnClosing; window.Deactivated += OnDeactivated;
        window.PreviewMouseLeftButtonDown += OnDrag;
        window.SizeChanged += OnSizeChanged; window.LocationChanged += OnLocationChanged;
        window.SourceInitialized += OnSourceInitialized;
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        AttachHook();
    }
    public void Apply(AppSettings value) { settings = value; window.Opacity = settings.Window.Opacity; SetMode(settings.Mode); }
    public void Show() { window.Show(); EnsureVisible(); window.Activate(); }
    public void Hide() { SavePosition(); window.Hide(); }
    public void Toggle() { if (window.IsVisible) Hide(); else Show(); }
    public void SetMode(DisplayMode mode) { settings.Mode = mode; window.Topmost = mode == DisplayMode.Fix; window.ShowInTaskbar = true; }
    public void SavePosition()
    {
        boundsTimer.Stop();
        window.UpdateLayout();
        settings.Window.Left = window.Left; settings.Window.Top = window.Top;
        settings.Window.Width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        settings.Window.Height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
        save();
    }
    public void ResetSize()
    {
        window.Width = WindowSettings.DefaultWidth; window.Height = WindowSettings.DefaultHeight;
        EnsureVisible(); SavePosition();
    }
    public void CloseForExit() { exiting = true; boundsTimer.Stop(); window.Close(); }
    private void OnClosing(object? sender, CancelEventArgs e) { if (exiting) return; e.Cancel = true; Hide(); }
    private void OnDeactivated(object? sender, EventArgs e) { if (settings.Mode == DisplayMode.Float) Hide(); }
    public static bool IsDragSource(DependencyObject target)
    {
        for (DependencyObject? current = target; current is not null; current = Parent(current))
            if (current is ButtonBase or Thumb or ScrollBar or TextBoxBase or ComboBox) return false;
        return true;
    }
    private static DependencyObject? Parent(DependencyObject target) => target is Visual or Visual3D
        ? VisualTreeHelper.GetParent(target) : (target as FrameworkContentElement)?.Parent;
    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject target || !IsDragSource(target) || e.LeftButton != MouseButtonState.Pressed) return;
        window.DragMove(); EnsureVisible(); SavePosition(); e.Handled = true;
    }
    private void ScheduleBoundsSave()
    {
        if (placing || exiting || disposed) return;
        boundsTimer.Stop(); boundsTimer.Start();
    }
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => ScheduleBoundsSave();
    private void OnLocationChanged(object? sender, EventArgs e) => ScheduleBoundsSave();
    private void OnBoundsTimer(object? sender, EventArgs e) { boundsTimer.Stop(); EnsureVisible(); SavePosition(); }
    private void OnSourceInitialized(object? sender, EventArgs e) => AttachHook();
    private void AttachHook()
    {
        if (source is not null) return;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        source = HwndSource.FromHwnd(handle);
        source?.AddHook(OnMessage);
    }
    private IntPtr OnMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // WPF applies the suggested DPI rectangle before this queued correction.
        if (message is 0x02E0 or 0x001A) QueuePlacement();
        return IntPtr.Zero;
    }
    private void QueuePlacement() => window.Dispatcher.BeginInvoke(() => { if (!exiting && !disposed) { EnsureVisible(); SavePosition(); } }, DispatcherPriority.Loaded);
    private void OnDisplayChanged(object? sender, EventArgs e) => QueuePlacement();
    public void EnsureVisible()
    {
        if (placing || disposed) return;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        placing = true;
        try
        {
            var area = Forms.Screen.FromHandle(handle).WorkingArea;
            var scale = Math.Max(1, GetDpiForWindow(handle)) / 96d;
            // Work areas and HWND rectangles are physical pixels under PerMonitorV2.
            window.MinWidth = Math.Min(WindowSettings.MinimumWidth, area.Width / scale);
            window.MinHeight = Math.Min(WindowSettings.MinimumHeight, area.Height / scale);
            window.MaxWidth = area.Width / scale; window.MaxHeight = area.Height / scale;
            window.Width = Math.Clamp(window.Width, window.MinWidth, window.MaxWidth);
            window.Height = Math.Clamp(window.Height, window.MinHeight, window.MaxHeight);
            window.UpdateLayout();
            if (!GetWindowRect(handle, out var rect)) return;
            var bounds = new ScreenRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            var corrected = WindowPlacement.Constrain(bounds, new(area.X, area.Y, area.Width, area.Height));
            if (bounds != corrected)
                SetWindowPos(handle, IntPtr.Zero, (int)corrected.X, (int)corrected.Y, (int)corrected.Width, (int)corrected.Height, 0x0004 | 0x0010);
        }
        finally { placing = false; }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; boundsTimer.Stop(); boundsTimer.Tick -= OnBoundsTimer;
        SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        window.Closing -= OnClosing; window.Deactivated -= OnDeactivated;
        window.PreviewMouseLeftButtonDown -= OnDrag; window.SizeChanged -= OnSizeChanged;
        window.LocationChanged -= OnLocationChanged; window.SourceInitialized -= OnSourceInitialized;
        source?.RemoveHook(OnMessage);
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
}
