using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WatchMe.Shelf;

/// <summary>
/// Space-bar Quick Look: images, text files, and best-effort media playback, with ◀▶
/// navigation across the shelf — the Windows stand-in for the macOS QLPreviewPanel.
/// </summary>
public partial class QuickLookWindow : Window
{
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico", ".tiff"];
    private static readonly string[] TextExtensions = [".txt", ".md", ".log", ".json", ".xml", ".yml", ".yaml", ".cs", ".js", ".ts", ".py", ".html", ".css", ".sql", ".csv", ".ini", ".bat", ".ps1", ".sh"];
    private static readonly string[] MediaExtensions = [".mp4", ".mp3", ".wav", ".m4a", ".wmv", ".avi", ".mkv", ".flac", ".ogg"];
    private const int MaxTextBytes = 128 * 1024;

    private IReadOnlyList<ShelfItem> _items = [];
    private int _index;

    public QuickLookWindow() => InitializeComponent();

    public void OpenOn(IReadOnlyList<ShelfItem> items, int index)
    {
        _items = items;
        _index = Math.Clamp(index, 0, Math.Max(0, items.Count - 1));
        Render();
        Show();
        Activate();
        Focus();
    }

    private void Render()    {
        if (_items.Count == 0)
        {
            Hide();
            return;
        }

        var item = _items[_index];
        FileNameText.Text = item.FileName;
        MetaText.Text = DescribeMeta(item);
        Host.Content = BuildContent(item);
    }

    private static string DescribeMeta(ShelfItem item)
    {
        try
        {
            var info = new FileInfo(item.Path);
            return info.Exists
                ? $"{FormatSize(info.Length)} · {info.LastWriteTime:yyyy-MM-dd HH:mm}"
                : "文件不可用";
        }
        catch (IOException)
        {
            return "文件不可用";
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1 << 30 => $"{bytes / (double)(1 << 30):F1} GB",
        >= 1 << 20 => $"{bytes / (double)(1 << 20):F1} MB",
        >= 1 << 10 => $"{bytes / (double)(1 << 10):F1} KB",
        _ => $"{bytes} B",
    };

    private object BuildContent(ShelfItem item)
    {
        var ext = Path.GetExtension(item.Path).ToLowerInvariant();
        if (item.Availability == ShelfItemAvailability.Missing || !File.Exists(item.Path))
            return InfoPanel(item, "文件不存在或已被移动");

        if (ImageExtensions.Contains(ext))
        {
            try
            {
                var image = new Image
                {
                    Source = BitmapFrame.Create(new Uri(item.Path), BitmapCreateOptions.None, BitmapCacheOption.OnLoad),
                    Stretch = Stretch.Uniform,
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                return image;
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or OutOfMemoryException)
            {
                return InfoPanel(item, "图片无法解码");
            }
        }

        if (TextExtensions.Contains(ext))
            return TextPanel(item);

        if (MediaExtensions.Contains(ext))
        {
            var media = new MediaElement
            {
                Source = new Uri(item.Path),
                LoadedBehavior = MediaState.Play,
                UnloadedBehavior = MediaState.Close,
                Stretch = Stretch.Uniform,
            };
            return media;
        }

        return InfoPanel(item, "此格式暂不支持预览，双击文件用默认程序打开");
    }

    private static object TextPanel(ShelfItem item)
    {
        string text;
        try
        {
            using var reader = new StreamReader(item.Path);
            var buffer = new char[MaxTextBytes];
            var read = reader.ReadBlock(buffer, 0, buffer.Length);
            text = new string(buffer, 0, read);
            if (read == buffer.Length && reader.Peek() >= 0)
                text += "\n… (内容过长，仅预览前 128 KB)";
        }
        catch (IOException)
        {
            return InfoPanel(item, "文件无法读取");
        }

        return new TextBox
        {
            Text = text,
            IsReadOnly = true,
            FontFamily = new FontFamily("Cascadia Mono,Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = FindResourceFromWindow("Brush.TextPrimary"),
            BorderThickness = new Thickness(0),
        };
    }

    private static object InfoPanel(ShelfItem item, string hint) => new StackPanel
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    }.Also(panel =>
    {
        panel.Children.Add(new TextBlock
        {
            Text = "📄",
            FontSize = 56,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        panel.Children.Add(new TextBlock
        {
            Text = item.FileName,
            FontSize = 13,
            Foreground = FindResourceFromWindow("Brush.TextPrimary"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 380,
        });
        panel.Children.Add(new TextBlock
        {
            Text = hint,
            FontSize = 11,
            Foreground = FindResourceFromWindow("Brush.TextSecondary"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0),
        });
    });

    private static Brush FindResourceFromWindow(string key) =>
        Application.Current.TryFindResource(key) as Brush ?? System.Windows.Media.Brushes.White;

    private void OnPrev(object sender, RoutedEventArgs e) => Move(-1);

    private void OnNext(object sender, RoutedEventArgs e) => Move(1);

    private void Move(int delta)
    {
        if (_items.Count == 0)
            return;

        _index = (_index + delta + _items.Count) % _items.Count;
        Render();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Hide();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.Space)
        {
            Hide();
            e.Handled = true;
        }
        else if (e.Key == Key.Left)
        {
            Move(-1);
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            Move(1);
            e.Handled = true;
        }
    }

    private void OnDeactivated(object sender, EventArgs e)
    {
        // Clicking elsewhere closes the preview, like the native Quick Look.
        if (IsVisible)
            Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        Host.Content = null;
        base.OnClosed(e);
    }
}

internal static class QuickLookExtensions
{
    public static T Also<T>(this T self, Action<T> configure)
    {
        configure(self);
        return self;
    }
}
