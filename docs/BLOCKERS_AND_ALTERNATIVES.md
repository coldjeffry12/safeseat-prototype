# Blockers and alternatives

The full requirement remains **incomplete**. SafeSeat implements a controlled application input experiment. It does not make arbitrary Windows applications independent for two simultaneous users. The [technical assessment](TECHNICAL_FEASIBILITY.md) gives the source-supported platform and licensing analysis.

## What blocks general Windows multiseat

1. **The missing routing primitive.** Raw Input tells SafeSeat which device produced an event. Normal application activation still uses Windows foreground focus. No reviewed documented API binds Keyboard A/Mouse A and Monitor A to an independent native foreground context while binding the other devices to Monitor B. This is the project's API-level blocker, not a demonstrated universal impossibility result.
2. **Observation preserves shared native input.** The prototype deliberately leaves the Windows pointer and ordinary keyboard path active. Its virtual pointers and text models are independent only inside code that explicitly routes raw events. A native click can still activate another application; SafeSeat must then stop observation.
3. **Separate desktop objects do not solve local concurrency.** Only one input desktop is active in the interactive station. A supported concurrent-session product would require an appropriate Windows edition/deployment and rights; it is not a feature this prototype can enable on Pro.
4. **Arbitrary apps need more than initial placement.** Child windows, dialogs, shell brokers, elevation prompts, IME, games, capture and fullscreen behavior do not become seat-aware because a launcher moves an HWND. Per-application hacks cannot establish universal compatibility.
5. **Licensing and physical acceptance require separate evidence.** Review the agreement applying to the intended deployment; SafeSeat asserts no two-human licensing exception. This public release contains no physical demonstration of two unmodified Chrome windows accepting separate streams concurrently. Acceptance tests 5–7 remain NOT TESTED, and the architecture is a NO-GO for those tests.

`SendInput`, posted key/mouse messages, fake cursors and rapidly changing foreground windows do not remove these blockers. Their API contracts and limitations are reviewed in [Technical feasibility](TECHNICAL_FEASIBILITY.md#rejected-forwarding-mechanisms). No unsafe fallback is pending.

## Useful subset

The controlled prototype is useful for reviewing device identity, assignment validation, reconnect behavior, raw-event parsing, input-state isolation and bounded virtual pointers. Its explicit **Observe sources (no text)** mode identifies a device's row from basic event metadata without starting text routing. Both modes accept foreground-application events only; either owned seat window can be active, and background packet contents are not decoded. Simulated tests can validate that a Seat A event cannot mutate Seat B's model. An operator can later validate two physical device sets inside the application's own windows, subject to the [license boundary](TECHNICAL_FEASIBILITY.md#license-boundary). None of these results passes the generic-application requirement.

The project introduces no new OS security boundary. Both seats are application state within the same user/process. Do not use it to separate confidential work between mutually untrusted people.

## Alternatives requiring a changed requirement

| Alternative | What it can change | What still needs verification | Why it does not meet the original target |
| --- | --- | --- | --- |
| Keep the SafeSeat controlled demo | Cooperating application code can route each device to a separate internal state. Original code and .NET/WPF are open source. | Physical USB/display behavior and the license terms for the intended use. | Generic Chrome/Word/game control remains unsupported. |
| Separate OS instances using QEMU | Guests can have their own input/focus state. QEMU is open source; see its [license](https://www.qemu.org/docs/master/about/license.html). | Independent device assignment on the chosen host, safe drivers, graphics/audio capacity, guest licensing, workload and anti-cheat policies. QEMU's [USB support](https://www.qemu.org/docs/master/system/devices/usb.html) is host-dependent. | Adds an OS instance and configuration; a Windows guest is not automatically free. No VM is included or installed here. |
| A platform designed for licensed concurrent sessions | Microsoft's [Enterprise multi-session FAQ](https://learn.microsoft.com/en-us/azure/virtual-desktop/windows-multisession-faq) describes the supported service context. | Entitlements, service cost, endpoints, display/USB/audio routing and app compatibility. | Changes edition/deployment and normally the cost/dependency assumptions. It does not promise two local USB seats on Pro. |
| Separate physical PC per person | Gives each PC its own native input and application environment. | Hardware, OS and application licenses, shared data policy. | Requires a second computer and expense. |
| Linux multiseat research | Changes the platform's seat-management options. | Specific GPU/USB/audio topology, applications and supported distribution setup. | Changes the required Windows platform; Windows app compatibility is not guaranteed. |

These are decisions to evaluate separately, not substitutions silently included in SafeSeat. No hypervisor, alternative OS, paid service or third-party multiseat package is a required dependency.

## Conditions for reopening Gate 3

A future proposal must identify the documented per-device input/focus mechanism, show that it works without changing the other seat's activation, explain security boundaries and licensing, and pass physical tests 5–7 with unmodified applications. It must preserve the strict safety policy. A proposal requiring OS binary patches, session-policy workarounds, kernel/filter/virtual-HID drivers, elevation, process injection, weakened security or anti-cheat bypass remains outside this project.
