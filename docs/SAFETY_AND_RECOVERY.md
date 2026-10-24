# Safety and recovery

SafeSeat is a standard-user, user-mode prototype. It observes device events only after an explicit **Observe sources (no text)** or **Start controlled demo** action. Source observation shows source/action metadata; the controlled demo routes events into its own state. Native Windows pointer/keyboard input remains active. No claim of zero risk or complete compatibility is made. Review [the technical boundary](TECHNICAL_FEASIBILITY.md) before testing.

## Before a hardware run

1. Save work and close password managers, confidential documents and other applications containing sensitive input. Use expendable test text only. Do not type passwords, account details or clipboard contents into the demo.
2. Back up important files using your normal backup method; verify that the backup can be read. A restore point does not replace a file backup.
3. Before the physical experiment, prepare a restore point through Start → **Create a restore point** → **System Protection** → **Create**, if protection is already enabled and the action is permitted. Microsoft documents this in [System Protection](https://support.microsoft.com/en-us/windows/experience/backup-recovery/system-protection). If protection is disabled or additional permissions are required, stop and arrange the precaution with the system owner. SafeSeat does not enable protection, create restore points or execute an elevation command.
4. Use a standard non-admin Windows account. Keep Secure Boot, UAC, Defender, Memory Integrity/Core Isolation and driver signature enforcement enabled. The prototype requires no elevation, security exclusion, driver installation, service, scheduled task or boot change.
5. Start with one operator and two device sets or automated simulation. Review the [Windows license boundary](TECHNICAL_FEASIBILITY.md#license-boundary) before any simultaneous two-human use.
6. Prefer a disposable lab PC or a separate test account. A VM can test application behavior but does not prove physical HID assignment on the host. Keep a native keyboard/mouse recovery path available.

Restore-point creation is a system-owner-controlled precaution, separate from SafeSeat's setup. The application writes its explicitly saved local profile under `profiles/default.json` beside the executable. Build/test tools also write local build outputs and result files.

The build scripts use an already available SDK, disable first-run certificate generation and telemetry, and redirect SDK state into the build directory. These process-local controls do not install or configure Windows. A source review or successful build does not certify host protections; acceptance test 1 requires its own before/after comparison.

## Observation and privacy contract

- Starting the UI or enumerating hardware must not start raw-event observation. **Start controlled demo** requires valid device/monitor assignments and the standard US layout. **Observe sources (no text)** is also an explicit opt-in; it can identify devices without complete assignments or two monitors and does not start text routing.
- While either mode is running, an owned SafeSeat window must remain the foreground application. The window-message handler stops on `WM_ACTIVATEAPP` deactivation and checks foreground process ownership before reading each packet. A 33 ms dispatcher-timer check is a fallback, not a guaranteed response bound. Session lock/disconnect, suspend, device arrival/removal and display configuration change must stop the run. Resume requires another explicit user action; returning to SafeSeat must not restart it automatically. Real lock/disconnect delivery remains part of the manual lifecycle test.
- SafeSeat registers session notifications for its own HWND/current session and refuses to start observation if registration fails. It does not enable Remote Desktop, create a session, change a policy or observe other users' sessions. Closing unregisters that notification subscription.
- Both observation modes register `RIDEV_DEVNOTIFY` only. They do not request `RIDEV_INPUTSINK` or `RIDEV_EXINPUTSINK`; delivery remains foreground-application only, including when either owned seat window is active. The observer rejects background `RIM_INPUTSINK` messages before any packet data read, and the main window checks current foreground ownership before each packet-reader call. It excludes `RIDEV_NOLEGACY` and all interception/capture flags. [Microsoft's flag reference](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawinputdevice) and [WM_INPUT reference](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-input) explain these distinctions. Background packet contents are not decoded.
- Its own WPF windows handle native Space/Enter locally so typed demo text cannot activate a focused Start/Stop button. This changes only SafeSeat's button behavior. Escape stops the run; native system shortcuts remain available.
- Source-observation mode shows a short device identifier and basic action kind without translating or buffering text. Controlled-demo text is bounded application state and is cleared on Stop. No typed strings, raw event stream, password, clipboard content, screenshot or keycode trace should be written to diagnostics or settings. Clearing visible text/held state is not a forensic guarantee of memory erasure; operating-system crash dumps or a debugger can retain process data.
- Settings contain seat assignments and hashed device identifiers, not text. Hashing an interface path is not proof of anonymity. Review profiles before sharing them; identical devices and reconnects can require reassignment.
- Optional metrics are disabled by default. When enabled, retain aggregate event counts, process CPU/memory and processing-time summaries in memory only. They must not include typed content, per-key timing history or a persistent activity record.
- There is no application telemetry, background network listener, credential collection or automated screenshot capture. Do not extend diagnostics into an always-running input recorder.

The foreground/session safeguards reduce unintended observation; real timing and lifecycle behavior still require acceptance tests. An OS notification race is a reason to test the boundary, not a reason to claim perfect isolation.

## Stop and recovery

1. Select **STOP / clear temporary text** in the controller or **Stop / Clear temporary text** in a seat window. Expected behavior: stop routing immediately, clear key/button/text state, close the seat windows and unregister Raw Input delivery. The controller remains open. Both physical devices still use the normal Windows path. If native unregistration fails, the UI reports the Win32 error code; select **Exit** to destroy the receiving HWND. Its guarded handler stops reading packets even while cleanup is pending.
2. Native Escape/closing the controller can be used according to the UI instructions. Switching to another application must stop the run automatically; do not rely on a virtual pointer to recover.
3. If SafeSeat is unresponsive, use native **Ctrl+Shift+Esc** or **Ctrl+Alt+Delete**, open Task Manager and end only the SafeSeat process. Its state is process-owned. No global input suppression, hook, driver or persistent helper should need removal.
4. Reopen the application stopped. Re-enumerate devices/monitors, inspect assignments and explicitly Start again only after correcting the cause. Hotplug/suspend must never silently assign another person's device or restart observation.
5. To remove the prototype, close it and remove its project/build files and, if desired, the saved local profile described in the README. No registry, Windows binary or boot restoration is part of normal removal. Delete only the verified SafeSeat path, never a computed parent folder.

No reboot or reinstall should be required by this design. That is an expected property to demonstrate in acceptance test 10, not a completed physical result. If native input is unusable after the process ends, stop testing and investigate with the owner; do not install a filter driver or disable a security feature as a workaround.

If Windows Application Control or another security policy refuses `SafeSeat.exe`, leave that policy in force and stop. Do not use a different executable host, rename the binary to evade a rule, add an exclusion or request elevated execution as a workaround. A code signature identifies a signed artifact; it does not guarantee permission under a particular Windows policy.

## Changes excluded from this project

No system-DLL/RDP/termsrv/Winlogon/CSRSS/kernel patching; no concurrent-session policy or boot modification; no SYSTEM token; no process injection or UIPI bypass; no keyboard/mouse hooks or filter/virtual-HID drivers; no Secure Boot/Defender/UAC/Memory Integrity changes; no automatic installers, startup registration or third-party scripts. Any future architecture needing elevation requires a separate explanation and explicit owner approval before execution. Such an architecture is not waiting to be run by this repository.

The public [verification report](VERIFICATION_REPORT.md) records the scope of software checks. Code review can identify operations in the submitted source; it cannot certify every host security setting or replace a before/after host audit. All physical acceptance criteria remain **NOT TESTED** until the relevant procedure is completed and recorded.
