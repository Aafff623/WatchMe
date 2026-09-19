using System.IO;
using WatchMe.Clipboard;

namespace WatchMe.Tests;

public class ClipboardHistoryStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"watchme-clip-{Guid.NewGuid():N}");
    private string EntriesFile => Path.Combine(_dir, "entries.json");
    private string ImagesDir => Path.Combine(_dir, "images");

    private ClipboardHistoryStore NewStore(int cap = 50) => new(EntriesFile, ImagesDir, cap);

    [Fact]
    public void AddText_Prepends_AndSuppressesConsecutiveDuplicates()
    {
        var store = NewStore();
        Assert.NotNull(store.AddText("first"));
        Assert.NotNull(store.AddText("second"));
        Assert.Null(store.AddText("second")); // consecutive duplicate suppressed
        Assert.NotNull(store.AddText("first")); // re-capture after moving away is kept

        Assert.Equal(["first", "second", "first"], store.Entries.Select(e => e.Text).ToArray());
    }

    [Fact]
    public void AddText_EmptyText_ReturnsNull()
    {
        var store = NewStore();
        Assert.Null(store.AddText(""));
        Assert.Empty(store.Entries);
    }

    [Fact]
    public void Cap_PruneOldest_BeyondMax()
    {
        var store = NewStore(cap: 10);
        for (var i = 0; i < 15; i++)
            Assert.NotNull(store.AddText($"note-{i}"));

        Assert.Equal(10, store.Entries.Count);
        Assert.Equal("note-14", store.Entries[0].Text);
        Assert.Equal("note-5", store.Entries[^1].Text);
    }

    [Fact]
    public void AddImage_WritesFile_AndDedupesByHash()
    {
        var store = NewStore();
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };

        Assert.NotNull(store.AddImage(png));
        Assert.Null(store.AddImage(png)); // same content hash → suppressed

        Assert.Single(store.Entries);
        Assert.True(File.Exists(store.Entries[0].ImageFile));
        Assert.True(Directory.GetFiles(ImagesDir).Length == 1);
    }

    [Fact]
    public void Search_MatchesCaseInsensitively_AcrossText()
    {
        var store = NewStore();
        store.AddText("Hello World");
        store.AddText("goodbye");
        store.AddText("WORLD peace");

        var hits = store.Search("world");
        Assert.Equal(2, hits.Count);
    }

    [Fact]
    public void Remove_ImageEntry_DeletesFile()
    {
        var store = NewStore();
        var entry = store.AddImage([1, 2, 3, 4])!;
        var file = entry.ImageFile!;
        Assert.True(File.Exists(file));

        store.Remove(entry);
        Assert.False(File.Exists(file));
        Assert.Empty(store.Entries);
    }

    [Fact]
    public void SaveNow_SurvivesReload()
    {
        var store = NewStore();
        store.AddText("persist me");
        store.SaveNow();

        var reloaded = NewStore();
        reloaded.Load();
        Assert.Single(reloaded.Entries);
        Assert.Equal("persist me", reloaded.Entries[0].Text);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
