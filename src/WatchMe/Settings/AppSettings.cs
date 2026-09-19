namespace WatchMe.Settings;

public enum TriggerMode
{
    Hover,
    Click,
}

public enum ThemeMode
{
    FollowSystem,
    Light,
    Dark,
}

public sealed class AppSettings
{
    public TriggerMode Trigger { get; set; } = TriggerMode.Hover;

    public ThemeMode Theme { get; set; } = ThemeMode.Dark;

    public bool ClipboardHistoryEnabled { get; set; } = true;

    public int ClipboardHistoryMaxEntries { get; set; } = 200;

    public bool StartWithSystem { get; set; }

    /// <summary>Global hotkey in display form, e.g. "Ctrl+Alt+W". Parsed by <see cref="Core.HotkeyPattern"/>.</summary>
    public string HotkeyDisplay { get; set; } = "Ctrl+Alt+W";

    /// <summary>Device name of the preferred screen; null/empty means the primary screen.</summary>
    public string? PreferredScreenDeviceName { get; set; }
}
