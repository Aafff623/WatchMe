using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using WatchMe.Interop;

namespace WatchMe.Shelf;

public sealed class ShelfChip : INotifyPropertyChanged
{
    private BitmapSource? _thumbnail;
    private bool _isSelected;

    public required ShelfItem Item { get; init; }

    public BitmapSource? Thumbnail
    {
        get => _thumbnail;
        set => SetField(ref _thumbnail, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// The file shelf strip: drop files in (path references only), drag them out to any app,
/// space-bar Quick Look, multi-select with Ctrl/Shift, and missing-file markers.
/// </summary>
public partial class FileShelfView : UserControl
{
    private const double DragThreshold = 8;

    private readonly FileShelfStore _store;
    private readonly ObservableCollection<ShelfChip> _chips = [];
    private readonly ShelfSelection<ShelfChip> _selection = new();
    private readonly Dictionary<string, BitmapSource?> _thumbnailCache = new(StringComparer.OrdinalIgnoreCase);
    private Point _dragStart;
    private bool _dragPending;

    public FileShelfView()
    {
        InitializeComponent();
        _store = FileShelfStore.Default();
        ChipsList.ItemsSource = _chips;
        Reload();
    }

    /// <summary>The full ordered item list; the controller uses it for Quick Look navigation.</summary>
    public IReadOnlyList<ShelfItem> Items => _store.Items;

    /// <summary>True while an OLE drag-out is in flight; the panel must not auto-collapse then.</summary>
    public bool IsDraggingOut { get; private set; }

    public event Action? ShelfChanged;

    public event Action<int>? QuickLookRequested;

    private void Reload()
    {
        _store.Load();
        _selection.Clear();
        _chips.Clear();
        foreach (var item in _store.Items)
        {
            var chip = new ShelfChip { Item = item };
            _chips.Add(chip);
            LoadThumbnail(chip);
        }

        UpdateCount();
    }

    private void UpdateCount() =>
        CountLabel.Text = _chips.Count == 0 ? string.Empty : $"{_chips.Count}/{FileShelfStore.MaxItems}";

    private void LoadThumbnail(ShelfChip chip)
    {
        if (_thumbnailCache.TryGetValue(chip.Item.Path, out var cached))
        {
            chip.Thumbnail = cached;
            return;
        }

        var path = chip.Item.Path;
        var dispatcher = Dispatcher;
        _ = Task.Run(() =>
        {
            BitmapSource? thumb = null;
            try
            {
                thumb = ShellThumbnailProvider.GetThumbnail(path, 96);
            }
            catch
            {
                // A bad path or locked file simply falls back to the default glyph.
            }

            _thumbnailCache[path] = thumb;
            dispatcher.Invoke(() => chip.Thumbnail = thumb);
        });
    }

    public void AddFiles(IReadOnlyList<string> paths)
    {
        var added = _store.AddRange(paths);
        if (added.Count == 0)
            return;

        foreach (var item in added)
        {
            var chip = new ShelfChip { Item = item };
            _chips.Add(chip);
            LoadThumbnail(chip);
        }

        RefreshSelectionFlags();
        Persist();
    }

    private void Persist()
    {
        _store.SaveNow();
        UpdateCount();
        ShelfChanged?.Invoke();
    }

    private void RefreshSelectionFlags()
    {
        foreach (var chip in _chips)
            chip.IsSelected = _selection.IsSelected(chip);
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        _dragStart = e.GetPosition(this);
        _dragPending = true;

        if (e.OriginalSource is FrameworkElement { DataContext: ShelfChip chip })
        {
            var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            _selection.ApplyClick(chip, ctrl, shift, _chips.ToList());
            RefreshSelectionFlags();
        }
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (!_dragPending || e.LeftButton != MouseButtonState.Pressed || IsDraggingOut)
            return;

        var delta = e.GetPosition(this) - _dragStart;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
            return;

        var selected = _chips.Where(c => c.IsSelected).Select(c => c.Item).ToList();
        if (selected.Count > 0)
            StartDragOut(selected);
        else
            _dragPending = false;
    }

    private void StartDragOut(IReadOnlyList<ShelfItem> items)
    {
        IsDraggingOut = true;
        try
        {
            var dropList = new System.Collections.Specialized.StringCollection();
            dropList.AddRange(items.Select(i => i.Path).ToArray());
            _ = DragDrop.DoDragDrop(this, new DataObject(DataFormats.FileDrop, dropList), DragDropEffects.Copy);
        }
        finally
        {
            IsDraggingOut = false;
            _dragPending = false;
        }
    }

    protected override void OnPreviewMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDoubleClick(e);
        if (e.OriginalSource is FrameworkElement { DataContext: ShelfChip chip })
        {
            OpenInShell(chip);
            e.Handled = true;
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.OriginalSource is not FrameworkElement { DataContext: ShelfChip chip })
            return;

        var index = _chips.IndexOf(chip);
        switch (e.Key)
        {
            case Key.Space:
                if (index >= 0)
                    QuickLookRequested?.Invoke(index);
                e.Handled = true;
                break;
            case Key.Delete:
                RemoveSelected();
                e.Handled = true;
                break;
            case Key.Enter:
                OpenInShell(chip);
                e.Handled = true;
                break;
        }
    }

    private void OpenInShell(ShelfChip chip)
    {
        if (chip.Item.Availability == ShelfItemAvailability.Missing)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(chip.Item.Path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            // No file association or blocked launch — keep the shelf usable.
        }
    }

    private void RemoveSelected()
    {
        var selected = _chips.Where(c => c.IsSelected).ToList();
        if (selected.Count == 0)
            return;

        _store.Remove(selected.Select(c => c.Item));
        foreach (var chip in selected)
            _chips.Remove(chip);
        _selection.Clear();
        RefreshSelectionFlags();
        Persist();
    }

    // ----- chip context menu -----

    private ShelfChip? ChipFromMenu(object sender) =>
        sender is MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement target } }
            ? target.DataContext as ShelfChip
            : null;

    private void OnMenuOpen(object sender, RoutedEventArgs e)
    {
        if (ChipFromMenu(sender) is { } chip)
            OpenInShell(chip);
    }

    private void OnMenuReveal(object sender, RoutedEventArgs e)
    {
        if (ChipFromMenu(sender) is not { } chip || chip.Item.Availability == ShelfItemAvailability.Missing)
            return;

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{chip.Item.Path}\""));
        }
        catch (Win32Exception)
        {
        }
    }

    private void OnMenuRemove(object sender, RoutedEventArgs e)
    {
        if (ChipFromMenu(sender) is { } chip)
        {
            _selection.SelectMany([chip]);
            RefreshSelectionFlags();
            RemoveSelected();
        }
    }

    // ----- drag & drop in -----

    private void OnDragOver(object sender, DragEventArgs e) =>
        e.Effects = IsFileDrop(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            AddFiles(paths);
    }

    private static bool IsFileDrop(IDataObject data) => data.GetDataPresent(DataFormats.FileDrop);
}
