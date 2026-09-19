using System.IO;
using System.Security.Cryptography;

namespace WatchMe.Notes;

/// <summary>Persists pasted note images under %APPDATA%\WatchMe\Images and returns markdown-ready paths.</summary>
public sealed class NoteImageStore
{
    private readonly string _directory;

    public NoteImageStore(string directory) => _directory = directory;

    public static NoteImageStore Default() => new(AppPaths.NoteImagesDir);

    public string SavePng(byte[] pngBytes)
    {
        Directory.CreateDirectory(_directory);
        var name = $"img-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Convert.ToHexString(SHA256.HashData(pngBytes))[..6]}.png";
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, pngBytes);
        return path;
    }

    /// <summary>Markdown link with forward slashes so the MdXaml renderer can resolve it.</summary>
    public static string ToMarkdownLink(string path, string label) =>
        $"![{label}]({path.Replace('\\', '/')})";
}
