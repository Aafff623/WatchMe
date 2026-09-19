using WatchMe.Core;

namespace WatchMe.Settings;

public sealed class SettingsStore
{
    private readonly string _filePath;

    public SettingsStore(string filePath) => _filePath = filePath;

    public static SettingsStore Default() => new(AppPaths.SettingsFile);

    public AppSettings Load()
    {
        var loaded = JsonFile.TryLoad<AppSettings>(_filePath);
        if (loaded is null)
            return new AppSettings();

        // Guard against a corrupted cap coming from an older release.
        if (loaded.ClipboardHistoryMaxEntries is < 10 or > 1000)
            loaded.ClipboardHistoryMaxEntries = 200;
        return loaded;
    }

    public void Save(AppSettings settings) => JsonFile.Save(_filePath, settings);
}
