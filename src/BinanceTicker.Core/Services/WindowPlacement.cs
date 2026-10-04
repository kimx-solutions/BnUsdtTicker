namespace BinanceTicker.Core.Services;

public readonly record struct ScreenRect(double X, double Y, double Width, double Height);

public static class WindowPlacement
{
    public static ScreenRect Constrain(ScreenRect window, ScreenRect area)
    {
        var width = Math.Min(window.Width, area.Width);
        var height = Math.Min(window.Height, area.Height);
        return new(Math.Clamp(window.X, area.X, area.X + area.Width - width),
            Math.Clamp(window.Y, area.Y, area.Y + area.Height - height), width, height);
    }

    public static ScreenRect Resize(ScreenRect window, string edge, double dx, double dy,
        double minWidth, double minHeight, double maxWidth, double maxHeight)
    {
        var width = edge.Contains('W') ? window.Width - dx : edge.Contains('E') ? window.Width + dx : window.Width;
        var height = edge.Contains('N') ? window.Height - dy : edge.Contains('S') ? window.Height + dy : window.Height;
        width = Math.Clamp(width, minWidth, maxWidth);
        height = Math.Clamp(height, minHeight, maxHeight);
        return new(window.X + (edge.Contains('W') ? window.Width - width : 0),
            window.Y + (edge.Contains('N') ? window.Height - height : 0), width, height);
    }

    public static bool IsVisible(ScreenRect window, IEnumerable<ScreenRect> workAreas) => workAreas.Any(screen =>
        window.X >= screen.X && window.Y >= screen.Y &&
        window.X + window.Width <= screen.X + screen.Width &&
        window.Y + window.Height <= screen.Y + screen.Height);
}
