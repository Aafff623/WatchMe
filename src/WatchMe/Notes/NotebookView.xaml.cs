using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WatchMe.Notes;

public enum EditorViewMode
{
    Edit,
    Split,
    Preview,
}

/// <summary>
/// The markdown editing surface: format toolbar, find/replace, edit/split/preview modes,
/// list Tab-indentation, and paste-image insertion. The consumer (notch panel) drives
/// which note is loaded and when it is saved.
/// </summary>
public partial class NotebookView : UserControl
{
    private readonly DispatcherTimer _saveDebounce = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly NoteImageStore _imageStore;
    private string? _noteId;
    private string _lastLoadedText = string.Empty;

    public NotebookView()
    {
        InitializeComponent();
        _imageStore = NoteImageStore.Default();
        Editor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance
            .GetDefinitionByExtension(".md");
        // AvalonEdit paints its own TextArea background; sync it with the theme.
        Editor.TextArea.Background = TryFindResource("Brush.EditorBg") as System.Windows.Media.Brush
            ?? System.Windows.Media.Brushes.Transparent;
        Editor.TextChanged += (_, _) => ScheduleSave();
        _saveDebounce.Tick += (_, _) => Flush();
        SetViewMode(EditorViewMode.Split);
    }

    /// <summary>Fired (debounced) whenever the text changed and should be persisted for the current note.</summary>
    public event Action<string, string>? NoteTextChanged;

    private bool IsModified { get; set; }

    public void LoadNote(string noteId, string text)
    {
        Flush();
        _noteId = noteId;
        _lastLoadedText = text;
        Editor.Text = text;
        Editor.CaretOffset = 0;
        IsModified = false;
        UpdatePreview();
    }

    public void Flush()
    {
        _saveDebounce.Stop();
        if (_noteId is not null && IsModified)
        {
            NoteTextChanged?.Invoke(_noteId, Editor.Text);
            _lastLoadedText = Editor.Text;
            IsModified = false;
        }
    }

    public void FocusEditor()
    {
        Editor.Focus();
        Editor.TextArea.Caret.BringCaretToView();
    }

    public void AppendText(string text)
    {
        var at = Editor.Document.TextLength;
        if (at > 0 && Editor.Document.GetCharAt(at - 1) != '\n')
        {
            Editor.Document.Insert(at, "\n");
            at++;
        }

        Editor.Document.Insert(at, text);
        if (!text.EndsWith('\n'))
            Editor.Document.Insert(at + text.Length, "\n");
        Select(at + text.Length + 1, 0);
        FocusEditor();
    }

    private void ScheduleSave()
    {
        if (Editor.Text == _lastLoadedText)
            return;

        IsModified = true;
        _saveDebounce.Stop();
        _saveDebounce.Start();
    }

    private void UpdatePreview() => Preview.HereMarkdown = Editor.Text;

    // ----- view modes -----

    private void OnEditMode(object sender, RoutedEventArgs e) => SetViewMode(EditorViewMode.Edit);

    private void OnSplitMode(object sender, RoutedEventArgs e) => SetViewMode(EditorViewMode.Split);

    private void OnPreviewMode(object sender, RoutedEventArgs e) => SetViewMode(EditorViewMode.Preview);

    private void SetViewMode(EditorViewMode mode)
    {
        var editing = mode != EditorViewMode.Preview;
        var previewing = mode != EditorViewMode.Edit;
        Editor.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        Preview.Visibility = previewing ? Visibility.Visible : Visibility.Collapsed;
        Splitter.Visibility = mode == EditorViewMode.Split ? Visibility.Visible : Visibility.Collapsed;
        EditorColumn.Width = editing ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        PreviewColumn.Width = previewing ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        SplitterColumn.Width = mode == EditorViewMode.Split ? new GridLength(4) : new GridLength(0);
        if (previewing)
            UpdatePreview();
    }

    // ----- find & replace -----

    private void OnToggleFind(object sender, RoutedEventArgs e)
    {
        FindBar.Visibility = FindBar.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        if (FindBar.Visibility == Visibility.Visible)
            FindInput.Focus();
        else
            Editor.Focus();
    }

    private void OnReplaceOne(object sender, RoutedEventArgs e)
    {
        var needle = FindInput.Text;
        if (needle.Length == 0)
            return;

        var index = FindNext(needle, Editor.SelectionStart + Math.Max(Editor.SelectionLength, 1));
        if (index < 0)
            index = FindNext(needle, 0);
        if (index >= 0)
        {
            Editor.Document.Replace(index, needle.Length, ReplaceInput.Text);
            Select(index, ReplaceInput.Text.Length);
        }
    }

    private void OnReplaceAll(object sender, RoutedEventArgs e)
    {
        var needle = FindInput.Text;
        if (needle.Length == 0 || !Editor.Text.Contains(needle, StringComparison.OrdinalIgnoreCase))
            return;

        Editor.Document.Replace(0, Editor.Text.Length,
            Editor.Text.Replace(needle, ReplaceInput.Text, StringComparison.OrdinalIgnoreCase));
        Select(0, 0);
    }

    private int FindNext(string needle, int from)
    {
        if (needle.Length == 0)
            return -1;

        var start = Math.Clamp(from, 0, Editor.Text.Length);
        var index = Editor.Text.IndexOf(needle, start, StringComparison.OrdinalIgnoreCase);
        return index >= 0 ? index : Editor.Text.IndexOf(needle, 0, start, StringComparison.OrdinalIgnoreCase);
    }

    private void Select(int offset, int length)
    {
        Editor.Select(offset, length);
        Editor.TextArea.Caret.Offset = offset + length;
        Editor.TextArea.Caret.BringCaretToView();
        Editor.ScrollToLine(Editor.Document.GetLineByOffset(offset).LineNumber);
    }

    // ----- format commands -----

    private void OnBold(object sender, RoutedEventArgs e) => ToggleWrap("**");

    private void OnItalic(object sender, RoutedEventArgs e) => ToggleWrap("*");

    private void OnStrike(object sender, RoutedEventArgs e) => ToggleWrap("~~");

    private void OnInlineCode(object sender, RoutedEventArgs e) => ToggleWrap("`");

    private void OnLink(object sender, RoutedEventArgs e)
    {
        var sel = Editor.Text.Substring(Editor.SelectionStart, Editor.SelectionLength);
        if (sel.Length == 0)
            sel = "链接文字";
        var inserted = $"[{sel}](https://)";
        ReplaceSelection(inserted, inserted.Length - 9, 8);
    }

    private void OnQuote(object sender, RoutedEventArgs e) => ToggleLinePrefix("> ", null);

    private void OnBulletList(object sender, RoutedEventArgs e) => ToggleLinePrefix("- ", null);

    private void OnTaskList(object sender, RoutedEventArgs e) => ToggleLinePrefix("- [ ] ", "- [x] ");

    private void ToggleWrap(string marker)
    {
        var start = Editor.SelectionStart;
        var length = Editor.SelectionLength;
        if (length == 0)
        {
            ReplaceSelection(marker + marker, marker.Length, 0);
            return;
        }

        var text = Editor.Text;
        // unwrap when already wrapped
        if (start >= marker.Length && text.Length - start - length >= marker.Length
            && text.Substring(start - marker.Length, marker.Length) == marker
            && text.Substring(start + length, marker.Length) == marker)
        {
            Editor.Document.Remove(start + length, marker.Length);
            Editor.Document.Remove(start - marker.Length, marker.Length);
            Select(start - marker.Length, length);
        }
        else
        {
            Editor.Document.Insert(start + length, marker);
            Editor.Document.Insert(start, marker);
            Select(start + marker.Length, length);
        }
    }

    private void ReplaceSelection(string text, int caretOffsetFromInsertStart, int selectionLength)
    {
        var start = Editor.SelectionStart;
        Editor.Document.Replace(start, Editor.SelectionLength, text);
        Select(start + caretOffsetFromInsertStart, selectionLength);
    }

    private void ToggleLinePrefix(string prefix, string? alternate)
    {
        var document = Editor.Document;
        var startLine = document.GetLineByOffset(Editor.SelectionStart);
        var endLine = document.GetLineByOffset(Editor.SelectionStart + Editor.SelectionLength);

        using var _ = document.RunUpdate();
        for (var line = startLine; line != endLine.NextLine; line = line.NextLine)
        {
            var lineText = document.GetText(line.Offset, line.Length);
            if (lineText.TrimStart().Length == 0)
                continue;

            var existing = alternate is not null && lineText.TrimStart().StartsWith(alternate, StringComparison.Ordinal)
                ? alternate
                : lineText.TrimStart().StartsWith(prefix, StringComparison.Ordinal) ? prefix : null;
            if (existing is null)
            {
                document.Insert(line.Offset, prefix);
            }
            else
            {
                var at = lineText.IndexOf(existing, StringComparison.Ordinal);
                document.Replace(line.Offset, line.Length,
                    (lineText[..at] + lineText[(at + existing.Length)..]).TrimStart());
            }
        }
    }

    // ----- keyboard behaviors -----

    private void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            IndentSelectedLines(outdent: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            IndentSelectedLines(outdent: false);
            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            OnToggleFind(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && TryPasteImage())
        {
            e.Handled = true;
        }
    }

    private void IndentSelectedLines(bool outdent)
    {
        var document = Editor.Document;
        var startLine = document.GetLineByOffset(Editor.SelectionStart);
        var endLine = document.GetLineByOffset(Editor.SelectionStart + Editor.SelectionLength);

        using var _ = document.RunUpdate();
        for (var line = startLine; line != endLine.NextLine; line = line.NextLine)
        {
            var text = document.GetText(line.Offset, line.Length);
            if (outdent)
            {
                var remove = text.StartsWith("  ", StringComparison.Ordinal) ? 2
                    : text.StartsWith('\t') ? 1 : 0;
                if (remove > 0)
                    document.Remove(line.Offset, remove);
            }
            else
            {
                document.Insert(line.Offset, "  ");
            }
        }
    }

    private bool TryPasteImage()
    {
        if (System.Windows.Clipboard.ContainsText() || !System.Windows.Clipboard.ContainsImage())
            return false;

        var source = System.Windows.Clipboard.GetImage();
        if (source is null)
            return false;

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        var path = _imageStore.SavePng(stream.ToArray());
        var link = NoteImageStore.ToMarkdownLink(path, Path.GetFileName(path));
        var insertAt = Editor.SelectionStart + Editor.SelectionLength;
        var trailing = Editor.Text.Length > 0 && Editor.Text[^1] != '\n' ? "\n" : string.Empty;
        Editor.Document.Insert(insertAt, trailing + link + "\n");
        Select(insertAt + trailing.Length + link.Length + 1, 0);
        return true;
    }
}
