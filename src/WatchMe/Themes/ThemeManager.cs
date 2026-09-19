using Microsoft.Win32;
using WatchMe.Settings;

namespace WatchMe.Themes;

/// <summary>Swaps the merged theme dictionary. FollowSystem reads the Windows apps-light/dark registry value.</summary>
public static class ThemeManager
{
    private const string ThemeDictionaryUri = "pack://application:,,,/Themes/{0}.xaml";

    public static void Apply(ThemeMode mode)
    {
        var effective = mode switch
        {
            ThemeMode.Light => "Light",
            ThemeMode.Dark => "Dark",
            _ => SystemUsesLightTheme() ? "Light" : "Dark",
        };

        var uri = new Uri(string.Format(System.Globalization.CultureInfo.InvariantCulture, ThemeDictionaryUri, effective));
        var dictionary = new System.Windows.ResourceDictionary { Source = uri };
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var stale = dictionaries.Where(d => d.Source?.AbsolutePath.Contains("/Themes/") == true).ToList();
        foreach (var old in stale)
            dictionaries.Remove(old);
        dictionaries.Add(dictionary);
    }

    public static bool SystemUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch
        {
            return true;
        }
    }
}
