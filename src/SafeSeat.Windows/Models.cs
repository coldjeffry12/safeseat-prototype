namespace SafeSeat.Windows;

public enum RawDeviceKind { Mouse, Keyboard, Other }

/// <summary>IDs are hashes of device interface paths, never a claim of immutable hardware identity.</summary>
public sealed record RawDeviceDescriptor(
    string Id, RawDeviceKind Kind, string DisplayName, bool IsIdentityAmbiguous, string StabilityNote,
    string? CandidateGroupId = null, string? TransportHint = null, string? CorrelationNote = null);

/// <summary>Coordinates are physical pixels when the caller uses PerMonitorV2 DPI awareness.</summary>
public sealed record MonitorDescriptor(
    string Id, string DisplayName, int Left, int Top, int Width, int Height,
    int WorkLeft, int WorkTop, int WorkWidth, int WorkHeight,
    bool IsPrimary, bool IsIdentityAmbiguous, string StabilityNote);

public sealed record InventorySnapshot(
    IReadOnlyList<RawDeviceDescriptor> Devices,
    IReadOnlyList<MonitorDescriptor> Monitors,
    IReadOnlyList<string> Warnings);

/// <summary>Transient raw keyboard data; callers must not persist or print its content.</summary>
public sealed record KeyboardData(
    ushort VirtualKey, ushort ScanCode, bool IsDown, bool IsExtended, bool IsE1, ushort Flags);

/// <summary>Absolute values are normalized device coordinates, not seat-local positions.</summary>
public sealed record MouseData(
    int DeltaX, int DeltaY, ushort ButtonFlags, short WheelDelta,
    bool IsAbsolute, double AbsoluteX, double AbsoluteY, bool UsesVirtualDesktop, ushort Flags);

/// <summary>Monotonic Stopwatch timestamp; no typed text or OS keyboard-state translation.</summary>
public sealed record DeviceInput(
    string DeviceId, RawDeviceKind Kind, long TimestampTicks, KeyboardData? Keyboard, MouseData? Mouse);

public enum RawInputMessageKind { DeviceInput, DevicesChanged }

public sealed record RawInputMessage(
    RawInputMessageKind Kind, DeviceInput? Input = null, InventorySnapshot? Inventory = null);

/// <summary>Parser output uses an integer handle only in memory; never persist handles as identity.</summary>
public sealed record ParsedRawPacket(
    ulong DeviceHandle, RawDeviceKind Kind, KeyboardData? Keyboard, MouseData? Mouse);
