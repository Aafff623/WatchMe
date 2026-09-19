using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WatchMe.Notes;

/// <summary>
/// The sticky-note board: a quick-add box on top, tag filter chips, and colored note
/// cards — modelled on 好用便签's desktop form (add, categorize, check off, delete).
/// </summary>
public partial class StickyBoardView : UserControl
{
    /// <summary>Pastel sticky colors cycled per tag index.</summary>
    private static readonly string[] TagPalette =
    [
        "#FFF3A3", // yellow
        "#C9F0CE", // green
        "#BFE0FF", // blue
        "#FFD3E0", // pink
        "#E3D5FF", // purple
        "#FFE2BE", // orange
    ];

    private readonly NoteStore _store = NoteStore.Default();
    private string _filter = "全部";

    public StickyBoardView() => InitializeComponent();

    /// <summary>The single note store instance the panel shares.</summary>
    public NoteStore Store => _store;

    /// <summary>The board changed content; the host should persist soon.</summary>
    public event Action? Changed;

    public void FocusQuickAdd() => QuickAdd.Focus();

    /// <summary>Loads current notes and builds the board; call once after the store has loaded.</summary>
    public void RebuildAll()
    {
        RebuildTagChips();
        RebuildCards();
    }

    // ----- tag color -----

    private Brush BrushForTag(string tag)
    {
        var index = _store.Tags.IndexOf(tag);
        if (index < 0)
            index = 0;
        var brush = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(TagPalette[index % TagPalette.Length]));
        brush.Freeze();
        return brush;
    }

    // ----- quick add -----

    private void OnQuickAddKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        var text = QuickAdd.Text.Trim();
        if (text.Length == 0)
            return;

        _store.AddNote(text, NewTagCombo.Text);
        _store.SaveNow();
        QuickAdd.Clear();
        RebuildAll();
        Changed?.Invoke();
        e.Handled = true;
    }

    // ----- filter chips -----

    private void RebuildTagChips()
    {
        var selected = _filter;
        TagChips.Items.Clear();
        foreach (var tag in _store.Tags)
        {
            var chip = new ToggleButton
            {
                Style = (Style)FindResource("Chip"),
                Content = tag,
                IsChecked = tag == selected,
            };
            var captured = tag;
            chip.Click += (_, _) =>
            {
                _filter = captured;
                AllChip.IsChecked = false;
                RebuildTagChips();
                RebuildCards();
            };
            TagChips.Items.Add(chip);
        }

        AllChip.IsChecked = selected == "全部";
    }

    private void OnFilterAll(object sender, RoutedEventArgs e)
    {
        _filter = "全部";
        RebuildTagChips();
        RebuildCards();
    }

    // ----- cards -----

    private void RebuildCards()
    {
        Cards.Items.Clear();
        var notes = _store.FilteredBy(_filter);
        EmptyHint.Visibility = notes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var note in notes)
            Cards.Items.Add(BuildCard(note));
    }

    private UIElement BuildCard(StickyNote note)
    {
        var textBox = new TextBox { Style = (Style)FindResource("CardText"), Text = note.Text };
        var debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        debounce.Tick += (_, _) =>
        {
            debounce.Stop();
            if (_store.SetText(note.Id, textBox.Text))
                Changed?.Invoke();
        };
        textBox.TextChanged += (_, _) =>
        {
            debounce.Stop();
            debounce.Start();
        };
        textBox.LostFocus += (_, _) =>
        {
            debounce.Stop();
            if (_store.SetText(note.Id, textBox.Text))
            {
                _store.SaveNow();
                Changed?.Invoke();
            }
        };

        var doneCheck = new CheckBox { Style = (Style)FindResource("DoneCircle"), IsChecked = note.IsDone };
        doneCheck.Click += (_, _) =>
        {
            _store.SetDone(note.Id, doneCheck.IsChecked == true);
            _store.SaveNow();
            RefreshCardStrikethrough(textBox, doneCheck.IsChecked == true);
            Changed?.Invoke();
        };
        RefreshCardStrikethrough(textBox, note.IsDone);

        var tagCombo = new ComboBox { Style = (Style)FindResource("MiniCombo") };
        foreach (var tag in _store.Tags)
            tagCombo.Items.Add(tag);
        tagCombo.SelectedItem = note.Tag;
        tagCombo.SelectionChanged += (_, _) =>
        {
            if (tagCombo.SelectedItem is not string tag || tag == note.Tag)
                return;

            _store.SetTag(note.Id, tag);
            _store.SaveNow();
            RebuildAll();
            Changed?.Invoke();
        };

        var deleteButton = new Button
        {
            Content = "✕",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x6A, 0x6A)),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(4, 0, 4, 0),
        };
        deleteButton.Click += (_, _) => DeleteNote(note.Id);

        var timeText = new TextBlock
        {
            Text = note.UpdatedAt.LocalDateTime.ToString("MM-dd HH:mm"),
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x7A)),
        };

        var header = new DockPanel();
        header.Children.Add(deleteButton);
        DockPanel.SetDock(deleteButton, Dock.Right);
        header.Children.Add(doneCheck);
        DockPanel.SetDock(doneCheck, Dock.Left);
        header.Children.Add(tagCombo);
        DockPanel.SetDock(tagCombo, Dock.Right);

        var footer = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
        footer.Children.Add(timeText);
        DockPanel.SetDock(timeText, Dock.Left);

        var cardInner = new StackPanel();
        cardInner.Children.Add(header);
        cardInner.Children.Add(textBox);
        cardInner.Children.Add(footer);

        var card = new Border
        {
            Background = BrushForTag(note.Tag),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 7, 8, 7),
            Margin = new Thickness(0, 0, 8, 8),
        };
        card.Child = cardInner;
        return card;
    }

    private static void RefreshCardStrikethrough(TextBox textBox, bool done)
    {
        textBox.TextDecorations = done
            ? new TextDecorationCollection([TextDecorations.Strikethrough[0]])
            : null;
        textBox.Opacity = done ? 0.55 : 1.0;
    }

    // ----- delete + undo -----

    private void DeleteNote(string id)
    {
        if (!_store.DeleteNote(id))
            return;

        _store.SaveNow();
        RebuildAll();
        Changed?.Invoke();

        UndoRow.Visibility = Visibility.Visible;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            UndoRow.Visibility = Visibility.Collapsed;
        };
        timer.Start();
    }

    private void OnUndoDelete(object sender, RoutedEventArgs e)
    {
        if (_store.UndoDeleteNote() is not null)
        {
            _store.SaveNow();
            RebuildAll();
            Changed?.Invoke();
        }

        UndoRow.Visibility = Visibility.Collapsed;
    }
}
