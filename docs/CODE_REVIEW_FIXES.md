# Source review notes

These corrections address the controlled-app prototype. They do not establish physical two-seat acceptance or resolve the separate arbitrary-application architecture blocker.

## Unreleased 0.1.1-dev: revalidate a known handle's current identity

A deterministic fake-backend regression starts with a handle assigned to identity A, changes the backend's current identity to B without a device-change notification or inventory refresh, and sends another packet with that same handle. The original observer emitted input using its cached A descriptor. The regression was run against the original implementation and failed before the guard was added.

The candidate checks the current interface-path ID and device kind for known, unambiguous input before emitting `DeviceInput`. This uses a narrow `GetRawInputDeviceInfoW` query, not full PnP/display enumeration on each packet. An unavailable, changed or failed lookup pauses acceptance before refresh or error propagation. A plain refresh cannot resume the run; successful cleanup and an explicit Start are required. In-flight packets must also be discarded if a refresh or restart changes their source-map snapshot during native backend work.

These are source and simulated-policy checks. Microsoft documents device handles, interface-name/type queries and change notifications, but provides no atomic source-generation token in these APIs. Reused identical interface paths, queued-packet/query races, physical notification ordering and the performance cost of per-packet lookup remain **NOT TESTED**. The guard detects observable identity changes; it cannot establish immutable physical identity or independent input for third-party apps. [GetRawInputDeviceInfoW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getrawinputdeviceinfow), [RAWINPUTHEADER](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawinputheader), [device-change notifications](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-input-device-change)

## Published 0.1.0 fixes

## Ambiguous monitor seats in a saved profile

The .NET JSON enum converter treated monitor keys `A` and `a` as the same seat, while the existing duplicate-property check compared their spelling exactly. A profile containing both could silently retain only the final assignment. Parsing now rejects duplicate semantic monitor seats before deserialization. Device identities retain their ordinal, case-sensitive behavior; a single lowercase seat key remains supported.

Regression cases cover reversed alias order, both seats, same-value aliases and distinct device IDs differing only by case. They are part of the 36-check core console suite; recorded run results belong in the public [verification report](VERIFICATION_REPORT.md).

## Reconnect requires an explicit restart

An input packet from an unknown handle previously refreshed the identity map and could immediately route that packet under a saved assignment. This could admit a simulated reconnect before the UI had stopped for its device-change notification.

The observer now disables packet acceptance when a device-change notification or unknown handle is encountered. It reports a topology change instead of emitting input from the refreshed mapping. A changed map found by an explicit live refresh also disables acceptance. The UI stops and clears the controlled states when the observer reports a change. Cleanup and a fresh explicit Start are required; an inventory refresh alone cannot revive packet reading. Regression checks use a fully fake native backend and form part of the 210-check adapter console suite.

In published 0.1.0, handle identity is cached between notifications. Its known-handle reuse gap is the subject of the candidate guard above. Neither version has physical validation of notification ordering, immutable hardware identity or actual USB recovery.

## Newest multiline text remains in the viewport

The renderer previously drew the temporary text suffix from a fixed top position. After several newline or wrapped lines, new text could fall below the existing clip. Tall formatted text now moves upward to keep its newest lines within the same viewport. The logical text limit and in-memory-only behavior are unchanged.

Compilation cannot verify the resulting visual layout. Visible trailing-line behavior and physical display/DPI behavior remain **NOT TESTED**. Offscreen construction or routed-event checks cannot substitute for that visual acceptance.

The default console tests do not start a production input observer. Hardware behavior and native recovery must be established through the separate [acceptance procedures](ACCEPTANCE_TESTS.md) under existing security protections.
