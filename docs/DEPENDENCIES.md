# Dependencies and provenance

SafeSeat has **zero PackageReference dependencies**. The restore source list is empty. Its only application frameworks are the open-source .NET runtime and WPF supplied by the Windows Desktop shared framework. Windows is the existing proprietary host; this project cannot turn Windows or its licensing into open source. No paid multiseat software, proprietary helper, driver or remote server is required by the implemented subset.

| Component | Declared version or requirement | Purpose | License / provenance | Main risk |
| --- | --- | --- | --- | --- |
| SafeSeat original source | 0.1.1-dev (unreleased) | App, adapters, tests | [MIT](../LICENSE) | Unverified physical behavior; no security isolation between seats |
| .NET SDK | 8.0.426, MSBuild 17.11.69 | Build only | [dotnet/sdk MIT](https://github.com/dotnet/sdk/blob/main/LICENSE.TXT), official Microsoft archive | Trusted compiler/toolchain dependency; maintain security updates |
| Roslyn C# compiler | 4.11.0-3.25569.22, commit 3fb752d4 | SDK build tool | [dotnet/roslyn MIT](https://github.com/dotnet/roslyn/blob/main/License.txt) | SDK includes additional third-party notices |
| Microsoft.NETCore.App | .NET 8; reference SDK distribution supplies 8.0.32 | Runtime, JSON, collections, SHA-256 | [dotnet/runtime MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) | Installed compatible runtime patch can differ; framework internals have separate notices |
| Microsoft.WindowsDesktop.App / WPF | .NET 8; reference SDK distribution supplies 8.0.32 | Windows UI | [dotnet/wpf MIT](https://github.com/dotnet/wpf/blob/main/LICENSE.TXT) | Windows-only; per-monitor DPI and UI require hardware validation |
| Generated .NET apphost | Reference host pack 8.0.32 | Native `SafeSeat.exe` entry point and manifest | [dotnet/runtime MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) | Derived from an upstream host template; signing eligibility requires separate review |
| PowerShell | 7.0 or later for packaging | Optional build/run scripts | [PowerShell MIT](https://github.com/PowerShell/PowerShell/blob/master/LICENSE.txt) | Scripts do not install software or change execution policy |
| Windows APIs: user32, setupapi, wtsapi32 | External Windows host; research target is Windows 11 Pro | OS input/display/session services | Applicable proprietary Windows terms; no OS binary redistributed | Shared native focus/pointer; simultaneous-user licensing constraints |

This is a framework-level inventory, not an exhaustive enumeration of every internal .NET component. [sbom.cdx.json](sbom.cdx.json) records the declared components and graph. Framework versions describe the pinned reference distribution, not an inventory of a particular user's installed runtime. The framework-dependent app package contains SafeSeat assemblies and a generated apphost, and requires a separately supplied compatible .NET Windows Desktop runtime. It does not bundle the SDK or shared frameworks. Retain the applicable .NET LICENSE and ThirdPartyNotices files with the generated host, and review the full inventories before redistributing additional runtime components.

The SDK [creates the apphost from a native template](https://github.com/dotnet/runtime/blob/main/src/installer/managed/Microsoft.NET.HostModel/AppHost/HostWriter.cs), patches the managed entry-point path and copies application resources. This dependency must remain visible in release provenance. SafeSeat product metadata does not change upstream ownership or establish permission from a signing service. Consult the separate [code signing policy](CODE_SIGNING_POLICY.md); this document makes no claim that an artifact is signed or accepted for signing.

## Pinned toolchain and acquisition

`global.json` pins SDK 8.0.426 with patch roll-forward. Obtain it through the project's reviewed CI setup or Microsoft's official distribution. The [release metadata](https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/8.0/releases.json) lists the [Windows x64 SDK archive](https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.426/dotnet-sdk-8.0.426-win-x64.zip). Compare the archive's SHA-512 with Microsoft's metadata before extraction. The declared reference archive hash is:

Before extraction, its SHA-512 was verified against the official metadata:

```text
10261657109f1c62126f106a9ac966739b025b5f6b4a9f88ab7c6e73cacce9aec771895daa3267b8da933a83c32ee91bf458dc6704e1ddcd9bcf0d5edd036abb
```

Hash comparison supports provenance; it is not a complete supply-chain or vulnerability audit. Reassess the pinned SDK and runtime patches when maintaining the project. Do not treat this reference hash as authorization to execute an installer or change a machine configuration.

Build scripts use a supplied SDK path. They set process-local `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`, disable CLI telemetry/global-tool PATH registration and redirect SDK state into the build directory. Packaging does not install a runtime or modify Windows settings. The public [verification report](VERIFICATION_REPORT.md) records the scope of actual software checks; physical acceptance remains NOT TESTED.
