using System.Windows;
using BinanceTicker.Core.Models;

namespace BinanceTicker.Services;

public static class ThemeService
{
    public static void Apply(ColorTheme theme)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var palette = new ResourceDictionary
        {
            Source = new Uri($"/BinanceTicker;component/Themes/{theme}.xaml", UriKind.Relative)
        };
        var previous = dictionaries.FirstOrDefault(d => d.Source?.OriginalString.Contains("Themes/", StringComparison.Ordinal) == true);
        if (previous is null) dictionaries.Add(palette);
        else dictionaries[dictionaries.IndexOf(previous)] = palette;
    }
}
