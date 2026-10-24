using System.ComponentModel;
using SafeSeat.Windows;

/// <summary>Exercises the observer state machine with a completely simulated backend.</summary>
internal static class RegistrationLifecycleChecks
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL fake observer lifecycle: " + name);
            checks++;
        }

        T Throws<T>(Action action, string name) where T : Exception
        {
            try { action(); }
            catch (T error) { checks++; return error; }
            throw new InvalidOperationException("FAIL fake observer lifecycle: expected " + typeof(T).Name + " for " + name);
        }

        // Every observer started below receives FakeBackend explicitly. No fake delegates to Win32.
        var stoppedBackend = new FakeBackend();
        using (var observer = new RawInputObserver(stoppedBackend))
        {
            Check(!observer.IsRunning, "initially stopped");
            Check(observer.CurrentInventory.Devices.Count == 0, "no automatic inventory");
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null, "stopped message ignored");
            observer.Stop(); observer.Stop();
            Check(stoppedBackend.TotalCalls == 0, "constructor, stopped processing and Stop never touch backend");
        }
        Check(stoppedBackend.TotalCalls == 0, "unstarted Dispose never touches backend");

        var invalidBackend = new FakeBackend { Ownership = WindowOwnership.Invalid };
        using (var observer = new RawInputObserver(invalidBackend))
        {
            Throws<ArgumentException>(() => observer.Start(0), "null HWND");
            Check(invalidBackend.ValidateCalls == 0, "null HWND rejected before ownership lookup");
            Throws<ArgumentException>(() => observer.Start(12), "invalid HWND");
            invalidBackend.Ownership = WindowOwnership.Foreign;
            Throws<ArgumentException>(() => observer.Start(12), "foreign HWND");
            Check(invalidBackend.InventoryCalls == 0 && invalidBackend.Registrations.Count == 0,
                "invalid or foreign window never inventories or registers");
        }

        var unsupportedBackend = new FakeBackend { PlatformError = new PlatformNotSupportedException("simulated platform") };
        using (var observer = new RawInputObserver(unsupportedBackend))
        {
            Throws<PlatformNotSupportedException>(() => observer.Start(12), "unsupported platform");
            Check(unsupportedBackend.ValidateCalls == 0 && unsupportedBackend.Registrations.Count == 0,
                "platform failure never validates window or registers");
        }

        var discoveryFailure = new FakeBackend { InventoryError = new InvalidOperationException("simulated discovery failure") };
        using (var observer = new RawInputObserver(discoveryFailure))
        {
            Throws<InvalidOperationException>(() => observer.Start(12), "Start inventory failure");
            Check(discoveryFailure.Registrations.Count == 0 && !observer.IsRunning, "failed discovery never registers");
        }

        var normal = new FakeBackend();
        using (var observer = new RawInputObserver(normal))
        {
            observer.Start(12);
            Check(observer.IsRunning, "successful start owns registration");
            Check(normal.InventoryCalls == 1 && normal.Registrations.Count == 1, "inventory precedes one registration");
            Check(normal.Registrations[0] == (0x2000u, (nint)12), "only DEVNOTIFY foreground flags and owned target");
            Check((normal.Registrations[0].Flags & (0x100u | 0x1000u | 0x30u)) == 0,
                "no INPUTSINK, EXINPUTSINK or NOLEGACY flags");
            observer.Start(12); observer.Start(13);
            Check(normal.Registrations.Count == 1 && normal.InventoryCalls == 1, "same owner double Start is idempotent");
            observer.Stop(); observer.Stop();
            Check(!observer.IsRunning, "Stop releases ownership");
            Check(normal.Registrations.Count == 2 && normal.Registrations[1] == (1u, (nint)0),
                "Stop removes usages with NULL target exactly once");
        }

        var ownerBackend = new FakeBackend();
        var contenderBackend = new FakeBackend();
        using (var owner = new RawInputObserver(ownerBackend))
        using (var contender = new RawInputObserver(contenderBackend))
        {
            owner.Start(12);
            Throws<InvalidOperationException>(() => contender.Start(13), "second process observer");
            Check(contenderBackend.Registrations.Count == 0 && contenderBackend.InventoryCalls == 0,
                "blocked contender cannot disturb registration or discovery");
            contender.Stop();
            Check(ownerBackend.Registrations.Count == 1 && owner.IsRunning, "non-owner Stop cannot unregister owner");
            owner.Stop(); contender.Start(13);
            Check(contender.IsRunning && contenderBackend.Registrations.Count == 1,
                "ownership can transfer only after successful Stop");
        }

        var failedStart = new FakeBackend();
        failedStart.Results.Enqueue(new(false, 5));
        failedStart.Results.Enqueue(new(true));
        using (var observer = new RawInputObserver(failedStart))
        {
            var error = Throws<Win32Exception>(() => observer.Start(12), "failed registration");
            Check(error.NativeErrorCode == 5, "registration error code preserved across rollback");
            Check(!observer.IsRunning && failedStart.Registrations.Count == 2 &&
                failedStart.Registrations[1] == (1u, (nint)0), "failed Start rolls back with NULL target");
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null && failedStart.PacketReads == 0,
                "rolled-back observer cannot read packets");
            observer.Start(12);
            Check(observer.IsRunning, "successful rollback permits explicit retry");
        }

        var failedRollback = new FakeBackend();
        failedRollback.Results.Enqueue(new(false, 5));
        failedRollback.Results.Enqueue(new(false, 87));
        var rollbackContender = new FakeBackend();
        using (var observer = new RawInputObserver(failedRollback))
        using (var contender = new RawInputObserver(rollbackContender))
        {
            var error = Throws<Win32Exception>(() => observer.Start(12), "failed Start and rollback");
            Check(error.NativeErrorCode == 5 && error.Data["UnregisterWin32Code"] is 87,
                "both registration and rollback error codes preserved");
            Check(observer.IsRunning, "failed rollback retains cleanup obligation");
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null && failedRollback.PacketReads == 0,
                "cleanup-only observer never reads packets");
            Throws<InvalidOperationException>(() => observer.Start(12), "Start while rollback cleanup pending");
            Throws<InvalidOperationException>(() => contender.Start(13), "ownership retained after failed rollback");
            Check(rollbackContender.Registrations.Count == 0, "rollback contender never registers");
            observer.Stop();
            Check(!observer.IsRunning && failedRollback.Registrations.Count == 3, "Stop retries failed rollback cleanup");
            contender.Start(13);
            Check(contender.IsRunning, "cleanup retry releases process owner");
        }

        var failedStop = new FakeBackend();
        failedStop.Results.Enqueue(new(true));
        failedStop.Results.Enqueue(new(false, 170));
        var stopContender = new FakeBackend();
        using (var observer = new RawInputObserver(failedStop))
        using (var contender = new RawInputObserver(stopContender))
        {
            observer.Start(12);
            var error = Throws<Win32Exception>(observer.Stop, "failed Stop");
            Check(error.NativeErrorCode == 170 && observer.IsRunning, "failed Stop preserves error and cleanup state");
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null && failedStop.PacketReads == 0,
                "Stop failure still shuts off packet reading");
            Throws<InvalidOperationException>(() => observer.Start(12), "Start cannot revive failed Stop");
            Throws<InvalidOperationException>(() => contender.Start(13), "Stop failure keeps exclusive owner");
            observer.Stop();
            Check(!observer.IsRunning && failedStop.Registrations.Count == 3, "Stop retries native removal");
            contender.Start(13);
            Check(contender.IsRunning, "successful retry allows another observer");
        }

        var failedDispose = new FakeBackend();
        failedDispose.Results.Enqueue(new(true));
        failedDispose.Results.Enqueue(new(false, 6));
        using (var observer = new RawInputObserver(failedDispose))
        {
            observer.Start(12);
            Throws<Win32Exception>(observer.Dispose, "first Dispose cleanup failure");
            Check(observer.IsRunning, "failed Dispose keeps cleanup reachable");
            observer.Dispose(); observer.Dispose();
            Check(!observer.IsRunning && failedDispose.Registrations.Count == 3, "second Dispose retries, third is idempotent");
            Throws<ObjectDisposedException>(() => observer.Start(12), "Start after disposed");
            Throws<ObjectDisposedException>(() => observer.Refresh(), "Refresh after disposed");
            Throws<ObjectDisposedException>(() => observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1), "message after disposed");
        }

        var messages = new FakeBackend();
        messages.Inventory = KnownInventory(1, "device-one");
        messages.Packet = Packet(1);
        using (var observer = new RawInputObserver(messages))
        {
            observer.Start(12);
            foreach (nint code in new nint[] { 1, 2, 0x101, -1 })
                Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, code, 1) is null,
                    "background/unknown generation code ignored before read");
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 0) is null, "null packet token ignored");
            Check(observer.ProcessMessage(12345, 0, 1) is null, "unrelated window message ignored");
            Check(messages.PacketReads == 0, "fake-active background messages never read packet content");
            var routed = observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1);
            Check(routed?.Input?.DeviceId == "device-one" && messages.PacketReads == 1,
                "known source routes only a simulated foreground packet");
            messages.Packet = Packet(99);
            var unknown = observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1);
            Check(unknown is { Kind: RawInputMessageKind.DevicesChanged, Input: null } && messages.InventoryCalls == 2,
                "unknown handle reports a topology change rather than guessing a source");
            int unknownReads = messages.PacketReads;
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null && messages.PacketReads == unknownReads,
                "unknown handle pauses all subsequent packet reads");
            Throws<InvalidOperationException>(() => observer.Start(12), "unknown handle requires cleanup before restart");
            observer.Stop(); observer.Start(12);
            messages.Inventory = KnownInventory(1, "device-one", ambiguous: true);
            observer.Refresh(); messages.Packet = Packet(1);
            int pausedReads = messages.PacketReads;
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null && messages.PacketReads == pausedReads,
                "changed ambiguity during Refresh pauses before reading more packets");
            observer.Stop(); observer.Start(12);
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null, "ambiguous identity never routes");
            messages.Inventory = KnownInventory(1, "device-one", kind: RawDeviceKind.Mouse);
            observer.Stop(); observer.Start(12);
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null, "device kind mismatch never routes");
            messages.Packet = null;
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null, "invalid simulated packet ignored");
        }

        var hotplug = new FakeBackend { Inventory = KnownInventory(1, "old-identity"), Packet = Packet(1) };
        using (var observer = new RawInputObserver(hotplug))
        {
            observer.Start(12);
            hotplug.Inventory = KnownInventory(2, "new-identity");
            bool pausedBeforeHotplugRefresh = false;
            hotplug.BeforeInventoryRead = () =>
            {
                int reads = hotplug.PacketReads;
                pausedBeforeHotplugRefresh = observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null && hotplug.PacketReads == reads;
            };
            var changed = observer.ProcessMessage(RawInputObserver.WM_INPUT_DEVICE_CHANGE, 1, 2);
            hotplug.BeforeInventoryRead = null;
            Check(changed?.Kind == RawInputMessageKind.DevicesChanged && hotplug.PacketReads == 0,
                "hotplug refresh reports topology without reading input");
            Check(pausedBeforeHotplugRefresh, "device-change notification pauses acceptance before inventory enumeration");
            Check(observer.CurrentInventory.Devices.Single().Id == "new-identity", "hotplug replaces inventory");
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null,
                "removed handle cannot inherit new device mapping");
            hotplug.Packet = Packet(2);
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null && hotplug.PacketReads == 0,
                "newly discovered handle cannot route until explicit cleanup and restart");
            Throws<InvalidOperationException>(() => observer.Start(12), "device-change notification requires cleanup before restart");
            observer.Stop(); observer.Start(12);
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1)?.Input?.DeviceId == "new-identity",
                "new handle routes only after explicit Stop and Start");
            hotplug.InventoryError = new InvalidOperationException("simulated hotplug refresh failure");
            Throws<InvalidOperationException>(() => observer.ProcessMessage(RawInputObserver.WM_INPUT_DEVICE_CHANGE, 1, 2),
                "hotplug refresh failure");
            Check(observer.CurrentInventory.Devices.Count == 0 && observer.IsRunning,
                "failed refresh clears stale identity map while retaining cleanup");
            int reads = hotplug.PacketReads;
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null && hotplug.PacketReads == reads,
                "failed refresh stops packet reads until cleanup and restart");
            Throws<InvalidOperationException>(() => observer.Start(12), "failed refresh requires explicit cleanup");
            observer.Stop(); hotplug.InventoryError = null; observer.Start(12);
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1)?.Input?.DeviceId == "new-identity",
                "successful cleanup and fresh Start restore explicitly discovered mapping");
        }

        var reconnect = new FakeBackend { Inventory = KnownInventory(1, "saved-identity"), Packet = Packet(1) };
        using (var observer = new RawInputObserver(reconnect))
        {
            observer.Start(12);
            reconnect.Inventory = KnownInventory(2, "saved-identity");
            reconnect.Packet = Packet(2);
            bool pausedBeforeReconnectRefresh = false;
            reconnect.BeforeInventoryRead = () =>
            {
                int reads = reconnect.PacketReads;
                pausedBeforeReconnectRefresh = observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null && reconnect.PacketReads == reads;
            };
            var changed = observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1);
            reconnect.BeforeInventoryRead = null;
            Check(changed is { Kind: RawInputMessageKind.DevicesChanged, Input: null },
                "same saved identity on new handle never emits its first packet as DeviceInput");
            Check(pausedBeforeReconnectRefresh, "unknown-handle refresh pauses acceptance before enumeration");
            Check(observer.IsRunning && observer.CurrentInventory.Devices.Single().Id == "saved-identity",
                "reconnect discovery retains cleanup obligation and updated metadata only");
            int readsAfterReconnect = reconnect.PacketReads;
            reconnect.Packet = Packet(1);
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null && reconnect.PacketReads == readsAfterReconnect,
                "old handle cannot route or trigger another read after reconnect");
            reconnect.Packet = Packet(2);
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null && reconnect.PacketReads == readsAfterReconnect,
                "new handle remains paused despite matching saved identity");
            observer.Refresh();
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null && reconnect.PacketReads == readsAfterReconnect,
                "unchanged Refresh cannot revive a reconnect-paused run");
            Throws<InvalidOperationException>(() => observer.Start(12), "same-identity reconnect requires Stop before Start");
            observer.Stop(); observer.Start(12);
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1)?.Input?.DeviceId == "saved-identity",
                "same saved identity on new handle routes after explicit cleanup and fresh Start");
            reconnect.Packet = Packet(1);
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is { Kind: RawInputMessageKind.DevicesChanged, Input: null },
                "stale pre-reconnect handle is absent from the restarted mapping");
        }

        foreach (var change in new (string Name, RawInputInventory Inventory)[]
        {
            ("new handle", KnownInventory(2, "saved-identity")),
            ("changed identity", KnownInventory(1, "different-identity")),
            ("changed kind", KnownInventory(1, "saved-identity", kind: RawDeviceKind.Mouse)),
            ("changed ambiguity", KnownInventory(1, "saved-identity", ambiguous: true)),
            ("removed handle", new RawInputInventory(new([], [], []), new Dictionary<nint, RawDeviceDescriptor>()))
        })
        {
            var refreshBackend = new FakeBackend { Inventory = KnownInventory(1, "saved-identity"), Packet = Packet(1) };
            using var observer = new RawInputObserver(refreshBackend);
            observer.Start(12);
            refreshBackend.Inventory = change.Inventory;
            observer.Refresh();
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null && refreshBackend.PacketReads == 0,
                "public Refresh fails closed for " + change.Name);
            Check(observer.IsRunning, "public Refresh retains cleanup obligation for " + change.Name);
            Throws<InvalidOperationException>(() => observer.Start(12), "public Refresh requires Stop before restart for " + change.Name);
        }

        var metadataRefresh = new FakeBackend { Inventory = KnownInventory(1, "saved-identity"), Packet = Packet(1) };
        using (var observer = new RawInputObserver(metadataRefresh))
        {
            observer.Start(12);
            var updated = metadataRefresh.Inventory.Devices[1] with { DisplayName = "Updated label", TransportHint = "Updated descriptive hint" };
            metadataRefresh.Inventory = new RawInputInventory(new([updated], [], []), new Dictionary<nint, RawDeviceDescriptor> { [1] = updated });
            bool pausedBeforeMetadataRefresh = false;
            metadataRefresh.BeforeInventoryRead = () =>
            {
                pausedBeforeMetadataRefresh = observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1) is null && metadataRefresh.PacketReads == 0;
            };
            observer.Refresh();
            metadataRefresh.BeforeInventoryRead = null;
            Check(pausedBeforeMetadataRefresh, "public Refresh pauses before reading inventory even for unchanged source map");
            Check(observer.ProcessMessage(RawInputObserver.WM_INPUT, 0, 1)?.Input?.DeviceId == "saved-identity",
                "unchanged source map can resume after descriptive metadata refresh");
        }

        // Earlier failure paths must all have released the static owner after their successful retries.
        var finalBackend = new FakeBackend();
        using (var observer = new RawInputObserver(finalBackend))
        {
            observer.Start(12);
            Check(observer.IsRunning, "no test stranded the static process owner");
        }
        return checks;
    }

    private static RawInputInventory KnownInventory(nint handle, string identity,
        bool ambiguous = false, RawDeviceKind kind = RawDeviceKind.Keyboard)
    {
        var device = new RawDeviceDescriptor(identity, kind, "Identical simulated device", ambiguous, "Fake backend only");
        return new RawInputInventory(new([device], [], []), new Dictionary<nint, RawDeviceDescriptor> { [handle] = device });
    }

    private static ParsedRawPacket Packet(ulong handle) =>
        new(handle, RawDeviceKind.Keyboard, new(0x41, 0x1e, true, false, false, 0), null);

    private sealed class FakeBackend : IRawInputBackend
    {
        public WindowOwnership Ownership { get; set; } = WindowOwnership.Owned;
        public Exception? PlatformError { get; set; }
        public Exception? InventoryError { get; set; }
        public Action? BeforeInventoryRead { get; set; }
        public RawInputInventory Inventory { get; set; } = new(new([], [], []), new Dictionary<nint, RawDeviceDescriptor>());
        public ParsedRawPacket? Packet { get; set; }
        public Queue<RegistrationResult> Results { get; } = new();
        public List<(uint Flags, nint Target)> Registrations { get; } = [];
        public int EnsureCalls { get; private set; }
        public int ValidateCalls { get; private set; }
        public int InventoryCalls { get; private set; }
        public int PacketReads { get; private set; }
        public int TotalCalls => EnsureCalls + ValidateCalls + InventoryCalls + PacketReads + Registrations.Count;

        public void EnsureSupportedPlatform()
        {
            EnsureCalls++;
            if (PlatformError is not null) throw PlatformError;
        }

        public WindowOwnership ValidateWindow(nint hwnd)
        {
            ValidateCalls++;
            return Ownership;
        }

        public RawInputInventory ReadInventory()
        {
            InventoryCalls++;
            BeforeInventoryRead?.Invoke();
            if (InventoryError is not null) throw InventoryError;
            return Inventory;
        }

        public RegistrationResult Register(uint flags, nint hwnd)
        {
            Registrations.Add((flags, hwnd));
            return Results.Count == 0 ? new(true) : Results.Dequeue();
        }

        public ParsedRawPacket? ReadPacket(nint rawInput)
        {
            PacketReads++;
            return Packet;
        }
    }
}
