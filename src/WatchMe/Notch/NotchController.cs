using System.Windows;
using WatchMe.Clipboard;
using WatchMe.KeepAwake;
using WatchMe.Settings;
using WatchMe.Shell;
using WatchMe.Themes;

namespace WatchMe.Notch;

/// <summary>
/// Orchestrates the notch experience: capsule hot zone, drop-down panel, tray, hotkey,
/// clipboard history and keep-awake, plus applying settings changes at runtime.
/// </summary>
public sealed class NotchController : IDisposable
{
    private readonly NotchCapsuleWindow _capsule = new();
    private readonly NotchPanelWindow _panel = new();
    private readonly TrayIconService _tray = new();
    private readonly GlobalHotkeyService _hotkey = new();
    private readonly KeepAwakeController _keepAwake;
    private ClipboardHistoryStore? _clipboardStore;
    private ClipboardMonitor? _clipboardMonitor;
    private AppSettings _settings;

    public NotchController(AppSettings settings, KeepAwakeController keepAwake)
    {
        _settings = settings;
        _keepAwake = keepAwake;

        _capsule.ExpansionRequested += OnCapsuleExpansion;
        _capsule.FilesDropped += paths =>
        {
            ExpandPanel(activate: true);
            _panel.Shelf.AddFiles(paths);
        };
        _panel.CollapseRequested += CollapsePanel;
        _panel.SettingsRequested += OpenSettings;
        _panel.ClipboardRequested += OpenClipboardHistory;
        _panel.CoffeeToggled += () => ToggleCoffee();

        _tray.TogglePanelRequested += TogglePanel;
        _tray.NewNoteRequested += () =>
        {
            ExpandPanel(activate: true);
            _panel.NewNote();
        };
        _tray.SettingsRequested += OpenSettings;
        _tray.ClipboardRequested += OpenClipboardHistory;
        _tray.CoffeeToggled += ToggleCoffee;
        _tray.LidNeverSleepToggled += desired => ApplyLidNeverSleep(desired);
        _tray.ExitRequested += () => Application.Current.Shutdown();

        _hotkey.HotkeyPressed += TogglePanel;

        ApplySettings(settings, save: false);
        if (settings.ClipboardHistoryEnabled)
            StartClipboardHistory();

        SyncKeepAwakeUi();

        _tray.Show();
        _hotkeyRegistrationOk = _hotkey.Apply(settings.HotkeyDisplay);
        if (!_hotkeyRegistrationOk)
            WarnHotkeyUnavailable(settings.HotkeyDisplay);
        _capsule.Show();

        // Crash recovery may raise a UAC prompt; run it off the UI thread so startup never blocks.
        if (_keepAwake.LidNeverSleepActive)
        {
            _ = Task.Run(() =>
            {
                _keepAwake.RecoverOnStartup();
                _tray.SetLidNeverSleepActive(_keepAwake.LidNeverSleepActive);
            });
        }
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
                $"{hotkeyDisplay} 已被其他程序占用，呼出面板请悬停顶部胶囊，或在设置中更换热键。");
        };
        timer.Start();
    }

    private void OnCapsuleExpansion() => ExpandPanel(activate: false);

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
        _capsule.SetCoffeeActive(_keepAwake.CoffeeActive);
        _capsule.Show();
    }

    public void ToggleCoffee()
    {
        _keepAwake.ToggleCoffee();
        SyncKeepAwakeUi();
    }

    private void ApplyLidNeverSleep(bool desired)
    {
        if (_keepAwake.LidNeverSleepActive == desired)
            return;

        if (ApplyLidNeverSleepCore(desired))
            SyncKeepAwakeUi();
    }

    private bool ApplyLidNeverSleepCore(bool desired) => _keepAwake.SetLidNeverSleep(desired);

    private void SyncKeepAwakeUi()
    {
        _tray.SetCoffeeActive(_keepAwake.CoffeeActive);
        _tray.SetLidNeverSleepActive(_keepAwake.LidNeverSleepActive);
        _panel.SetCoffeeActive(_keepAwake.CoffeeActive);
        _capsule.SetCoffeeActive(_keepAwake.CoffeeActive);
    }

    private void Reposition()
    {
        var monitor = ScreenLocator.ResolveMonitor(_settings);
        ScreenLocator.PlaceTopCenter(_capsule, monitor);
        ScreenLocator.PlaceTopCenter(_panel, monitor);
    }

    // ----- clipboard history -----

    private void StartClipboardHistory()
    {
        _clipboardStore ??= ClipboardHistoryStore.Default(_settings.ClipboardHistoryMaxEntries);
        _clipboardStore.Load();
        _clipboardMonitor ??= new ClipboardMonitor(_clipboardStore);
        _clipboardMonitor.Start();
    }

    private void StopClipboardHistory()
    {
        _clipboardMonitor?.Dispose();
        _clipboardMonitor = null;
        _clipboardStore?.SaveNow();
        _clipboardStore = null;
    }

    private void OpenClipboardHistory()
    {
        if (_clipboardStore is null)
            return;

        var window = new ClipboardHistoryWindow(
            _clipboardStore,
            _clipboardMonitor!,
            _panel.AppendToCurrentNote)
        {
            Owner = null,
        };
        window.RefreshView();
        window.Show();
        window.Activate();
    }

    // ----- settings -----

    public void OpenSettings()
    {
        var window = new SettingsWindow(_settings, _keepAwake.LidNeverSleepActive) { Topmost = true };
        window.ShowDialog();
        if (window.PendingSettings is { } pending)
        {
            ApplySettings(pending, save: true);
            if (window.LidNeverSleepDesired != _keepAwake.LidNeverSleepActive)
                ApplyLidNeverSleep(window.LidNeverSleepDesired);
        }
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

        if (settings.ClipboardHistoryEnabled)
        {
            // A changed cap needs a rebuilt store; an existing monitor is kept as-is.
            if (_clipboardStore is not null
                && _clipboardStore.Entries.Count > 0
                && _settings.ClipboardHistoryMaxEntries != settings.ClipboardHistoryMaxEntries)
            {
                StopClipboardHistory();
            }

            if (_clipboardStore is null || _clipboardMonitor is null)
                StartClipboardHistory();
        }
        else
        {
            StopClipboardHistory();
        }

        // Never touch the registry on the startup path — only when the user changes the toggle,
        // so a manually configured autostart is not silently deleted.
        if (save)
            AutoStartManager.SetEnabled(settings.StartWithSystem);
        if (save)
            SettingsStore.Default().Save(settings);
    }

    public void Shutdown()
    {
        _panel.Collapse();
        StopClipboardHistory();
        _keepAwake.Shutdown();
        SettingsStore.Default().Save(_settings);
    }

    public void Dispose()
    {
        _hotkey.Dispose();
        _tray.Dispose();
        _clipboardMonitor?.Dispose();
    }
}
