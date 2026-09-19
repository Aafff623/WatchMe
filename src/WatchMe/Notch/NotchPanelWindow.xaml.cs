using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WatchMe.Notes;
using WatchMe.Shelf;

namespace WatchMe.Notch;

/// <summary>
/// The drop-down drawer itself: note tabs + markdown editor + file shelf. The controller
/// owns positioning and lifecycle; this window owns note state and auto-collapse behavior.
/// </summary>
public partial class NotchPanelWindow : Window
{
    private static readonly TimeSpan ExpandDuration = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan CollapseGrace = TimeSpan.FromMilliseconds(800);
    private static readonly double ExpandedHeight = 620;

    private readonly NoteStore _notes = NoteStore.Default();
    private readonly QuickLookWindow _quickLook = new();
    private readonly DispatcherTimer _collapseGrace = new();
    private string? _currentNoteId;
    private Button? _undoPill;

    public NotchPanelWindow()
    {
        InitializeComponent();
        _collapseGrace.Interval = CollapseGrace;
        _collapseGrace.Tick += (_, _) => ConsiderAutoCollapse();
        _quickLook.Deactivated += (_, _) => ConsiderAutoCollapse();
        Shelf.QuickLookRequested += index => _quickLook.OpenOn(Shelf.Items, index);
        Shelf.ShelfChanged += () => Notebook.Flush();
        Notebook.NoteTextChanged += HandleNoteTextChange;
        _notes.Load();
        LoadNote(_notes.Notes[0].Id);
    }

    public bool IsExpanded { get; private set; }

    /// <summary>Suppression flags consulted before auto-collapsing.</summary>
    private bool SuppressAutoCollapse => Shelf.IsDraggingOut || _quickLook.IsVisible;

    public event Action? CollapseRequested;

    public event Action? SettingsRequested;

    public event Action? ClipboardRequested;

    public event Action? CoffeeToggled;

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

        Notebook.Flush();
        _quickLook.Hide();
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
            Notebook.FocusEditor();
            return;
        }

        // Foreground lock refused activation; the classic ALT-key nudge releases it.
        const byte VK_MENU = 0x12;
        const uint KEYEVENTF_KEYUP = 0x0002;
        WatchMe.Interop.NativeMethods.keybd_event(VK_MENU, 0, 0, IntPtr.Zero);
        WatchMe.Interop.NativeMethods.keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
        _ = WatchMe.Interop.NativeMethods.SetForegroundWindow(
            new System.Windows.Interop.WindowInteropHelper(this).Handle);
        Focus();
        Notebook.FocusEditor();
    }

    public void SetCoffeeActive(bool active) => Dispatcher.Invoke(() =>
        CoffeeButton.Foreground = active
            ? (Brush)FindResource("Brush.Coffee")
            : (Brush)FindResource("Brush.TextSecondary"));

    // ----- note tabs -----

    private void LoadNote(string noteId)
    {
        var note = _notes.Notes.FirstOrDefault(n => n.Id == noteId) ?? _notes.Notes[0];
        _currentNoteId = note.Id;
        Notebook.LoadNote(note.Id, note.Text);
        RebuildTabs();
    }

    private void RebuildTabs()
    {
        var strip = TabStripHost;
        strip.Children.Clear();
        foreach (var note in _notes.Notes)
        {
            var button = new Button
            {
                Style = (Style)FindResource("TabPill"),
                Content = note.Title,
                Tag = note.Id == _currentNoteId ? "Active" : null,
                ToolTip = $"保存于 {note.SavedAt:MM-dd HH:mm}",
            };
            button.Click += (_, _) =>
            {
                if (note.Id != _currentNoteId)
                    LoadNote(note.Id);
                else
                    Notebook.FocusEditor();
            };
            var captured = note;
            var menu = new ContextMenu();
            var delete = new MenuItem { Header = "删除标签页" };
            delete.Click += (_, _) => DeleteNote(captured.Id);
            menu.Items.Add(delete);
            menu.Closed += (_, _) => Dispatcher.BeginInvoke(ConsiderAutoCollapse, DispatcherPriority.Background);
            button.ContextMenu = menu;
            strip.Children.Add(button);
        }

        var add = new Button { Style = (Style)FindResource("TabPill"), Content = "＋", ToolTip = "新建笔记" };
        add.Click += (_, _) => NewNote();
        strip.Children.Add(add);

        if (_undoPill is not null)
            strip.Children.Add(_undoPill);
    }

    public void NewNote()
    {
        var note = _notes.CreateNote();
        _notes.SaveNow();
        LoadNote(note.Id);
        Notebook.FocusEditor();
    }

    private void DeleteNote(string id)
    {
        Notebook.Flush();
        if (!_notes.DeleteNote(id))
            return;

        _notes.SaveNow();
        ShowUndoPill();
        LoadNote(_notes.Notes[0].Id);
    }

    private void ShowUndoPill()
    {
        var pill = new Button
        {
            Style = (Style)FindResource("TabPill"),
            Content = "↩ 撤销删除",
            Foreground = (Brush)FindResource("Brush.Danger"),
        };
        pill.Click += (_, _) =>
        {
            if (_notes.UndoDeleteNote() is { } restored)
            {
                _notes.SaveNow();
                _undoPill = null;
                LoadNote(restored.Id);
            }
        };
        _undoPill = pill;
        RebuildTabs();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (ReferenceEquals(_undoPill, pill))
            {
                _undoPill = null;
                RebuildTabs();
            }
        };
        timer.Start();
    }

    // ----- collapse behaviors -----

    private void ConsiderAutoCollapse()
    {
        if (!IsExpanded || IsMouseOver || SuppressAutoCollapse || Mouse.Captured is not null)
            return;

        _collapseGrace.Stop();
        CollapseRequested?.Invoke();
    }

    private void OnPanelDeactivated(object sender, EventArgs e)
    {
        // Focus went elsewhere (another app, or our own Quick Look/settings window).
        if (!_quickLook.IsVisible && !Shelf.IsDraggingOut)
            CollapseRequested?.Invoke();
    }

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
            NewNote();
            e.Handled = true;
        }
    }

    // ----- header actions & drop -----

    private void OnCollapseClick(object sender, RoutedEventArgs e) => CollapseRequested?.Invoke();

    private void OnSettingsClick(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();

    private void OnClipboardClick(object sender, RoutedEventArgs e) => ClipboardRequested?.Invoke();

    private void OnCoffeeClick(object sender, RoutedEventArgs e) => CoffeeToggled?.Invoke();

    private void OnDragOver(object sender, DragEventArgs e)
    {
        var isFile = e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = isFile ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        if (isFile && IsExpanded)
            DropOverlay.Visibility = Visibility.Visible;
    }

    protected override void OnDragLeave(DragEventArgs e)
    {
        base.OnDragLeave(e);
        DropOverlay.Visibility = Visibility.Collapsed;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            Shelf.AddFiles(paths);
    }

    internal void HandleNoteTextChange(string noteId, string text)
    {
        _notes.SetText(noteId, text);
        _notes.SaveNow();
        var activeButton = TabStripHost.Children.OfType<Button>()
            .FirstOrDefault(b => b.Tag as string == "Active");
        if (activeButton is not null && _notes.Notes.FirstOrDefault(n => n.Id == noteId) is { } note)
            activeButton.Content = note.Title;
    }

    public string GetCurrentNoteText() => Notebook.Editor.Text;

    public void AppendToCurrentNote(string text) => Notebook.AppendText(text);
}
