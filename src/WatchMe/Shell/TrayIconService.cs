using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;

namespace WatchMe.Shell;

/// <summary>Tray icon + menu: the always-available control surface while the panel stays hidden.</summary>
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

    public event Action? ExitRequested;

    public void Show()
    {
        _icon.TrayMouseDoubleClick += (_, _) => TogglePanelRequested?.Invoke();
        _icon.ContextMenu = BuildMenu();
        _icon.Visibility = Visibility.Visible;
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();

        var show = Item("显示 / 隐藏便签 (Ctrl+Alt+W)", () => TogglePanelRequested?.Invoke());
        var note = Item("新建便签", () => NewNoteRequested?.Invoke());
        var settings = Item("设置", () => SettingsRequested?.Invoke());
        var exit = Item("退出 WatchMe", () => ExitRequested?.Invoke());

        foreach (var item in new[] { show, note, settings })
            menu.Items.Add(item);
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

    public void ShowBalloonTip(string title, string message) => Application.Current.Dispatcher.Invoke(() =>
        _icon.ShowBalloonTip(title, message, Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Warning));

    public void Dispose()
    {
        _icon.Visibility = Visibility.Collapsed;
        _icon.Dispose();
    }
}
