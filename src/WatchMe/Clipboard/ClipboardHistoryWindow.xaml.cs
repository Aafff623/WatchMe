using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace WatchMe.Clipboard;

public sealed class ClipboardRow : INotifyPropertyChanged
{
    private System.Windows.Media.Imaging.BitmapSource? _image;

    public required ClipboardEntry Entry { get; init; }

    public required string Display { get; init; }

    public System.Windows.Media.Imaging.BitmapSource? Image
    {
        get => _image;
        private set => SetField(ref _image, value);
    }

    public void LoadImage()
    {
        if (Entry.Kind != ClipboardEntryKind.Image || Entry.ImageFile is null || Image is not null)
            return;

        try
        {
            if (File.Exists(Entry.ImageFile))
                Image = BitmapFrame.Create(new Uri(Entry.ImageFile), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
        catch (IOException)
        {
        }
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

/// <summary>Searchable clipboard history: copy back, append to the current note, prune entries.</summary>
public partial class ClipboardHistoryWindow : Window
{
    private readonly ClipboardHistoryStore _store;
    private readonly ClipboardMonitor _monitor;
    private readonly Func<string> _noteTextProvider;
    private readonly Action<string> _noteTextAppender;

    public ClipboardHistoryWindow(ClipboardHistoryStore store, ClipboardMonitor monitor,
        Func<string> noteTextProvider, Action<string> noteTextAppender)
    {
        InitializeComponent();
        _store = store;
        _monitor = monitor;
        _noteTextProvider = noteTextProvider;
        _noteTextAppender = noteTextAppender;
    }

    public void RefreshView()
    {
        var query = SearchInput.Text;
        var rows = _store.Search(query).Select(e => new ClipboardRow { Entry = e, Display = e.Display }).ToList();
        foreach (var row in rows)
            row.LoadImage();
        EntriesList.ItemsSource = rows;
        CountText.Text = $"{rows.Count} 条 / 共 {_store.Entries.Count}";
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => RefreshView();

    private ClipboardRow? RowFrom(object sender) =>
        sender is FrameworkElement { DataContext: ClipboardRow row } ? row : null;

    private void CopyBack(ClipboardRow row)
    {
        _monitor.SuppressNextCapture();
        if (row.Entry.Kind == ClipboardEntryKind.Text && row.Entry.Text is not null)
            System.Windows.Clipboard.SetText(row.Entry.Text);
        else if (row.Entry.ImageFile is not null && File.Exists(row.Entry.ImageFile))
            System.Windows.Clipboard.SetImage(
                BitmapFrame.Create(new Uri(row.Entry.ImageFile), BitmapCreateOptions.None, BitmapCacheOption.OnLoad));
    }

    private void OnCopyBack(object sender, RoutedEventArgs e)
    {
        if (RowFrom(sender) is { } row)
            CopyBack(row);
    }

    private void OnInsertToNote(object sender, RoutedEventArgs e)
    {
        if (RowFrom(sender) is not { } row)
            return;

        var text = row.Entry.Kind == ClipboardEntryKind.Text
            ? row.Entry.Text
            : row.Entry.ImageFile is not null ? $"![clipboard]({row.Entry.ImageFile.Replace('\\', '/')})" : null;
        if (text is null)
            return;

        var current = _noteTextProvider();
        _noteTextAppender((current.Length > 0 && !current.EndsWith('\n') ? current + "\n" : current) + text + "\n");
        Close();
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (RowFrom(sender) is { } row)
        {
            _store.Remove(row.Entry);
            _store.SaveNow();
            RefreshView();
        }
    }

    private void OnClearAll(object sender, RoutedEventArgs e)
    {
        _store.Clear();
        _store.SaveNow();
        RefreshView();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }
}
