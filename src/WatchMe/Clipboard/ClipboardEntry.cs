using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace WatchMe.Clipboard;

public enum ClipboardEntryKind
{
    Text,
    Image,
}

public sealed class ClipboardEntry
{
    [JsonPropertyName("id")] public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("kind")] public ClipboardEntryKind Kind { get; set; }

    [JsonPropertyName("text")] public string? Text { get; set; }

    [JsonPropertyName("imageFile")] public string? ImageFile { get; set; }

    [JsonPropertyName("capturedAt")] public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.Now;

    [JsonIgnore] public string Display
    {
        get
        {
            if (Kind == ClipboardEntryKind.Text && Text is not null)
            {
                var oneLine = Text.Replace("\r", " ").Replace("\n", " ");
                return oneLine.Length > 120 ? oneLine[..120] + "…" : oneLine;
            }

            return ImageFile is not null ? $"🖼 {Path.GetFileName(ImageFile)}" : "(empty)";
        }
    }
}

public sealed class ClipboardHistoryFile
{
    [JsonPropertyName("version")] public int Version { get; set; } = 1;

    [JsonPropertyName("entries")] public List<ClipboardEntry> Entries { get; set; } = [];
}

/// <summary>Searchable clipboard history: consecutive-duplicate suppression, image files on disk, FIFO caps.</summary>
public sealed class ClipboardHistoryStore
{
    private readonly string _entriesFile;
    private readonly string _imagesDir;
    private readonly int _maxEntries;

    public ClipboardHistoryStore(string entriesFile, string imagesDir, int maxEntries)
    {
        _entriesFile = entriesFile;
        _imagesDir = imagesDir;
        _maxEntries = Math.Clamp(maxEntries, 10, 1000);
    }

    public static ClipboardHistoryStore Default(int maxEntries)
        => new(AppPaths.ClipboardEntriesFile, AppPaths.ClipboardImagesDir, maxEntries);

    public IReadOnlyList<ClipboardEntry> Entries { get; private set; } = [];

    public void Load()
    {
        var file = Core.JsonFile.TryLoad<ClipboardHistoryFile>(_entriesFile);
        Entries = file?.Entries.Where(e => e is not null).ToList() ?? [];
    }

    /// <summary>Returns the stored entry, or null when suppressed as a consecutive duplicate.</summary>
    public ClipboardEntry? AddText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        if (Entries.Count > 0 && Entries[0].Kind == ClipboardEntryKind.Text && Entries[0].Text == text)
            return null;

        var entry = new ClipboardEntry { Kind = ClipboardEntryKind.Text, Text = text };
        Prepend(entry);
        return entry;
    }

    public ClipboardEntry? AddImage(byte[] pngBytes)
    {
        if (pngBytes.Length == 0)
            return null;

        var name = Convert.ToHexString(SHA256.HashData(pngBytes))[..16] + ".png";
        var path = Path.Combine(_imagesDir, name);
        if (Entries.Count > 0 && Entries[0].Kind == ClipboardEntryKind.Image
            && string.Equals(Path.GetFileName(Entries[0].ImageFile), name, StringComparison.OrdinalIgnoreCase))
            return null;

        Directory.CreateDirectory(_imagesDir);
        if (!File.Exists(path))
            File.WriteAllBytes(path, pngBytes);

        var entry = new ClipboardEntry { Kind = ClipboardEntryKind.Image, ImageFile = path };
        Prepend(entry);
        return entry;
    }

    public void Remove(ClipboardEntry entry)
    {
        Entries = Entries.Where(e => e.Id != entry.Id).ToList();
        if (entry.Kind == ClipboardEntryKind.Image && entry.ImageFile is not null)
        {
            try
            {
                if (File.Exists(entry.ImageFile))
                    File.Delete(entry.ImageFile);
            }
            catch (IOException)
            {
                // A locked image file is not worth failing the removal over.
            }
        }
    }

    public void Clear()
    {
        foreach (var image in Entries.Where(e => e.Kind == ClipboardEntryKind.Image))
        {
            try
            {
                if (image.ImageFile is not null && File.Exists(image.ImageFile))
                    File.Delete(image.ImageFile);
            }
            catch (IOException)
            {
            }
        }

        Entries = [];
    }

    public IReadOnlyList<ClipboardEntry> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Entries;

        return Entries.Where(e =>
            e.Display.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (e.Text?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
    }

    public void SaveNow() => Core.JsonFile.Save(_entriesFile, new ClipboardHistoryFile { Entries = [.. Entries] });

    private void Prepend(ClipboardEntry entry)
    {
        var list = new List<ClipboardEntry> { entry };
        list.AddRange(Entries);
        if (list.Count > _maxEntries)
        {
            foreach (var dropped in list.Skip(_maxEntries).Where(e => e.Kind == ClipboardEntryKind.Image))
            {
                try
                {
                    if (dropped.ImageFile is not null && File.Exists(dropped.ImageFile))
                        File.Delete(dropped.ImageFile);
                }
                catch (IOException)
                {
                }
            }

            list = list.Take(_maxEntries).ToList();
        }

        Entries = list;
    }
}
