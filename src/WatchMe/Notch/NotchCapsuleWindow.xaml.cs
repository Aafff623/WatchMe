using System.Windows;
using System.Windows.Input;
using WatchMe.Interop;
using WatchMe.Settings;

namespace WatchMe.Notch;

/// <summary>
/// The always-visible "notch" pill at the top-center of the screen: the hot zone that
/// opens the drop-down panel on hover or click (per trigger mode), and accepts file drops.
/// It never activates so hovering it can never steal keyboard focus.
/// </summary>
public partial class NotchCapsuleWindow : Window
{
    public NotchCapsuleWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var style = NativeMethods.GetWindowLong(new System.Windows.Interop.WindowInteropHelper(this).Handle,
                NativeMethods.GWL_EXSTYLE);
            _ = NativeMethods.SetWindowLong(new System.Windows.Interop.WindowInteropHelper(this).Handle,
                NativeMethods.GWL_EXSTYLE, style | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);
        };
    }

    internal TriggerMode TriggerMode { private get; set; } = TriggerMode.Hover;

    public event Action? ExpansionRequested;

    public event Action<IReadOnlyList<string>>? FilesDropped;

    public void SetCoffeeActive(bool active) => Dispatcher.Invoke(() =>
        CoffeeDot.Visibility = active ? Visibility.Visible : Visibility.Collapsed);

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        Pill.Width = 200;
        Pill.Height = 18;
        if (TriggerMode == TriggerMode.Hover)
            ExpansionRequested?.Invoke();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        Pill.Width = 150;
        Pill.Height = 14;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        ExpansionRequested?.Invoke();
    }

    private void OnDragOver(object sender, DragEventArgs e) => e.Effects = DragDropEffects.Copy;

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            FilesDropped?.Invoke(paths);
    }
}
