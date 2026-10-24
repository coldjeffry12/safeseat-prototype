using System.Runtime.InteropServices;

namespace SafeSeat.Windows;

// This seam is internal so application callers cannot substitute an input registration policy.
// Tests inject a backend whose every operation is simulated, including window ownership and packet reads.
internal enum WindowOwnership { Invalid, Foreign, Owned }

internal sealed record RawInputInventory(
    InventorySnapshot Snapshot, IReadOnlyDictionary<nint, RawDeviceDescriptor> Devices);

internal readonly record struct RegistrationResult(bool Succeeded, int ErrorCode = 0);

internal interface IRawInputBackend
{
    void EnsureSupportedPlatform();
    WindowOwnership ValidateWindow(nint hwnd);
    RawInputInventory ReadInventory();
    RegistrationResult Register(uint flags, nint hwnd);
    ParsedRawPacket? ReadPacket(nint rawInput);
}

internal sealed class Win32RawInputBackend : IRawInputBackend
{
    public void EnsureSupportedPlatform() => WindowsInventory.EnsureWindows();

    public WindowOwnership ValidateWindow(nint hwnd)
    {
        if (hwnd == 0 || !Native.IsWindow(hwnd)) return WindowOwnership.Invalid;
        Native.GetWindowThreadProcessId(hwnd, out uint processId);
        return processId == (uint)Environment.ProcessId ? WindowOwnership.Owned : WindowOwnership.Foreign;
    }

    public RawInputInventory ReadInventory()
    {
        var result = WindowsInventory.Enumerate();
        return new RawInputInventory(result.Snapshot,
            result.Entries.ToDictionary(entry => entry.Handle, entry => entry.Descriptor));
    }

    public RegistrationResult Register(uint flags, nint hwnd)
    {
        Native.RawInputDevice[] registrations =
        [
            new() { UsagePage = 1, Usage = 2, Flags = flags, Target = hwnd }, // Mouse
            new() { UsagePage = 1, Usage = 6, Flags = flags, Target = hwnd }  // Keyboard
        ];
        bool success = Native.RegisterRawInputDevices(registrations, checked((uint)registrations.Length),
            checked((uint)Marshal.SizeOf<Native.RawInputDevice>()));
        return new RegistrationResult(success, success ? 0 : Marshal.GetLastWin32Error());
    }

    public ParsedRawPacket? ReadPacket(nint rawInput)
    {
        uint headerSize = checked((uint)Marshal.SizeOf<Native.RawInputHeader>());
        uint bytes = 0;
        if (Native.GetRawInputData(rawInput, Native.RidInput, 0, ref bytes, headerSize) == uint.MaxValue)
            throw WindowsInventory.Error("GetRawInputData(size)");
        if (bytes < headerSize || bytes > RawPacketParser.MaximumPacketBytes) return null;
        nint buffer = Marshal.AllocHGlobal(checked((int)bytes));
        byte[]? managed = null;
        try
        {
            uint capacity = bytes;
            uint copied = Native.GetRawInputData(rawInput, Native.RidInput, buffer, ref capacity, headerSize);
            if (copied == uint.MaxValue) throw WindowsInventory.Error("GetRawInputData");
            if (copied < headerSize || copied > bytes) return null;
            managed = new byte[checked((int)copied)];
            Marshal.Copy(buffer, managed, 0, managed.Length);
            return RawPacketParser.Parse(managed, IntPtr.Size);
        }
        finally
        {
            if (managed is not null) Array.Clear(managed);
            Marshal.FreeHGlobal(buffer);
        }
    }
}
