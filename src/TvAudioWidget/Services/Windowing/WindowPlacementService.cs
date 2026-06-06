using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TvAudioWidget.Services.Windowing;

public sealed class WindowPlacementService
{
    private const uint MonitorDefaultToNearest = 2;
    private const int CursorSuccess = 1;

    public void ApplyStartupPlacement(Window window, string? lastScreenDeviceName)
    {
        var screen = ChooseScreen(lastScreenDeviceName);
        ApplyMaximizedBounds(window, screen);
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        ApplyNormalPlacement(window, screen);
    }

    public void FitToCurrentScreen(Window window)
    {
        var screen = GetCurrentScreen(window);
        ApplyMaximizedBounds(window, screen);
        if (window.WindowState == WindowState.Maximized)
        {
            return;
        }

        ApplyNormalPlacement(window, screen);
    }

    public void ApplyCurrentScreenMaximizedBounds(Window window)
    {
        ApplyMaximizedBounds(window, GetCurrentScreen(window));
    }

    private static void ApplyNormalPlacement(Window window, MonitorInfo screen)
    {
        var bounds = screen.WorkArea;
        var width = Math.Min(Math.Max(bounds.Width * 0.92, Math.Min(900, bounds.Width)), bounds.Width);
        var height = Math.Min(Math.Max(bounds.Height * 0.90, Math.Min(560, bounds.Height)), bounds.Height);

        window.Width = width;
        window.Height = height;
        window.Left = bounds.Left + (bounds.Width - width) / 2;
        window.Top = bounds.Top + (bounds.Height - height) / 2;
    }

    private static void ApplyMaximizedBounds(Window window, MonitorInfo screen)
    {
        window.MaxWidth = screen.WorkArea.Width;
        window.MaxHeight = screen.WorkArea.Height;
    }

    public string? GetCurrentScreenDeviceName(Window window)
    {
        return GetCurrentScreen(window).DeviceName;
    }

    private static MonitorInfo GetCurrentScreen(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        return handle != IntPtr.Zero
            ? GetMonitorFromWindow(handle)
            : GetMonitorFromCursor();
    }

    private static MonitorInfo ChooseScreen(string? lastScreenDeviceName)
    {
        var monitors = EnumerateMonitors();
        if (!string.IsNullOrWhiteSpace(lastScreenDeviceName))
        {
            var remembered = monitors.FirstOrDefault(screen =>
                string.Equals(screen.DeviceName, lastScreenDeviceName, StringComparison.OrdinalIgnoreCase));

            if (remembered is not null)
            {
                return remembered;
            }
        }

        var cursorMonitor = GetMonitorFromCursor();
        return monitors.FirstOrDefault(screen =>
                string.Equals(screen.DeviceName, cursorMonitor.DeviceName, StringComparison.OrdinalIgnoreCase))
            ?? monitors.FirstOrDefault(screen => screen.IsPrimary)
            ?? monitors[0];
    }

    private static MonitorInfo GetMonitorFromWindow(IntPtr windowHandle)
    {
        var monitorHandle = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
        return GetMonitorInfo(monitorHandle);
    }

    private static MonitorInfo GetMonitorFromCursor()
    {
        var point = GetCursorPos(out var cursorPoint) == CursorSuccess
            ? cursorPoint
            : new NativePoint();

        var monitorHandle = MonitorFromPoint(point, MonitorDefaultToNearest);
        return GetMonitorInfo(monitorHandle);
    }

    private static IReadOnlyList<MonitorInfo> EnumerateMonitors()
    {
        var monitors = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            monitors.Add(GetMonitorInfo(monitor));
            return true;
        }, IntPtr.Zero);

        if (monitors.Count == 0)
        {
            monitors.Add(GetMonitorFromCursor());
        }

        return monitors;
    }

    private static MonitorInfo GetMonitorInfo(IntPtr monitorHandle)
    {
        var nativeInfo = new NativeMonitorInfo
        {
            Size = Marshal.SizeOf<NativeMonitorInfo>()
        };

        if (!GetMonitorInfo(monitorHandle, ref nativeInfo))
        {
            throw new InvalidOperationException("无法读取显示器信息。");
        }

        return new MonitorInfo(
            nativeInfo.DeviceName,
            ToRect(nativeInfo.WorkArea),
            (nativeInfo.Flags & 1) == 1);
    }

    private static Rect ToRect(NativeRect rect)
    {
        return new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    private sealed record MonitorInfo(string DeviceName, Rect WorkArea, bool IsPrimary);

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr clipRect,
        MonitorEnumProc callback,
        IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref NativeMonitorInfo monitorInfo);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr windowHandle, uint flags);

    [DllImport("user32.dll")]
    private static extern int GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeMonitorInfo
    {
        public int Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }
}
