using SafeSeat.Windows;

internal static class ReadinessChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Readiness check failed: " + name);
            checks++;
        }
        RawDeviceDescriptor Device(string id, RawDeviceKind kind, string? group = null, bool ambiguous = false) =>
            new(id, kind, "Same friendly label", ambiguous, "Synthetic test", CandidateGroupId: group);
        MonitorDescriptor Monitor(string id, int x = 0, bool ambiguous = false) =>
            new(id, "Synthetic monitor", x, 0, 1920, 1080, x, 0, 1920, 1040, x == 0, ambiguous, "Synthetic test");
        RawDeviceDescriptor[] devices = [Device("ka", RawDeviceKind.Keyboard, "group-a"), Device("ma", RawDeviceKind.Mouse, "group-a"),
            Device("kb", RawDeviceKind.Keyboard, "group-b"), Device("mb", RawDeviceKind.Mouse, "group-b")];
        MonitorDescriptor[] monitors = [Monitor("screen-a"), Monitor("screen-b", -1920)];
        var mapping = new Dictionary<string, string> { ["ka"] = "A", ["ma"] = "A", ["kb"] = "B", ["mb"] = "B" };
        var screens = new Dictionary<string, string> { ["A"] = "screen-a", ["B"] = "screen-b" };
        var inventory = new InventorySnapshot(devices, monitors, []);
        var ready = DiscoveryReadiness.Evaluate(inventory, mapping, screens);
        Check(ready.AssignmentsReady && ready.Blockers.Count == 0, "distinct complete assignment including negative monitor origin");
        Check(ready.KeyboardCollections == 2 && ready.MouseCollections == 2 && ready.CandidatePnpGroups == 2,
            "counts are separately named collection and candidate-group counts");
        Check(ready.ConfirmedPhysicalKeyboards is null && ready.ConfirmedPhysicalMice is null && ready.PhysicalAcceptanceStatus == "NOT TESTED",
            "complete structural assignment never becomes physical acceptance");
        Check(!DiscoveryReadiness.Evaluate(inventory).AssignmentsReady, "no automatic assignments from discovery");
        Check(!DiscoveryReadiness.Evaluate(inventory with { Monitors = [monitors[0]] }, mapping, screens).AssignmentsReady, "single monitor blocks two-window start");
        Check(!DiscoveryReadiness.Evaluate(inventory, mapping, new Dictionary<string, string> { ["A"] = "screen-a", ["B"] = "screen-a" }).AssignmentsReady,
            "one monitor cannot be selected twice");
        Check(!DiscoveryReadiness.Evaluate(inventory with { Monitors = [monitors[0], monitors[1] with { IsIdentityAmbiguous = true }] }, mapping, screens).AssignmentsReady,
            "ambiguous monitor fails closed");
        Check(!DiscoveryReadiness.Evaluate(inventory with { Devices = [devices[0] with { IsIdentityAmbiguous = true }, .. devices[1..]] }, mapping, screens).AssignmentsReady,
            "ambiguous assigned collection fails closed");
        Check(!DiscoveryReadiness.Evaluate(inventory with { Devices = devices[..3] }, mapping, screens).AssignmentsReady, "disconnected assigned device prevents readiness");
        Check(!DiscoveryReadiness.Evaluate(inventory with { Devices = [..devices, Device("extra", RawDeviceKind.Keyboard)] },
            new Dictionary<string, string>(mapping) { ["extra"] = "A" }, screens).AssignmentsReady, "two keyboards in one slot rejected");
        Check(!DiscoveryReadiness.Evaluate(inventory, new Dictionary<string, string>(mapping) { ["ka"] = "C" }, screens).AssignmentsReady, "unknown seat rejected");
        var shared = DiscoveryReadiness.Evaluate(inventory with { Devices = devices.Select(d => d with { CandidateGroupId = "receiver" }).ToArray() }, mapping, screens);
        Check(shared.AssignmentsReady && shared.CandidatePnpGroups == 1 && shared.Warnings.Any(w => w.Contains("Both seats", StringComparison.Ordinal)),
            "shared receiver yields warning without false physical-count rejection");
        var unknown = DiscoveryReadiness.Evaluate(inventory with { Devices = devices.Select(d => d with { CandidateGroupId = null }).ToArray() }, mapping, screens);
        Check(unknown.AssignmentsReady && unknown.CandidatePnpGroups == 0 && unknown.UncorrelatedCollections == 4 && unknown.ConfirmedPhysicalKeyboards is null,
            "missing metadata does not disable known collection routing or invent physical counts");
        Check(!DiscoveryReadiness.Evaluate(inventory with { Devices = [..devices, devices[0]] }, mapping, screens).AssignmentsReady, "duplicate collection identity rejected");
        Check(!DiscoveryReadiness.Evaluate(inventory with { Monitors = [monitors[0], monitors[1] with { Width = 0 }] }, mapping, screens).AssignmentsReady,
            "invalid monitor geometry rejected");
        Check(mapping.Count == 4 && screens["B"] == "screen-b" && devices[0].CandidateGroupId == "group-a", "preflight is side-effect free");
        Check(DiscoveryReadiness.SameTopology(inventory, inventory with { Devices = devices.Reverse().ToArray(), Monitors = monitors.Reverse().ToArray() }),
            "enumeration order does not change topology");
        Check(!DiscoveryReadiness.SameTopology(inventory, inventory with { Devices = devices[..3] }), "disconnect between preflight and registration is detected");
        Check(!DiscoveryReadiness.SameTopology(inventory, inventory with { Monitors = [monitors[0], monitors[1] with { Left = 1920 }] }),
            "monitor reposition between preflight and registration is detected");
        Check(DiscoveryReadiness.SameTopology(inventory, inventory with { Devices = devices.Select(d => d with { CandidateGroupId = null }).ToArray() }),
            "optional metadata failures do not invent topology changes");
        return checks;
    }
}
