using WatchMe.Core;

namespace WatchMe.Settings;

public sealed class SettingsStore
{
    private readonly string _filePath;

    public SettingsStore(string filePath) => _filePath = filePath;

    public static SettingsStore Default() => new(AppPaths.SettingsFile);

    public AppSettings Load()
    {
        return JsonFile.TryLoad<AppSettings>(_filePath) ?? new AppSettings();
    }

    public void Save(AppSettings settings) => JsonFile.Save(_filePath, settings);
}
