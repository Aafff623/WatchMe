using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;

namespace WatchMe.Shell;

/// <summary>Tray icon + menu: the always-available control surface while the notch stays hidden.</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly TaskbarIcon _icon = new()
    {
        ToolTipText = "WatchMe",
        IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
    };

    public event Action? TogglePanelRequested;

    public event Action? NewNoteRequested;

    public event Action? SettingsRequested;

    public event Action? ClipboardRequested;

    public event Action? CoffeeToggled;

    public event Action<bool>? LidNeverSleepToggled;

    public event Action? ExitRequested;

    private bool _coffeeActive;
    private bool _lidActive;
    private MenuItem? _coffeeItem;
    private MenuItem? _lidItem;

    public void Show()
    {
        _icon.TrayMouseDoubleClick += (_, _) => TogglePanelRequested?.Invoke();
        _icon.ContextMenu = BuildMenu();
        _icon.Visibility = Visibility.Visible;
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();

        var show = Item("显示 / 隐藏面板 (Ctrl+Alt+W)", () => TogglePanelRequested?.Invoke());
        var note = Item("新建笔记", () => NewNoteRequested?.Invoke());
        var clipboard = Item("剪贴板历史", () => ClipboardRequested?.Invoke());
        var settings = Item("设置", () => SettingsRequested?.Invoke());
        _coffeeItem = Item("☕ 咖啡模式（阻止休眠）", () => CoffeeToggled?.Invoke());
        _lidItem = Item("合盖不休眠（管理员）", () => LidNeverSleepToggled?.Invoke(!_lidActive));
        var exit = Item("退出 WatchMe", () => ExitRequested?.Invoke());

        foreach (var item in new[] { show, note, clipboard, settings })
            menu.Items.Add(item);
        menu.Items.Add(new Separator());
        menu.Items.Add(_coffeeItem);
        menu.Items.Add(_lidItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);
        return menu;
    }

    private static MenuItem Item(string header, Action onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => onClick();
        return item;
    }

    public void SetCoffeeActive(bool active)
    {
        _coffeeActive = active;
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_coffeeItem is not null)
                _coffeeItem.IsChecked = active;
            _icon.ToolTipText = $"WatchMe{(active ? " · 咖啡模式中" : "")}";
        });
    }

    public void SetLidNeverSleepActive(bool active)
    {
        _lidActive = active;
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_lidItem is not null)
                _lidItem.IsChecked = active;
        });
    }

    public void Dispose()
    {
        _icon.Visibility = Visibility.Collapsed;
        _icon.Dispose();
    }
}
