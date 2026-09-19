using System.Text.Json.Serialization;

namespace WatchMe.Notes;

/// <summary>One sticky note: free text, a category tag, and an optional done flag (待办).</summary>
public sealed class StickyNote
{
    [JsonPropertyName("id")] public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;

    [JsonPropertyName("tag")] public string Tag { get; set; } = "默认";

    [JsonPropertyName("createdAt")] public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")] public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("done")] public bool IsDone { get; set; }

    /// <summary>Old v1 files stored the timestamp under "savedAt"; keep it during migration.</summary>
    [JsonPropertyName("savedAt")] public DateTimeOffset? LegacySavedAt
    {
        get => null;
        set
        {
            if (value.HasValue && UpdatedAt == default)
                UpdatedAt = value.Value;
        }
    }
}

public sealed class NoteFile
{
    [JsonPropertyName("version")] public int Version { get; set; } = 2;

    [JsonPropertyName("notes")] public List<StickyNote> Notes { get; set; } = [];

    [JsonPropertyName("tags")] public List<string> Tags { get; set; } = [];
}
