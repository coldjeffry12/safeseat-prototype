using System.Text;

namespace SafeSeat.Core;

/// <summary>
/// Routes observations into this application's own state only. It never delivers input to other
/// processes, moves the OS pointer, or creates another Windows keyboard focus.
/// The host must stop/clear this router whenever it ceases to be the foreground application.
/// </summary>
public sealed class SeatRouter
{
    public const int MaximumTextLength = 4096;
    private readonly object _gate = new();
    private readonly double _width;
    private readonly double _height;
    private readonly Dictionary<string, DeviceDescriptor> _devices = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SeatId> _assignments;
    private readonly Dictionary<string, KeyboardState> _keyboardStates = new(StringComparer.Ordinal);
    private readonly Dictionary<SeatId, SeatState> _seats;
    private bool _running;

    public SeatRouter(Settings? settings = null, double canvasWidth = SeatLayout.Width, double canvasHeight = SeatLayout.Height)
    {
        if (!double.IsFinite(canvasWidth) || !double.IsFinite(canvasHeight) || canvasWidth < 1 || canvasHeight < 1)
            throw new ArgumentOutOfRangeException(nameof(canvasWidth), "Canvas dimensions must be finite and at least one.");
        settings ??= new Settings();
        if (settings.Validate().Count > 0) throw new ArgumentException("Invalid settings.", nameof(settings));
        _width = canvasWidth;
        _height = canvasHeight;
        _assignments = new Dictionary<string, SeatId>(settings.DeviceAssignments, StringComparer.Ordinal);
        _seats = Enum.GetValues<SeatId>().ToDictionary(s => s,
            _ => new SeatState(Math.Min(_width / 2, _width - 1), Math.Min(_height / 2, _height - 1)));
    }

    public IReadOnlyList<string> RegisterDevices(IEnumerable<DeviceDescriptor> devices)
    {
        ArgumentNullException.ThrowIfNull(devices);
        var supplied = devices.ToArray();
        if (supplied.Any(d => d is null || string.IsNullOrWhiteSpace(d.Id) || !Enum.IsDefined(d.Kind)))
            throw new ArgumentException("Descriptors require a nonempty ID and valid kind.", nameof(devices));
        lock (_gate)
        {
            var warnings = new List<string>();
            var next = _devices.ToDictionary(p => p.Key, p => p.Value with { Connected = false }, StringComparer.Ordinal);
            foreach (var group in supplied.GroupBy(d => d.Id, StringComparer.Ordinal))
            {
                var descriptor = group.First();
                if (group.Count() > 1)
                {
                    warnings.Add($"Ambiguous identity {descriptor.Id}; routing disabled.");
                    descriptor = descriptor with { Connected = false, IdentityWarning = "Duplicate device identity; routing disabled." };
                }
                if (_devices.TryGetValue(descriptor.Id, out var old) && old.Kind != descriptor.Kind)
                {
                    warnings.Add($"Identity kind changed for {descriptor.Id}; routing disabled.");
                    descriptor = old with { Connected = false, IdentityWarning = "Device identity changed kind; remove assignment and recheck hardware." };
                }
                next[descriptor.Id] = descriptor;
            }
            foreach (var (id, descriptor) in next)
            {
                if (!_devices.TryGetValue(id, out var previous) || !descriptor.Connected || !previous.Connected || previous.Kind != descriptor.Kind)
                    ResetDevice(id);
            }
            _devices.Clear();
            foreach (var pair in next) _devices.Add(pair.Key, pair.Value);
            // Restored configuration can contain two known devices in a slot. Neither may win implicitly.
            foreach (var slot in _assignments.Where(a => _devices.TryGetValue(a.Key, out var d) && d.Connected)
                .GroupBy(a => (a.Value, _devices[a.Key].Kind)).Where(g => g.Count() > 1))
            {
                warnings.Add($"Seat {slot.Key.Value} has multiple {slot.Key.Kind} assignments; routing for that slot is disabled.");
                foreach (var assignment in slot) ResetDevice(assignment.Key);
            }
            return warnings;
        }
    }

    public void AssignDevice(string deviceId, SeatId? seat)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        if (seat.HasValue && !Enum.IsDefined(seat.Value)) throw new ArgumentOutOfRangeException(nameof(seat));
        lock (_gate)
        {
            if (seat.HasValue)
            {
                if (!_devices.TryGetValue(deviceId, out var device)) throw new ArgumentException("Unknown device.", nameof(deviceId));
                if (_assignments.Any(a => a.Key != deviceId && a.Value == seat.Value &&
                    _devices.TryGetValue(a.Key, out var other) && other.Kind == device.Kind))
                    throw new InvalidOperationException($"Seat {seat.Value} already has a {device.Kind.ToString().ToLowerInvariant()} assignment.");
            }
            ResetDevice(deviceId);
            if (seat.HasValue) _assignments[deviceId] = seat.Value;
            else _assignments.Remove(deviceId);
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            ClearHeldState();
            _running = true;
            foreach (var seat in _seats.Values) seat.LastAction = "Ready — US ASCII demo";
        }
    }

    /// <summary>
    /// Immediately stops routing and clears logical text buffers and held state. Managed snapshot
    /// strings are not securely zeroed; never use this demonstration for credentials or secrets.
    /// </summary>
    public void Stop()
    {
        lock (_gate)
        {
            _running = false;
            ClearHeldState();
            foreach (var seat in _seats.Values)
            {
                seat.Text.Clear();
                seat.LastAction = "Stopped; text cleared";
            }
        }
    }

    public void ClearSeatText(SeatId seat)
    {
        lock (_gate) _seats[seat].Text.Clear();
    }

    public SeatSnapshot GetSnapshot(SeatId seat)
    {
        lock (_gate)
        {
            var state = _seats[seat];
            var keys = _keyboardStates.Where(k => _assignments.TryGetValue(k.Key, out var assigned) && assigned == seat)
                .Sum(k => k.Value.Down.Count);
            return new SeatSnapshot(seat, _running, state.Text.ToString(), state.X, state.Y,
                state.ClickCount, state.Scroll, keys, state.Left, state.Right, state.LastAction);
        }
    }

    public RouteResult ProcessKeyboard(string deviceId, KeyboardInput input)
    {
        lock (_gate)
        {
            var reason = CheckRoute(deviceId, DeviceKind.Keyboard, out var seatId);
            if (reason is not null) return RouteResult.Reject(reason);
            if (input.VirtualKey < 1 || input.VirtualKey > 255 || input.ScanCode < 0 || input.ScanCode > 65535)
                return RouteResult.Reject("Invalid keyboard event.");
            if (!_keyboardStates.TryGetValue(deviceId, out var keyboard))
                _keyboardStates[deviceId] = keyboard = new KeyboardState();
            var state = _seats[seatId];
            var firstDown = input.IsDown && keyboard.Down.Add(input.VirtualKey);
            if (!input.IsDown)
            {
                keyboard.Down.Remove(input.VirtualKey);
                return Accepted(seatId, "Key up");
            }
            if (input.VirtualKey == 0x14 && firstDown) keyboard.Caps = !keyboard.Caps;
            if (HasAny(keyboard, 0x11, 0xA2, 0xA3, 0x12, 0xA4, 0xA5, 0x5B, 0x5C))
                return Accepted(seatId, "Combination ignored");
            if (input.VirtualKey == 0x08)
            {
                if (state.Text.Length > 0) state.Text.Length--;
                return Accepted(seatId, "Backspace");
            }
            var shift = HasAny(keyboard, 0x10, 0xA0, 0xA1);
            var character = TranslateAscii(input.VirtualKey, shift, keyboard.Caps);
            if (character.HasValue)
            {
                if (state.Text.Length < MaximumTextLength) state.Text.Append(character.Value);
                return Accepted(seatId, state.Text.Length >= MaximumTextLength ? "Text limit reached" : "Text input");
            }
            return Accepted(seatId, "Non-text key (US ASCII only)");
        }
    }

    public RouteResult ProcessMouse(string deviceId, MouseInput input)
    {
        lock (_gate)
        {
            var reason = CheckRoute(deviceId, DeviceKind.Mouse, out var seatId);
            if (reason is not null) return RouteResult.Reject(reason);
            const MouseButtons allowed = MouseButtons.LeftDown | MouseButtons.LeftUp | MouseButtons.RightDown |
                MouseButtons.RightUp | MouseButtons.MiddleDown | MouseButtons.MiddleUp;
            if ((input.Buttons & ~allowed) != 0 || !double.IsFinite(input.DeltaX) || !double.IsFinite(input.DeltaY) ||
                (input.Absolute && (!double.IsFinite(input.AbsoluteX) || !double.IsFinite(input.AbsoluteY))))
                return RouteResult.Reject("Invalid mouse event.");
            var state = _seats[seatId];
            state.X = input.Absolute ? Math.Clamp(input.AbsoluteX, 0, 1) * (_width - 1)
                : Math.Clamp(state.X + input.DeltaX, 0, _width - 1);
            state.Y = input.Absolute ? Math.Clamp(input.AbsoluteY, 0, 1) * (_height - 1)
                : Math.Clamp(state.Y + input.DeltaY, 0, _height - 1);
            var action = "Pointer move";
            if (input.Buttons.HasFlag(MouseButtons.LeftDown) && !state.Left)
            {
                state.Left = true;
                action = "Left down";
                if (SeatLayout.CounterButton.Contains(state.X, state.Y))
                {
                    if (state.ClickCount < int.MaxValue) state.ClickCount++;
                    action = "Counter click";
                }
                else if (SeatLayout.ClearButton.Contains(state.X, state.Y))
                {
                    state.Text.Clear();
                    action = "Clear text click";
                }
            }
            if (input.Buttons.HasFlag(MouseButtons.LeftUp)) { state.Left = false; action = "Left up"; }
            if (input.Buttons.HasFlag(MouseButtons.RightDown)) { state.Right = true; action = "Right down"; }
            if (input.Buttons.HasFlag(MouseButtons.RightUp)) { state.Right = false; action = "Right up"; }
            if (input.WheelDelta != 0)
            {
                if (SeatLayout.ScrollPanel.Contains(state.X, state.Y))
                {
                    state.Scroll = Math.Clamp(state.Scroll - input.WheelDelta / 120.0 * 40, 0, 4000);
                    action = "Panel scroll";
                }
                else action = "Wheel outside scroll panel";
            }
            return Accepted(seatId, action);
        }
    }

    private string? CheckRoute(string id, DeviceKind kind, out SeatId seat)
    {
        seat = default;
        if (!_running) return "Demo is stopped.";
        if (id is null || !_devices.TryGetValue(id, out var device)) return "Unknown device.";
        if (!device.Connected) return "Device is disconnected or identity is ambiguous.";
        if (device.Kind != kind) return "Device kind does not match event.";
        if (!_assignments.TryGetValue(id, out seat)) return "Device is unassigned.";
        var assignedSeat = seat;
        if (_assignments.Count(a => a.Value == assignedSeat && _devices.TryGetValue(a.Key, out var other) &&
            other.Connected && other.Kind == kind) != 1) return "Multiple devices assigned to this seat slot.";
        return null;
    }

    private RouteResult Accepted(SeatId seat, string action)
    {
        _seats[seat].LastAction = action;
        return RouteResult.Accept(seat, action);
    }

    private void ResetDevice(string id)
    {
        _keyboardStates.Remove(id);
        if (_assignments.TryGetValue(id, out var seat))
        {
            _seats[seat].Left = false;
            _seats[seat].Right = false;
        }
    }

    private void ClearHeldState()
    {
        _keyboardStates.Clear();
        foreach (var state in _seats.Values) { state.Left = false; state.Right = false; }
    }

    private static bool HasAny(KeyboardState state, params int[] keys) => keys.Any(state.Down.Contains);

    private static char? TranslateAscii(int key, bool shift, bool caps)
    {
        if (key is >= 0x41 and <= 0x5A) return (char)((shift ^ caps) ? key : key + 32);
        if (key is >= 0x30 and <= 0x39) return shift ? ")!@#$%^&*("[key - 0x30] : (char)key;
        return key switch
        {
            0x20 => ' ', 0x0D => '\n', 0x09 => '\t',
            0xBA => shift ? ':' : ';', 0xBB => shift ? '+' : '=',
            0xBC => shift ? '<' : ',', 0xBD => shift ? '_' : '-',
            0xBE => shift ? '>' : '.', 0xBF => shift ? '?' : '/',
            0xC0 => shift ? '~' : '`', 0xDB => shift ? '{' : '[',
            0xDC => shift ? '|' : '\\', 0xDD => shift ? '}' : ']',
            0xDE => shift ? '"' : '\'',
            _ => null
        };
    }

    private sealed class KeyboardState
    {
        public HashSet<int> Down { get; } = new();
        public bool Caps { get; set; }
    }

    private sealed class SeatState(double x, double y)
    {
        public StringBuilder Text { get; } = new();
        public double X { get; set; } = x;
        public double Y { get; set; } = y;
        public int ClickCount { get; set; }
        public double Scroll { get; set; }
        public bool Left { get; set; }
        public bool Right { get; set; }
        public string LastAction { get; set; } = "Stopped";
    }
}
