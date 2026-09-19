using System.Text.Json.Serialization;

namespace WatchMe.Notes;

public sealed class Note
{
    [JsonPropertyName("id")] public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;

    [JsonPropertyName("savedAt")] public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>Display title: first non-empty line, stripped of leading markdown markers.</summary>
    [JsonIgnore]
    public string Title
    {
        get
        {
            foreach (var raw in Text.Split('\n'))
            {
                var line = raw.Trim().TrimStart('#', '>', '-', '*', '+', '`');
                line = line.TrimStart();
                if (line.Length == 0)
                    continue;

                if (line.Length > 24)
                    line = line[..24].TrimEnd() + "…";
                return line.Length > 0 ? line : "New Note";
            }

            return "New Note";
        }
    }
}
