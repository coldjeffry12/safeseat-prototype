using System.Text.Json;
using SafeSeat.Core;

// Dependency-free test executable. This verifies simulated state routing, not physical HID behavior.
var tests = new (string Name, Action Run)[]
{
    ("device identity is deterministic and redacts paths", Identity),
    ("settings round trip only assignments", SettingsRoundTrip),
    ("settings reject unknown versions and seats", InvalidSettings),
    ("settings reject null maps and duplicate properties", AmbiguousSettings),
    ("settings reject aliased monitor seats and preserve device ID case", AliasedMonitorSeats),
    ("settings reject duplicate monitors and devices per slot", DuplicateSettingsSlots),
    ("settings reject malformed and excessive input", MalformedSettings),
    ("settings atomic save replaces existing file", AtomicSettings),
    ("monitor containment supports negative origins", NegativeMonitor),
    ("monitor containment rejects invalid and edge coordinates", InvalidMonitor),
    ("router rejects unknown and unassigned sources", UnknownSources),
    ("router validates device kinds", WrongKinds),
    ("router enforces one device of each kind per seat", AssignmentSlots),
    ("router explicitly reassigns and resets held state", Reassignment),
    ("router accepts duplicate names with distinct identities", DuplicateNames),
    ("router blocks ambiguous duplicate identities", DuplicateIdentities),
    ("router blocks restored duplicate assignment slots", RestoredDuplicateSlots),
    ("ambiguous reconnect clears previously held modifiers", AmbiguousReconnect),
    ("disconnect releases keys and preserves assignment", Reconnect),
    ("changed identity is never implicitly reassigned", ChangedIdentity),
    ("identity kind change fails closed", ChangedKind),
    ("interleaved input keeps text and modifiers independent", InterleavedTyping),
    ("left and right modifiers retain independent held state", SidedModifiers),
    ("repeat and caps lock transitions are handled", RepeatAndCaps),
    ("control alt and win combinations produce no text", ModifierCombinations),
    ("ASCII punctuation backspace and text bound", TextBehavior),
    ("pointer bounds and absolute coordinates are independent", PointerBounds),
    ("invalid event values are rejected", InvalidEvents),
    ("counter clicks debounce and remain seat local", IndependentClicks),
    ("clear button clears only the assigned seat", IndependentClear),
    ("wheel affects only the own scroll panel", IndependentScroll),
    ("disconnect releases mouse buttons", MouseDisconnect),
    ("stop clears logical text and held keys then rejects routing", StopBehavior),
    ("start resets modifier state", StartResets),
    ("concurrent simulated streams preserve independent state", ConcurrentStreams),
    ("10000 seeded interleaved packets match isolated streams", SustainedInterleaving)
};

var failures = 0;
foreach (var (name, run) in tests)
{
    try { run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}");
    }
}
Console.WriteLine($"RESULT {tests.Length - failures}/{tests.Length} passed; {failures} failed.");
Console.WriteLine("Evidence scope: simulated core input only; physical two-seat independence NOT TESTED.");
return failures == 0 ? 0 : 1;

static void Check(bool condition, string message = "Assertion failed.")
{
    if (!condition) throw new InvalidOperationException(message);
}

static void Equal<T>(T expected, T actual, string message = "Unexpected value.")
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException(message);
}

static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException("Expected exception was not thrown.");
}

static DeviceDescriptor[] Devices() =>
[
    new("ka", "Identical Keyboard", DeviceKind.Keyboard),
    new("kb", "Identical Keyboard", DeviceKind.Keyboard),
    new("ma", "Identical Mouse", DeviceKind.Mouse),
    new("mb", "Identical Mouse", DeviceKind.Mouse)
];

static Settings AssignedSettings() => new()
{
    DeviceAssignments = new() { ["ka"] = SeatId.A, ["kb"] = SeatId.B, ["ma"] = SeatId.A, ["mb"] = SeatId.B },
    MonitorAssignments = new() { [SeatId.A] = "monitor-a", [SeatId.B] = "monitor-b" }
};

static SeatRouter Ready()
{
    var router = new SeatRouter(AssignedSettings());
    router.RegisterDevices(Devices());
    router.Start();
    return router;
}

static void Key(SeatRouter router, string device, int key, bool down = true) =>
    Check(router.ProcessKeyboard(device, new(key, 0, down)).Accepted);

static void Tap(SeatRouter router, string device, int key)
{
    Key(router, device, key);
    Key(router, device, key, false);
}

static void MoveTo(SeatRouter router, string device, double x, double y) =>
    Check(router.ProcessMouse(device, new(Absolute: true, AbsoluteX: x / 799, AbsoluteY: y / 499)).Accepted);

static void Click(SeatRouter router, string device)
{
    Check(router.ProcessMouse(device, new(Buttons: MouseButtons.LeftDown)).Accepted);
    Check(router.ProcessMouse(device, new(Buttons: MouseButtons.LeftUp)).Accepted);
}

static void Identity()
{
    var first = DeviceIdentity.Hash(@"\\?\HID#PRIVATE_SERIAL");
    Equal(first, DeviceIdentity.Hash(@"\\?\hid#private_serial"));
    Check(first.StartsWith("sha256_", StringComparison.Ordinal));
    Check(!first.Contains("PRIVATE", StringComparison.OrdinalIgnoreCase));
    Check(first != DeviceIdentity.Hash("another-device"));
    Throws<ArgumentException>(() => DeviceIdentity.Hash(" "));
}

static void SettingsRoundTrip()
{
    var json = AssignedSettings().ToJson();
    var settings = Settings.Parse(json);
    Equal(SeatId.B, settings.DeviceAssignments["kb"]);
    Equal("monitor-a", settings.MonitorAssignments[SeatId.A]);
    Check(!json.Contains("Text", StringComparison.Ordinal));
    Equal(0, settings.Validate(Devices()).Count);
}

static void InvalidSettings()
{
    Throws<InvalidDataException>(() => Settings.Parse("{\"Version\":2}"));
    Throws<JsonException>(() => Settings.Parse("{\"DeviceAssignments\":{\"x\":\"C\"}}"));
    Throws<JsonException>(() => Settings.Parse("{\"DeviceAssignments\":{\"x\":0}}"));
    var settings = new Settings { DeviceAssignments = new() { ["x"] = (SeatId)12 } };
    Check(settings.Validate().Count > 0);
    settings = new Settings { DeviceAssignments = new() { [" "] = SeatId.A } };
    Throws<InvalidDataException>(() => settings.ToJson());
}

static void AmbiguousSettings()
{
    Throws<InvalidDataException>(() => Settings.Parse("{\"DeviceAssignments\":null}"));
    Throws<InvalidDataException>(() => Settings.Parse("{\"MonitorAssignments\":null}"));
    Throws<InvalidDataException>(() => Settings.Parse("{\"Version\":1,\"Version\":1}"));
    Throws<InvalidDataException>(() => Settings.Parse("{\"DeviceAssignments\":{\"ka\":\"A\",\"ka\":\"B\"}}"));
    Throws<JsonException>(() => Settings.Parse("{\"TypedText\":\"must never be a setting\"}"));
}

static void DuplicateSettingsSlots()
{
    var settings = AssignedSettings();
    settings.MonitorAssignments[SeatId.B] = "monitor-a";
    Check(settings.Validate().Count > 0);
    settings = AssignedSettings();
    settings.DeviceAssignments["kb"] = SeatId.A;
    Check(settings.Validate(Devices()).Count > 0);
    settings.DeviceAssignments["kb"] = SeatId.B;
    settings.DeviceAssignments["mb"] = SeatId.A;
    Check(settings.Validate(Devices()).Count > 0);
}

static void AliasedMonitorSeats()
{
    foreach (var json in new[]
    {
        "{\"MonitorAssignments\":{\"A\":\"monitor-one\",\"a\":\"monitor-two\"}}",
        "{\"MonitorAssignments\":{\"a\":\"monitor-one\",\"A\":\"monitor-two\"}}",
        "{\"MonitorAssignments\":{\"B\":\"monitor-one\",\"b\":\"monitor-two\"}}",
        "{\"MonitorAssignments\":{\"A\":\"monitor-one\",\"a\":\"monitor-one\"}}"
    })
        Throws<InvalidDataException>(() => Settings.Parse(json));

    // Device IDs are ordinal strings, not enum keys: case differences must remain distinct.
    var settings = Settings.Parse("{\"DeviceAssignments\":{\"device\":\"A\",\"DEVICE\":\"B\"}," +
        "\"MonitorAssignments\":{\"a\":\"monitor-one\",\"B\":\"monitor-two\"}}");
    Equal(2, settings.DeviceAssignments.Count);
    Equal(SeatId.A, settings.DeviceAssignments["device"]);
    Equal(SeatId.B, settings.DeviceAssignments["DEVICE"]);
    Equal(2, settings.MonitorAssignments.Count);
    Equal("monitor-one", settings.MonitorAssignments[SeatId.A]);
    Equal("monitor-two", settings.MonitorAssignments[SeatId.B]);
}

static void MalformedSettings()
{
    Throws<JsonException>(() => Settings.Parse("{"));
    Throws<InvalidDataException>(() => Settings.Parse(new string(' ', 1_048_577)));
    Throws<InvalidDataException>(() => Settings.Parse("null"));
}

static void AtomicSettings()
{
    var directory = Path.Combine(AppContext.BaseDirectory, "safeseat-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, "settings.json");
    try
    {
        Equal(0, SettingsStore.Load(path).DeviceAssignments.Count);
        SettingsStore.SaveAtomic(path, AssignedSettings());
        Equal(4, SettingsStore.Load(path).DeviceAssignments.Count);
        var settings = AssignedSettings();
        settings.DeviceAssignments.Remove("kb");
        SettingsStore.SaveAtomic(path, settings);
        Equal(3, SettingsStore.Load(path).DeviceAssignments.Count);
        Equal(1, Directory.GetFiles(directory).Length);
        settings.Version = 5;
        Throws<InvalidDataException>(() => SettingsStore.SaveAtomic(path, settings));
        Equal(3, SettingsStore.Load(path).DeviceAssignments.Count);
    }
    finally
    {
        // Only the individually named files in our fresh directory are removed; no recursive deletion.
        if (File.Exists(path)) File.Delete(path);
        Directory.Delete(directory);
    }
}

static void NegativeMonitor()
{
    var left = new RectD(-1920, -200, 1920, 1080);
    Check(left.IsValid);
    Check(left.Contains(-1920, -200));
    Check(left.Contains(-1, 879));
    Check(!left.Contains(0, 0));
    var upper = new RectD(0, -1080, 1920, 1080);
    Check(upper.Contains(100, -100));
    Check(!upper.Contains(100, 0));
}

static void InvalidMonitor()
{
    Check(!new RectD(0, 0, 0, 10).Contains(0, 0));
    Check(!new RectD(0, 0, double.NaN, 10).IsValid);
    Check(!new RectD(double.PositiveInfinity, 0, 1, 1).IsValid);
    Check(!new RectD(0, 0, 10, 10).Contains(10, 5));
    Check(!new RectD(0, 0, 10, 10).Contains(double.NaN, 5));
}

static void UnknownSources()
{
    var router = Ready();
    Check(!router.ProcessKeyboard("unknown", new(0x41, 0, true)).Accepted);
    router.RegisterDevices(Devices().Append(new("new", "Extra", DeviceKind.Keyboard)));
    Check(!router.ProcessKeyboard("new", new(0x41, 0, true)).Accepted);
    Equal("", router.GetSnapshot(SeatId.A).Text);
    Equal("", router.GetSnapshot(SeatId.B).Text);
}

static void WrongKinds()
{
    var router = Ready();
    Check(!router.ProcessKeyboard("ma", new(0x41, 0, true)).Accepted);
    Check(!router.ProcessMouse("ka", new(1, 1)).Accepted);
    Throws<ArgumentException>(() => router.RegisterDevices([new("bad", "Bad", (DeviceKind)100)]));
}

static void AssignmentSlots()
{
    var router = Ready();
    Throws<InvalidOperationException>(() => router.AssignDevice("kb", SeatId.A));
    Throws<InvalidOperationException>(() => router.AssignDevice("mb", SeatId.A));
    Throws<ArgumentException>(() => router.AssignDevice("unknown", SeatId.A));
    Throws<ArgumentOutOfRangeException>(() => router.AssignDevice("ka", (SeatId)12));
    router.AssignDevice("ka", null);
    router.AssignDevice("kb", SeatId.A);
    Tap(router, "kb", 0x41);
    Equal("a", router.GetSnapshot(SeatId.A).Text);
    Equal("", router.GetSnapshot(SeatId.B).Text);
}

static void Reassignment()
{
    var router = Ready();
    Key(router, "ka", 0x10);
    router.AssignDevice("kb", null);
    router.AssignDevice("ka", SeatId.B);
    Tap(router, "ka", 0x41);
    Equal("a", router.GetSnapshot(SeatId.B).Text);
    Equal(0, router.GetSnapshot(SeatId.A).KeysDown);
}

static void DuplicateNames()
{
    var router = Ready();
    Tap(router, "ka", 0x41);
    Tap(router, "kb", 0x42);
    Equal("a", router.GetSnapshot(SeatId.A).Text);
    Equal("b", router.GetSnapshot(SeatId.B).Text);
}

static void DuplicateIdentities()
{
    var router = Ready();
    Key(router, "ka", 0x10);
    var warnings = router.RegisterDevices(Devices().Append(new("ka", "Duplicate", DeviceKind.Keyboard)));
    Check(warnings.Count > 0);
    Check(!router.ProcessKeyboard("ka", new(0x41, 0, true)).Accepted);
    Equal(0, router.GetSnapshot(SeatId.A).KeysDown);
    Tap(router, "kb", 0x42);
    Equal("b", router.GetSnapshot(SeatId.B).Text);
}

static void RestoredDuplicateSlots()
{
    var settings = AssignedSettings();
    settings.DeviceAssignments["kb"] = SeatId.A;
    var router = new SeatRouter(settings);
    Check(router.RegisterDevices(Devices()).Count > 0);
    router.Start();
    Check(!router.ProcessKeyboard("ka", new(0x41, 0, true)).Accepted);
    Check(!router.ProcessKeyboard("kb", new(0x42, 0, true)).Accepted);
}

static void AmbiguousReconnect()
{
    var settings = AssignedSettings();
    settings.DeviceAssignments["kb"] = SeatId.A;
    var router = new SeatRouter(settings);
    router.RegisterDevices(Devices().Where(d => d.Id != "kb"));
    router.Start();
    Key(router, "ka", 0x10);
    router.RegisterDevices(Devices());
    Equal(0, router.GetSnapshot(SeatId.A).KeysDown);
    router.RegisterDevices(Devices().Where(d => d.Id != "kb"));
    Tap(router, "ka", 0x41);
    Equal("a", router.GetSnapshot(SeatId.A).Text);
}

static void Reconnect()
{
    var router = Ready();
    Key(router, "ka", 0x10);
    router.RegisterDevices(Devices().Where(d => d.Id != "ka"));
    Check(!router.ProcessKeyboard("ka", new(0x41, 0, true)).Accepted);
    Equal(0, router.GetSnapshot(SeatId.A).KeysDown);
    router.RegisterDevices(Devices());
    Tap(router, "ka", 0x41);
    Equal("a", router.GetSnapshot(SeatId.A).Text);
}

static void ChangedIdentity()
{
    var router = Ready();
    router.RegisterDevices(Devices().Where(d => d.Id != "ka").Append(new("new-ka", "Identical Keyboard", DeviceKind.Keyboard)));
    Check(!router.ProcessKeyboard("new-ka", new(0x41, 0, true)).Accepted);
    Check(!router.ProcessKeyboard("ka", new(0x41, 0, true)).Accepted);
    Equal("", router.GetSnapshot(SeatId.A).Text);
}

static void ChangedKind()
{
    var router = Ready();
    Check(router.RegisterDevices(Devices().Where(d => d.Id != "ka").Append(new("ka", "Changed", DeviceKind.Mouse))).Count > 0);
    Check(!router.ProcessMouse("ka", new(1, 1)).Accepted);
    Check(!router.ProcessKeyboard("ka", new(0x41, 0, true)).Accepted);
}

static void InterleavedTyping()
{
    var router = Ready();
    Key(router, "ka", 0x10);
    Key(router, "ka", 0x41);
    Tap(router, "kb", 0x42);
    Key(router, "ka", 0x41, false);
    Key(router, "ka", 0x10, false);
    Key(router, "kb", 0x10);
    Tap(router, "ka", 0x43);
    Tap(router, "kb", 0x44);
    Key(router, "kb", 0x10, false);
    Equal("Ac", router.GetSnapshot(SeatId.A).Text);
    Equal("bD", router.GetSnapshot(SeatId.B).Text);
    Equal(0, router.GetSnapshot(SeatId.A).KeysDown);
    Equal(0, router.GetSnapshot(SeatId.B).KeysDown);
}

static void SidedModifiers()
{
    var router = Ready();
    Key(router, "ka", 0xA0); Key(router, "ka", 0xA1);
    Key(router, "ka", 0xA0, false); Tap(router, "ka", 0x41);
    Key(router, "ka", 0xA1, false); Tap(router, "ka", 0x42);
    Key(router, "ka", 0xA2); Key(router, "ka", 0xA3);
    Key(router, "ka", 0xA2, false); Tap(router, "ka", 0x43);
    Key(router, "ka", 0xA3, false); Tap(router, "ka", 0x44);
    Equal("Abd", router.GetSnapshot(SeatId.A).Text);
    Equal(0, router.GetSnapshot(SeatId.A).KeysDown);
}

static void RepeatAndCaps()
{
    var router = Ready();
    Key(router, "ka", 0x41); Key(router, "ka", 0x41); Key(router, "ka", 0x41);
    Key(router, "ka", 0x41, false);
    Key(router, "ka", 0x14); Key(router, "ka", 0x14); Key(router, "ka", 0x14, false);
    Tap(router, "ka", 0x42);
    Key(router, "ka", 0x10); Tap(router, "ka", 0x43); Key(router, "ka", 0x10, false);
    Tap(router, "kb", 0x44);
    Equal("aaaBc", router.GetSnapshot(SeatId.A).Text);
    Equal("d", router.GetSnapshot(SeatId.B).Text);
}

static void ModifierCombinations()
{
    var router = Ready();
    foreach (var modifier in new[] { 0x11, 0xA2, 0xA3, 0x12, 0xA4, 0xA5, 0x5B, 0x5C })
    {
        Key(router, "ka", modifier);
        Tap(router, "ka", 0x41);
        Tap(router, "ka", 0x08);
        Key(router, "ka", modifier, false);
    }
    Equal("", router.GetSnapshot(SeatId.A).Text);
    Tap(router, "ka", 0x42);
    Equal("b", router.GetSnapshot(SeatId.A).Text);
}

static void TextBehavior()
{
    var router = Ready();
    Key(router, "ka", 0x10);
    foreach (var key in new[] { 0x31, 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0, 0xDB, 0xDC, 0xDD, 0xDE }) Tap(router, "ka", key);
    Key(router, "ka", 0x10, false);
    Equal("!:+<_>?~{|}\"", router.GetSnapshot(SeatId.A).Text);
    Tap(router, "ka", 0x08);
    Tap(router, "ka", 0x0D);
    Tap(router, "ka", 0x09);
    Check(router.GetSnapshot(SeatId.A).Text.EndsWith("\n\t", StringComparison.Ordinal));
    router.ClearSeatText(SeatId.A);
    for (var i = 0; i < 4200; i++) Key(router, "ka", 0x41);
    Equal(SeatRouter.MaximumTextLength, router.GetSnapshot(SeatId.A).Text.Length);
    Equal("", router.GetSnapshot(SeatId.B).Text);
}

static void PointerBounds()
{
    var router = Ready();
    router.ProcessMouse("ma", new(-10000, -10000));
    router.ProcessMouse("mb", new(10000, 10000));
    Equal(0.0, router.GetSnapshot(SeatId.A).PointerX);
    Equal(0.0, router.GetSnapshot(SeatId.A).PointerY);
    Equal(799.0, router.GetSnapshot(SeatId.B).PointerX);
    Equal(499.0, router.GetSnapshot(SeatId.B).PointerY);
    router.ProcessMouse("ma", new(Absolute: true, AbsoluteX: 2, AbsoluteY: -1));
    Equal(799.0, router.GetSnapshot(SeatId.A).PointerX);
    Equal(0.0, router.GetSnapshot(SeatId.A).PointerY);
    Equal(499.0, router.GetSnapshot(SeatId.B).PointerY);
    Throws<ArgumentOutOfRangeException>(() => new SeatRouter(canvasWidth: double.NaN));
    var tiny = new SeatRouter(canvasWidth: 1, canvasHeight: 1);
    Equal(0.0, tiny.GetSnapshot(SeatId.A).PointerX);
    Equal(0.0, tiny.GetSnapshot(SeatId.A).PointerY);
}

static void InvalidEvents()
{
    var router = Ready();
    Check(!router.ProcessKeyboard("ka", new(0, 0, true)).Accepted);
    Check(!router.ProcessKeyboard("ka", new(0x41, -1, true)).Accepted);
    Check(!router.ProcessMouse("ma", new(double.NaN, 0)).Accepted);
    Check(!router.ProcessMouse("ma", new(Buttons: (MouseButtons)256)).Accepted);
    Check(!router.ProcessMouse("ma", new(Absolute: true, AbsoluteX: double.PositiveInfinity)).Accepted);
    Equal(400.0, router.GetSnapshot(SeatId.A).PointerX);
}

static void IndependentClicks()
{
    var router = Ready();
    MoveTo(router, "ma", 50, 310);
    MoveTo(router, "mb", 700, 100);
    router.ProcessMouse("ma", new(Buttons: MouseButtons.LeftDown));
    router.ProcessMouse("ma", new(Buttons: MouseButtons.LeftDown));
    Click(router, "mb");
    Equal(1, router.GetSnapshot(SeatId.A).ClickCount);
    Equal(0, router.GetSnapshot(SeatId.B).ClickCount);
    Check(router.GetSnapshot(SeatId.A).LeftButtonDown);
    Check(!router.GetSnapshot(SeatId.B).LeftButtonDown);
    router.ProcessMouse("ma", new(Buttons: MouseButtons.LeftUp));
    MoveTo(router, "mb", 60, 315);
    Click(router, "mb");
    Equal(1, router.GetSnapshot(SeatId.B).ClickCount);
}

static void IndependentClear()
{
    var router = Ready();
    Tap(router, "ka", 0x41); Tap(router, "kb", 0x42);
    MoveTo(router, "ma", 250, 310); Click(router, "ma");
    Equal("", router.GetSnapshot(SeatId.A).Text);
    Equal("b", router.GetSnapshot(SeatId.B).Text);
}

static void IndependentScroll()
{
    var router = Ready();
    MoveTo(router, "ma", 500, 350);
    MoveTo(router, "mb", 100, 100);
    router.ProcessMouse("ma", new(WheelDelta: -120));
    router.ProcessMouse("mb", new(WheelDelta: -120));
    Equal(40.0, router.GetSnapshot(SeatId.A).ScrollOffset);
    Equal(0.0, router.GetSnapshot(SeatId.B).ScrollOffset);
    MoveTo(router, "mb", 500, 350);
    router.ProcessMouse("mb", new(WheelDelta: -60));
    Equal(20.0, router.GetSnapshot(SeatId.B).ScrollOffset);
    router.ProcessMouse("ma", new(WheelDelta: int.MinValue));
    Equal(4000.0, router.GetSnapshot(SeatId.A).ScrollOffset);
    router.ProcessMouse("ma", new(WheelDelta: int.MaxValue));
    Equal(0.0, router.GetSnapshot(SeatId.A).ScrollOffset);
}

static void MouseDisconnect()
{
    var router = Ready();
    router.ProcessMouse("ma", new(Buttons: MouseButtons.LeftDown | MouseButtons.RightDown));
    Check(router.GetSnapshot(SeatId.A).LeftButtonDown);
    router.RegisterDevices(Devices().Where(d => d.Id != "ma"));
    Check(!router.GetSnapshot(SeatId.A).LeftButtonDown);
    Check(!router.GetSnapshot(SeatId.A).RightButtonDown);
    router.RegisterDevices(Devices());
    MoveTo(router, "ma", 50, 310); Click(router, "ma");
    Equal(1, router.GetSnapshot(SeatId.A).ClickCount);
}

static void StopBehavior()
{
    var router = Ready();
    Key(router, "ka", 0x10); Tap(router, "ka", 0x41); Tap(router, "kb", 0x42);
    router.ProcessMouse("ma", new(Buttons: MouseButtons.LeftDown));
    router.Stop();
    foreach (var seat in Enum.GetValues<SeatId>())
    {
        var state = router.GetSnapshot(seat);
        Check(!state.IsRunning); Equal("", state.Text); Equal(0, state.KeysDown);
        Check(!state.LeftButtonDown && !state.RightButtonDown);
    }
    Check(!router.ProcessKeyboard("ka", new(0x41, 0, true)).Accepted);
    Check(!router.ProcessMouse("mb", new(1, 1)).Accepted);
    router.Start(); Tap(router, "ka", 0x41);
    Equal("a", router.GetSnapshot(SeatId.A).Text);
}

static void StartResets()
{
    var router = Ready();
    Key(router, "ka", 0x10);
    router.Start();
    Tap(router, "ka", 0x41);
    Equal("a", router.GetSnapshot(SeatId.A).Text);
}

static void ConcurrentStreams()
{
    var router = Ready();
    Parallel.Invoke(
        () => { for (var i = 0; i < 500; i++) Tap(router, "ka", 0x41); },
        () => { for (var i = 0; i < 500; i++) Tap(router, "kb", 0x42); },
        () => { for (var i = 0; i < 500; i++) router.ProcessMouse("ma", new(1, -1)); },
        () => { for (var i = 0; i < 500; i++) router.ProcessMouse("mb", new(-1, 1)); });
    Equal(new string('a', 500), router.GetSnapshot(SeatId.A).Text);
    Equal(new string('b', 500), router.GetSnapshot(SeatId.B).Text);
    Equal(799.0, router.GetSnapshot(SeatId.A).PointerX);
    Equal(0.0, router.GetSnapshot(SeatId.B).PointerX);
}

static void SustainedInterleaving()
{
    var random = new Random(8675309);
    var streams = new[] { new List<SimulatedPacket>(), new List<SimulatedPacket>() };
    for (var seat = 0; seat < streams.Length; seat++)
        for (var i = 0; i < 5000; i++)
        {
            var kind = random.Next(10);
            var down = random.Next(2) == 0;
            var keyboard = kind switch
            {
                0 => new KeyboardInput(0x10, 0, down),
                1 => new KeyboardInput(0x11, 0, down),
                2 => new KeyboardInput(0x14, 0, down),
                3 => new KeyboardInput(0x08, 0, down),
                _ => new KeyboardInput(0x41 + random.Next(26), 0, down)
            };
            var mouse = kind switch
            {
                6 => new MouseInput(Absolute: true, AbsoluteX: random.NextDouble(), AbsoluteY: random.NextDouble()),
                7 => new MouseInput(random.Next(-100, 101), random.Next(-100, 101)),
                8 => new MouseInput(Buttons: down ? MouseButtons.LeftDown : MouseButtons.LeftUp),
                _ => new MouseInput(WheelDelta: down ? 120 : -120)
            };
            streams[seat].Add(new SimulatedPacket(kind < 6, keyboard, mouse));
        }
    var baselineA = Ready();
    var baselineB = Ready();
    foreach (var packet in streams[0]) packet.Apply(baselineA, SeatId.A);
    foreach (var packet in streams[1]) packet.Apply(baselineB, SeatId.B);
    var shared = Ready();
    var indexes = new int[2];
    var count = 0;
    while (indexes[0] < 5000 || indexes[1] < 5000)
    {
        var seat = indexes[0] == 5000 ? 1 : indexes[1] == 5000 ? 0 : random.Next(2);
        streams[seat][indexes[seat]++].Apply(shared, (SeatId)seat);
        count++;
    }
    Equal(10000, count);
    // A shared mutable modifier, pointer, click, scroll, or text state would break these comparisons.
    Equal(baselineA.GetSnapshot(SeatId.A), shared.GetSnapshot(SeatId.A));
    Equal(baselineB.GetSnapshot(SeatId.B), shared.GetSnapshot(SeatId.B));
}

internal readonly record struct SimulatedPacket(bool IsKeyboard, KeyboardInput Keyboard, MouseInput Mouse)
{
    public void Apply(SeatRouter router, SeatId seat)
    {
        var suffix = seat == SeatId.A ? "a" : "b";
        var result = IsKeyboard ? router.ProcessKeyboard("k" + suffix, Keyboard)
            : router.ProcessMouse("m" + suffix, Mouse);
        if (!result.Accepted) throw new InvalidOperationException("Seeded packet unexpectedly rejected.");
    }
}
