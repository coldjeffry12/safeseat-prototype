# SafeSeat

SafeSeat 0.1.0 is an MIT-licensed Windows input research prototype. It enumerates monitors and Raw Input keyboard/mouse collections and routes explicitly enabled observations into two windows owned by SafeSeat.

**It does not provide two independent Windows users or independent Chrome, Word, File Explorer or game input. Physical two-seat acceptance remains NOT TESTED. This release is unsigned; no signing sponsorship or approval is claimed.**

## Implemented subset

- Manual collection and monitor assignments with validated local JSON profiles.
- Foreground-only, default-off source observation and controlled input routing.
- Two app-owned temporary ASCII text states, virtual pointers, buttons and scroll panels.
- Stop/Escape handling and conservative recovery after device, display, session or power changes.
- Simulated routing, packet, registration and reconnect tests without production input registration.

Normal Windows keyboard and pointer input continue. Both seats share one Windows account and native focus. SafeSeat provides no security isolation between people. Read the [feasibility assessment](docs/TECHNICAL_FEASIBILITY.md), [safety instructions](docs/SAFETY_AND_RECOVERY.md) and [acceptance checklist](docs/ACCEPTANCE_TESTS.md).

## Build and verify

Use Windows, PowerShell 7 and the .NET 8 SDK pinned in `global.json`. No NuGet packages are used. The SDK and Windows Desktop runtime must already be available; local scripts do not download or install them.

```powershell
pwsh -NoProfile -File scripts/build.ps1 -SkipUiSmoke
pwsh -NoProfile -File scripts/safety-scan.ps1
pwsh -NoProfile -File tests/SafeSeat.PackageTests/Verify-Package.ps1
pwsh -NoProfile -File scripts/package.ps1 -SkipBuild -SkipGuiSmoke
```

Pass `-DotnetPath 'C:\path\to\dotnet.exe'` to build, packaging or package-test scripts to use an existing portable SDK. Console test runners are dependency-free executable test programs; `dotnet test` is not their invocation. The build script runs both with simulated input only. `-SkipUiSmoke` and `-SkipGuiSmoke` explicitly leave GUI verification NOT TESTED. They never substitute a different executable host to evade Windows execution restrictions.

The [Windows workflow](.github/workflows/build.yml) builds and tests on a GitHub-hosted runner and uploads unsigned archives. It has read-only repository permissions and no signing credentials. Its remote run status is separate from [local verification](docs/VERIFICATION_REPORT.md).

Build/probe logs under `docs/evidence` are private working files. They can contain device metadata or local paths and are excluded from public source and demo packages. Saved profiles, caches, local test fixtures and debug symbols are excluded as well. See [privacy](docs/PRIVACY.md) and [dependencies](docs/DEPENDENCIES.md).

## Controlled demonstration

Only launch `SafeSeat.exe` when the existing Windows policy permits it. Do not elevate or disable protections. Run scripts preserve the executable's DPI/asInvoker manifest and stop on execution errors.

1. Back up important files, read the safety instructions and use a standard-user lab with disposable test text.
2. Refresh inventory. Use **Observe sources (no text)** to identify one device at a time, then press Escape.
3. Assign one keyboard collection and one mouse collection to each seat and select different monitors. Collections and PnP groups are not physical device counts; verify ownership manually.
4. Release all keys, select the standard US English keyboard layout and choose **Start controlled demo**.
5. Test the app's own text, virtual pointer, Counter/Clear and scrolling state. Native Windows input remains shared.
6. Press Escape or Stop. Device/display/session/foreground changes stop observation and require review and another explicit Start.

Text support is a bounded US ASCII subset. IME, Unicode composition, dead keys, AltGr, clipboard, numpad/Num Lock behavior, general text editing, absolute pointing devices and third-party application routing are unsupported. Physical USB recovery, visible rendering/DPI and performance remain unverified. Temporary strings are not securely zeroed from managed memory; never use credentials or sensitive text.

## Code signing policy

See the [Code signing policy](docs/CODE_SIGNING_POLICY.md). The proposed free signing route requires external acceptance and an approved, verifiable build process. No private keys, credentials or certificates are included. Signing cannot solve the independent Windows focus limitation.

Original source is covered by [LICENSE](LICENSE). Windows remains a proprietary host prerequisite with its own licensing terms. No trial, paid multiseat app, driver or network service is required by the implemented prototype.
