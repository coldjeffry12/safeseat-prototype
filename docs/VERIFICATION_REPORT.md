# Verification of public source preparation

This is an unsigned research release. The original arbitrary-application multiseat requirement remains incomplete. All physical acceptance criteria remain NOT TESTED.

## Repeatable software checks

Run `scripts/build.ps1 -SkipUiSmoke` on Windows with the SDK pinned in `global.json`. It restores without external NuGet feeds, builds the solution with warnings treated as errors, runs the core console tests, and runs adapter checks with fake native registration backends. No production observer or GUI is started by those commands.

The current software suite contains 36 core tests and 210 adapter checks: 62 ABI/packet/foreground-code checks, 22 PnP-group checks, 20 readiness/topology checks and 106 simulated registration/recovery checks. Test output is local build evidence, not proof of physical two-user independence.

Run `tests/SafeSeat.PackageTests/Verify-Package.ps1` after a successful build to check that synthetic private reports, saved profiles, stale files, debug symbols and a chosen output subtree do not enter public packages. Existing local files are preserved. Run `scripts/safety-scan.ps1` for supporting source/manifest checks; it is not an antivirus scan or a complete security audit.

## Publication status

The public repository [coldjeffry12/safeseat-prototype](https://github.com/coldjeffry12/safeseat-prototype) was created on 24 October 2026. Sanitized source upload is in progress at this checkpoint. The GitHub workflow has been prepared, but the published source commit, successful remote run and unsigned binary release remain to be verified. Repository creation alone is not build or release evidence.

The signing application is a draft and has not been submitted. The project is not approved or sponsored and its artifacts are not digitally signed. Provider review of the generated .NET apphost's eligibility remains necessary; no signing credentials or provider-side permissions have been configured.

Private machine inventories, raw diagnostics, local file paths and original operator evidence are intentionally excluded from this public source snapshot. The maintained source, tests, repeatable commands and a future public CI run provide the review path.

## Unverified behavior

The revised renderer has compilation verification only. Actual simultaneous devices, source ownership, visible placement/DPI, lock/suspend/USB recovery, Stop recovery, runtime performance and arbitrary third-party app independence have not passed physical acceptance. Input-handle identity is cached between notifications; reuse of an already-known handle before a change notification remains physically unverified. Do not convert simulated passes into hardware or compatibility claims.
