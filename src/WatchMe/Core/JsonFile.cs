using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WatchMe.Core;

/// <summary>Atomic JSON file IO: writes to a temp file first so a crash mid-save never corrupts data.</summary>
public static class JsonFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public static T? TryLoad<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path))
                return null;

            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<T>(stream, Options);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Save<T>(string path, T value) where T : class
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        using (var stream = File.Create(tmp))
        {
            JsonSerializer.Serialize(stream, value, Options);
        }

        File.Move(tmp, path, overwrite: true);
    }
}
