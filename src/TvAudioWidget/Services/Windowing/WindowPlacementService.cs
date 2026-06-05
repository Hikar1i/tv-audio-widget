using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace TvAudioWidget.Services.Windowing;

public sealed class WindowPlacementService
{
    public void ApplyStartupPlacement(Window window, string? lastScreenDeviceName)
    {
        var screen = ChooseScreen(lastScreenDeviceName);
        var bounds = screen.WorkingArea;
        var width = Math.Min(Math.Max(bounds.Width * 0.92, Math.Min(900, bounds.Width)), bounds.Width);
        var height = Math.Min(Math.Max(bounds.Height * 0.90, Math.Min(560, bounds.Height)), bounds.Height);

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Width = width;
        window.Height = height;
        window.Left = bounds.Left + (bounds.Width - width) / 2;
        window.Top = bounds.Top + (bounds.Height - height) / 2;
    }

    public string? GetCurrentScreenDeviceName(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var screen = handle != IntPtr.Zero
            ? Forms.Screen.FromHandle(handle)
            : Forms.Screen.FromPoint(Forms.Cursor.Position);

        return screen.DeviceName;
    }

    private static Forms.Screen ChooseScreen(string? lastScreenDeviceName)
    {
        if (!string.IsNullOrWhiteSpace(lastScreenDeviceName))
        {
            var remembered = Forms.Screen.AllScreens.FirstOrDefault(screen =>
                string.Equals(screen.DeviceName, lastScreenDeviceName, StringComparison.OrdinalIgnoreCase));

            if (remembered is not null)
            {
                return remembered;
            }
        }

        var cursorPosition = Forms.Cursor.Position;
        var mouseScreen = Forms.Screen.AllScreens.FirstOrDefault(screen => screen.Bounds.Contains(cursorPosition));
        return mouseScreen
            ?? Forms.Screen.PrimaryScreen
            ?? Forms.Screen.AllScreens[0];
    }
}
