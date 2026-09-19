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
    public static string ShelfFile => Path.Combine(DataDir, "shelf.json");
    public static string SettingsFile => Path.Combine(DataDir, "settings.json");
    public static string ClipboardDir => Path.Combine(DataDir, "clipboard");
    public static string ClipboardEntriesFile => Path.Combine(ClipboardDir, "entries.json");
    public static string ClipboardImagesDir => Path.Combine(ClipboardDir, "images");
    public static string NoteImagesDir => Path.Combine(DataDir, "Images");
    public static string KeepAwakeStateDir => Path.Combine(DataDir, "state");
}
