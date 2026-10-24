using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using SafeSeat.Windows;

// Production listeners are never started here. Lifecycle tests use a fully fake native backend.
if (args.SequenceEqual(new[] { "--inventory" }) || args.SequenceEqual(new[] { "--readiness" }))
{
    var options = new JsonSerializerOptions { WriteIndented = true };
    options.Converters.Add(new JsonStringEnumConverter());
    var inventory = WindowsInventory.ReadSnapshot();
    Console.WriteLine(args[0] == "--inventory"
        ? JsonSerializer.Serialize(inventory, options)
        : JsonSerializer.Serialize(DiscoveryReadiness.Evaluate(inventory), options));
    return;
}
if (args.Length != 0)
    throw new ArgumentException("Usage: SafeSeat.NativeTests [--inventory|--readiness]");

int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException($"FAIL: {name}");
    checks++;
}

RawPacketParser.NativeSelfCheck();
Check(true, "runtime native ABI sizes and offsets");

foreach (int pointerSize in new[] { 4, 8 })
{
    int header = pointerSize == 8 ? 24 : 16;
    byte[] Keyboard(ushort key = 0x41, ushort scan = 0x1e, ushort flags = 0)
    {
        byte[] bytes = Packet(pointerSize, 1, 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(header), scan);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(header + 2), flags);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(header + 6), key);
        return bytes;
    }

    var keyboard = RawPacketParser.Parse(Keyboard(), pointerSize);
    Check(keyboard is { DeviceHandle: 0x1234, Kind: RawDeviceKind.Keyboard, Keyboard: { VirtualKey: 0x41, ScanCode: 0x1e, IsDown: true, IsExtended: false, IsE1: false } }, $"{pointerSize}: keyboard make");
    var released = RawPacketParser.Parse(Keyboard(flags: 7), pointerSize);
    Check(released?.Keyboard is { IsDown: false, IsExtended: true, IsE1: true, Flags: 7 }, $"{pointerSize}: keyboard flags");
    Check(RawPacketParser.Parse(Keyboard(scan: 0xff), pointerSize) is null, $"{pointerSize}: overrun ignored");
    Check(RawPacketParser.Parse(Keyboard(key: 0xff), pointerSize) is null, $"{pointerSize}: unmapped key ignored");

    foreach (var test in new (ushort Key, ushort Scan, ushort Flags, ushort Expected)[]
    {
        (0x10, 0x2a, 0, 0xa0), (0x10, 0x36, 0, 0xa1),
        (0x10, 0x2a, 1, 0xa0), (0x10, 0x36, 1, 0xa1),
        (0x11, 0x1d, 0, 0xa2), (0x11, 0x1d, 2, 0xa3),
        (0x11, 0x1d, 3, 0xa3), (0x12, 0x38, 0, 0xa4),
        (0x12, 0x38, 2, 0xa5), (0x12, 0x38, 3, 0xa5)
    })
        Check(RawPacketParser.Parse(Keyboard(test.Key, test.Scan, test.Flags), pointerSize)?.Keyboard?.VirtualKey == test.Expected,
            $"{pointerSize}: sided modifier {test.Expected:x2}/{test.Flags}");

    byte[] mouse = Packet(pointerSize, 0, 24);
    BinaryPrimitives.WriteUInt16LittleEndian(mouse.AsSpan(header + 4), 0x0401);
    BinaryPrimitives.WriteInt16LittleEndian(mouse.AsSpan(header + 6), -120);
    BinaryPrimitives.WriteInt32LittleEndian(mouse.AsSpan(header + 12), -31);
    BinaryPrimitives.WriteInt32LittleEndian(mouse.AsSpan(header + 16), 47);
    Check(RawPacketParser.Parse(mouse, pointerSize)?.Mouse is { DeltaX: -31, DeltaY: 47, ButtonFlags: 0x0401, WheelDelta: -120, IsAbsolute: false }, $"{pointerSize}: relative motion, button and signed wheel");
    BinaryPrimitives.WriteUInt16LittleEndian(mouse.AsSpan(header), 3);
    BinaryPrimitives.WriteInt32LittleEndian(mouse.AsSpan(header + 12), 65535);
    BinaryPrimitives.WriteInt32LittleEndian(mouse.AsSpan(header + 16), 0);
    Check(RawPacketParser.Parse(mouse, pointerSize)?.Mouse is { DeltaX: 0, DeltaY: 0, AbsoluteX: 1, AbsoluteY: 0, IsAbsolute: true, UsesVirtualDesktop: true }, $"{pointerSize}: absolute endpoints");
    BinaryPrimitives.WriteInt32LittleEndian(mouse.AsSpan(header + 12), int.MaxValue);
    BinaryPrimitives.WriteInt32LittleEndian(mouse.AsSpan(header + 16), int.MinValue);
    Check(RawPacketParser.Parse(mouse, pointerSize)?.Mouse is { AbsoluteX: 1, AbsoluteY: 0 }, $"{pointerSize}: absolute range is bounded");
    BinaryPrimitives.WriteUInt16LittleEndian(mouse.AsSpan(header + 4), 0);
    Check(RawPacketParser.Parse(mouse, pointerSize)?.Mouse?.WheelDelta == 0, $"{pointerSize}: button data is not falsely interpreted as wheel");

    byte[] zero = Keyboard();
    Array.Clear(zero, 8, pointerSize);
    Check(RawPacketParser.Parse(zero, pointerSize) is null, $"{pointerSize}: zero source handle ignored");
    Check(RawPacketParser.Parse(Keyboard().AsSpan(0, header - 1), pointerSize) is null, $"{pointerSize}: truncated header");
    Check(RawPacketParser.Parse(Keyboard().AsSpan(0, header + 15), pointerSize) is null, $"{pointerSize}: truncated body");
    byte[] wrong = Keyboard();
    BinaryPrimitives.WriteUInt32LittleEndian(wrong.AsSpan(4), uint.MaxValue);
    Check(RawPacketParser.Parse(wrong, pointerSize) is null, $"{pointerSize}: oversized declared length");
    BinaryPrimitives.WriteUInt32LittleEndian(wrong.AsSpan(4), checked((uint)header - 1));
    Check(RawPacketParser.Parse(wrong, pointerSize) is null, $"{pointerSize}: declared length below header");
    Check(RawPacketParser.Parse(Packet(pointerSize, 2, 24), pointerSize) is null, $"{pointerSize}: generic HID unsupported");
    Check(RawPacketParser.Parse(Packet(pointerSize, 0, 23), pointerSize) is null, $"{pointerSize}: truncated mouse payload");
}
Check(RawPacketParser.Parse(new byte[RawPacketParser.MaximumPacketBytes + 1], 8) is null, "oversized supplied payload");
bool invalidPointerRejected = false;
try { RawPacketParser.Parse([], 16); }
catch (ArgumentOutOfRangeException) { invalidPointerRejected = true; }
Check(invalidPointerRejected, "invalid ABI pointer size rejected");
Check(RawInputObserver.IsForegroundInputCode(0), "RIM_INPUT foreground generation code accepted");
Check(!RawInputObserver.IsForegroundInputCode(1), "RIM_INPUTSINK background generation code rejected");
Check(!RawInputObserver.IsForegroundInputCode(2), "unknown raw input code rejected");
Check(RawInputObserver.IsForegroundInputCode(0x100), "foreground code extracted using GET_RAWINPUT_CODE_WPARAM low byte");
Check(!RawInputObserver.IsForegroundInputCode(0x101), "background code extracted despite high bits");
Check(!RawInputObserver.IsForegroundInputCode(-1), "invalid negative input code rejected");

using (var stopped = new RawInputObserver())
{
    Check(!stopped.IsRunning, "observer initially stopped");
    Check(stopped.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null, "stopped observer ignores messages without user32 calls");
    stopped.Stop(); stopped.Stop();
    Check(!stopped.IsRunning, "Stop is idempotent before Start");
}
Console.WriteLine($"PASS: {checks} native ABI / packet parser / foreground-code checks.");
int groupingChecks = HardwareGroupingChecks.Run();
int readinessChecks = ReadinessChecks.Run();
int registrationChecks = RegistrationLifecycleChecks.Run();
Console.WriteLine($"PASS: {groupingChecks} candidate PnP grouping checks; {readinessChecks} readiness/topology checks; {registrationChecks} simulated registration/recovery checks.");
Console.WriteLine($"RESULT: {checks + groupingChecks + readinessChecks + registrationChecks} checks passed. No production input registration or hardware input collection was started. Physical two-seat acceptance NOT TESTED.");

static byte[] Packet(int pointerSize, uint type, int bodySize)
{
    int header = pointerSize == 8 ? 24 : 16;
    byte[] bytes = new byte[header + bodySize];
    BinaryPrimitives.WriteUInt32LittleEndian(bytes, type);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), checked((uint)bytes.Length));
    if (pointerSize == 8) BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8), 0x1234);
    else BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0x1234);
    return bytes;
}
