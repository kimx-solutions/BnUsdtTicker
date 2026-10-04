namespace BinanceTicker.Core.Services;

public readonly record struct HotkeyGesture(uint Modifiers, uint Key)
{
    private static readonly Dictionary<string, uint> Navigation = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = 32, ["PageUp"] = 33, ["PageDown"] = 34, ["End"] = 35,
        ["Home"] = 36, ["Left"] = 37, ["Up"] = 38, ["Right"] = 39, ["Down"] = 40, ["Insert"] = 45, ["Delete"] = 46
    };

    public static HotkeyGesture Parse(string text)
    {
        var parts = (text ?? "").Split('+', StringSplitOptions.TrimEntries);
        uint modifiers = 0;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            uint flag = parts[i].ToUpperInvariant() switch { "ALT" => 1, "CTRL" => 2, "SHIFT" => 4, "WIN" => 8, _ => 0 };
            if (flag == 0 || (modifiers & flag) != 0) throw Invalid();
            modifiers |= flag;
        }
        var name = parts[^1].ToUpperInvariant();
        uint key = name.Length == 1 && (name[0] is >= 'A' and <= 'Z' or >= '0' and <= '9') ? name[0] :
            name.StartsWith('F') && int.TryParse(name.AsSpan(1), out var number) && number is >= 1 and <= 24 ? (uint)(111 + number) :
            Navigation.GetValueOrDefault(name);
        // Windows reserves F12 for the debugger regardless of modifiers.
        if (modifiers == 0 || key == 0 || key == 123) throw Invalid();
        return new(modifiers, key);
    }

    public override string ToString()
    {
        var names = new List<string>();
        if ((Modifiers & 2) != 0) names.Add("Ctrl");
        if ((Modifiers & 1) != 0) names.Add("Alt");
        if ((Modifiers & 4) != 0) names.Add("Shift");
        if ((Modifiers & 8) != 0) names.Add("Win");
        var key = Key;
        names.Add(key is >= 112 and <= 135 ? "F" + (key - 111) : Navigation.FirstOrDefault(p => p.Value == key).Key ?? ((char)key).ToString());
        return string.Join('+', names);
    }
    private static ArgumentException Invalid() => new("快捷鍵須包含 Ctrl、Alt、Shift 或 Win 與一個字母、數字、功能鍵或導航鍵；F12 為系統保留。例：Ctrl+Alt+T。");
}
