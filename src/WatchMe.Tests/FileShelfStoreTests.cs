using System.IO;
using WatchMe.Shelf;

namespace WatchMe.Tests;

public class FileShelfStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"watchme-shelf-{Guid.NewGuid():N}.json");
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"watchme-real-{Guid.NewGuid():N}.txt");

    private FileShelfStore NewStore() => new(_path);

    public FileShelfStoreTests() => File.WriteAllText(_tempFile, "exists");

    [Fact]
    public void AddRange_Deduplicates_CaseInsensitively()
    {
        var store = NewStore();
        store.Load();
        store.AddRange([_tempFile]);
        store.AddRange([_tempFile.ToUpperInvariant()]);

        Assert.Single(store.Items);
    }

    [Fact]
    public void AddRange_CapsAt100_KeepingNewest()
    {
        var store = NewStore();
        store.Load();
        var paths = Enumerable.Range(0, 105).Select(i => $@"C:\fake\file-{i:D3}.txt").ToArray();
        store.AddRange(paths);

        Assert.Equal(FileShelfStore.MaxItems, store.Items.Count);
        Assert.DoesNotContain(store.Items, i => i.Path.EndsWith("file-000.txt"));
        Assert.Contains(store.Items, i => i.Path.EndsWith("file-104.txt"));
    }

    [Fact]
    public void RefreshAvailability_MarksMissingFiles()
    {
        var store = NewStore();
        store.Load();
        store.AddRange([_tempFile, @"C:\definitely\not\here.bin"]);
        store.RefreshAvailability();

        Assert.Equal(ShelfItemAvailability.Ok, store.Items.First(i => i.Path == _tempFile).Availability);
        Assert.Equal(ShelfItemAvailability.Missing,
            store.Items.First(i => i.Path == @"C:\definitely\not\here.bin").Availability);
    }

    [Fact]
    public void Remove_DeletesSelectedPaths()
    {
        var store = NewStore();
        store.Load();
        store.AddRange([_tempFile, @"C:\fake\a.txt", @"C:\fake\b.txt"]);
        store.Remove(store.Items.Where(i => i.Path.StartsWith(@"C:\fake")));

        Assert.Single(store.Items);
        Assert.Equal(_tempFile, store.Items[0].Path);
    }

    [Fact]
    public void SaveNow_SurvivesReload()
    {
        var store = NewStore();
        store.Load();
        store.AddRange([_tempFile]);
        store.SaveNow();

        var reloaded = NewStore();
        reloaded.Load();
        Assert.Single(reloaded.Items);
        Assert.Equal(_tempFile, reloaded.Items[0].Path);
    }

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }
}
