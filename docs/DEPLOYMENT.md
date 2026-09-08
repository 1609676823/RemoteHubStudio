# Deployment targets and modes

**English** | [简体中文](DEPLOYMENT.zh-CN.md)

RemoteHubStudio uses .NET 10, WinForms, AntdUI, and Windows APIs. By default it produces **self-contained Windows x86, x64, and ARM64 ZIPs plus a Windows portable ZIP (AnyCPU DLL + native EXE launcher)**. Extract all files into a writable directory; preserve the application's `data` directory when upgrading. Remote clients are installed separately.

## Supported targets

Visual Studio's runtime menu lists general .NET targets; it does not certify that every target supports this application.

| Target | This application | Launch / meaning |
| --- | --- | --- |
| `win-x86` | Enabled | 32-bit Windows process; `RemoteHubStudio.exe`. Can also use supported x86 emulation on 64-bit Windows |
| `win-x64` | Enabled | Intel/AMD 64-bit Windows; preferred for most PCs |
| `win-arm64` | Enabled | ARM64 Windows; validate UI behavior on an ARM64 device |
| Portable, no RID | Enabled | AnyCPU DLL + native EXE, without a bundled runtime; run `RemoteHubStudio.exe` (x64 on current Actions runners), `dotnet RemoteHubStudio.dll`, or `Start-RemoteHubStudio.cmd` |
| `win-arm` | Unsupported | Windows ARM32 is different from ARM64; no corresponding .NET 10 Windows desktop runtime |
| Linux RIDs | Unsupported | WinForms, DPAPI, and window APIs require Windows |
| macOS RIDs | Unsupported | Changing a RID cannot port the UI or OS-specific services |

All ZIPs are installation-free folder deployments. **The Visual Studio “portable” setting means no fixed RID, not no runtime prerequisite or cross-OS compatibility.** The portable EXE requires the .NET 10 Windows Desktop Runtime matching its architecture. When using CMD or `dotnet`, install the Desktop Runtime matching the selected `dotnet` host instead. The plain .NET Runtime and ASP.NET Core Runtime do not include WinForms. Use an explicit path to `dotnet.exe` when selecting a particular host architecture; the convenience CMD launcher uses `PATH`.

## Why Visual Studio portable publishing includes an EXE

Portable publishing can include a native launcher (apphost). The previous script explicitly set `UseAppHost=false`, producing DLLs and a CMD launcher only. Packaging now matches Visual Studio's default behavior: **empty `RuntimeIdentifier`, `UseAppHost=true`, `SelfContained=false`, and `PlatformTarget=AnyCPU`**.

**The DLL is AnyCPU; the native EXE has a fixed architecture.** Without a RID, the SDK defaults the apphost to its own platform. The current x64 Actions SDK produces an x64 EXE; local SDKs of other architectures may differ. The script reads the generated EXE's actual architecture into `PACKAGE-README.txt` and the release download table. Missing EXEs fail packaging for every target.

For an x86 or ARM64 EXE, select the `win-x86` or `win-arm64` package. Alternatively, run the portable AnyCPU DLL with the corresponding architecture's `dotnet`. The retained `Start-RemoteHubStudio.cmd` runs the DLL through `dotnet` on `PATH`, not the bundled x64 EXE.

## Windows version requirements

RID selection distinguishes CPU architectures, not separate Windows 10/11 editions. Use Windows versions in Microsoft's current .NET 10 support list: supported Windows 11 versions and the listed Windows 10 LTSC/Enterprise versions. Do not assume all Windows 10 editions remain supported.

The `windows7.0` suffix in `net10.0-windows7.0` describes the compile-time Windows API surface. **It does not promise Windows 7 compatibility.** Windows 7/8/8.1 are outside the current .NET 10 support range; bundling the runtime does not change that. Windows Server also needs a suitable desktop GUI environment. Generic .NET support for Server Core/Nano Server is not evidence that this WinForms app supports those environments.

Packages can be built on x64 Windows. The regression suite runs on the x64 build host; successful ARM64 packaging and content checks do not constitute native ARM64 UI testing. OS support changes with vendor lifecycle policies.

## Choosing a deployment mode

| Mode | Bundles .NET | Typical audience | Runtime patching |
| --- | --- | --- | --- |
| `self-contained` (default) | Yes; larger download | General desktop users and offline distribution | Maintainer rebuilds and republishes with updated runtime |
| `framework-dependent` | No; smaller download | Managed environments with centrally installed runtimes | Administrator/user updates the matching Desktop Runtime |
| `both` | Offers both variants for every RID | Mixed audiences | Depends on the selected download |

Self-contained is the default for this public desktop tool to reduce installation prerequisites. Framework-dependent deployments are useful where runtime installation and patching are centrally managed; they generally use compatible installed runtime patches. Self-contained runtimes do not automatically update when the system .NET installation updates. Neither mode removes Windows system requirements.

## Switching modes

Every **Run workflow** form—**All Releases (Manual)**, **Force Build and Release (Manual)**, **Nightly Release**, **Stable Release**, and **Build Release Package**—accepts `deployment-mode`:

- `repository-default`: read the version-controlled defaults.
- `self-contained`: bundle the runtime for each configured RID.
- `framework-dependent`: require a preinstalled Desktop Runtime for each RID.
- `both`: produce both variants for every RID.

The manual parent passes the choice through both children to the shared build. When enabled, the portable package is always framework-dependent and is added only once. The separate **Daily Releases (Scheduled)** entry point runs daily at **00:00 Asia/Shanghai**; scheduled and tag-triggered runs use the repository defaults.

Edit the commented [`.github/release-settings.psd1`](../.github/release-settings.psd1) to change permanent defaults:

```powershell
@{
    DeploymentMode = 'self-contained' # framework-dependent or both also supported
    RuntimeIdentifiers = @('win-x86', 'win-x64', 'win-arm64')
    IncludePortable = $true
}
```

The default produces four ZIPs; `both` produces seven. Remove unwanted architectures or set `IncludePortable = $false` as needed. Unsupported RIDs and duplicate targets fail validation. Every target/mode uses a separate fresh publish directory and is built sequentially to avoid mixed output.

## Package naming and publication

Example default assets:

```text
RemoteHubStudio-v0.1.0-nightly-win-x86-self-contained.zip
RemoteHubStudio-v0.1.0-nightly-win-x64-self-contained.zip
RemoteHubStudio-v0.1.0-nightly-win-arm64-self-contained.zip
RemoteHubStudio-v0.1.0-nightly-win-portable-framework-dependent.zip
SHA256SUMS.txt
```

RID-specific framework-dependent assets end in `-framework-dependent.zip`. Each archive contains `PACKAGE-README.txt`; release notes explain the runtime requirements and launch command for every package. Titles remain version-only, and changing deployment modes does not create additional tags or date-based asset names.

Every stable and nightly ZIP, across all architectures and deployment modes, contains one top-level folder matching the archive name without `.zip`. Extracting into the current directory keeps all application files inside that folder. Open it to run the EXE or portable CMD launcher. For example:

```text
RemoteHubStudio-v0.1.0-nightly-win-x64-self-contained.zip
└── RemoteHubStudio-v0.1.0-nightly-win-x64-self-contained/
    ├── RemoteHubStudio.exe
    ├── RemoteHubStudio.dll
    ├── PACKAGE-README.txt
    ├── Languages/
    └── ...
```

Nightlies replace the package set under the same release, removing obsolete application ZIPs for that version, including legacy `win-x64.zip` assets and packages from a previous mode. Only ZIPs listed in and verified against `SHA256SUMS.txt` are uploaded. Failed builds or verification do not publish.

**Normal publications preserve existing stable assets.** To apply new targets or deployment modes to an existing version, run [Force Build and Release (Manual)](../.github/workflows/force-build.yml) on the default branch, select `nightly`, `stable`, or `both`, and choose the deployment mode. It rebuilds current source, runs tests, replaces the corresponding Release ZIPs and checksums, removes obsolete application ZIPs, and updates the build notes and source tag. The stable channel requires a plain `X.Y.Z` version; immutable Releases cannot be replaced. The updated assets are downloadable from **Releases**, with Actions artifacts also retained for 7 days. Use **Build Release Package** for build-only artifacts. When normal publication builds an existing source tag, the shared workflow uses the invoking workflow's packaging scripts/settings while application code remains at the selected source commit.

## Local packaging

Use Windows, PowerShell 7, and the .NET 10 SDK. From the repository root, run regression tests before packaging:

```powershell
dotnet run --project .\RemoteHubStudio.Tests\RemoteHubStudio.Tests.csproj -c Release
.\.github\scripts\package-release.ps1 -DeploymentMode both -ReleaseTag v0.1.0-nightly -Version 0.1.0-nightly.1.1 -Channel nightly -SourceCommit (git rev-parse HEAD) -OutputDirectory artifacts/local-packages
```

The script generates files only and never calls GitHub publication APIs. Use a fresh output directory for each local verification. Its SHA256 manifest identifies the packages from the current invocation. Specify deployment options explicitly when publishing through Visual Studio or the CLI to match Actions.

These are multi-file ZIPs. Single-file, trimming, and Native AOT are disabled to preserve compatibility with WinForms, reflection, AntdUI, and external language packs. MSI/MSIX/ClickOnce concern installation and update delivery rather than CPU RIDs or runtime deployment modes; installers, code signing, and an updater are outside this change.

## References

- [.NET publishing, portable DLLs, and apphosts](https://learn.microsoft.com/en-us/dotnet/core/deploying/)
- [Windows Forms overview](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/overview/)
- [.NET 10 supported systems and architectures](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)
- [Windows runtime installation and Desktop Runtime](https://learn.microsoft.com/en-us/dotnet/core/install/windows)
- [RID catalog](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog)
