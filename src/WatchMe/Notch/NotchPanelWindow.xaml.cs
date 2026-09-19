using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WatchMe.Notes;

namespace WatchMe.Notch;

/// <summary>
/// The drop-down sticky-note drawer. The controller owns positioning and lifecycle;
/// this window owns the note board and auto-collapse behavior.
/// </summary>
public partial class NotchPanelWindow : Window
{
    private static readonly TimeSpan ExpandDuration = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan CollapseGrace = TimeSpan.FromMilliseconds(800);
    private const double ExpandedHeight = 600;

    private readonly DispatcherTimer _collapseGrace = new();

    public NotchPanelWindow()
    {
        InitializeComponent();
        _collapseGrace.Interval = CollapseGrace;
        _collapseGrace.Tick += (_, _) => ConsiderAutoCollapse();
        Board.Changed += () => { };
        Board.Store.Load();
        Board.RebuildAll();
    }

    public bool IsExpanded { get; private set; }

    /// <summary>Suppression flags consulted before auto-collapsing.</summary>
    private bool SuppressAutoCollapse => Mouse.Captured is not null;

    public event Action? CollapseRequested;

    public event Action? SettingsRequested;

    public void Expand(bool activate)
    {
        if (IsExpanded)
        {
            if (activate)
                FocusPanel();
            return;
        }

        IsExpanded = true;
        if (!IsVisible)
            Show();

        var height = new DoubleAnimation(0, ExpandedHeight, ExpandDuration) { EasingFunction = new QuadraticEase() };
        var opacity = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120));
        Root.BeginAnimation(HeightProperty, height);
        Root.BeginAnimation(OpacityProperty, opacity);
        if (activate)
            FocusPanel();
    }

    public void Collapse()
    {
        if (!IsExpanded)
            return;

        Board.Store.SaveNow();
        IsExpanded = false;
        _collapseGrace.Stop();
        Hide();
        Root.BeginAnimation(HeightProperty, null);
        Root.BeginAnimation(OpacityProperty, null);
        Root.Height = 0;
        Root.Opacity = 0;
    }

    private void FocusPanel()
    {
        if (Activate())
        {
            Focus();
            Board.FocusQuickAdd();
            return;
        }

        // Foreground lock refused activation; the classic ALT-key nudge releases it.
        const byte vkMenu = 0x12;
        const uint keyUp = 0x0002;
        Interop.NativeMethods.keybd_event(vkMenu, 0, 0, IntPtr.Zero);
        Interop.NativeMethods.keybd_event(vkMenu, 0, keyUp, IntPtr.Zero);
        _ = Interop.NativeMethods.SetForegroundWindow(
            new System.Windows.Interop.WindowInteropHelper(this).Handle);
        Focus();
        Board.FocusQuickAdd();
    }

    // ----- collapse behaviors -----

    private void ConsiderAutoCollapse()
    {
        if (!IsExpanded || IsMouseOver || SuppressAutoCollapse)
            return;

        _collapseGrace.Stop();
        CollapseRequested?.Invoke();
    }

    private void OnPanelDeactivated(object sender, EventArgs e) => CollapseRequested?.Invoke();

    private void OnPanelMouseLeave(object sender, MouseEventArgs e)
    {
        _collapseGrace.Stop();
        _collapseGrace.Start();
    }

    private void OnPanelMouseEnter(object sender, MouseEventArgs e) => _collapseGrace.Stop();

    private void OnPanelPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CollapseRequested?.Invoke();
            e.Handled = true;
        }
        else if (e.Key == Key.N && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            Board.FocusQuickAdd();
            e.Handled = true;
        }
    }

    // ----- header actions -----

    private void OnCollapseClick(object sender, RoutedEventArgs e) => CollapseRequested?.Invoke();

    private void OnSettingsClick(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();

    /// <summary>Persists the board (called by the controller on collapse too).</summary>
    public void Flush() => Board.Store.SaveNow();
}
