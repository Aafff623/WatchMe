using System.IO;
using WatchMe.Notes;

namespace WatchMe.Tests;

public class NoteStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"watchme-notes-{Guid.NewGuid():N}.json");

    private NoteStore NewStore() => new(_path);

    [Fact]
    public void Load_CreatesInitialNote_WhenFileMissing()
    {
        var store = NewStore();
        store.Load();
        Assert.Single(store.Notes);
    }

    [Fact]
    public void CreateNote_DeleteNote_UndoDelete_RoundTrips()
    {
        var store = NewStore();
        store.Load();
        var note = store.CreateNote();
        store.SetText(note.Id, "second note");

        Assert.True(store.DeleteNote(note.Id));
        Assert.Single(store.Notes);

        var restored = store.UndoDeleteNote();
        Assert.NotNull(restored);
        Assert.Equal("second note", restored!.Text);
        Assert.Equal(2, store.Notes.Count);
    }

    [Fact]
    public void DeleteLastNote_AlwaysKeepsOne()
    {
        var store = NewStore();
        store.Load();
        var only = store.Notes[0];
        Assert.True(store.DeleteNote(only.Id));
        Assert.Single(store.Notes);
        Assert.NotEqual(only.Id, store.Notes[0].Id);
    }

    [Fact]
    public void UndoDelete_WithoutPriorDelete_ReturnsNull()
    {
        var store = NewStore();
        store.Load();
        Assert.Null(store.UndoDeleteNote());
    }

    [Fact]
    public void SaveNow_SurvivesReload()
    {
        var store = NewStore();
        store.Load();
        var note = store.CreateNote();
        store.SetText(note.Id, "# 标题\n正文");
        store.SaveNow();

        var reloaded = NewStore();
        reloaded.Load();
        Assert.Equal(2, reloaded.Notes.Count);
        Assert.Equal("# 标题\n正文", reloaded.Notes.First(n => n.Id == note.Id).Text);
    }

    [Fact]
    public void Title_DerivesFromFirstMeaningfulLine_StrippingMarkers()
    {
        var note = new Note { Text = "\n# Hello *World*\nsecond line" };
        Assert.Equal("Hello *World*", note.Title);
    }

    [Fact]
    public void Title_TruncatesLongLines()
    {
        var note = new Note { Text = new string('x', 60) };
        Assert.EndsWith("…", note.Title);
        Assert.True(note.Title.Length <= 26);
    }

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
