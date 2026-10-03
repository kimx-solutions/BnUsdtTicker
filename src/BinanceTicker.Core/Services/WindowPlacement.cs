namespace BinanceTicker.Core.Services;

public readonly record struct ScreenRect(double X, double Y, double Width, double Height);

public static class WindowPlacement
{
    public static bool IsVisible(ScreenRect window, IEnumerable<ScreenRect> workAreas) => workAreas.Any(screen =>
        window.X >= screen.X && window.Y >= screen.Y &&
        window.X + window.Width <= screen.X + screen.Width &&
        window.Y + window.Height <= screen.Y + screen.Height);
}
