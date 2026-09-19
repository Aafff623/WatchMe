using System.Windows;
using WatchMe.Settings;
using WatchMe.Shell;
using WatchMe.Themes;

namespace WatchMe.Notch;

/// <summary>
/// Orchestrates the notch experience: capsule hot zone, drop-down sticky-note panel,
/// tray, global hotkey, plus applying settings changes at runtime.
/// </summary>
public sealed class NotchController : IDisposable
{
    private readonly NotchCapsuleWindow _capsule = new();
    private readonly NotchPanelWindow _panel = new();
    private readonly TrayIconService _tray = new();
    private readonly GlobalHotkeyService _hotkey = new();
    private AppSettings _settings;

    public NotchController(AppSettings settings)
    {
        _settings = settings;

        _capsule.ExpansionRequested += () => ExpandPanel(activate: false);
        _panel.CollapseRequested += CollapsePanel;
        _panel.SettingsRequested += OpenSettings;

        _tray.TogglePanelRequested += TogglePanel;
        _tray.NewNoteRequested += () =>
        {
            ExpandPanel(activate: true);
            _panel.Board.FocusQuickAdd();
        };
        _tray.SettingsRequested += OpenSettings;
        _tray.ExitRequested += () => Application.Current.Shutdown();

        _hotkey.HotkeyPressed += TogglePanel;

        ApplySettings(settings, save: false);

        _tray.Show();
        _hotkeyRegistrationOk = _hotkey.Apply(settings.HotkeyDisplay);
        if (!_hotkeyRegistrationOk)
            WarnHotkeyUnavailable(settings.HotkeyDisplay);
        _capsule.Show();
    }

    private bool _hotkeyRegistrationOk = true;

    private void WarnHotkeyUnavailable(string hotkeyDisplay)
    {
        // Another app owns the combination; without a hint the user never learns why the hotkey is dead.
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _tray.ShowBalloonTip("全局热键不可用",
                $"{hotkeyDisplay} 已被其他程序占用，呼出便签请悬停顶部胶囊，或在设置中更换热键。");
        };
        timer.Start();
    }

    public void TogglePanel()
    {
        if (_panel.IsExpanded)
            CollapsePanel();
        else
            ExpandPanel(activate: true);
    }

    public void ExpandPanel(bool activate)
    {
        _capsule.Hide();
        _panel.Expand(activate);
    }

    public void CollapsePanel()
    {
        _panel.Collapse();
        _capsule.Show();
    }

    private void Reposition()
    {
        var monitor = ScreenLocator.ResolveMonitor(_settings);
        ScreenLocator.PlaceTopCenter(_capsule, monitor);
        ScreenLocator.PlaceTopCenter(_panel, monitor);
    }

    // ----- settings -----

    public void OpenSettings()
    {
        var window = new SettingsWindow(_settings) { Topmost = true };
        window.ShowDialog();
        if (window.PendingSettings is { } pending)
            ApplySettings(pending, save: true);
    }

    private void ApplySettings(AppSettings settings, bool save)
    {
        _settings = settings;
        _capsule.TriggerMode = settings.Trigger;
        ThemeManager.Apply(settings.Theme);
        Reposition();
        _hotkeyRegistrationOk = _hotkey.Apply(settings.HotkeyDisplay);
        if (!_hotkeyRegistrationOk)
            _tray.ShowBalloonTip("全局热键不可用", $"{settings.HotkeyDisplay} 已被其他程序占用，请换一个组合。");

        // Never touch the registry on the startup path — only when the user changes the toggle,
        // so a manually configured autostart is not silently deleted.
        if (save)
        {
            AutoStartManager.SetEnabled(settings.StartWithSystem);
            SettingsStore.Default().Save(settings);
        }
    }

    public void Shutdown()
    {
        _panel.Collapse();
        SettingsStore.Default().Save(_settings);
    }

    public void Dispose()
    {
        _hotkey.Dispose();
        _tray.Dispose();
    }
}
