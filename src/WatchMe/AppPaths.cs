using System.IO;

namespace WatchMe;

/// <summary>Centralized on-disk locations for WatchMe. Everything lives under %APPDATA%\WatchMe.</summary>
public static class AppPaths
{
    public const string AppName = "WatchMe";
    public const string BundleId = "dev.threetwoa.WatchMe";

    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

    public static string NotesFile => Path.Combine(DataDir, "notes.json");
    public static string SettingsFile => Path.Combine(DataDir, "settings.json");
}
