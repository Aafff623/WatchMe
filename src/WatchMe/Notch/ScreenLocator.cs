using System.Windows;
using System.Windows.Interop;
using WatchMe.Interop;
using WatchMe.Settings;

namespace WatchMe.Notch;

/// <summary>
/// Places notch windows in physical pixels via SetWindowPos: pick the target monitor,
/// then top-center the window on it. This sidesteps WPF DIP math on mixed-DPI setups.
/// </summary>
public static class ScreenLocator
{
    public static IReadOnlyList<NativeMethods.MonitorInfo> Monitors() => NativeMethods.EnumerateMonitors();

    public static NativeMethods.MonitorInfo ResolveMonitor(AppSettings settings)
    {
        var monitors = Monitors();
        if (monitors.Count == 0)
        {
            // Degenerate fallback: treat the whole virtual screen as one monitor via the primary window.
            return new NativeMethods.MonitorInfo("PRIMARY_FALLBACK", true,
                new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 });
        }

        if (!string.IsNullOrEmpty(settings.PreferredScreenDeviceName))
        {
            var preferred = monitors.FirstOrDefault(m => m.DeviceName == settings.PreferredScreenDeviceName);
            if (preferred.DeviceName == settings.PreferredScreenDeviceName)
                return preferred;
        }

        return monitors.First(m => m.IsPrimary);
    }

    /// <summary>Top-centers the window on the monitor's top edge, in physical pixels.</summary>
    public static void PlaceTopCenter(IntPtr hwnd, NativeMethods.MonitorInfo monitor, double widthDip)
    {
        if (hwnd == IntPtr.Zero)
            return;

        // The hwnd may not be laid out yet (GetWindowRect would read 0), so derive the
        // physical width from the window's own DPI instead.
        var dpi = NativeMethods.GetDpiForWindow(hwnd);
        if (dpi == 0)
            dpi = 96;
        var physicalWidth = (int)Math.Round(widthDip * dpi / 96.0);
        var x = monitor.PhysicalBounds.Left + (monitor.PhysicalBounds.Width - physicalWidth) / 2;
        var y = monitor.PhysicalBounds.Top;
        _ = NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    public static void PlaceTopCenter(Window window, NativeMethods.MonitorInfo monitor)
    {
        var helper = new WindowInteropHelper(window);
        helper.EnsureHandle();
        PlaceTopCenter(helper.Handle, monitor, window.Width);
    }
}
