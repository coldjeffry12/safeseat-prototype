using System.Text.Json;
using System.Text.RegularExpressions;
using SafeSeat.Windows;

internal static class HardwareGroupingChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL: grouping: " + name);
            checks++;
        }
        var a = new RawDeviceDescriptor("raw-keyboard-hash-a", RawDeviceKind.Keyboard, "Identical HID label", false, "Original identity warning");
        var b = a with { Id = "raw-keyboard-hash-b" };
        Guid shared = Guid.Parse("10eb9a11-4fce-4c7d-9fb1-dff72687707a");
        Guid distinct = Guid.Parse("e46cf48e-156a-4b5d-a40d-cc011fe72ec7");
        var groupedA = HardwareGrouping.WithMetadata(a, shared, "HID");
        var groupedB = HardwareGrouping.WithMetadata(b, shared, "HID");
        var distinctB = HardwareGrouping.WithMetadata(b, distinct, "HID");
        Check(groupedA.CandidateGroupId == groupedB.CandidateGroupId, "multiple collections may share one candidate group");
        Check(groupedA.Id != groupedB.Id, "shared group does not merge collection identity");
        Check(groupedA.CandidateGroupId != distinctB.CandidateGroupId, "duplicate labels with distinct containers remain distinct");
        Check(HardwareGrouping.WithMetadata(b with { DisplayName = "Different label" }, shared, null).CandidateGroupId == groupedA.CandidateGroupId,
            "labels are not correlation identity");
        Check(Regex.IsMatch(groupedA.CandidateGroupId!, "^pnp-container-sha256-[0-9a-f]{64}$"), "container is exposed only as hash");
        Check(HardwareGrouping.CandidateGroupId(Guid.Parse(shared.ToString("B").ToUpperInvariant())) == groupedA.CandidateGroupId,
            "canonical GUID representations have stable hash");
        Check(HardwareGrouping.CandidateGroupId(null) is null && HardwareGrouping.CandidateGroupId(Guid.Empty) is null,
            "missing and empty container values stay unknown");
        var unknownA = HardwareGrouping.WithMetadata(a, null, "HID");
        var unknownB = HardwareGrouping.WithMetadata(b, null, "HID");
        Check(unknownA.CandidateGroupId is null && unknownB.CandidateGroupId is null,
            "duplicate names and enumerators never create a guessed group");
        Check(unknownA.Id != unknownB.Id, "unknown collections remain individually identifiable");
        Check(unknownA.IsIdentityAmbiguous == a.IsIdentityAmbiguous && unknownA.StabilityNote == a.StabilityNote,
            "missing optional metadata does not disable working collection identity");
        Check(groupedA.Id == a.Id && groupedA.Kind == a.Kind && groupedA.StabilityNote == a.StabilityNote &&
            groupedA.IsIdentityAmbiguous == a.IsIdentityAmbiguous, "metadata preserves routing-relevant fields");
        var ambiguous = HardwareGrouping.WithMetadata(a with { IsIdentityAmbiguous = true }, shared, "USB");
        Check(ambiguous.IsIdentityAmbiguous, "candidate correlation never repairs ambiguous identity");
        Check(groupedA.TransportHint!.Contains("unknown", StringComparison.OrdinalIgnoreCase), "HID alone cannot establish transport");
        Check(HardwareGrouping.DescribeEnumerator(" usb ")!.Contains("unverified", StringComparison.OrdinalIgnoreCase), "USB hint does not establish physical hardware");
        Check(HardwareGrouping.DescribeEnumerator("BTHENUM")!.Contains("unverified", StringComparison.OrdinalIgnoreCase), "Bluetooth hint remains unverified");
        Check(HardwareGrouping.DescribeEnumerator("ROOT")!.Contains("unknown", StringComparison.OrdinalIgnoreCase), "root enumerator is not automatically classified physical or virtual");
        Check(HardwareGrouping.DescribeEnumerator("RDPBUS")!.Contains("hint", StringComparison.OrdinalIgnoreCase), "remote enumerator is only a hint");
        Check(HardwareGrouping.DescribeEnumerator(null) is null && HardwareGrouping.DescribeEnumerator("unexpected-enumerator") is null,
            "unknown enumerators remain unknown");
        const string rawPath = @"HID\VID_1234&PID_5678\SYNTHETIC-PRIVATE-SERIAL";
        var privateSource = HardwareGrouping.WithMetadata(a, shared, rawPath);
        string json = JsonSerializer.Serialize(privateSource);
        Check(!json.Contains(shared.ToString("D"), StringComparison.OrdinalIgnoreCase) &&
            !json.Contains("SYNTHETIC-PRIVATE-SERIAL", StringComparison.Ordinal) && !json.Contains("VID_1234", StringComparison.Ordinal),
            "raw container IDs, paths and unexpected enumerator contents are not exposed");
        Check(privateSource.TransportHint is null, "unrecognized property string is not echoed");
        Check(groupedA.CorrelationNote!.Contains("not a confirmed physical device", StringComparison.Ordinal) &&
            groupedA.CorrelationNote.Contains("multiple physical devices", StringComparison.Ordinal),
            "candidate metadata explicitly refuses physical-device-count inference");
        Check(unknownA.CorrelationNote!.Contains("unavailable", StringComparison.Ordinal), "failed query remains an explanatory unknown");
        return checks;
    }
}
