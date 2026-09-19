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

        _keepAwake.RecoverOnStartup();
        SyncKeepAwakeUi();

        _tray.Show();
        _ = _hotkey.Apply(settings.HotkeyDisplay);
        _capsule.Show();
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
            _panel.GetCurrentNoteText,
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
        _ = _hotkey.Apply(settings.HotkeyDisplay);

        if (settings.ClipboardHistoryEnabled)
        {
            if (_clipboardStore is null || _clipboardMonitor is null)
            {
                StartClipboardHistory();
            }
            else if (_clipboardStore.Entries.Count > settings.ClipboardHistoryMaxEntries)
            {
                _clipboardStore.SaveNow();
                StartClipboardHistory();
            }
        }
        else
        {
            StopClipboardHistory();
        }

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
