using System.Runtime.InteropServices;

namespace SafeSeat.App;

internal static class NativeWindow
{
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern nint GetKeyboardLayout(uint threadId);
    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSRegisterSessionNotification(nint window, uint flags);
    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSUnRegisterSessionNotification(nint window);

    internal static bool RegisterSessionChanges(nint ownWindow) => WTSRegisterSessionNotification(ownWindow, 0);
    internal static void UnregisterSessionChanges(nint ownWindow) => WTSUnRegisterSessionNotification(ownWindow);

    internal static bool OwnsForeground()
    {
        nint window = GetForegroundWindow();
        if (window == 0) return false;
        GetWindowThreadProcessId(window, out uint processId);
        return processId == (uint)Environment.ProcessId;
    }

    // Text translation is deliberately a tested US-ASCII subset. Reject other active HKLs.
    internal static bool IsUsEnglishLayout() =>
        unchecked((nuint)GetKeyboardLayout(0)) == 0x04090409;

    internal static void PlaceOwnWindow(nint window, int left, int top, int width, int height)
    {
        // SWP_NOZORDER | SWP_NOACTIVATE; only called with this process's seat HWNDs.
        if (!SetWindowPos(window, 0, left, top, width, height, 0x0014))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
}
