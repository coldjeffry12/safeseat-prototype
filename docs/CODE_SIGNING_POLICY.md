# Code signing policy

Updated 24 October 2026. **SafeSeat is unsigned. SignPath Foundation has not approved, sponsored or signed this project.** This document describes the intended release process, not a working signing integration or a certificate already granted to the project.

## Project and responsibility

SafeSeat is an MIT-licensed Windows input research prototype. It routes observed devices into two application-owned demonstration windows. It does not provide independent input/focus for arbitrary Windows applications, and physical two-seat acceptance remains unverified. A future signature would not change those limits or grant Windows simultaneous-user rights.

The intended maintainer is the owner of the GitHub account [coldjeffry12](https://github.com/coldjeffry12). This is an account handle, not a claim about a person's legal identity or an organization. The repository containing this policy is the source of the project's release process.

| Intended role | Responsibility | Proposed account |
| --- | --- | --- |
| Author/maintainer | Review and maintain source, build scripts and CI configuration | coldjeffry12 |
| Reviewer | Review contributions before accepting them into a release | coldjeffry12 |
| Release approver | Review the exact commit, artifacts and signing request; manually approve or reject it | coldjeffry12 |

These are proposed roles; provider-side permissions have not been configured. No independent second reviewer is claimed. MFA on repository and signing accounts must be verified before production signing. The account owner remains responsible for reviewing AI-assisted changes and release decisions.

## Proposed signing scope

The proposed managed outputs are `SafeSeat.dll`, `SafeSeat.Core.dll` and `SafeSeat.Windows.dll`. Release launch scripts may be added only when explicitly listed in an approved artifact configuration.

`SafeSeat.exe` is a generated .NET apphost based on an upstream MIT-licensed native template supplied by the SDK. It is not wholly SafeSeat-authored source. Its eligibility under the Foundation's own-source-binary rules must be confirmed before it is included in a Foundation signing configuration. No blanket authorization to sign the .NET SDK, runtime or other upstream binaries is proposed. The shared runtime is supplied separately and is not bundled.

## Intended release gates

1. Build the chosen source commit using the reviewed workflow, with source, build scripts and CI configuration available in the repository. GitHub-hosted runners must perform every job leading to an OSS signing request. Upload the build artifact to GitHub Actions before submitting it. [SignPath GitHub integration](https://docs.signpath.io/trusted-build-systems/github)
2. Require origin verification for the approved repository and release branch/commit. Locally built or manually substituted binaries must not enter the release-signing path. [Origin verification](https://docs.signpath.io/origin-verification/)
3. Validate an explicit artifact list. Every own binary submitted for signing must identify its product as `SafeSeat` and use the same release product version. Record the commit separately and reject missing or inconsistent metadata. [Artifact metadata restrictions](https://docs.signpath.io/artifact-configuration/reference#file-metadata-restrictions)
4. Review test results and limitations, then require the designated owner's manual approval of each request. Signing credentials belong in restricted CI secrets and must not be committed or exposed to untrusted contributions.
5. Retrieve the signed outputs, verify signatures and timestamps, and generate checksums from the final distribution bytes. Preserve the commit and signing-request provenance. Do not describe an unsigned fallback as a signed release.

These gates are planned requirements. No provider credentials, certificate or successful signing run are implied by this policy. If the provider rejects the project or the apphost scope, stop the proposed signing path; do not work around its constraints or Windows application controls.

## Provider status and attribution

The proposed route is the sponsored program described in the [SignPath Foundation conditions](https://signpath.org/terms.html). Acceptance is discretionary, including review of project reputation and provenance. No approval date or entitlement is promised.

Only after actual approval and an active sponsored signing arrangement may release pages present the following attribution as active:

> Free code signing provided by SignPath.io, certificate by SignPath Foundation.

Until then, that sentence is a conditional attribution example, not a sponsorship claim. If accepted, the provider's certificate identifies SignPath Foundation as publisher and its private key remains in the provider's signing infrastructure. [Program overview](https://signpath.org/)

GitHub and any future signing provider are external release services, not runtime dependencies. Their use does not make a claim that those hosted services are open-source software. The app's data handling is described in [Privacy](PRIVACY.md). Public project questions belong in this repository's Issues tab; do not post credentials or private diagnostic data.
