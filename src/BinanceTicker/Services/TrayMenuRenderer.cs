using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace BinanceTicker.Services;

internal sealed class TrayMenuRenderer : Forms.ToolStripProfessionalRenderer
{
    internal static Drawing.Color Background => Color("SettingsBackground");
    internal static Drawing.Color Text => Color("SettingsText");
    private static Drawing.Color Accent => Color("SettingsAccent");
    private static Drawing.Color Border => Color("SettingsBorder");
    private static Drawing.Color Hover => Color("TickerHover");

    private static Drawing.Color Color(string key)
    {
        var brush = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.FindResource(key);
        return Drawing.Color.FromArgb(brush.Color.A, brush.Color.R, brush.Color.G, brush.Color.B);
    }

    public TrayMenuRenderer() : base(new MenuColors()) => RoundedEdges = false;

    protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
    {
        var highlighted = e.Item.Selected || e.Item.Pressed;
        using var background = new Drawing.SolidBrush(highlighted ? Hover : Background);
        e.Graphics.FillRectangle(background, new Drawing.Rectangle(Drawing.Point.Empty, e.Item.Size));
        if (!highlighted) return;
        using var accent = new Drawing.SolidBrush(Accent);
        e.Graphics.FillRectangle(accent, 0, 6, 2, Math.Max(1, e.Item.Height - 12));
    }

    protected override void OnRenderImageMargin(Forms.ToolStripRenderEventArgs e)
    {
        using var background = new Drawing.SolidBrush(Background);
        e.Graphics.FillRectangle(background, e.AffectedBounds);
    }

    protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Text : Color("SettingsMuted");
        // Menu layout retains the unpadded text bounds when vertical padding enlarges a row.
        // Keep horizontal placement, but center against the complete item height.
        e.TextRectangle = new Drawing.Rectangle(e.TextRectangle.X, 0, e.TextRectangle.Width, e.Item.Height);
        e.TextFormat = (e.TextFormat & ~Forms.TextFormatFlags.Bottom) | Forms.TextFormatFlags.VerticalCenter;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderItemCheck(Forms.ToolStripItemImageRenderEventArgs e)
    {
        var state = e.Graphics.Save();
        try
        {
            e.Graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Drawing.Pen(Accent, Math.Max(1.5f, e.ImageRectangle.Width / 8f))
            {
                StartCap = Drawing.Drawing2D.LineCap.Round,
                EndCap = Drawing.Drawing2D.LineCap.Round,
                LineJoin = Drawing.Drawing2D.LineJoin.Round
            };
            var rect = e.ImageRectangle;
            e.Graphics.DrawLines(pen,
            [
                new Drawing.PointF(rect.Left + rect.Width * 0.2f, rect.Top + rect.Height * 0.5f),
                new Drawing.PointF(rect.Left + rect.Width * 0.4f, rect.Top + rect.Height * 0.7f),
                new Drawing.PointF(rect.Left + rect.Width * 0.8f, rect.Top + rect.Height * 0.25f)
            ]);
        }
        finally { e.Graphics.Restore(state); }
    }

    protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Drawing.Pen(Border);
        e.Graphics.DrawLine(pen, 8, e.Item.Height / 2, e.Item.Width - 8, e.Item.Height / 2);
    }

    private sealed class MenuColors : Forms.ProfessionalColorTable
    {
        public MenuColors() => UseSystemColors = false;
        public override Drawing.Color ToolStripDropDownBackground => Background;
        public override Drawing.Color ToolStripBorder => Border;
        public override Drawing.Color MenuBorder => Border;
    }
}
