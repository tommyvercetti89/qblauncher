using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QBLauncherPro.Services;

public sealed record MonitorSnapshot(Rect DipBounds, BitmapSource? Screenshot);

public static class ScreenCaptureService
{
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;
    private const int LOGPIXELSX = 88;
    private const int LOGPIXELSY = 90;
    private const uint SRCCOPY = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(
        IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    public static MonitorSnapshot CaptureActiveMonitor(Window? owner)
    {
        IntPtr targetHwnd = IntPtr.Zero;
        if (owner != null && owner.IsVisible)
            targetHwnd = new WindowInteropHelper(owner).Handle;
        if (targetHwnd == IntPtr.Zero)
            targetHwnd = GetForegroundWindow();

        IntPtr hMonitor = MonitorFromWindow(targetHwnd, MONITOR_DEFAULTTONEAREST);
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

        int leftPx = 0;
        int topPx = 0;
        int widthPx = (int)SystemParameters.PrimaryScreenWidth;
        int heightPx = (int)SystemParameters.PrimaryScreenHeight;

        if (hMonitor != IntPtr.Zero && GetMonitorInfoW(hMonitor, ref mi))
        {
            leftPx = mi.rcMonitor.Left;
            topPx = mi.rcMonitor.Top;
            widthPx = Math.Max(1, mi.rcMonitor.Right - mi.rcMonitor.Left);
            heightPx = Math.Max(1, mi.rcMonitor.Bottom - mi.rcMonitor.Top);
        }

        var (scaleX, scaleY) = GetScaleForMonitor(owner, hMonitor);
        var dipBounds = new Rect(
            Math.Round(leftPx / scaleX),
            Math.Round(topPx / scaleY),
            Math.Round(widthPx / scaleX),
            Math.Round(heightPx / scaleY));

        var screenshot = CapturePhysicalRect(leftPx, topPx, widthPx, heightPx);
        return new MonitorSnapshot(dipBounds, screenshot);
    }

    private static (double scaleX, double scaleY) GetScaleForMonitor(Window? owner, IntPtr hMonitor)
    {
        try
        {
            if (hMonitor != IntPtr.Zero && GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out uint dx, out uint dy) == 0 && dx > 0 && dy > 0)
            {
                return (dx / 96.0, dy / 96.0);
            }
        }
        catch
        {
            // Older Windows or fallback handled below
        }

        if (owner != null)
        {
            try
            {
                var dpi = VisualTreeHelper.GetDpi(owner);
                if (dpi.DpiScaleX > 0 && dpi.DpiScaleY > 0)
                    return (dpi.DpiScaleX, dpi.DpiScaleY);
            }
            catch
            {
                // Ignore
            }
        }

        IntPtr screenDc = GetDC(IntPtr.Zero);
        try
        {
            int px = GetDeviceCaps(screenDc, LOGPIXELSX);
            int py = GetDeviceCaps(screenDc, LOGPIXELSY);
            return (px > 0 ? px / 96.0 : 1.0, py > 0 ? py / 96.0 : 1.0);
        }
        finally
        {
            if (screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static BitmapSource? CapturePhysicalRect(int leftPx, int topPx, int widthPx, int heightPx)
    {
        if (widthPx <= 0 || heightPx <= 0) return null;

        IntPtr hdcSrc = IntPtr.Zero;
        IntPtr hdcDest = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr hOld = IntPtr.Zero;

        try
        {
            hdcSrc = GetDC(IntPtr.Zero);
            if (hdcSrc == IntPtr.Zero) return null;

            hdcDest = CreateCompatibleDC(hdcSrc);
            hBitmap = CreateCompatibleBitmap(hdcSrc, widthPx, heightPx);
            if (hdcDest == IntPtr.Zero || hBitmap == IntPtr.Zero) return null;

            hOld = SelectObject(hdcDest, hBitmap);
            bool ok = BitBlt(hdcDest, 0, 0, widthPx, heightPx, hdcSrc, leftPx, topPx, SRCCOPY);
            SelectObject(hdcDest, hOld);
            hOld = IntPtr.Zero;

            if (!ok) return null;

            var bmpSource = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bmpSource.Freeze();
            return bmpSource;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hOld != IntPtr.Zero && hdcDest != IntPtr.Zero) SelectObject(hdcDest, hOld);
            if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
            if (hdcDest != IntPtr.Zero) DeleteDC(hdcDest);
            if (hdcSrc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, hdcSrc);
        }
    }
}
