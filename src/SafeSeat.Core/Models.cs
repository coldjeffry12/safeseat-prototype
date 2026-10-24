using System.Security.Cryptography;
using System.Text;

namespace SafeSeat.Core;

public enum SeatId { A, B }
public enum DeviceKind { Keyboard, Mouse }

/// <summary>A redacted identity. Raw OS device paths should not be logged or persisted.</summary>
public sealed record DeviceDescriptor(string Id, string Name, DeviceKind Kind,
    bool Connected = true, string? IdentityWarning = null);

public static class DeviceIdentity
{
    /// <summary>Deterministic path hash, not a guarantee that HID identity survives a reconnect.</summary>
    public static string Hash(string rawIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawIdentity);
        return "sha256_" + Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(rawIdentity.ToUpperInvariant()))).ToLowerInvariant();
    }
}

/// <summary>Physical desktop coordinates may be negative. Right/bottom edges are exclusive.</summary>
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public bool IsValid => double.IsFinite(X) && double.IsFinite(Y) &&
        double.IsFinite(Width) && double.IsFinite(Height) && Width > 0 && Height > 0 &&
        double.IsFinite(X + Width) && double.IsFinite(Y + Height);

    public bool Contains(double x, double y) => IsValid && double.IsFinite(x) &&
        double.IsFinite(y) && x >= X && x < X + Width && y >= Y && y < Y + Height;
}

public sealed record MonitorDescriptor(string Id, string Name, RectD Bounds, bool IsPrimary = false);

public static class SeatLayout
{
    public const double Width = 800;
    public const double Height = 500;
    public static RectD TextArea { get; } = new(40, 100, 720, 170);
    public static RectD CounterButton { get; } = new(40, 290, 180, 60);
    public static RectD ClearButton { get; } = new(240, 290, 180, 60);
    public static RectD ScrollPanel { get; } = new(460, 290, 300, 170);
}

public readonly record struct KeyboardInput(int VirtualKey, int ScanCode, bool IsDown, bool IsExtended = false);

[Flags]
public enum MouseButtons
{
    None = 0,
    LeftDown = 1,
    LeftUp = 2,
    RightDown = 4,
    RightUp = 8,
    MiddleDown = 16,
    MiddleUp = 32
}

/// <summary>Absolute coordinates are normalized [0,1] within the owned demo canvas.</summary>
public readonly record struct MouseInput(double DeltaX = 0, double DeltaY = 0,
    MouseButtons Buttons = MouseButtons.None, int WheelDelta = 0,
    bool Absolute = false, double AbsoluteX = 0, double AbsoluteY = 0);

public sealed record RouteResult(bool Accepted, SeatId? Seat, string Action, string Reason)
{
    internal static RouteResult Reject(string reason) => new(false, null, "Ignored", reason);
    internal static RouteResult Accept(SeatId seat, string action) => new(true, seat, action, "");
}

public sealed record SeatSnapshot(SeatId Seat, bool IsRunning, string Text,
    double PointerX, double PointerY, int ClickCount, double ScrollOffset, int KeysDown,
    bool LeftButtonDown, bool RightButtonDown, string LastAction);
