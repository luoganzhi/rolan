using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;
using Rolan.Helpers;

namespace Rolan.Helpers;

internal static class IconHelper
{
    public static BitmapSource? ExtractIcon(string filePath)
    {
        try
        {
            var shfi = new NativeMethods.SHFILEINFO();
            var flags = NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON;

            // 对可执行文件(.exe/.lnk)直接提取，其他文件/文件夹用文件属性
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            uint attributes;
            if (ext is ".exe" or ".lnk" or ".url")
            {
                attributes = 0;
            }
            else if (filePath.StartsWith("http://") || filePath.StartsWith("https://"))
            {
                // URL 使用默认浏览器图标或链接图标
                flags |= NativeMethods.SHGFI_USEFILEATTRIBUTES;
                attributes = 0;
                filePath = ".url";
            }
            else
            {
                flags |= NativeMethods.SHGFI_USEFILEATTRIBUTES;
                attributes = 0x80; // FILE_ATTRIBUTE_NORMAL
            }

            var ret = NativeMethods.SHGetFileInfo(filePath, attributes, ref shfi,
                (uint)System.Runtime.InteropServices.Marshal.SizeOf(shfi), flags);

            if (ret == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
                return null;

            using var icon = Icon.FromHandle(shfi.hIcon);
            using var ms = new MemoryStream();
            icon.ToBitmap().Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Seek(0, SeekOrigin.Begin);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = ms;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            NativeMethods.DestroyIcon(shfi.hIcon);
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public static BitmapSource? LoadFromBytes(byte[]? iconData)
    {
        if (iconData == null || iconData.Length == 0) return null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = new MemoryStream(iconData);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public static byte[]? BitmapSourceToBytes(BitmapSource? source)
    {
        if (source == null) return null;
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
