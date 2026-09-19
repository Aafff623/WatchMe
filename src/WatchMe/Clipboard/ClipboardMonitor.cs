using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using WatchMe.Interop;

namespace WatchMe.Clipboard;

/// <summary>Listens for WM_CLIPBOARDUPDATE and feeds new clipboard content into the history store.</summary>
public sealed class ClipboardMonitor : IDisposable
{
    private readonly MessageWindow _window = new("WatchMe.ClipboardMonitor");
    private readonly ClipboardHistoryStore _store;
    private bool _suppressNext;

    public ClipboardMonitor(ClipboardHistoryStore store)
    {
        _store = store;
        _window.MessageReceived += OnMessage;
    }

    public void Start()
    {
        _ = NativeMethods.AddClipboardFormatListener(_window.Handle);
        _ = NativeMethods.GetClipboardSequenceNumber(); // baseline so we don't ingest stale content
    }

    /// <summary>Suppresses capturing the next update (used when we ourselves write to the clipboard).</summary>
    public void SuppressNextCapture() => _suppressNext = true;

    private bool OnMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg != NativeMethods.WM_CLIPBOARDUPDATE)
            return false;

        if (_suppressNext)
        {
            _suppressNext = false;
            return true;
        }

        Capture();
        return true;
    }

    private void Capture()
    {
        try
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                var text = System.Windows.Clipboard.GetText();
                if (_store.AddText(text) is not null)
                    Persist();
            }
            else if (System.Windows.Clipboard.ContainsImage())
            {
                var source = System.Windows.Clipboard.GetImage();
                if (source is not null && ToPngBytes(source) is { Length: > 0 } png)
                {
                    if (_store.AddImage(png) is not null)
                        Persist();
                }
            }
        }
        catch (COMException)
        {
            // The clipboard can be locked by another process; we simply skip that update.
        }
    }

    /// <summary>Persists right away so a crash or forced kill never loses history.</summary>
    private void Persist()
    {
        try
        {
            _store.SaveNow();
        }
        catch (IOException)
        {
            // Best effort — the next capture retries.
        }
    }

    private static byte[] ToPngBytes(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    public void Dispose()
    {
        _ = NativeMethods.RemoveClipboardFormatListener(_window.Handle);
        _window.MessageReceived -= OnMessage;
        _window.Dispose();
    }
}
