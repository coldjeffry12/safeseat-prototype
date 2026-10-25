using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace SafeSeat.Windows;

/// <summary>Bounded, architecture-explicit parser, usable by tests without calling any Windows API.</summary>
public static class RawPacketParser
{
    public const int MaximumPacketBytes = 1024 * 1024;

    public static ParsedRawPacket? Parse(ReadOnlySpan<byte> bytes, int pointerSize)
    {
        if (pointerSize is not (4 or 8))
            throw new ArgumentOutOfRangeException(nameof(pointerSize));
        int headerSize = pointerSize == 8 ? 24 : 16;
        if (bytes.Length < headerSize || bytes.Length > MaximumPacketBytes)
            return null;
        uint type = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        uint declaredSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        if (declaredSize < headerSize || declaredSize > bytes.Length)
            return null;
        ulong handle = pointerSize == 8
            ? BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..])
            : BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]);
        // Precision touchpads and other system sources may use a null device handle.
        // They cannot be assigned to a particular physical seat by this prototype.
        if (handle == 0)
            return null;
        ReadOnlySpan<byte> body = bytes.Slice(headerSize, checked((int)declaredSize - headerSize));
        if (type == 1 && body.Length >= 16)
        {
            ushort scan = BinaryPrimitives.ReadUInt16LittleEndian(body);
            ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(body[2..]);
            ushort key = BinaryPrimitives.ReadUInt16LittleEndian(body[6..]);
            // Microsoft documents these as keyboard overrun / unmapped keys.
            if (scan == 0xff || key >= 0xff)
                return null;
            // RAWKEYBOARD reports generic VK_SHIFT/VK_CONTROL/VK_MENU. Preserve independent
            // left/right held state without querying the shared OS keyboard state.
            key = key switch
            {
                0x10 when scan == 0x2a => 0xa0, // VK_LSHIFT
                0x10 when scan == 0x36 => 0xa1, // VK_RSHIFT
                0x11 => (ushort)((flags & 2) != 0 ? 0xa3 : 0xa2), // RCONTROL / LCONTROL
                0x12 => (ushort)((flags & 2) != 0 ? 0xa5 : 0xa4), // RMENU / LMENU
                _ => key
            };
            return new ParsedRawPacket(handle, RawDeviceKind.Keyboard,
                new KeyboardData(key, scan, (flags & 1) == 0, (flags & 2) != 0, (flags & 4) != 0, flags), null);
        }
        if (type == 0 && body.Length >= 24)
        {
            ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(body);
            ushort buttons = BinaryPrimitives.ReadUInt16LittleEndian(body[4..]);
            short wheel = (buttons & 0x0c00) != 0
                ? BinaryPrimitives.ReadInt16LittleEndian(body[6..]) : (short)0;
            int x = BinaryPrimitives.ReadInt32LittleEndian(body[12..]);
            int y = BinaryPrimitives.ReadInt32LittleEndian(body[16..]);
            bool absolute = (flags & 1) != 0;
            return new ParsedRawPacket(handle, RawDeviceKind.Mouse, null,
                new MouseData(absolute ? 0 : x, absolute ? 0 : y, buttons, wheel, absolute,
                    absolute ? Math.Clamp(x / 65535d, 0, 1) : 0,
                    absolute ? Math.Clamp(y / 65535d, 0, 1) : 0, (flags & 2) != 0, flags));
        }
        return null; // Generic HID reports are deliberately unsupported.
    }

    /// <summary>Throws if this runtime's interop layout differs from the documented ABI.</summary>
    public static void NativeSelfCheck()
    {
        Check(Marshal.SizeOf<Native.RawInputHeader>(), IntPtr.Size == 8 ? 24 : 16, "RAWINPUTHEADER size");
        Check(Marshal.SizeOf<Native.RawInputDeviceList>(), IntPtr.Size == 8 ? 16 : 8, "RAWINPUTDEVICELIST size");
        Check(Marshal.SizeOf<Native.RawInputDevice>(), IntPtr.Size == 8 ? 16 : 12, "RAWINPUTDEVICE size");
        Check(Marshal.SizeOf<Native.RawDeviceInfo>(), 32, "RID_DEVICE_INFO size");
        Check(Marshal.OffsetOf<Native.RawDeviceInfo>(nameof(Native.RawDeviceInfo.Type)).ToInt32(), 4, "RID_DEVICE_INFO dwType offset");
        Check(Marshal.OffsetOf<Native.RawInputHeader>(nameof(Native.RawInputHeader.Device)).ToInt32(), 8, "RAWINPUTHEADER hDevice offset");
        Check(Marshal.SizeOf<Native.MonitorInfo>(), 104, "MONITORINFOEXW size");
        Check(Marshal.SizeOf<Native.DisplayDevice>(), 840, "DISPLAY_DEVICEW size");
        Check(Marshal.SizeOf<Native.DeviceInfoData>(), IntPtr.Size == 8 ? 32 : 28, "SP_DEVINFO_DATA size");
    }

    private static void Check(int actual, int expected, string name)
    {
        if (actual != expected)
            throw new InvalidOperationException($"{name}: expected {expected}, got {actual}.");
    }
}
