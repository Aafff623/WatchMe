using System.Text.Json.Serialization;

namespace WatchMe.Notes;

public sealed class NoteFile
{
    [JsonPropertyName("version")] public int Version { get; set; } = 1;

    [JsonPropertyName("notes")] public List<Note> Notes { get; set; } = [];
}

/// <summary>
/// Multi-tab note persistence. Ported from the macOS NoteStore: atomic saves, delete with
/// single-level undo, and the guarantee that at least one note always exists.
/// </summary>
public sealed class NoteStore
{
    private readonly string _filePath;
    private Note? _lastDeleted;

    public NoteStore(string filePath) => _filePath = filePath;

    public static NoteStore Default() => new(AppPaths.NotesFile);

    public IReadOnlyList<Note> Notes { get; private set; } = [];

    public void Load()
    {
        var file = Core.JsonFile.TryLoad<NoteFile>(_filePath);
        Notes = file?.Notes.Where(n => n is not null).ToList() ?? [];
        if (Notes.Count == 0)
            Notes = [new Note()];
    }

    public Note CreateNote()
    {
        var note = new Note();
        Notes = [.. Notes, note];
        return note;
    }

    public bool DeleteNote(string id)
    {
        var victim = Notes.FirstOrDefault(n => n.Id == id);
        if (victim is null)
            return false;

        Notes = Notes.Where(n => n.Id != id).ToList();
        if (Notes.Count == 0)
            Notes = [new Note()];

        _lastDeleted = victim;
        return true;
    }

    /// <summary>Restores the most recently deleted note (same-session undo, like the macOS version).</summary>
    public Note? UndoDeleteNote()
    {
        if (_lastDeleted is null)
            return null;

        if (!Notes.Contains(_lastDeleted))
            Notes = [.. Notes, _lastDeleted];

        var restored = _lastDeleted;
        _lastDeleted = null;
        return restored;
    }

    public void SetText(string id, string text)
    {
        var note = Notes.FirstOrDefault(n => n.Id == id);
        if (note is null || note.Text == text)
            return;

        note.Text = text;
        note.SavedAt = DateTimeOffset.Now;
    }

    public void SaveNow() => Core.JsonFile.Save(_filePath, new NoteFile { Notes = [.. Notes] });
}
