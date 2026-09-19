using System.Windows.Interop;
using System.Windows.Threading;

namespace WatchMe.Interop;

/// <summary>
/// A message-only Win32 window (parented to HWND_MESSAGE) that receives broadcast messages —
/// the shared transport for global hotkeys and clipboard notifications.
/// </summary>
public sealed class MessageWindow : IDisposable
{
    private readonly HwndSource _source;

    public MessageWindow(string name)
    {
        // HWND_MESSAGE (-3): a message-only window that never appears on screen.
        var parameters = new HwndSourceParameters(name) { ParentWindow = new IntPtr(-3) };
        _source = new HwndSource(parameters);
        _source.AddHook(OnMessage);
    }

    public IntPtr Handle => _source.Handle;

    /// <summary>Return true from a handler to mark the message handled.</summary>
    public event Func<int, IntPtr, IntPtr, bool>? MessageReceived;

    private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (MessageReceived?.Invoke(msg, wParam, lParam) == true)
            handled = true;

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _source.RemoveHook(OnMessage);
        _source.Dispose();
    }
}
