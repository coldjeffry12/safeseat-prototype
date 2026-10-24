namespace SafeSeat.Windows;

/// <summary>A structural preflight, never evidence of physical independence.</summary>
public sealed record DiscoveryReadinessReport(
    int KeyboardCollections, int MouseCollections, int EnumeratedMonitors,
    int CandidatePnpGroups, int UncorrelatedCollections, bool AssignmentsReady,
    int? ConfirmedPhysicalKeyboards, int? ConfirmedPhysicalMice,
    string PhysicalAcceptanceStatus, IReadOnlyList<string> Blockers, IReadOnlyList<string> Warnings)
{
    /// <summary>Concise UI wording keeps counts distinct from physical acceptance.</summary>
    public string Summary() =>
        $"Discovery: {KeyboardCollections} keyboard / {MouseCollections} mouse collections; " +
        $"{CandidatePnpGroups} candidate PnP groups; {UncorrelatedCollections} uncorrelated; " +
        $"{EnumeratedMonitors} monitor entries. " +
        (AssignmentsReady ? "Assignment structure ready. " : "Assignment structure incomplete. ") +
        "Physical device counts and simultaneous two-seat acceptance: NOT TESTED.";
}

/// <summary>
/// Uses collection identities for assignment. PnP containers are a correlation hint only: a USB
/// receiver, composite device, virtual provider or Bluetooth container is not a human/device count.
/// </summary>
public static class DiscoveryReadiness
{
    /// <summary>Ordering and optional metadata are irrelevant; assigned source/display topology is not.</summary>
    public static bool SameTopology(InventorySnapshot expected, InventorySnapshot current)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(current);
        var devicesA = expected.Devices.Select(d => (d.Id, d.Kind, d.IsIdentityAmbiguous)).OrderBy(d => d.Id, StringComparer.Ordinal);
        var devicesB = current.Devices.Select(d => (d.Id, d.Kind, d.IsIdentityAmbiguous)).OrderBy(d => d.Id, StringComparer.Ordinal);
        var monitorsA = expected.Monitors.Select(m => (m.Id, m.Left, m.Top, m.Width, m.Height, m.WorkLeft, m.WorkTop, m.WorkWidth, m.WorkHeight, m.IsIdentityAmbiguous))
            .OrderBy(m => m.Id, StringComparer.Ordinal);
        var monitorsB = current.Monitors.Select(m => (m.Id, m.Left, m.Top, m.Width, m.Height, m.WorkLeft, m.WorkTop, m.WorkWidth, m.WorkHeight, m.IsIdentityAmbiguous))
            .OrderBy(m => m.Id, StringComparer.Ordinal);
        return devicesA.SequenceEqual(devicesB) && monitorsA.SequenceEqual(monitorsB);
    }

    public static DiscoveryReadinessReport Evaluate(InventorySnapshot inventory,
        IReadOnlyDictionary<string, string>? deviceAssignments = null,
        IReadOnlyDictionary<string, string>? monitorAssignments = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        deviceAssignments ??= new Dictionary<string, string>();
        monitorAssignments ??= new Dictionary<string, string>();
        string[] seats = ["A", "B"];
        var blockers = new List<string>();
        var warnings = new List<string>(inventory.Warnings);
        var devices = inventory.Devices.Where(d => d.Kind is RawDeviceKind.Keyboard or RawDeviceKind.Mouse).ToArray();
        if (devices.GroupBy(d => d.Id, StringComparer.Ordinal).Any(g => g.Count() != 1))
            blockers.Add("Duplicate collection identity; assignment cannot be resolved safely.");
        if (inventory.Monitors.GroupBy(m => m.Id, StringComparer.Ordinal).Any(g => g.Count() != 1))
            blockers.Add("Duplicate monitor identity; review extended display configuration.");
        if (inventory.Monitors.Count < 2)
            blockers.Add("Two distinct monitor entries are required for the controlled two-window demonstration.");
        foreach (var assignment in deviceAssignments)
        {
            if (!seats.Contains(assignment.Value, StringComparer.Ordinal))
                blockers.Add("A device assignment uses an unsupported seat.");
            if (!devices.Any(d => d.Id == assignment.Key))
                blockers.Add("A saved device collection is unavailable; review its assignment.");
        }
        foreach (var assignment in monitorAssignments)
            if (!seats.Contains(assignment.Key, StringComparer.Ordinal))
                blockers.Add("A monitor assignment uses an unsupported seat.");
        foreach (string seat in seats)
        {
            foreach (var kind in new[] { RawDeviceKind.Keyboard, RawDeviceKind.Mouse })
            {
                var assigned = devices.Where(d => d.Kind == kind &&
                    deviceAssignments.TryGetValue(d.Id, out string? assignedSeat) && assignedSeat == seat).ToArray();
                if (assigned.Length != 1 || assigned.Any(d => d.IsIdentityAmbiguous))
                    blockers.Add($"Seat {seat} needs exactly one available, unambiguous {kind.ToString().ToLowerInvariant()} collection.");
            }
            if (!monitorAssignments.TryGetValue(seat, out string? monitorId))
                blockers.Add($"Seat {seat} has no selected monitor.");
            else
            {
                var matching = inventory.Monitors.Where(m => m.Id == monitorId).ToArray();
                if (matching.Length != 1 || matching.Any(m => m.IsIdentityAmbiguous ||
                    m.Width <= 0 || m.Height <= 0 || m.WorkWidth <= 0 || m.WorkHeight <= 0))
                    blockers.Add($"Seat {seat}'s selected monitor is missing, ambiguous or has invalid geometry.");
            }
        }
        if (monitorAssignments.TryGetValue("A", out string? monitorA) &&
            monitorAssignments.TryGetValue("B", out string? monitorB) && monitorA == monitorB)
            blockers.Add("Seats A and B must select different monitor identities.");
        foreach (var group in devices.Where(d => !string.IsNullOrWhiteSpace(d.CandidateGroupId))
            .GroupBy(d => d.CandidateGroupId!, StringComparer.Ordinal))
        {
            var assignedSeats = group.Where(d => deviceAssignments.ContainsKey(d.Id))
                .Select(d => deviceAssignments[d.Id]).Distinct(StringComparer.Ordinal).ToArray();
            if (assignedSeats.Length > 1)
                warnings.Add("Both seats use collections in one candidate PnP group. Verify the physical devices manually; containers/receivers are not physical device counts.");
        }
        warnings.Add("PnP groups and Raw Input collections do not establish the number or ownership of physical keyboards/mice. Confirm by operating each device separately.");
        warnings.Add("Structural readiness does not verify layout, foreground/session notifications, visible placement, native recovery, licensing or third-party application independence.");
        return new DiscoveryReadinessReport(
            devices.Count(d => d.Kind == RawDeviceKind.Keyboard), devices.Count(d => d.Kind == RawDeviceKind.Mouse),
            inventory.Monitors.Count,
            devices.Where(d => !string.IsNullOrWhiteSpace(d.CandidateGroupId)).Select(d => d.CandidateGroupId).Distinct(StringComparer.Ordinal).Count(),
            devices.Count(d => string.IsNullOrWhiteSpace(d.CandidateGroupId)), blockers.Count == 0,
            null, null, "NOT TESTED", blockers.Distinct(StringComparer.Ordinal).ToArray(), warnings.Distinct(StringComparer.Ordinal).ToArray());
    }
}
