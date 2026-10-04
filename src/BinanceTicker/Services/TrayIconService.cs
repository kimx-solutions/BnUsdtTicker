using System.Windows;
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
    public TrayIconService(Dispatcher dispatcher, Action show, Action settings, Action<DisplayMode> setMode, Action exit, Action? showHistory = null)
    {
        void Dispatch(Action action) => dispatcher.BeginInvoke(action);
        menu = new()
        {
            Renderer = new TrayMenuRenderer(),
            BackColor = TrayMenuRenderer.Background,
            ForeColor = TrayMenuRenderer.Text,
            Padding = new Forms.Padding(6)
        };
        menu.Items.Add("顯示報價", null, (_, _) => Dispatch(show));
        menu.Items.Add("設定", null, (_, _) => Dispatch(settings));
        if (showHistory is not null) menu.Items.Add("提醒紀錄", null, (_, _) => Dispatch(showHistory));
        menu.Items.Add(new Forms.ToolStripSeparator());
        fix = (Forms.ToolStripMenuItem)menu.Items.Add("Fix Mode", null, (_, _) => Dispatch(() => setMode(DisplayMode.Fix)));
        floating = (Forms.ToolStripMenuItem)menu.Items.Add("Float Mode", null, (_, _) => Dispatch(() => setMode(DisplayMode.Float)));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("結束程式", null, (_, _) => Dispatch(exit));
        foreach (var item in menu.Items.OfType<Forms.ToolStripMenuItem>())
            item.Padding = new Forms.Padding(0, 6, 12, 6);
        icon = CreateIcon();
        tray = new() { Icon = icon, Text = "Binance USDT Ticker", ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) Dispatch(show); };
    }
    public void SetMode(DisplayMode mode) { fix.Checked = mode == DisplayMode.Fix; floating.Checked = mode == DisplayMode.Float; }
    public void RefreshTheme()
    {
        menu.BackColor = TrayMenuRenderer.Background;
        menu.ForeColor = TrayMenuRenderer.Text;
        menu.Invalidate(true);
    }
    public void ShowWarning(string message) => tray.ShowBalloonTip(6000, "Binance Ticker", message, Forms.ToolTipIcon.Warning);
    public void ShowNotification(string title, string message) => tray.ShowBalloonTip(6000, title, message, Forms.ToolTipIcon.Info);
    private static Drawing.Icon CreateIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/BinanceTicker;component/Assets/app.ico"));
        using var stream = resource.Stream;
        return new Drawing.Icon(stream, 32, 32);
    }
    public void Dispose() { tray.Visible = false; tray.Dispose(); menu.Dispose(); icon.Dispose(); }
}
