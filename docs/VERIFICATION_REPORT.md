# Verification of the unsigned research release

This is an unsigned research release. The original arbitrary-application multiseat requirement remains incomplete. All physical acceptance criteria remain NOT TESTED.

## Repeatable software checks

Run `scripts/build.ps1 -SkipUiSmoke` on Windows with the SDK pinned in `global.json`. It restores without external NuGet feeds, builds the solution with warnings treated as errors, runs the core console tests, and runs adapter checks with fake native registration backends. No production observer or GUI is started by those commands.

The current software suite contains 36 core tests and 210 adapter checks: 62 ABI/packet/foreground-code checks, 22 PnP-group checks, 20 readiness/topology checks and 106 simulated registration/recovery checks. Test output is local build evidence, not proof of physical two-user independence.

Run `tests/SafeSeat.PackageTests/Verify-Package.ps1` after a successful build to check that synthetic private reports, saved profiles, stale files, debug symbols and a chosen output subtree do not enter public packages. Existing local files are preserved. Run `scripts/safety-scan.ps1` for supporting source/manifest checks; it is not an antivirus scan or a complete security audit.

## Verified release evidence

The public repository [coldjeffry12/safeseat-prototype](https://github.com/coldjeffry12/safeseat-prototype) and [unsigned prerelease v0.1.0](https://github.com/coldjeffry12/safeseat-prototype/releases/tag/v0.1.0) were published on 24 October 2026. The release is tied to [commit 66afd3f930ffed2ada72e9932107ead661fd2288](https://github.com/coldjeffry12/safeseat-prototype/commit/66afd3f930ffed2ada72e9932107ead661fd2288). All 57 uploaded Git blobs matched the checked source files byte for byte, with no unexpected paths.

The [GitHub Actions run](https://github.com/coldjeffry12/safeseat-prototype/actions/runs/40055295259) passed on a GitHub-hosted Windows Server 2025 runner using .NET SDK 8.0.426. The build produced zero warnings/errors, 36 core tests and 210 adapter checks passed, and the package privacy regression passed 545 checks. GUI smoke and production input observation were explicitly skipped. This runner does not demonstrate physical Windows 11 two-seat behavior.

The unchanged CI archives were inspected before publication, and GitHub's final release asset SHA-256 digests matched the inspected files:

| Asset | SHA-256 |
| --- | --- |
| SafeSeat-source.zip | dad39d37999cf99114ba6767d75514658b37bcf085c4ffdc67edd826723ab454 |
| SafeSeat-demo.zip | d2ac36ff7607c4a4998cf91c9d4c3ba61bbf995590259a6a7ac8df6b2f09cb32 |
| SHA256SUMS.txt | 0de6e5e1b6d4b0acc00440a73c5cd1671a6e04f2a4301714419c9f8cfbeb7002 |

The source archive contains exactly the expected 57 files. Windows Git checkout changed line endings in 56 files; all 57 match the staged UTF-8 text after CRLF-to-LF normalization. The archives were not edited or repackaged. The demo contains 27 allowlisted files, expected SafeSeat product/version metadata and required notices. No private diagnostics, profiles, PDBs, SDK or shared runtime were included. All four PE binaries are unsigned. Hash and metadata checks are not an antivirus scan or a signature-trust verdict.

Later documentation updates on the default branch do not replace the frozen v0.1.0 source, build or release assets. Use the commit-pinned evidence above when reproducing that release.

## Signing status

The SignPath Foundation application has been submitted and receipt acknowledged. This is not provider acceptance, sponsorship, apphost eligibility approval, a certificate or a signed build. Review of the generated .NET apphost's eligibility remains necessary; no signing credentials or provider-side integration have been configured. See the [code signing policy](CODE_SIGNING_POLICY.md).

Private machine inventories, raw diagnostics, local file paths, operator evidence and applicant contact information are excluded from public source. The published source, repeatable commands and linked CI run provide the review path.

## Unverified behavior

The revised renderer has compilation verification only. Actual simultaneous devices, source ownership, visible placement/DPI, lock/suspend/USB recovery, Stop recovery, runtime performance and arbitrary third-party app independence have not passed physical acceptance. Input-handle identity is cached between notifications; reuse of an already-known handle before a change notification remains physically unverified. Do not convert simulated passes into hardware or compatibility claims.
