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
        Assert.Equal(NoteStore.DefaultTags, store.Tags);
    }

    [Fact]
    public void AddNote_AssignsTag_AndRegistersUnknownTag()
    {
        var store = NewStore();
        store.Load();
        store.AddNote("买牛奶", "生活");
        store.AddNote("写周报", "工作");

        Assert.Equal(3, store.Notes.Count);
        Assert.Equal("生活", store.Notes[1].Tag);
        Assert.Contains("生活", store.Tags);
        Assert.Contains("工作", store.Tags);
    }

    [Fact]
    public void AddNote_BlankTag_FallsBackToDefault()
    {
        var store = NewStore();
        store.Load();
        store.AddNote("quick", "  ");
        Assert.Equal("默认", store.Notes[1].Tag);
    }

    [Fact]
    public void DeleteNote_UndoDelete_RoundTrips()
    {
        var store = NewStore();
        store.Load();
        var note = store.AddNote("temporary", "工作");
        Assert.Equal(2, store.Notes.Count);

        Assert.True(store.DeleteNote(note.Id));
        Assert.Single(store.Notes);

        var restored = store.UndoDeleteNote();
        Assert.NotNull(restored);
        Assert.Equal("temporary", restored!.Text);
        Assert.Equal(2, store.Notes.Count);
    }

    [Fact]
    public void UndoDelete_WithoutPriorDelete_ReturnsNull()
    {
        var store = NewStore();
        store.Load();
        Assert.Null(store.UndoDeleteNote());
    }

    [Fact]
    public void SetDone_TogglesFlag()
    {
        var store = NewStore();
        store.Load();
        var note = store.AddNote("todo item", "工作");
        Assert.False(note.IsDone);

        store.SetDone(note.Id, true);
        Assert.True(store.Notes.First(n => n.Id == note.Id).IsDone);

        store.SetDone(note.Id, false);
        Assert.False(store.Notes.First(n => n.Id == note.Id).IsDone);
    }

    [Fact]
    public void SetTag_MovesNote_AndRegistersNewTag()
    {
        var store = NewStore();
        store.Load();
        var note = store.AddNote("flexible", "默认");
        store.SetTag(note.Id, "灵感");

        Assert.Equal("灵感", store.Notes.First(n => n.Id == note.Id).Tag);
        Assert.Contains("灵感", store.Tags);
    }

    [Fact]
    public void FilteredBy_ReturnsOnlyMatchingTag()
    {
        var store = NewStore();
        store.Load();
        store.AddNote("a", "工作");
        store.AddNote("b", "生活");
        store.AddNote("c", "工作");

        Assert.Equal(2, store.FilteredBy("工作").Count);
        Assert.Single(store.FilteredBy("生活"));
        Assert.Equal(4, store.FilteredBy("全部").Count);
        Assert.Equal(4, store.FilteredBy(null).Count);
    }

    [Fact]
    public void SetText_UpdatesTimestamp()
    {
        var store = NewStore();
        store.Load();
        var note = store.AddNote("first", "默认");
        var before = note.UpdatedAt;
        store.SetText(note.Id, "second");

        var stored = store.Notes.First(n => n.Id == note.Id);
        Assert.Equal("second", stored.Text);
        Assert.True(stored.UpdatedAt >= before);
    }

    [Fact]
    public void SaveNow_SurvivesReload()
    {
        var store = NewStore();
        store.Load();
        store.AddNote("# 保留", "工作");
        store.AddNote("done one", "生活");
        store.SetDone(store.Notes[2].Id, true);
        store.SaveNow();

        var reloaded = NewStore();
        reloaded.Load();
        Assert.Equal(3, reloaded.Notes.Count);
        Assert.Equal("# 保留", reloaded.Notes[1].Text);
        Assert.True(reloaded.Notes[2].IsDone);
        Assert.Contains("工作", reloaded.Tags);
    }

    [Fact]
    public void Load_MigratesV1Notes_WithSavedAtAndText()
    {
        const string v1Json = """
            {
              "version": 1,
              "notes": [
                { "id": "abc", "text": "legacy note", "savedAt": "2026-01-02T03:04:05+08:00" }
              ]
            }
            """;
        File.WriteAllText(_path, v1Json);

        var store = NewStore();
        store.Load();
        Assert.Single(store.Notes);
        Assert.Equal("legacy note", store.Notes[0].Text);
        Assert.Equal("默认", store.Notes[0].Tag);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(8)), store.Notes[0].UpdatedAt);
    }

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
