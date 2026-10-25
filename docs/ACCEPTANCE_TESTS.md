# Acceptance tests

**The full project requirement is not demonstrated.** Neither published SafeSeat 0.1.0 nor the unreleased 0.1.1-dev candidate has a public physical two-seat acceptance result. This checklist separates physical acceptance from automated software tests: a build, simulated event replay or source inspection cannot pass a physical criterion.

All criteria below remain **NOT TESTED**. If Windows policy prevents a permitted run of `SafeSeat.exe`, record the execution blocker and stop. Do not weaken protections or run the DLL under another executable to continue.

## Current result

| # | Required physical result | Status | Scope and evidence required |
| --- | --- | --- | --- |
| 1 | Windows 11 Pro host unchanged; security protections remain enabled | NOT TESTED | A recorded before/after comparison is required. Source review and successful compilation cannot certify host state. |
| 2 | Two monitors, keyboards and mice detected and reliably assigned/reassigned | NOT TESTED | Identify each physical source, verify extended display mode and repeat assignment after reconnect. Collection/container counts alone are insufficient. |
| 3 | Simultaneous A/B input changes only the corresponding in-app text state | NOT TESTED | A timed physical run is required. Simulated router tests establish model behavior only. |
| 4 | Simultaneous A/B virtual pointers stay within their assigned areas | NOT TESTED | Exercise both physical mice, visible controls and mixed-DPI placement. Geometry tests alone are insufficient. |
| 5 | Separate ordinary Chrome windows accept simultaneous typing with no cross-effects | NOT TESTED | No supported design or physical demonstration; [Gate 3 NO-GO](TECHNICAL_FEASIBILITY.md#gate-decision). SafeSeat input must not be counted as Chrome success. |
| 6 | Launching tabs/windows/Word/Explorer from either seat never changes the other seat's focus or monitor | NOT TESTED | Generic app launch/activation isolation is not implemented; [blockers](BLOCKERS_AND_ALTERNATIVES.md). |
| 7 | Clicking/scrolling generic apps never affects the other person's apps | NOT TESTED | Native Windows input remains shared; virtual SafeSeat pointers do not establish generic-app independence. |
| 8 | Disconnect/reconnect devices; recover without trapping both operators | NOT TESTED | Simulated reconnect tests cover software policy, not physical HID/USB notification timing or recovery. |
| 9 | Performance measured while both users operate, including graphics/game limits | NOT TESTED | Optional aggregate measurements need a physical workload. No gaming, anti-cheat, GPU or audio independence claim. |
| 10 | Stop reliably ends the run; normal OS use resumes without reinstall/boot changes/reboot | NOT TESTED | Stop logic is present, but no physical UI/lifecycle/recovery run has demonstrated the criterion. |

Tests 5–7 are mandatory for the full requirement. Their NOT TESTED status and the NO-GO architecture decision must remain visible in any release notes. Do not relabel unimplemented functionality as a measured FAIL; record a FAIL only when an actual procedure runs and fails. Conversely, do not convert NOT TESTED to PASS based on anticipated behavior.

## Test preparation and recording

Read [Safety and recovery](SAFETY_AND_RECOVERY.md), back up important files, prepare the owner-controlled restore-point precaution and use a standard account. Review [the license boundary](TECHNICAL_FEASIBILITY.md#license-boundary) before two-human testing. One operator can perform a device-routing check; record that explicitly and do not call it simultaneous two-human acceptance. Do not install a driver, change a security setting or enable concurrent-session workarounds to execute this checklist.

Record the repository revision or source hash, build command/result, Windows edition/build, .NET version, local versus remote session, two monitor modes/scales/coordinates, device classes, redacted device identifiers, USB/Bluetooth topology and run duration. Use a plain text result record with counts, timing summaries and error codes. Do not capture screenshots, credentials, typed strings or raw key-event logs. An observer can compare visible test text and record only whether expected/actual states matched.

For each test, record operator count, date/time/timezone, steps executed, PASS/FAIL/NOT TESTED and a reason. Keep any profile used for reproduction free of identifying device paths. Acceptance sign-off must reference the resulting local evidence file; this document is the procedure, not that sign-off.

## Repeatable procedures

### 1. Host and security invariants

Before running, record the Windows edition/build and the displayed status of Secure Boot, Defender, UAC and Memory Integrity/Core Isolation using Windows' normal UI. Do not enable or disable a feature as part of the test. Review the source and commands for system writes, installers, services, startup tasks, drivers, network listeners and protected-process access. Run/stop the app, then compare statuses and confirm no installation or elevation occurred. PASS requires a recorded before/after comparison; source review alone is insufficient.

### 2. Hardware discovery and assignment

Confirm the desktop is extended across two physical panels, not mirrored. Connect two distinguishable physical keyboards and two physical mice. Refresh inventory and verify that device class, names/short identifiers and monitor geometry allow each device to be selected. Choose **Observe sources (no text)** and operate one device at a time: its matching hashed row should be selected with a basic action kind, and no text field or text buffer should be started. Stop before assignment. This source-only mode may also be checked with one monitor, but that does not pass the two-monitor requirement. Composite devices may expose extra collections; identify which collections actually generate the intended events. Assign one keyboard, one mouse and one distinct monitor to each seat; save and reload the profile. Try duplicate assignments, missing devices, missing monitors and a changed layout: controlled Start must be refused or the condition clearly corrected. Reassign A/B and repeat. Identical ambiguous devices must require confirmation rather than silently exchange seats.

### 3. Controlled in-app keyboard isolation

Select the standard US English layout, release held keys and choose **Start controlled demo** with non-sensitive expendable text. Keep either owned SafeSeat seat window foreground; leave the other owned window inactive. Use the assigned A and B keyboards concurrently for at least 60 seconds, then swap the foreground owned window and repeat. Visually confirm each rendered state changes only for its own device. Exercise Shift down/up, Caps Lock, repeated key-down, Space, Enter, Tab, Backspace and release order on both devices, including holding Shift on A while typing on B. Caps Lock starts off for each seat on every run, independently of keyboard LEDs. Confirm supported ASCII results and excluded Ctrl/Alt/Windows combinations; do not infer support for dead keys, IME or numpad/Num Lock. Record only mismatch counts, never the typed content. Verify native Space/Enter do not activate the focused Start/Stop button while text is being routed.

Then switch to an unrelated application without typing secrets. SafeSeat must show a stopped state, clear demo text and stop observation; returning to an owned window must not restart it. Check the foreground-only registration and pre-read `RIM_INPUT`/ownership guards in source; background packet data must not be decoded. Repeat this transition from source-only mode. Lock/unlock the session and repeat. A controlled-app PASS is scoped to the tested layout/devices and cannot pass test 5.

### 4. Controlled virtual pointers and scrolling

With both owned windows visible, move both physical mice concurrently. Confirm each virtual pointer stays within its own canvas and only its matching visible controls/counters respond to left/right button actions and wheel input supported by the UI. Move to all edges/corners, hold/release buttons and reverse wheel direction. Observe that the single native pointer still moves normally; do not count native pointer movement as a virtual-pointer failure or mistake it for two OS cursors. A native click outside the application must stop the run.

Repeat with a monitor to the left/above the primary display, different scaling on each panel, and the windows resized. Confirm stop controls remain reachable and rendered areas match their input bounds. Do not PASS physical placement based solely on simulated rectangle tests.

### 5. Ordinary Chrome simultaneous text input

**Blocked procedure; do not run with SafeSeat as if it supplied this capability.** If a future supported design passes the [Gate 3 entry conditions](BLOCKERS_AND_ALTERNATIVES.md#conditions-for-reopening-gate-3), use unmodified Chrome windows with separate local profiles on the two physical panels. Open ordinary Google search text fields with consent for network activity; submit nothing sensitive. Both operators type concurrently for 60 seconds while alternating native clicks, selection and correction. PASS requires distinct device streams, stable independent activation and zero cross-effects without synthetic focus switching or app-specific extensions. Record mismatches only. A keyboard taking the other window's input is FAIL, not a cosmetic issue.

### 6. Generic application launch, dialogs and focus

**Blocked by the current architecture.** For a future supported implementation, keep a typing task active on B while A opens a Chrome tab, separate window, File Explorer and a Word document/dialog; reverse roles. Use Word only if already installed and appropriately licensed. Include closing a window, modal dialogs, file pickers, restore/maximize and fullscreen transition. PASS requires the other operator's focus, input and monitor placement to remain unaffected throughout. No installation or elevation is authorized by this checklist.

### 7. Generic mouse and scroll independence

**Blocked by the current architecture.** For a future supported implementation, both operators click and drag selections, scroll separate long pages, open context menus and interact with dialogs concurrently in unmodified apps. Include pointer capture and simultaneous button holds. PASS requires independent native hit testing, capture, activation and scroll targets, with zero cross-effects. Two drawn cursors alone fail to establish this.

### 8. Device, display and lifecycle changes

During a controlled run, disconnect A's mouse, reconnect it, then repeat for each keyboard/mouse. Stop must occur, native recovery must remain available, and automatic restart must not occur. Refresh and confirm the correct assignment before explicitly restarting. Repeat with identical devices/USB port changes and, if available, Bluetooth reconnects; ambiguous identity must require reassignment. Hold a modifier/button while disconnecting and ensure it is cleared on the next explicit run.

Disconnect a monitor or change extended-display layout, then restore it. Stop must occur and the controller must remain recoverable. Test session lock/unlock and suspend/resume. Verify the app starts stopped and does not retain typed demo text. Record cases not exercised rather than treating one successful USB unplug as all lifecycle coverage.

### 9. Performance and compatibility

Opt in to aggregate metrics explicitly; verify the default is off. Run the supported controlled workload for five minutes with both device sets active. Record the routed-packet count, process CPU, working set and handler-time mean/maximum while the run remains active. The built-in metrics do not measure managed-heap size. Use an independent clock for duration and note sampling method. Do not persist per-key timing or content. Repeat with metrics off to assess measurement overhead. Source-only observation does not produce a routed-text workload and must not stand in for this run.

Processing time measures application handling, not physical-key-to-display latency. Use Windows' normal performance tools for GPU observations if already available; do not install capture/monitoring drivers. Audio routing, game input and anti-cheat compatibility are unimplemented/unverified and must be listed as such. Do not test against protected games or change anti-cheat/security settings. A controlled workload measurement cannot establish generic-app or gaming performance.

### 10. Stop and recovery

Run the controlled demo; activate Stop with the native pointer from the controller and from each seat window. Confirm the stopped controller status, closed seat windows, cleared text/key/button state and absence of further routed changes. Repeat in source-only observation mode and after a malformed/missing profile, a rejected Start and a hotplug stop. Restart explicitly and confirm no stale modifier/button remains. If native unregistration reports an error, verify routing is still stopped and Exit closes the receiving HWND; report the cleanup failure separately. Close the controller and confirm no SafeSeat process remains; reopen and verify it is stopped.

Finally simulate an unresponsive UI only in an expendable development run and end the SafeSeat process using native Task Manager. Confirm normal Windows pointer/keyboard operation and no background helper/service. PASS requires recorded recovery without reboot, reinstall or system modification. Do not freeze or block global input to perform this test.

## Automated evidence is narrower

The candidate console suites contain 36 core checks and 270 adapter checks for seat assignment, settings validation, reconnect policy, monitor containment, packet parsing, current identity comparison and simulated interleaved input. Lifecycle checks use a fake backend, not production input registration. Build/test commands and any recorded outcomes belong in the public [verification report](VERIFICATION_REPORT.md). Passing these checks demonstrates only the software properties tested.

Hardware enumeration, per-monitor placement, lifecycle/privacy timing, native recovery and arbitrary-application behavior require separate manual acceptance. Keep identifying hardware details in local test records; publish only reviewed, redacted results. Automated checks must never change the physical table above to PASS.
