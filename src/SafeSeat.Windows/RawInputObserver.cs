using System.ComponentModel;
using System.Diagnostics;

namespace SafeSeat.Windows;

/// <summary>
/// An explicit opt-in observer of two generic-desktop usage classes. Call from one UI thread.
/// It preserves legacy OS input. It does not install a hook, capture input, inject input, or change focus.
/// </summary>
public sealed class RawInputObserver : IDisposable
{
    public const int WM_INPUT = 0x00ff;
    public const int WM_INPUT_DEVICE_CHANGE = 0x00fe;
    public const int WM_DISPLAYCHANGE = 0x007e;

    private static readonly object RegistrationLock = new();
    private static RawInputObserver? _activeOwner;
    private readonly IRawInputBackend _backend;
    private Dictionary<nint, RawDeviceDescriptor> _devices = [];
    private bool _disposed;
    private bool _acceptInput;
    /// <summary>Registration is active or cleanup remains outstanding after a native failure.</summary>
    public bool IsRunning { get; private set; }
    public InventorySnapshot CurrentInventory { get; private set; } = new([], [], []);

    public RawInputObserver() : this(new Win32RawInputBackend()) { }

    internal RawInputObserver(IRawInputBackend backend) =>
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));

    /// <summary>
    /// Read-only. Safe before Start. During a run, pauses packet acceptance while enumerating;
    /// changed handle/identity mappings require Stop and a fresh explicit Start before routing resumes.
    /// </summary>
    public InventorySnapshot Refresh()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        bool wasAccepting = _acceptInput;
        _acceptInput = false;
        try
        {
            var result = _backend.ReadInventory();
            var devices = result.Devices.ToDictionary(entry => entry.Key, entry => entry.Value);
            bool mappingChanged = devices.Count != _devices.Count || devices.Any(entry =>
                !_devices.TryGetValue(entry.Key, out var previous) ||
                !StringComparer.Ordinal.Equals(previous.Id, entry.Value.Id) ||
                previous.Kind != entry.Value.Kind || previous.IsIdentityAmbiguous != entry.Value.IsIdentityAmbiguous);
            _devices = devices;
            CurrentInventory = result.Snapshot;
            // A plain metadata refresh may preserve a run; reconnects and prior cleanup failures
            // must never revive packet acceptance through this discovery API.
            _acceptInput = IsRunning && wasAccepting && !mappingChanged;
            return CurrentInventory;
        }
        catch
        {
            // A failed refresh must not leave stale handles eligible for routing. Registration
            // cleanup remains reachable through Stop/Dispose; restart requires successful cleanup.
            _devices.Clear();
            CurrentInventory = new([], [], []);
            _acceptInput = false;
            throw;
        }
    }

    /// <summary>
    /// Call only after an explicit user action, passing a live application-owned HWND.
    /// Registration affects this process's raw-input destination, not the Windows input policy.
    /// </summary>
    public void Start(nint hwnd)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _backend.EnsureSupportedPlatform();
        var ownership = hwnd == 0 ? WindowOwnership.Invalid : _backend.ValidateWindow(hwnd);
        if (ownership == WindowOwnership.Invalid)
            throw new ArgumentException("A live application-owned window handle is required.", nameof(hwnd));
        if (ownership != WindowOwnership.Owned)
            throw new ArgumentException("The target window must belong to this application process.", nameof(hwnd));
        lock (RegistrationLock)
        {
            if (IsRunning)
            {
                if (!_acceptInput)
                    throw new InvalidOperationException("Raw Input cleanup is pending. Retry Stop before starting again.");
                return;
            }
            if (_activeOwner is not null)
                throw new InvalidOperationException("Only one SafeSeat Raw Input observer may register in this process.");
            Refresh();
            // Default delivery is foreground-application only. Do not opt into INPUTSINK:
            // another app's input is never needed to route these app-owned demo windows.
            var registration = _backend.Register(Native.RidevDevNotify, hwnd);
            if (!registration.Succeeded)
            {
                var rollback = _backend.Register(Native.RidevRemove, 0);
                var error = new Win32Exception(registration.ErrorCode, "Raw Input observation registration failed. No input suppression was requested.");
                if (!rollback.Succeeded)
                {
                    error.Data["UnregisterWin32Code"] = rollback.ErrorCode;
                    // Keep cleanup reachable through Stop/Dispose if Windows rejected rollback.
                    IsRunning = true;
                    _activeOwner = this;
                }
                throw error;
            }
            IsRunning = true;
            _activeOwner = this;
            _acceptInput = true;
        }
    }

    /// <summary>Idempotent on success. Removes both usage registrations with NULL target, as Windows requires.</summary>
    public void Stop()
    {
        lock (RegistrationLock)
        {
            _acceptInput = false;
            if (!IsRunning)
                return;
            var removal = _backend.Register(Native.RidevRemove, 0);
            if (!removal.Succeeded)
                throw new Win32Exception(removal.ErrorCode, "Raw Input observation unregistration failed.");
            IsRunning = false;
            if (ReferenceEquals(_activeOwner, this))
                _activeOwner = null;
        }
    }

    /// <summary>
    /// Invoke from the HWND hook. Never mark WM_INPUT handled: foreground RIM_INPUT must still
    /// reach DefWindowProc for system cleanup. Returns no text and never writes event data to disk.
    /// </summary>
    public RawInputMessage? ProcessMessage(int message, nint wParam, nint lParam)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsRunning || !_acceptInput)
            return null;
        if (message == WM_INPUT_DEVICE_CHANGE)
        {
            _acceptInput = false;
            return new RawInputMessage(RawInputMessageKind.DevicesChanged, Inventory: Refresh());
        }
        if (message != WM_INPUT || lParam == 0 || !IsForegroundInputCode(wParam))
            return null;
        var packet = _backend.ReadPacket(lParam);
        if (packet is null)
            return null;
        nint handle = IntPtr.Size == 8
            ? unchecked((nint)(long)packet.DeviceHandle)
            : unchecked((nint)(int)packet.DeviceHandle);
        if (!_devices.TryGetValue(handle, out var descriptor))
        {
            // A new handle can be a reconnect of an already assigned path. Discovery must not
            // silently admit its first packet to that saved seat, even if its identity hash matches.
            _acceptInput = false;
            return new RawInputMessage(RawInputMessageKind.DevicesChanged, Inventory: Refresh());
        }
        if (descriptor.IsIdentityAmbiguous || descriptor.Kind != packet.Kind)
            return null; // Never guess the physical source of an event.
        return new RawInputMessage(RawInputMessageKind.DeviceInput,
            new DeviceInput(descriptor.Id, packet.Kind, Stopwatch.GetTimestamp(), packet.Keyboard, packet.Mouse));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        Stop();
        _devices.Clear();
        CurrentInventory = new([], [], []);
        _disposed = true;
    }

    /// <summary>
    /// Equivalent to GET_RAWINPUT_CODE_WPARAM(wParam) == RIM_INPUT. Reject background packets
    /// by their generation-time code, even if foreground ownership changed again before dispatch.
    /// This guard must run before GetRawInputData, not after parsing keyboard content.
    /// </summary>
    public static bool IsForegroundInputCode(nint wParam) => (unchecked((nuint)wParam) & 0xff) == 0;

}
