using System.Diagnostics;
namespace BinanceTicker.Services;
public interface IBrowserLauncher { void Open(Uri uri); }
public sealed class BrowserLauncher : IBrowserLauncher
{
    public void Open(Uri uri) => Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute=true });
}
