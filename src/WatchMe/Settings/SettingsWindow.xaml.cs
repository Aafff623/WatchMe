using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WatchMe.Core;

namespace WatchMe.Settings;

/// <summary>Settings dialog: trigger mode, theme, hotkey, screen, clipboard history, autostart, lid sleep.</summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings _current;

    public SettingsWindow(AppSettings current, bool lidNeverSleepActive)
    {
        InitializeComponent();
        _current = current;

        TriggerHover.IsChecked = current.Trigger == TriggerMode.Hover;
        TriggerClick.IsChecked = current.Trigger == TriggerMode.Click;
        ThemeCombo.SelectedIndex = (int)current.Theme;
        HotkeyInput.Text = current.HotkeyDisplay;
        ClipboardEnabled.IsChecked = current.ClipboardHistoryEnabled;
        ClipboardCapInput.Text = current.ClipboardHistoryMaxEntries.ToString();
        AutoStartCheck.IsChecked = current.StartWithSystem;
        LidNeverSleepCheck.IsChecked = lidNeverSleepActive;

        foreach (var screen in Notch.ScreenLocator.Monitors())
        {
            var label = screen.IsPrimary ? $"{screen.DeviceName}（主显示器）" : screen.DeviceName;
            var item = new ComboBoxItem { Content = label, Tag = screen.DeviceName };
            ScreenCombo.Items.Add(item);
            if (screen.DeviceName == current.PreferredScreenDeviceName)
                ScreenCombo.SelectedItem = item;
        }

        if (ScreenCombo.SelectedIndex < 0 && ScreenCombo.Items.Count > 0)
            ScreenCombo.SelectedIndex = 0;
    }

    /// <summary>The pending settings when the user pressed 保存; null otherwise.</summary>
    public AppSettings? PendingSettings { get; private set; }

    /// <summary>The lid-no-sleep state the user wants after saving.</summary>
    public bool LidNeverSleepDesired => LidNeverSleepCheck.IsChecked == true;

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (!TryBuildSettings(out var settings))
            return;

        // Only probe when the hotkey actually changed: our own live registration
        // would make RegisterHotKey fail for the same combination.
        var changed = !string.Equals(settings.HotkeyDisplay.Trim(), _current.HotkeyDisplay.Trim(),
            StringComparison.OrdinalIgnoreCase);
        if (changed && !ProbeHotkey(settings.HotkeyDisplay))
        {
            ShowError("该热键组合当前被其他程序占用，请换一个");
            return;
        }

        PendingSettings = settings;
        Close();
    }

    private bool TryBuildSettings(out AppSettings settings)
    {
        settings = new AppSettings
        {
            Trigger = TriggerHover.IsChecked == true ? TriggerMode.Hover : TriggerMode.Click,
            Theme = (ThemeMode)Math.Max(0, ThemeCombo.SelectedIndex),
            HotkeyDisplay = HotkeyInput.Text.Trim(),
            ClipboardHistoryEnabled = ClipboardEnabled.IsChecked == true,
            ClipboardHistoryMaxEntries = int.TryParse(ClipboardCapInput.Text.Trim(), out var cap) ? cap : 200,
            StartWithSystem = AutoStartCheck.IsChecked == true,
            PreferredScreenDeviceName = (ScreenCombo.SelectedItem as ComboBoxItem)?.Tag as string,
        };

        if (!HotkeyPattern.TryParse(settings.HotkeyDisplay, out _, out _))
        {
            ShowError("全局热键格式不正确，示例：Ctrl+Alt+W 或 Ctrl+Shift+Space");
            return false;
        }

        if (settings.ClipboardHistoryMaxEntries is < 10 or > 1000)
        {
            ShowError("剪贴板历史条数需在 10-1000 之间");
            return false;
        }

        return true;
    }

    /// <summary>Probes whether the hotkey can be registered, releasing it immediately.</summary>
    private static bool ProbeHotkey(string hotkeyDisplay)
    {
        using var probe = new Shell.GlobalHotkeyService();
        return probe.Apply(hotkeyDisplay);
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }
}
