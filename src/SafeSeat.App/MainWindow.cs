using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using SafeSeat.Core;
using SafeSeat.Windows;
using CoreDevice = SafeSeat.Core.DeviceDescriptor;
using RawMonitor = SafeSeat.Windows.MonitorDescriptor;

namespace SafeSeat.App;

internal sealed class MainWindow : Window
{
    private readonly Settings _settings;
    private readonly SeatRouter _router;
    private RawInputObserver? _observer;
    private readonly bool _discoveryEnabled;
    private InventorySnapshot _inventory = new([], [], []);
    private IReadOnlyList<string> _coreWarnings = [];
    private readonly ListBox _devices = new() { MinHeight = 140 };
    private readonly ComboBox _monitorA = new() { Margin = new Thickness(0, 4, 0, 12) };
    private readonly ComboBox _monitorB = new() { Margin = new Thickness(0, 4, 0, 12) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _diagnostics = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _readiness = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 0) };
    private readonly TextBlock _performance = new() { TextWrapping = TextWrapping.Wrap };
    private readonly CheckBox _metrics = new() { Content = "Show aggregate handling timing / CPU / RAM in memory", Margin = new Thickness(0, 10, 0, 6) };
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<SeatId, SeatWindow> _windows = new();
    private readonly string _profilePath = Path.Combine(AppContext.BaseDirectory, "profiles", "default.json");
    private HwndSource? _source;
    private bool _sessionNotifications;
    private bool _running;
    private bool _discoveryOnly;
    private bool _dirty;
    private bool _closingSeats;
    private long _metricCount;
    private double _totalMilliseconds;
    private double _maxMilliseconds;
    private long _lastMetricTick;
    private TimeSpan _lastCpu;

    internal MainWindow(bool discoveryEnabled = true)
    {
        _discoveryEnabled = discoveryEnabled;
        string profileNotice = "No observation is active. Assign devices and different monitors, then Start.";
        try { _settings = discoveryEnabled ? SettingsStore.Load(_profilePath) : new Settings(); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
        {
            _settings = new Settings();
            profileNotice = "Profile rejected (" + e.GetType().Name + "). Use fresh assignments; the original file was left in place.";
        }
        _router = new SeatRouter(_settings);
        Title = "SafeSeat — controlled app prototype";
        Width = 1100; Height = 800; MinWidth = 800; MinHeight = 650;
        var root = new DockPanel { Margin = new Thickness(22) };
        var heading = new StackPanel();
        DockPanel.SetDock(heading, Dock.Top);
        heading.Children.Add(new TextBlock { Text = "SafeSeat", FontSize = 32, FontWeight = FontWeights.SemiBold });
        heading.Children.Add(new TextBlock
        {
            Text = "Two independent states inside this app. Arbitrary Chrome / Word / game independence is blocked and unproven.",
            TextWrapping = TextWrapping.Wrap, FontSize = 16, Margin = new Thickness(0, 8, 0, 8)
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Normal Windows pointer and keyboard input continue. Both seats share one OS account and are not security boundaries. Use only disposable text in these test windows.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12)
        });
        var actions = new WrapPanel();
        actions.Children.Add(Button("Refresh inventory", () => { Stop("Refresh requested."); RefreshInventory(); }));
        actions.Children.Add(Button("Save profile", Save));
        actions.Children.Add(Button("Hardware readiness", ShowReadiness));
        actions.Children.Add(Button("Observe sources (no text)", StartIdentification));
        actions.Children.Add(Button("Start controlled demo", StartDemo));
        actions.Children.Add(Button("STOP / clear temporary text", () => Stop("Stopped by user.")));
        actions.Children.Add(Button("Exit", Close));
        heading.Children.Add(actions);
        heading.Children.Add(_metrics);
        heading.Children.Add(_performance);
        heading.Children.Add(_status);
        heading.Children.Add(_diagnostics);
        heading.Children.Add(_readiness);
        root.Children.Add(heading);
        var body = new Grid { Margin = new Thickness(0, 18, 0, 0) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        var left = new DockPanel { Margin = new Thickness(0, 0, 22, 0) };
        var deviceTitle = new TextBlock { Text = "Raw Input collections · select a row", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(deviceTitle, Dock.Top); left.Children.Add(deviceTitle);
        var assignmentActions = new WrapPanel(); DockPanel.SetDock(assignmentActions, Dock.Bottom);
        assignmentActions.Children.Add(Button("Assign Seat A", () => Assign(SeatId.A)));
        assignmentActions.Children.Add(Button("Assign Seat B", () => Assign(SeatId.B)));
        assignmentActions.Children.Add(Button("Unassign selected", () => Assign(null)));
        left.Children.Add(assignmentActions); left.Children.Add(_devices);
        body.Children.Add(left);
        var right = new StackPanel(); Grid.SetColumn(right, 1);
        right.Children.Add(new TextBlock { Text = "Seat A monitor", FontWeight = FontWeights.SemiBold }); right.Children.Add(_monitorA);
        right.Children.Add(new TextBlock { Text = "Seat B monitor", FontWeight = FontWeights.SemiBold }); right.Children.Add(_monitorB);
        right.Children.Add(new TextBlock
        {
            Text = "Exactly one keyboard and mouse per seat. IDs are path hashes, not hardware serial numbers. Recheck all assignments after reconnects. Ambiguous devices are disabled.\n\nText support: standard US English layout, ASCII, per-seat Shift and Caps Lock, repeat, Backspace. Caps starts off for each demo run; hardware LEDs may differ. Ctrl/Alt/Windows combinations do not produce demo text. IME, dead keys, AltGr and clipboard are unsupported.\n\nObservation stops on foreign-app focus, display changes, session changes or suspend. Release all keys before Start. Escape or either window's Stop button clears both text buffers.",
            TextWrapping = TextWrapping.Wrap
        });
        body.Children.Add(right); root.Children.Add(body);
        Content = root;
        _monitorA.SelectionChanged += (_, _) => { if (_running) Stop("Monitor selection changed."); UpdateReadiness(); };
        _monitorB.SelectionChanged += (_, _) => { if (_running) Stop("Monitor selection changed."); UpdateReadiness(); };
        _status.Text = profileNotice;
        UpdateReadiness();
        _metrics.Unchecked += (_, _) => { ResetMetrics(); _performance.Text = ""; };
        _metrics.Checked += (_, _) => ResetMetrics();
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        SourceInitialized += (_, _) =>
        {
            _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            _source?.AddHook(WindowMessage);
            _sessionNotifications = NativeWindow.RegisterSessionChanges(new WindowInteropHelper(this).Handle);
        };
        Loaded += (_, _) => { if (_discoveryEnabled) RefreshInventory(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Stop("Escape pressed."); e.Handled = true; }
            else if (_running && e.Key is Key.Space or Key.Enter) e.Handled = true;
        };
        PreviewKeyUp += (_, e) => { if (_running && e.Key is Key.Space or Key.Enter) e.Handled = true; };
        Closing += (_, _) =>
        {
            Stop("Closing."); _timer.Stop(); _source?.RemoveHook(WindowMessage);
            if (_sessionNotifications) NativeWindow.UnregisterSessionChanges(new WindowInteropHelper(this).Handle);
            try { _observer?.Dispose(); }
            catch (System.ComponentModel.Win32Exception) { /* The receiving HWND is being destroyed. */ }
        };
    }

    private static Button Button(string caption, Action action)
    {
        var button = new Button { Content = caption, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 8) };
        button.Click += (_, _) => action(); return button;
    }

    private void RefreshInventory() => TryRefreshInventory();

    private bool TryRefreshInventory()
    {
        if (!_discoveryEnabled) return false;
        try
        {
            _inventory = WindowsInventory.ReadSnapshot();
            RegisterCoreDevices();
            RenderDevices();
            FillMonitor(_monitorA, SeatId.A); FillMonitor(_monitorB, SeatId.B);
            _diagnostics.Text = $"Read-only discovery: {_inventory.Devices.Count} keyboard/mouse collections, {_inventory.Monitors.Count} monitors. " +
                string.Join(" ", _inventory.Warnings);
            UpdateReadiness();
            return true;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _inventory = new([], [], []); RegisterCoreDevices(); RenderDevices();
            _status.Text = "Discovery failed: " + e.GetType().Name + ". Observation cannot start.";
            UpdateReadiness();
            return false;
        }
    }

    private CoreDevice[] CoreDevices() => _inventory.Devices
        .Where(d => d.Kind is RawDeviceKind.Keyboard or RawDeviceKind.Mouse)
        .Select(d => new CoreDevice(d.Id, d.DisplayName,
            d.Kind == RawDeviceKind.Keyboard ? DeviceKind.Keyboard : DeviceKind.Mouse,
            !d.IsIdentityAmbiguous, d.IsIdentityAmbiguous ? d.StabilityNote : null)).ToArray();

    private void RegisterCoreDevices() => _coreWarnings = _router.RegisterDevices(CoreDevices());

    private void FillMonitor(ComboBox box, SeatId seat)
    {
        string? chosen = (box.SelectedItem as MonitorRow)?.Monitor.Id;
        if (chosen is null) _settings.MonitorAssignments.TryGetValue(seat, out chosen);
        box.Items.Clear();
        foreach (var monitor in _inventory.Monitors)
        {
            var row = new MonitorRow(monitor); box.Items.Add(row);
            if (monitor.Id == chosen) box.SelectedItem = row;
        }
        // Require explicit selection on first use or when the saved monitor is absent.
    }

    private void RenderDevices()
    {
        string? selected = (_devices.SelectedItem as DeviceRow)?.Id;
        _devices.Items.Clear();
        foreach (var device in _inventory.Devices.Where(d => d.Kind is RawDeviceKind.Keyboard or RawDeviceKind.Mouse))
        {
            string assignment = _settings.DeviceAssignments.TryGetValue(device.Id, out var seat) ? $"Seat {seat}" : "unassigned";
            var row = new DeviceRow(device.Id,
                $"{device.Kind} · {device.DisplayName}\n{Short(device.Id)} · {assignment}" +
                $"\nPnP group: {(device.CandidateGroupId is null ? "unknown" : Short(device.CandidateGroupId))} · {device.TransportHint ?? "transport unverified"}" +
                (device.IsIdentityAmbiguous ? " · AMBIGUOUS / DISABLED" : ""), device.IsIdentityAmbiguous);
            _devices.Items.Add(row);
            if (row.Id == selected) _devices.SelectedItem = row;
        }
        foreach (var saved in _settings.DeviceAssignments.Where(p => !_inventory.Devices.Any(d => d.Id == p.Key)))
            _devices.Items.Add(new DeviceRow(saved.Key, $"Disconnected saved device · {Short(saved.Key)} · Seat {saved.Value}", true));
    }

    private void Assign(SeatId? seat)
    {
        Stop("Assignment changed; start again when ready.");
        if (_devices.SelectedItem is not DeviceRow row) { _status.Text = "Select a device first."; return; }
        if (seat.HasValue && row.Unavailable) { _status.Text = "Device is disconnected or ambiguous. Resolve identity before assigning."; return; }
        try
        {
            _router.AssignDevice(row.Id, seat);
            if (seat.HasValue) _settings.DeviceAssignments[row.Id] = seat.Value;
            else _settings.DeviceAssignments.Remove(row.Id);
            RenderDevices(); UpdateReadiness(); _status.Text = "Assignment updated in memory. Save profile to keep it.";
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { _status.Text = e.Message; }
    }

    private void ReadMonitorAssignments()
    {
        _settings.MonitorAssignments.Clear();
        if (_monitorA.SelectedItem is MonitorRow a) _settings.MonitorAssignments[SeatId.A] = a.Monitor.Id;
        if (_monitorB.SelectedItem is MonitorRow b) _settings.MonitorAssignments[SeatId.B] = b.Monitor.Id;
        var errors = _settings.Validate(CoreDevices());
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));
    }

    private void Save()
    {
        try { ReadMonitorAssignments(); SettingsStore.SaveAtomic(_profilePath, _settings); _status.Text = "Saved hashed assignments to portable profiles/default.json. No input text was saved."; }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        { _status.Text = "Profile was not saved: " + e.GetType().Name + ". Check assignments and folder permissions."; }
    }

    private void StartDemo()
    {
        Stop("Validating demo configuration.");
        try
        {
            if (!TryRefreshInventory()) throw new InvalidOperationException("Fresh discovery failed.");
            ReadMonitorAssignments();
            if (_coreWarnings.Count > 0) throw new InvalidOperationException(string.Join(" ", _coreWarnings));
            var readiness = CurrentReadiness();
            if (!readiness.AssignmentsReady) throw new InvalidOperationException(string.Join(" ", readiness.Blockers));
            EnsureSessionNotifications();
            if (!NativeWindow.OwnsForeground()) throw new InvalidOperationException("Activate SafeSeat before Start.");
            if (!NativeWindow.IsUsEnglishLayout()) throw new InvalidOperationException("Select the standard US English keyboard layout before Start. Other layouts are unsupported.");
            _observer ??= new RawInputObserver();
            _observer.Start(new WindowInteropHelper(this).Handle);
            if (!DiscoveryReadiness.SameTopology(_inventory, _observer.CurrentInventory))
                throw new InvalidOperationException("Device/display topology changed during Start. Refresh and review assignments.");
            _router.Start(); _running = true; _discoveryOnly = false; ResetMetrics();
            foreach (var seat in Enum.GetValues<SeatId>())
            {
                var window = new SeatWindow(seat, () => _router.GetSnapshot(seat), () => Stop("Stopped from seat window."));
                _windows[seat] = window;
                window.Closed += (_, _) => { if (!_closingSeats) Stop("A seat window closed."); };
                window.Show();
                var monitor = _inventory.Monitors.Single(m => m.Id == _settings.MonitorAssignments[seat]);
                NativeWindow.PlaceOwnWindow(new WindowInteropHelper(window).Handle,
                    monitor.WorkLeft, monitor.WorkTop, monitor.WorkWidth, monitor.WorkHeight);
            }
            _dirty = true;
            _status.Text = "RUNNING · controlled app observation only · Escape stops both seats.";
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        { Stop("Start refused: " + e.Message); }
    }

    private DiscoveryReadinessReport CurrentReadiness()
    {
        var monitors = new Dictionary<string, string>();
        if (_monitorA.SelectedItem is MonitorRow a) monitors["A"] = a.Monitor.Id;
        if (_monitorB.SelectedItem is MonitorRow b) monitors["B"] = b.Monitor.Id;
        return DiscoveryReadiness.Evaluate(_inventory,
            _settings.DeviceAssignments.ToDictionary(a => a.Key, a => a.Value.ToString(), StringComparer.Ordinal), monitors);
    }

    private void UpdateReadiness()
    {
        var report = CurrentReadiness();
        _readiness.Text = report.Summary() + (report.Blockers.Count == 0 ? "" : " First blocker: " + report.Blockers[0]);
    }

    private void ShowReadiness()
    {
        Stop("Reviewing hardware readiness; observation remains off.");
        if (!TryRefreshInventory()) return;
        var report = CurrentReadiness();
        string text = report.Summary() + "\n\nBlockers\n" +
            (report.Blockers.Count == 0 ? "None in assignment structure; other Start checks still apply." : string.Join("\n", report.Blockers.Select(b => "• " + b))) +
            "\n\nLimits and warnings\n" + string.Join("\n", report.Warnings.Select(w => "• " + w)) +
            "\n\nPhysical confirmation\nUse Observe sources (no text), operate each physical device separately, then press Escape before moving another mouse. PnP grouping never changes assignments automatically. No physical acceptance evidence is inferred from this report.";
        var dialog = new Window
        {
            Title = "SafeSeat hardware readiness — physical acceptance NOT TESTED", Owner = this,
            Width = 850, Height = 580, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(22), FontSize = 15 } }
        };
        dialog.ShowDialog();
    }

    private void StartIdentification()
    {
        Stop("Preparing source observation.");
        try
        {
            if (!TryRefreshInventory()) throw new InvalidOperationException("Fresh discovery failed.");
            EnsureSessionNotifications();
            if (!NativeWindow.OwnsForeground()) throw new InvalidOperationException("Activate SafeSeat before observing sources.");
            _observer ??= new RawInputObserver();
            _observer.Start(new WindowInteropHelper(this).Handle);
            _running = true; _discoveryOnly = true; ResetMetrics();
            _status.Text = "OBSERVING SOURCES · no text translation or buffering · press one device to identify its row · Esc stops.";
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        { Stop("Source observation refused: " + e.Message); }
    }

    private void EnsureSessionNotifications()
    {
        if (!_sessionNotifications)
            _sessionNotifications = NativeWindow.RegisterSessionChanges(new WindowInteropHelper(this).Handle);
        if (!_sessionNotifications) throw new InvalidOperationException("Session-change notifications are unavailable; observation cannot start safely.");
    }

    internal void Stop(string reason)
    {
        _running = false;
        _discoveryOnly = false;
        _router.Stop(); // Always clear app text even if native unregistration fails.
        string nativeError = "";
        try { _observer?.Stop(); }
        catch (System.ComponentModel.Win32Exception e) { nativeError = $" Unregister error {e.NativeErrorCode}; Exit closes the receiving HWND. Native input was never blocked."; }
        _closingSeats = true;
        foreach (var window in _windows.Values.ToArray()) window.Close();
        _windows.Clear(); _closingSeats = false;
        _status.Text = "STOPPED · " + reason + nativeError;
    }

    private nint WindowMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        try
        {
            if (_running && ((msg == 0x001C && wParam == 0) || msg == 0x0051))
            {
                Stop(msg == 0x0051 ? "Keyboard layout changed." : "Application deactivated; observation stopped for privacy.");
                return 0;
            }
            if (msg is 0x007E or 0x0218 or 0x02B1 or 0x0011 or 0x0016) // display, power, registered session, session ending
            {
                Stop("Display, power or session state changed. Recheck assignments.");
                if (msg == 0x007E) Dispatcher.BeginInvoke(new Action(RefreshInventory));
                return 0;
            }
            if (_observer is null || !_running) return 0;
            if (msg == 0x00FE) // WM_INPUT_DEVICE_CHANGE; never silently migrate a live assignment
            {
                Stop("Device topology changed. Refresh and confirm assignments before restarting.");
                Dispatcher.BeginInvoke(new Action(RefreshInventory)); return 0;
            }
            if (msg != 0x00FF) return 0;
            if (!NativeWindow.OwnsForeground()) { Stop("SafeSeat lost foreground; observation stopped for privacy."); return 0; }
            if (!_discoveryOnly && !NativeWindow.IsUsEnglishLayout()) { Stop("Keyboard layout changed; only standard US English is supported."); return 0; }
            long start = Stopwatch.GetTimestamp();
            var message = _observer.ProcessMessage(msg, wParam, lParam);
            if (message?.Kind == RawInputMessageKind.DevicesChanged)
            {
                Stop("Input source topology changed. Refresh and confirm assignments before restarting.");
                Dispatcher.BeginInvoke(new Action(RefreshInventory));
                return 0;
            }
            if (message?.Input is not DeviceInput input) return 0;
            if (_discoveryOnly)
            {
                if (input.Keyboard is { VirtualKey: 0x1B, IsDown: true }) { Stop("Escape observed."); return 0; }
                string kind = input.Keyboard is { } observed ? (observed.IsDown ? "Key down" : "Key up") : "Pointer / button / wheel";
                _diagnostics.Text = $"Source only · {input.Kind} · {Short(input.DeviceId)} · {kind}";
                var row = _devices.Items.OfType<DeviceRow>().FirstOrDefault(d => d.Id == input.DeviceId);
                if (row is not null) _devices.SelectedItem = row;
                return 0;
            }
            RouteResult? result = null;
            if (input.Keyboard is { } keyboard)
            {
                if (keyboard.VirtualKey == 0x1B && keyboard.IsDown) { Stop("Escape observed."); return 0; }
                if (!keyboard.IsE1)
                    result = _router.ProcessKeyboard(input.DeviceId,
                        new KeyboardInput(keyboard.VirtualKey, keyboard.ScanCode, keyboard.IsDown, keyboard.IsExtended));
            }
            else if (input.Mouse is { } mouse && !mouse.IsAbsolute)
            {
                // Absolute tablets, precision touchpads and horizontal wheel reports have no validated seat mapping.
                result = _router.ProcessMouse(input.DeviceId, new MouseInput(mouse.DeltaX, mouse.DeltaY,
                    (MouseButtons)(mouse.ButtonFlags & 0x3f),
                    (mouse.ButtonFlags & 0x0400) != 0 ? mouse.WheelDelta : 0));
            }
            if (result is { Accepted: true })
            {
                _dirty = true;
                _diagnostics.Text = $"{input.Kind} · {Short(input.DeviceId)} → Seat {result.Seat} · {result.Action}";
                if (_metrics.IsChecked == true)
                {
                    double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    _metricCount++; _totalMilliseconds += ms; _maxMilliseconds = Math.Max(_maxMilliseconds, ms);
                }
            }
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
        { Stop("Input processing error: " + e.GetType().Name + "."); }
        // Keep handled=false: WPF/DefWindowProc must perform WM_INPUT foreground cleanup.
        return 0;
    }

    private void Tick()
    {
        if (!_running) return;
        if (!NativeWindow.OwnsForeground()) { Stop("SafeSeat lost foreground; observation stopped for privacy."); return; }
        if (_dirty) { foreach (var window in _windows.Values) window.Refresh(); _dirty = false; }
        if (_metrics.IsChecked == true && Stopwatch.GetElapsedTime(_lastMetricTick).TotalSeconds >= 1)
        {
            using var process = Process.GetCurrentProcess();
            double seconds = Stopwatch.GetElapsedTime(_lastMetricTick).TotalSeconds;
            var cpu = process.TotalProcessorTime;
            double percent = (cpu - _lastCpu).TotalSeconds / seconds / Environment.ProcessorCount * 100;
            _performance.Text = $"Opt-in memory-only metrics: {_metricCount} routed packets · mean {(_metricCount == 0 ? 0 : _totalMilliseconds / _metricCount):F3} ms · max {_maxMilliseconds:F3} ms handler duration · CPU {percent:F1}% · working set {process.WorkingSet64 / 1048576d:F1} MiB. End-to-end latency / GPU unmeasured.";
            _lastCpu = cpu; _lastMetricTick = Stopwatch.GetTimestamp();
        }
    }

    private void ResetMetrics()
    {
        _metricCount = 0; _totalMilliseconds = 0; _maxMilliseconds = 0;
        _lastMetricTick = Stopwatch.GetTimestamp();
        using var process = Process.GetCurrentProcess(); _lastCpu = process.TotalProcessorTime;
    }
    private static string Short(string id) => id.Length <= 24 ? id : id[..10] + "…" + id[^10..];
    private sealed record DeviceRow(string Id, string Label, bool Unavailable) { public override string ToString() => Label; }
    private sealed record MonitorRow(RawMonitor Monitor)
    {
        public override string ToString() => $"{Monitor.DisplayName} · {Monitor.Width}×{Monitor.Height} @ {Monitor.Left},{Monitor.Top}" + (Monitor.IsIdentityAmbiguous ? " · ambiguous" : "");
    }
}
