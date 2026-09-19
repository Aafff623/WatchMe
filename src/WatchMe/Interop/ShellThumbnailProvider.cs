using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace WatchMe.Interop;

/// <summary>
/// Shell thumbnails for shelf chips via IShellItemImageFactory, with an icon-only fallback —
/// the Windows counterpart of the macOS QLThumbnailGenerator path.
/// </summary>
public static class ShellThumbnailProvider
{
    private const int SIOI_ICONONLY_FALLBACK = 0x4; // SIIGBF_ICONONLY

    public static BitmapSource? GetThumbnail(string path, int pixelSize)
    {
        try
        {
            if (TryGetImage(path, pixelSize, 0x0 /*RESIZETOFIT*/, out var best))
                return best;

            if (TryGetImage(path, pixelSize, SIOI_ICONONLY_FALLBACK, out var icon))
                return icon;
        }
        catch (COMException)
        {
            // Missing shell items throw here; the chip falls back to its default glyph.
        }

        return null;
    }

    private static bool TryGetImage(string path, int pixelSize, int flags, out BitmapSource? image)
    {
        image = null;
        if (!System.IO.File.Exists(path) && !System.IO.Directory.Exists(path))
            return false;

        var factory = CreateItem(path);
        if (factory is null)
            return false;

        var size = new SIZE { X = pixelSize, Y = pixelSize };
        if (factory.GetImage(size, flags, out var hBitmap) != 0 || hBitmap == IntPtr.Zero)
            return false;

        try
        {
            image = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return true;
        }
        finally
        {
            _ = NativeMethods.DeleteObject(hBitmap);
        }
    }

    private static IShellItemImageFactory? CreateItem(string path)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory);
        return factory;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string pszPath, IntPtr pbc, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? ppv);

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int X, Y;
    }

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, int flags, out IntPtr phbm);
    }
}
