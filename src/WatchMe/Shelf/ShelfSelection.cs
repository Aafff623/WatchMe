namespace WatchMe.Shelf;

/// <summary>
/// Pure multi-selection model for the shelf: plain click selects a single item, Ctrl toggles,
/// Shift selects a range from the anchor. Ported from the macOS FileShelfSelection.
/// </summary>
public sealed class ShelfSelection<T> where T : class
{
    private readonly HashSet<T> _selected = [];
    private T? _anchor;

    public IReadOnlyCollection<T> Selected => _selected;

    public bool IsSelected(T item) => _selected.Contains(item);

    /// <summary>items must be the full ordered list the user sees, for range selection.</summary>
    public void ApplyClick(T item, bool ctrl, bool shift, IReadOnlyList<T> items)
    {
        if (shift && _anchor is not null)
        {
            var start = items.IndexOf(_anchor);
            var end = items.IndexOf(item);
            if (start >= 0 && end >= 0)
            {
                _selected.Clear();
                var (lo, hi) = start <= end ? (start, end) : (end, start);
                for (var i = lo; i <= hi; i++)
                    _selected.Add(items[i]);
                return;
            }
        }

        if (ctrl)
        {
            if (!_selected.Remove(item))
                _selected.Add(item);
            return;
        }

        _selected.Clear();
        _selected.Add(item);
        _anchor = item;
    }

    public void SelectMany(IEnumerable<T> items)
    {
        _selected.Clear();
        foreach (var item in items)
            _selected.Add(item);
    }

    public void Clear() => _selected.Clear();
}

internal static class ShelfSelectionListExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> list, T item) where T : class
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], item))
                return i;
        }

        return -1;
    }
}
