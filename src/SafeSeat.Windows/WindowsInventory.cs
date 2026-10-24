using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SafeSeat.Windows;

/// <summary>Read-only enumeration. This class never registers to observe input.</summary>
public static class WindowsInventory
{
    internal sealed record DeviceEntry(nint Handle, RawDeviceDescriptor Descriptor);
    internal sealed record InventoryResult(InventorySnapshot Snapshot, IReadOnlyList<DeviceEntry> Entries);
    private sealed record PnpMetadata(string? FriendlyName = null, Guid? ContainerId = null, string? EnumeratorName = null);

    public static InventorySnapshot ReadSnapshot() => Enumerate().Snapshot;

    internal static InventoryResult Enumerate()
    {
        EnsureWindows();
        RawPacketParser.NativeSelfCheck();
        var warnings = new List<string>();
        var devices = ReadDevices(warnings);
        var monitors = ReadMonitors(warnings);
        if (Native.GetAwarenessFromDpiAwarenessContext(Native.GetThreadDpiAwarenessContext()) < 2)
            warnings.Add("Current thread is not per-monitor DPI aware. Monitor coordinates may be DPI virtualized; use the app's PerMonitorV2 manifest.");
        var snapshot = new InventorySnapshot(devices.Select(d => d.Descriptor).ToArray(), monitors, warnings.ToArray());
        return new InventoryResult(snapshot, devices);
    }

    internal static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Device observation and inventory require Windows. The packet parser is platform independent.");
    }

    private static IReadOnlyList<DeviceEntry> ReadDevices(List<string> warnings)
    {
        uint elementSize = checked((uint)Marshal.SizeOf<Native.RawInputDeviceList>());
        for (int attempt = 0; attempt < 4; attempt++)
        {
            uint count = 0;
            if (Native.GetRawInputDeviceList(null, ref count, elementSize) == uint.MaxValue)
                throw Error("GetRawInputDeviceList(size)");
            if (count > 65536)
                throw new InvalidOperationException("Raw device count exceeded the defensive limit.");
            if (count == 0)
                return [];
            var native = new Native.RawInputDeviceList[checked((int)count)];
            uint actual = Native.GetRawInputDeviceList(native, ref count, elementSize);
            if (actual == uint.MaxValue)
            {
                if (Marshal.GetLastWin32Error() == Native.ErrorInsufficientBuffer)
                    continue;
                throw Error("GetRawInputDeviceList");
            }
            if (actual > native.Length)
                throw new InvalidOperationException("Raw device enumeration returned an invalid count.");

            var entries = new List<DeviceEntry>();
            foreach (var device in native.Take(checked((int)actual)))
            {
                // General HID top-level collections do not participate in this demo.
                if (device.Device == 0 || device.Type > 1)
                    continue;
                RawDeviceKind kind = device.Type == 0 ? RawDeviceKind.Mouse : RawDeviceKind.Keyboard;
                string? path = TryReadDevicePath(device.Device, warnings);
                bool ambiguous = string.IsNullOrWhiteSpace(path);
                string id = ambiguous
                    ? $"raw-session-{Guid.NewGuid():N}"
                    : $"raw-{kind.ToString().ToLowerInvariant()}-{Hash(path!)}";
                var metadata = path is null ? new PnpMetadata() : TryReadPnpMetadata(path);
                string name = metadata.FriendlyName ?? BuildFallbackName(kind, path);
                string note = ambiguous
                    ? "Session-only identity: interface name unavailable. Reassign manually; input routing is disabled for ambiguous identity."
                    : "SHA-256 of Windows device interface path. Port changes, Bluetooth reconnects, driver updates, and identical devices may require manual reassignment.";
                entries.Add(new DeviceEntry(device.Device,
                    HardwareGrouping.WithMetadata(new RawDeviceDescriptor(id, kind, name, ambiguous, note),
                        metadata.ContainerId, metadata.EnumeratorName)));
            }
            foreach (var group in entries.GroupBy(e => e.Descriptor.Id).Where(g => g.Count() > 1).ToArray())
            {
                int suffix = 0;
                foreach (var entry in group)
                {
                    int index = entries.IndexOf(entry);
                    entries[index] = entry with
                    {
                        Descriptor = entry.Descriptor with
                        {
                            Id = $"{entry.Descriptor.Id}:ambiguous-session-{++suffix}",
                            IsIdentityAmbiguous = true,
                            StabilityNote = "Duplicate interface identity. Session suffix is not stable; manual identification is required and input routing is disabled."
                        }
                    };
                }
                warnings.Add("Duplicate raw device interface identity was detected. Ambiguous devices will not be routed.");
            }
            if (entries.Any(e => e.Descriptor.IsIdentityAmbiguous))
                warnings.Add("Some devices have no reliable interface identity. Their events are ignored rather than assigned to a guessed seat.");
            return entries;
        }
        throw new InvalidOperationException("Raw device list changed repeatedly during enumeration. Retry refresh after hot-plug activity settles.");
    }

    private static string? TryReadDevicePath(nint handle, List<string> warnings)
    {
        uint chars = 0;
        if (Native.GetRawInputDeviceInfo(handle, Native.RidiDeviceName, 0, ref chars) == uint.MaxValue)
        {
            warnings.Add($"A raw device name could not be read (Win32 {Marshal.GetLastWin32Error()}); it may have disconnected.");
            return null;
        }
        if (chars is 0 or > 32768)
            return null;
        var text = new StringBuilder(checked((int)chars + 1));
        chars = checked((uint)text.Capacity);
        if (Native.GetRawInputDeviceName(handle, Native.RidiDeviceName, text, ref chars) == uint.MaxValue)
        {
            warnings.Add($"A raw device name changed or became unavailable (Win32 {Marshal.GetLastWin32Error()}).");
            return null;
        }
        return text.ToString().TrimEnd('\0');
    }

    private static string BuildFallbackName(RawDeviceKind kind, string? path)
    {
        if (path is null)
            return $"{kind} (identity unavailable)";
        var match = Regex.Match(path, @"VID_([0-9A-F]{4}).*?PID_([0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success
            ? $"{kind} · VID {match.Groups[1].Value.ToUpperInvariant()} / PID {match.Groups[2].Value.ToUpperInvariant()}"
            : $"{kind} (Windows raw input device)";
    }

    // SetupAPI opens an in-memory device information set and reads metadata properties only.
    // It does not open a HID device, write registry values, install a driver, or change configuration.
    // A failed optional query does not affect the raw collection's identity or routing eligibility.
    private static PnpMetadata TryReadPnpMetadata(string path)
    {
        nint set = Native.SetupDiCreateDeviceInfoList(0, 0);
        if (set == Native.InvalidHandle)
            return new PnpMetadata();
        try
        {
            var iface = new Native.DeviceInterfaceData { Size = checked((uint)Marshal.SizeOf<Native.DeviceInterfaceData>()) };
            if (!Native.SetupDiOpenDeviceInterface(set, path, 0, ref iface))
                return new PnpMetadata();
            var device = new Native.DeviceInfoData { Size = checked((uint)Marshal.SizeOf<Native.DeviceInfoData>()) };
            Native.SetupDiGetDeviceInterfaceDetail(set, ref iface, 0, 0, out uint required, ref device);
            if (required is < 6 or > 65536)
                return new PnpMetadata();
            nint detail = Marshal.AllocHGlobal(checked((int)required));
            try
            {
                Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6); // Unicode detail cbSize is architecture dependent.
                if (!Native.SetupDiGetDeviceInterfaceDetail(set, ref iface, detail, required, out _, ref device))
                    return new PnpMetadata();
                string? name = null;
                foreach (uint property in new uint[] { 12, 0 }) // SPDRP_FRIENDLYNAME, SPDRP_DEVICEDESC
                {
                    name = TryReadStringProperty(set, ref device, property);
                    if (name is not null)
                        break;
                }
                string? enumerator = TryReadStringProperty(set, ref device, 0x16); // SPDRP_ENUMERATOR_NAME
                Guid? containerId = null;
                var containerKey = new Native.DevicePropertyKey
                {
                    FormatId = new Guid("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"),
                    PropertyId = 2 // DEVPKEY_Device_ContainerId, from Devpkey.h
                };
                var containerBuffer = new byte[16];
                // DEVPROP_TYPE_GUID = 0x0d; reject missing, wrong-type or truncated values.
                if (Native.SetupDiGetDeviceProperty(set, ref device, ref containerKey, out uint propertyType,
                    containerBuffer, 16, out uint containerBytes, 0) && propertyType == 0x0d && containerBytes == 16)
                {
                    var candidate = new Guid(containerBuffer);
                    if (candidate != Guid.Empty)
                        containerId = candidate;
                }
                Array.Clear(containerBuffer);
                return new PnpMetadata(name, containerId, enumerator);
            }
            finally { Marshal.FreeHGlobal(detail); }
        }
        finally { Native.SetupDiDestroyDeviceInfoList(set); }
    }

    private static string? TryReadStringProperty(nint set, ref Native.DeviceInfoData device, uint property)
    {
        var buffer = new byte[4096];
        try
        {
            if (Native.SetupDiGetDeviceRegistryProperty(set, ref device, property, out uint type, buffer,
                checked((uint)buffer.Length), out uint bytes) && type == 1 && bytes is > 1 and <= 4096 && bytes % 2 == 0)
            {
                string value = Encoding.Unicode.GetString(buffer, 0, checked((int)bytes)).TrimEnd('\0');
                // REG_SZ should contain one string. Discard malformed property values.
                if (!string.IsNullOrWhiteSpace(value) && !value.Contains('\0'))
                {
                    return value;
                }
            }
        }
        finally { Array.Clear(buffer); }
        return null;
    }

    private static IReadOnlyList<MonitorDescriptor> ReadMonitors(List<string> warnings)
    {
        var monitors = new List<MonitorDescriptor>();
        Exception? callbackError = null;
        bool Callback(nint handle, nint hdc, ref Native.Rect rectangle, nint parameter)
        {
            try
            {
                var info = new Native.MonitorInfo { Size = checked((uint)Marshal.SizeOf<Native.MonitorInfo>()), Device = "" };
                if (!Native.GetMonitorInfo(handle, ref info))
                    throw Error("GetMonitorInfo");
                var display = new Native.DisplayDevice
                {
                    Size = checked((uint)Marshal.SizeOf<Native.DisplayDevice>()), DeviceName = "", DeviceString = "", DeviceId = "", DeviceKey = ""
                };
                bool hasDisplay = Native.EnumDisplayDevices(info.Device, 0, ref display, 1);
                bool ambiguous = !hasDisplay || string.IsNullOrWhiteSpace(display.DeviceId);
                string identity = ambiguous ? info.Device : display.DeviceId;
                string name = hasDisplay && !string.IsNullOrWhiteSpace(display.DeviceString) ? display.DeviceString : "Windows monitor";
                var r = info.Monitor;
                var w = info.Work;
                monitors.Add(new MonitorDescriptor($"monitor-{Hash(identity)}", $"{name} · {info.Device}",
                    r.Left, r.Top, checked(r.Right - r.Left), checked(r.Bottom - r.Top),
                    w.Left, w.Top, checked(w.Right - w.Left), checked(w.Bottom - w.Top),
                    (info.Flags & 1) != 0, ambiguous,
                    "Best-effort display-interface identity from EnumDisplayDevices. Cloned targets, ports, docks, or driver changes can require reassignment. Coordinates require per-monitor DPI awareness."));
                return true;
            }
            catch (Exception ex)
            {
                callbackError = ex;
                return false;
            }
        }
        if (!Native.EnumDisplayMonitors(0, 0, Callback, 0))
            throw callbackError ?? Error("EnumDisplayMonitors");
        if (monitors.GroupBy(m => m.Id).Any(g => g.Count() > 1))
        {
            warnings.Add("Duplicate monitor identity detected; cloned or ambiguous targets require manual reassignment.");
            monitors = monitors.Select((m, i) => m with { Id = $"{m.Id}:session-{i}", IsIdentityAmbiguous = true }).ToList();
        }
        if (monitors.Any(m => m.IsIdentityAmbiguous))
            warnings.Add("Some monitor identities are best-effort adapter names and may change after reconnect.");
        return monitors;
    }

    internal static Win32Exception Error(string operation) => new(Marshal.GetLastWin32Error(), $"{operation} failed.");
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToUpperInvariant()))).ToLowerInvariant();
}
