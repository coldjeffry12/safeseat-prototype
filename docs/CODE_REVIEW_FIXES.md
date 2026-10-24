# Source review notes for 0.1.0

These corrections address the controlled-app prototype. They do not establish physical two-seat acceptance or resolve the separate arbitrary-application architecture blocker.

## Ambiguous monitor seats in a saved profile

The .NET JSON enum converter treated monitor keys `A` and `a` as the same seat, while the existing duplicate-property check compared their spelling exactly. A profile containing both could silently retain only the final assignment. Parsing now rejects duplicate semantic monitor seats before deserialization. Device identities retain their ordinal, case-sensitive behavior; a single lowercase seat key remains supported.

Regression cases cover reversed alias order, both seats, same-value aliases and distinct device IDs differing only by case. They are part of the 36-check core console suite; recorded run results belong in the public [verification report](VERIFICATION_REPORT.md).

## Reconnect requires an explicit restart

An input packet from an unknown handle previously refreshed the identity map and could immediately route that packet under a saved assignment. This could admit a simulated reconnect before the UI had stopped for its device-change notification.

The observer now disables packet acceptance when a device-change notification or unknown handle is encountered. It reports a topology change instead of emitting input from the refreshed mapping. A changed map found by an explicit live refresh also disables acceptance. The UI stops and clears the controlled states when the observer reports a change. Cleanup and a fresh explicit Start are required; an inventory refresh alone cannot revive packet reading. Regression checks use a fully fake native backend and form part of the 210-check adapter console suite.

Handle identity is still cached between notifications. Reuse of an already-known handle before a device-change notification has not been validated on physical Windows hardware. This review does not establish notification ordering, perfect hardware identity or actual USB recovery.

## Newest multiline text remains in the viewport

The renderer previously drew the temporary text suffix from a fixed top position. After several newline or wrapped lines, new text could fall below the existing clip. Tall formatted text now moves upward to keep its newest lines within the same viewport. The logical text limit and in-memory-only behavior are unchanged.

Compilation cannot verify the resulting visual layout. Visible trailing-line behavior and physical display/DPI behavior remain **NOT TESTED**. Offscreen construction or routed-event checks cannot substitute for that visual acceptance.

The default console tests do not start a production input observer. Hardware behavior and native recovery must be established through the separate [acceptance procedures](ACCEPTANCE_TESTS.md) under existing security protections.
