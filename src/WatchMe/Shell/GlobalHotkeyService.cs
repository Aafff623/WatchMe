using WatchMe.Core;
using WatchMe.Interop;

namespace WatchMe.Shell;

/// <summary>Registers a configurable global hotkey (default Ctrl+Alt+W) that toggles the notch panel.</summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x574D; // "WM"

    private readonly MessageWindow _window = new("WatchMe.Hotkey");

    public event Action? HotkeyPressed;

    private uint _registeredVk;
    private uint _registeredModifiers;

    public GlobalHotkeyService() => _window.MessageReceived += OnMessage;

    /// <summary>Updates the registered hotkey. Returns false when the combination is unavailable.</summary>
    public bool Apply(string hotkeyDisplay)
    {
        Unregister();
        if (!HotkeyPattern.TryParse(hotkeyDisplay, out var modifiers, out var vk))
            return false;

        var winModifiers = ToWinModifiers(modifiers);
        if (!NativeMethods.RegisterHotKey(_window.Handle, HotkeyId, winModifiers, vk))
            return false;

        _registeredVk = vk;
        _registeredModifiers = winModifiers;
        return true;
    }

    private void Unregister()
    {
        if (_registeredVk == 0)
            return;

        _ = NativeMethods.UnregisterHotKey(_window.Handle, HotkeyId);
        _registeredVk = 0;
        _registeredModifiers = 0;
    }

    private bool OnMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            return true;
        }

        return false;
    }

    private static uint ToWinModifiers(HotkeyModifiers modifiers)
    {
        uint value = 0;
        if (modifiers.HasFlag(HotkeyModifiers.Alt))
            value |= NativeMethods.MOD_ALT;
        if (modifiers.HasFlag(HotkeyModifiers.Control))
            value |= NativeMethods.MOD_CONTROL;
        if (modifiers.HasFlag(HotkeyModifiers.Shift))
            value |= NativeMethods.MOD_SHIFT;
        if (modifiers.HasFlag(HotkeyModifiers.Win))
            value |= NativeMethods.MOD_WIN;
        return value;
    }

    public void Dispose()
    {
        Unregister();
        _window.MessageReceived -= OnMessage;
        _window.Dispose();
    }
}
