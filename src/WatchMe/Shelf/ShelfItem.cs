using System.IO;
using System.Text.Json.Serialization;

namespace WatchMe.Shelf;

public enum ShelfItemAvailability
{
    Ok,
    Missing,
}

public sealed class ShelfItem
{
    [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;

    [JsonPropertyName("addedAt")] public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;

    [JsonIgnore] public string FileName => System.IO.Path.GetFileName(Path);

    [JsonIgnore] public ShelfItemAvailability Availability { get; set; } = ShelfItemAvailability.Ok;
}

public sealed class ShelfFile
{
    [JsonPropertyName("version")] public int Version { get; set; } = 1;

    [JsonPropertyName("items")] public List<ShelfItem> Items { get; set; } = [];
}

/// <summary>
/// File shelf: stores path references only (no copies), dedupes, and caps at 100 items —
/// dropping the oldest when full, mirroring the macOS original.
/// </summary>
public sealed class FileShelfStore
{
    public const int MaxItems = 100;

    private readonly string _filePath;

    public FileShelfStore(string filePath) => _filePath = filePath;

    public static FileShelfStore Default() => new(AppPaths.ShelfFile);

    public IReadOnlyList<ShelfItem> Items { get; private set; } = [];

    public void Load()
    {
        var file = Core.JsonFile.TryLoad<ShelfFile>(_filePath);
        Items = file?.Items.Where(i => i is not null && !string.IsNullOrEmpty(i.Path)).ToList() ?? [];
        RefreshAvailability();
    }

    /// <summary>Adds paths (ignoring duplicates already on the shelf). Returns the newly added items.</summary>
    public IReadOnlyList<ShelfItem> AddRange(IEnumerable<string> paths)
    {
        var existing = new HashSet<string>(Items.Select(i => i.Path), StringComparer.OrdinalIgnoreCase);
        var added = new List<ShelfItem>();
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !existing.Add(path.TrimEnd('\\')))
                continue;

            var item = new ShelfItem { Path = path };
            added.Add(item);
        }

        if (added.Count == 0)
            return added;

        var all = Items.Concat(added).ToList();
        if (all.Count > MaxItems)
            all = all.TakeLast(MaxItems).ToList();

        Items = all;
        RefreshAvailability();
        return added;
    }

    public void Remove(IEnumerable<ShelfItem> items)
    {
        var doomed = items.Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Items = Items.Where(i => !doomed.Contains(i.Path)).ToList();
    }

    public void RefreshAvailability()
    {
        foreach (var item in Items)
            item.Availability = File.Exists(item.Path) || Directory.Exists(item.Path)
                ? ShelfItemAvailability.Ok
                : ShelfItemAvailability.Missing;
    }

    public void SaveNow() => Core.JsonFile.Save(_filePath, new ShelfFile { Items = [.. Items] });
}
