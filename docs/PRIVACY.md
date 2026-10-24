# Privacy

Updated 24 October 2026. This policy describes SafeSeat's desktop prototype and distinguishes its local operation from external source-hosting or proposed release-signing services. **The project is unsigned and has no approved SignPath Foundation sponsorship or configured production signing service.**

## Local application behavior

SafeSeat does not send application telemetry, typed content or hardware inventory to a network service. It has no background web server, automatic updater, advertising integration or account sign-in. It does not read the clipboard, capture the screen or collect credentials.

On discovery, the app reads Windows keyboard/mouse collection and monitor metadata. Interface paths and PnP container identifiers may be handled transiently to compute hashes and correlate collections. Friendly names are provided by drivers/Windows and can themselves contain identifying text. Hashes are best-effort correlation identifiers, not a promise of anonymity, permanent identity or a physical-device count.

Input observation is off until the operator explicitly chooses **Observe sources (no text)** or **Start controlled demo**. Both modes use foreground-only Raw Input registration. Background-generation packets are rejected before their data is read, and the application checks current foreground ownership before calling the packet reader. The source-only mode shows the device class, short identifier and action kind without translating or buffering text.

The controlled demo keeps bounded temporary ASCII text, held-key/button state and virtual-pointer state in its own process. Stop clears the visible text and held state and ends routing. Foreign-application activation, device/display changes and session/power notifications also stop a run. These safeguards still require physical lifecycle validation; they are not a claim of perfect event timing or security isolation.

There is no persistent keystroke or per-event activity log. Clearing app state is not secure memory erasure: old allocations, a debugger, Windows crash dumps or other independently configured OS facilities can retain process information. Do not type secrets into the prototype.

Optional metrics are off by default. When selected, aggregate packet-handling time, routed-packet count, process CPU and working set are displayed in memory. They do not include typed strings or a per-key timing history. Stop ends collection; aggregate display values can remain until reset or process exit.

## Files controlled by the operator

**Save profile** writes `profiles/default.json` beside the executable. It contains the schema version and seat assignments using hashed collection/monitor IDs, not typed content. Close the app and delete that profile to remove saved assignments. Any backup or synchronization software independently configured for that folder may copy the file.

Explicit developer/build/probe commands can write local test reports, inventory metadata and packaging outputs. Those artifacts are not an automatic background upload. Review them before sharing: driver-provided labels, hashes, paths and local environment details can be identifying. Public issues should contain only the minimum redacted technical information needed to explain a problem; exclude typed strings, credentials, profiles and raw event traces.

## External services

Viewing the repository, downloading releases or posting issues uses GitHub. Its handling of account information, submitted content and service-access metadata is governed by the [GitHub Privacy Statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement).

If the owner later applies for and configures sponsored signing, the signing provider will receive information deliberately supplied for the application and release process, such as project links, source/build provenance and signing artifacts. That is a separate maintainer action, not desktop-app telemetry. Review the [SignPath Privacy Policy](https://signpath.io/privacy-policy) before providing information or enabling an integration. No private email address or legal identity is asserted in this repository.

GitHub and SignPath would be external build/release services; SafeSeat does not contact them during ordinary app operation. Windows, .NET, security software, browsers and other tools have their own policies. This document does not override those policies or promise control over data they independently collect.

For public project questions, use the Issues tab of the repository containing this policy. Do not publish confidential data there. Any future change that adds networking, persistent input diagnostics or other materially different collection must first update this policy and the application's behavior disclosures.
