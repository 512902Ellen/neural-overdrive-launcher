using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CodexBootAnimation;

internal static class NativeWindows
{
    internal const int InsufficientBuffer = 122;
    internal const int NoApplication = 15703;

    [StructLayout(LayoutKind.Sequential)]
    internal record struct Point(int X, int Y);

    [StructLayout(LayoutKind.Sequential)]
    internal record struct Rect(int Left, int Top, int Right, int Bottom);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MonitorInfo
    {
        internal int Size;
        internal Rect Monitor;
        internal Rect Work;
        internal uint Flags;
    }

    internal delegate bool EnumCallback(nint window, nint data);
    internal delegate void WinEventCallback(nint hook, uint type, nint window, int objectId,
        int childId, uint eventThread, uint eventTime);

    [DllImport("user32.dll")]
    internal static extern bool EnumWindows(EnumCallback callback, nint data);

    [DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    internal static extern bool IsIconic(nint window);

    [DllImport("user32.dll")]
    internal static extern nint GetWindow(nint window, uint command);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    internal static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    internal static extern bool GetWindowRect(nint window, out Rect bounds);

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindowAsync(nint window, int command);

    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    internal static extern nint MonitorFromPoint(Point point, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    internal static extern bool SetWindowPos(nint window, nint after, int x, int y,
        int width, int height, uint flags);

    [DllImport("user32.dll")]
    internal static extern nint SetWinEventHook(uint minEvent, uint maxEvent, nint module,
        WinEventCallback callback, uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    internal static extern bool UnhookWinEvent(nint hook);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetWindowAttribute(nint window, uint attribute, out int value, int size);

    [DllImport("kernel32.dll")]
    internal static extern SafeProcessHandle OpenProcess(uint access, bool inherit, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetPackagesByPackageFamily(string family, ref uint count,
        nint names, ref uint length, nint buffer);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetPackageFamilyName(SafeProcessHandle process, ref uint size, [Out] char[] family);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetApplicationUserModelId(SafeProcessHandle process, ref uint size, [Out] char[] appId);
}
