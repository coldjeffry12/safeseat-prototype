using System.Runtime.InteropServices;
using System.Text;

namespace SafeSeat.Windows;

internal static class Native
{
    internal const uint RidInput = 0x10000003;
    internal const uint RidiDeviceName = 0x20000007;
    internal const uint RidevDevNotify = 0x00002000;
    internal const uint RidevRemove = 0x00000001;
    internal const int ErrorInsufficientBuffer = 122;
    internal static readonly nint InvalidHandle = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawInputDeviceList { internal nint Device; internal uint Type; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawInputDevice
    {
        internal ushort UsagePage;
        internal ushort Usage;
        internal uint Flags;
        internal nint Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawInputHeader
    {
        internal uint Type;
        internal uint Size;
        internal nint Device;
        internal nint WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect { internal int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfo
    {
        internal uint Size;
        internal Rect Monitor;
        internal Rect Work;
        internal uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string Device;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DisplayDevice
    {
        internal uint Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string DeviceString;
        internal uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string DeviceId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DeviceInfoData
    {
        internal uint Size;
        internal Guid ClassGuid;
        internal uint DevInst;
        internal nuint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DeviceInterfaceData
    {
        internal uint Size;
        internal Guid ClassGuid;
        internal uint Flags;
        internal nuint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DevicePropertyKey
    {
        internal Guid FormatId;
        internal uint PropertyId;
    }

    internal delegate bool MonitorEnumProc(nint monitor, nint hdc, ref Rect rectangle, nint parameter);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetRawInputDeviceList([Out] RawInputDeviceList[]? devices, ref uint count, uint elementSize);

    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint GetRawInputDeviceInfo(nint device, uint command, nint data, ref uint size);

    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint GetRawInputDeviceName(nint device, uint command, StringBuilder data, ref uint size);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetRawInputData(nint input, uint command, nint data, ref uint size, uint headerSize);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterRawInputDevices(RawInputDevice[] devices, uint count, uint elementSize);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint window);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    internal static extern nint GetThreadDpiAwarenessContext();

    [DllImport("user32.dll")]
    internal static extern int GetAwarenessFromDpiAwarenessContext(nint context);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorEnumProc callback, nint parameter);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayDevices(string device, uint index, ref DisplayDevice display, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    internal static extern nint SetupDiCreateDeviceInfoList(nint classGuid, nint parent);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiOpenDeviceInterfaceW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiOpenDeviceInterface(nint set, string path, uint flags, ref DeviceInterfaceData data);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceInterfaceDetailW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiGetDeviceInterfaceDetail(nint set, ref DeviceInterfaceData data, nint detail, uint size, out uint required, ref DeviceInfoData device);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceRegistryPropertyW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiGetDeviceRegistryProperty(nint set, ref DeviceInfoData device, uint property, out uint type, byte[] buffer, uint size, out uint required);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDevicePropertyW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiGetDeviceProperty(nint set, ref DeviceInfoData device, ref DevicePropertyKey key,
        out uint type, [Out] byte[] buffer, uint size, out uint required, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiDestroyDeviceInfoList(nint set);
}
