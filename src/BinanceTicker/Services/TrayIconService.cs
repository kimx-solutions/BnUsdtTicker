using System.Runtime.InteropServices;
using System.Windows.Threading;
using BinanceTicker.Core.Models;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace BinanceTicker.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon tray;
    private readonly Forms.ContextMenuStrip menu;
    private readonly Drawing.Icon icon;
    private readonly Forms.ToolStripMenuItem fix;
    private readonly Forms.ToolStripMenuItem floating;
    public TrayIconService(Dispatcher dispatcher, Action show, Action settings, Action<DisplayMode> setMode, Action exit)
    {
        void Dispatch(Action action) => dispatcher.BeginInvoke(action);
        menu = new();
        menu.Items.Add("顯示報價", null, (_, _) => Dispatch(show));
        menu.Items.Add("設定", null, (_, _) => Dispatch(settings));
        menu.Items.Add(new Forms.ToolStripSeparator());
        fix = (Forms.ToolStripMenuItem)menu.Items.Add("Fix Mode", null, (_, _) => Dispatch(() => setMode(DisplayMode.Fix)));
        floating = (Forms.ToolStripMenuItem)menu.Items.Add("Float Mode", null, (_, _) => Dispatch(() => setMode(DisplayMode.Float)));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("結束程式", null, (_, _) => Dispatch(exit));
        icon = CreateIcon();
        tray = new() { Icon = icon, Text = "Binance USDT Ticker", ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) Dispatch(show); };
    }
    public void SetMode(DisplayMode mode) { fix.Checked = mode == DisplayMode.Fix; floating.Checked = mode == DisplayMode.Float; }
    public void ShowWarning(string message) => tray.ShowBalloonTip(6000, "Binance Ticker", message, Forms.ToolTipIcon.Warning);
    private static Drawing.Icon CreateIcon()
    {
        using var bitmap = new Drawing.Bitmap(32, 32);
        using var graphics = Drawing.Graphics.FromImage(bitmap);
        graphics.Clear(Drawing.Color.FromArgb(23, 27, 34));
        using var brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(240, 185, 11));
        using var font = new Drawing.Font("Segoe UI", 21, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
        graphics.DrawString("B", font, brush, 5, 2);
        var handle = bitmap.GetHicon();
        try { using var borrowed = Drawing.Icon.FromHandle(handle); return (Drawing.Icon)borrowed.Clone(); }
        finally { DestroyIcon(handle); }
    }
    public void Dispose() { tray.Visible = false; tray.Dispose(); menu.Dispose(); icon.Dispose(); }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(IntPtr icon);
}
