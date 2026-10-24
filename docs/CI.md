# Public build and signing preparation

The workflow builds the unsigned prototype on a GitHub-hosted Windows runner. It uses a read-only repository token, persists no checkout credentials and needs no signing secrets. It does not launch the GUI, enumerate real devices, probe the runner's hardware or start a production Raw Input observer. Only simulated console tests run. Private local logs and profiles never become source or demo archive inputs.

The official actions are pinned to the full release commit IDs verified on 24 October 2026:

| Action | Release | Source commit |
| --- | --- | --- |
| `actions/checkout` | 7.0.1 | [3d3c42e5aac5ba805825da76410c181273ba90b1](https://github.com/actions/checkout/commit/3d3c42e5aac5ba805825da76410c181273ba90b1) |
| `actions/setup-dotnet` | 6.0.0 | [a98b56852c35b8e3190ac28c8c2271da59106c68](https://github.com/actions/setup-dotnet/commit/a98b56852c35b8e3190ac28c8c2271da59106c68) |
| `actions/upload-artifact` | 7.0.2 | [cf430e030ddbb5b0abf93d22962f4752f3646cd9](https://github.com/actions/upload-artifact/commit/cf430e030ddbb5b0abf93d22962f4752f3646cd9) |

The SDK version comes from `global.json`. Release debug symbols are disabled, and compiler paths are normalized; private developer paths must not enter distributed binaries. Version metadata is shared across the three original managed assemblies. A public source commit, remote workflow run and uploaded artifact are still required before claiming CI provenance. A local package does not establish that provenance.

Signing is not configured. After external acceptance, an approved provider integration must bind signatures to the reviewed repository/commit and build artifact, require manual release approval, and cover only explicitly approved files. The SDK-generated native .NET apphost needs a provider eligibility decision. Do not sign arbitrary runtime/toolchain binaries or accept unrelated uploaded executable files.

After signing, verify returned signatures and publisher identity, create archives from those exact returned files, and calculate new checksums. Rebuilding over a signed artifact or reusing unsigned hashes would invalidate that verification. Signing does not establish two-seat physical independence or override every possible Windows policy.
