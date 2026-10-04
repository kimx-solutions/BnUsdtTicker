using Microsoft.Win32;

namespace BinanceTicker.Services;

public interface IStartupRegistration
{
    string? Read();
    void Restore(string? command);
    void Apply(bool enabled);
}

public sealed class WindowsStartupService : IStartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string valueName;
    public WindowsStartupService(string valueName = "BinanceTicker") => this.valueName = valueName;
    public static string CreateCommand(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.Contains('"') || path.Contains('\r') || path.Contains('\n'))
            throw new ArgumentException("登入啟動需要有效的程式完整路徑。");
        var command = "\"" + path + "\"";
        if (command.Length > 260) throw new ArgumentException("程式路徑過長，Windows 登入啟動命令最多支援 260 字元，請移至較短路徑。");
        return command;
    }
    public string? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(valueName) as string;
    }
    public void Restore(string? command)
    {
        if (Read() == command) return;
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (command is null) key.DeleteValue(valueName, false);
        else key.SetValue(valueName, command, RegistryValueKind.String);
    }
    public void Apply(bool enabled)
    {
        if (!enabled) { Restore(null); return; }
        var path = Environment.ProcessPath ?? throw new InvalidOperationException("無法取得目前程式路徑。");
        if (!File.Exists(path) || !string.Equals(Path.GetFileName(path), "BinanceTicker.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("請從已發佈的 BinanceTicker.exe 設定登入自動啟動。");
        Restore(CreateCommand(path));
    }
}
