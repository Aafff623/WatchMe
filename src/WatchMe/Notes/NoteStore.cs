namespace WatchMe.Notes;

/// <summary>
/// Sticky-note persistence, ported from the macOS NoteStore's guarantees: atomic saves,
/// delete with single-level undo, at least one note always exists. Adds category tags.
/// </summary>
public sealed class NoteStore
{
    public static readonly string[] DefaultTags = ["默认", "工作", "生活"];

    private readonly string _filePath;
    private StickyNote? _lastDeleted;

    public NoteStore(string filePath) => _filePath = filePath;

    public static NoteStore Default() => new(AppPaths.NotesFile);

    public IReadOnlyList<StickyNote> Notes { get; private set; } = [];

    public List<string> Tags { get; private set; } = [.. DefaultTags];

    public void Load()
    {
        var file = Core.JsonFile.TryLoad<NoteFile>(_filePath);
        Notes = file?.Notes.Where(n => n is not null).ToList() ?? [];
        Tags = file?.Tags is { Count: > 0 } tags ? tags.Distinct().ToList() : [.. DefaultTags];
        foreach (var note in Notes.Where(n => string.IsNullOrWhiteSpace(n.Tag)))
            note.Tag = "默认";
        foreach (var tag in Notes.Select(n => n.Tag).Where(t => !Tags.Contains(t)))
            Tags.Add(tag);
        if (Notes.Count == 0)
            Notes = [new StickyNote()];
    }

    public StickyNote AddNote(string text, string tag)
    {
        var now = DateTimeOffset.Now;
        var note = new StickyNote { Text = text, Tag = NormalizeTag(tag), CreatedAt = now, UpdatedAt = now };
        Notes = [.. Notes, note];
        return note;
    }

    public bool DeleteNote(string id)
    {
        var victim = Notes.FirstOrDefault(n => n.Id == id);
        if (victim is null)
            return false;

        Notes = Notes.Where(n => n.Id != id).ToList();
        _lastDeleted = victim;
        return true;
    }

    /// <summary>Restores the most recently deleted note (same-session undo).</summary>
    public StickyNote? UndoDeleteNote()
    {
        if (_lastDeleted is null)
            return null;

        if (!Notes.Contains(_lastDeleted))
            Notes = [.. Notes, _lastDeleted];

        var restored = _lastDeleted;
        _lastDeleted = null;
        return restored;
    }

    /// <summary>Returns true when the text actually changed.</summary>
    public bool SetText(string id, string text)
    {
        var note = Notes.FirstOrDefault(n => n.Id == id);
        if (note is null || note.Text == text)
            return false;

        note.Text = text;
        note.UpdatedAt = DateTimeOffset.Now;
        return true;
    }

    public void SetDone(string id, bool done)
    {
        var note = Notes.FirstOrDefault(n => n.Id == id);
        if (note is null || note.IsDone == done)
            return;

        note.IsDone = done;
        note.UpdatedAt = DateTimeOffset.Now;
    }

    /// <summary>Moves a note to another tag; unknown tags are registered on the fly.</summary>
    public void SetTag(string id, string tag)
    {
        var note = Notes.FirstOrDefault(n => n.Id == id);
        if (note is null)
            return;

        tag = NormalizeTag(tag);
        note.Tag = tag;
        note.UpdatedAt = DateTimeOffset.Now;
    }

    /// <summary>Ensures the tag exists; returns the canonical name (trims, falls back to 默认).</summary>
    public string NormalizeTag(string tag)
    {
        tag = string.IsNullOrWhiteSpace(tag) ? "默认" : tag.Trim();
        if (!Tags.Contains(tag))
            Tags.Add(tag);
        return tag;
    }

    public IReadOnlyList<StickyNote> FilteredBy(string? tag) =>
        string.IsNullOrWhiteSpace(tag) || tag == "全部"
            ? Notes
            : Notes.Where(n => n.Tag == tag).ToList();

    public void SaveNow() =>
        Core.JsonFile.Save(_filePath, new NoteFile { Notes = [.. Notes], Tags = [.. Tags] });
}
