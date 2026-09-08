param(
    [ValidateSet('repository-default', 'self-contained', 'framework-dependent', 'both')]
    [string]$DeploymentMode = 'repository-default',
    [string]$SourceRoot = (Get-Location).Path,
    [string]$OutputDirectory = 'artifacts/packages',
    [string]$SettingsPath = (Join-Path $PSScriptRoot '../release-settings.psd1'),
    [Parameter(Mandatory)][string]$ReleaseTag,
    [Parameter(Mandatory)][string]$Version,
    [ValidateSet('nightly', 'stable')][string]$Channel = 'nightly',
    [string]$SourceCommit = $env:RELEASE_COMMIT
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$settings = Import-PowerShellDataFile -LiteralPath $SettingsPath
if ($DeploymentMode -eq 'repository-default') { $DeploymentMode = $settings.DeploymentMode }
if ($DeploymentMode -notin @('self-contained', 'framework-dependent', 'both')) {
    throw 'Invalid DeploymentMode in release-settings.psd1.'
}
$runtimes = @($settings.RuntimeIdentifiers)
if ($runtimes.Count -ne @($runtimes | Select-Object -Unique).Count) { throw 'Duplicate runtime identifiers.' }
foreach ($runtime in $runtimes) {
    if ($runtime -notin @('win-x86', 'win-x64', 'win-arm64')) { throw "Unsupported WinForms runtime: $runtime" }
}
if ($settings.IncludePortable -isnot [bool]) { throw 'IncludePortable must be a Boolean.' }
if (!$runtimes.Count -and !$settings.IncludePortable) { throw 'At least one package must be enabled.' }
if ($ReleaseTag -notmatch '^v[0-9]+\.[0-9]+\.[0-9]+(-nightly)?$') { throw 'Invalid release tag.' }

$sourcePath = (Resolve-Path -LiteralPath $SourceRoot).Path
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$scratchRoot = Join-Path $outputPath ('publish-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratchRoot | Out-Null
$modes = if ($DeploymentMode -eq 'both') { @('self-contained', 'framework-dependent') } else { @($DeploymentMode) }
$packages = @(
    foreach ($runtime in $runtimes) {
        foreach ($mode in $modes) { @{ Runtime = $runtime; Mode = $mode } }
    }
    if ($settings.IncludePortable) { @{ Runtime = 'win-portable'; Mode = 'framework-dependent' } }
)
$checksumLines = @()
$tableRows = @()

foreach ($package in $packages) {
    $runtime = $package.Runtime
    $mode = $package.Mode
    $portable = $runtime -eq 'win-portable'
    $selfContained = $mode -eq 'self-contained'
    $packageDirectoryName = "RemoteHubStudio-$ReleaseTag-$runtime-$mode"
    $publishDirectory = Join-Path $scratchRoot $packageDirectoryName
    # Each combination has a fresh output directory; never mix runtimes or deployment modes.
    # RID-specific EXEs use the matching architecture; portable DLLs stay AnyCPU.
    # Like Visual Studio Portable publishing, keep the SDK's native EXE alongside the portable DLL.
    $arguments = @(
        'publish', (Join-Path $sourcePath 'RemoteHubStudio/RemoteHubStudio.csproj'),
        '-c', 'Release', '--self-contained', $selfContained.ToString().ToLowerInvariant(),
        '-o', $publishDirectory, "-p:Version=$Version", '-p:ContinuousIntegrationBuild=true',
        '-p:PublishTrimmed=false', '-p:PublishSingleFile=false', '-p:PublishReadyToRun=false',
        '-p:DebugType=None', '-p:DebugSymbols=false'
    )
    if ($portable) {
        $arguments += @('-p:RuntimeIdentifier=', '-p:UseAppHost=true', '-p:PlatformTarget=AnyCPU')
    } else {
        $arguments += @('-r', $runtime, '-p:UseAppHost=true')
    }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Publishing $runtime / $mode failed: $LASTEXITCODE" }

    $requiredFiles = @(
        'RemoteHubStudio.exe', 'RemoteHubStudio.dll', 'RemoteHubStudio.deps.json', 'RemoteHubStudio.runtimeconfig.json',
        'AntdUI.dll', 'Languages/en.json', 'Languages/zh-Hans.json', 'LICENSE.txt', 'THIRD-PARTY-NOTICES.txt'
    )
    if ($selfContained) { $requiredFiles += @('coreclr.dll', 'hostfxr.dll', 'System.Windows.Forms.dll') }
    foreach ($file in $requiredFiles) {
        if (!(Test-Path -LiteralPath (Join-Path $publishDirectory $file) -PathType Leaf)) { throw "Missing $runtime/$mode file: $file" }
    }
    if (!$selfContained -and (Test-Path -LiteralPath (Join-Path $publishDirectory 'coreclr.dll'))) {
        throw 'A framework-dependent package unexpectedly contains the runtime.'
    }
    # Read the actual EXE architecture: a portable AnyCPU DLL does not make its native apphost AnyCPU.
    $exeBytes = [System.IO.File]::ReadAllBytes((Join-Path $publishDirectory 'RemoteHubStudio.exe'))
    $peOffset = [BitConverter]::ToInt32($exeBytes, 0x3c)
    $exeArchitecture = switch ([BitConverter]::ToUInt16($exeBytes, $peOffset + 4)) {
        0x014c { 'x86' }
        0x8664 { 'x64' }
        0xaa64 { 'arm64' }
        default { throw 'Unsupported Windows EXE architecture.' }
    }
    if (!$portable -and "win-$exeArchitecture" -ne $runtime) {
        throw "EXE architecture $exeArchitecture does not match $runtime."
    }

    $launch = "RemoteHubStudio.exe ($exeArchitecture)"
    if ($portable) { $launch += '; or Start-RemoteHubStudio.cmd / dotnet RemoteHubStudio.dll' }
    $requirement = if ($selfContained) { 'Included / 已附带 .NET 10 Windows Desktop Runtime' } else { "Install / 需安装 .NET 10 Windows Desktop Runtime ($exeArchitecture) for EXE" }
    $portableNote = ''
    if ($portable) {
        $portableNote = @"
Portable DLL: AnyCPU; the EXE is $exeArchitecture, matching the publishing SDK's default apphost.
可移植 DLL 为 AnyCPU；EXE 为 $exeArchitecture，与发布 SDK 的默认启动器一致。
To use another CPU architecture, run the DLL with that architecture's dotnet and .NET 10 Windows Desktop Runtime.
其他 CPU 架构可使用对应架构的 dotnet 和 .NET 10 Windows Desktop Runtime 运行 DLL。
Start-RemoteHubStudio.cmd uses dotnet on PATH; it does not use the bundled EXE.
Start-RemoteHubStudio.cmd 使用 PATH 中的 dotnet，不调用包内 EXE。
"@
        # A convenience launcher, not an apphost or a bundled runtime. Use the desired dotnet on PATH.
        $launcher = @'
@echo off
dotnet "%~dp0RemoteHubStudio.dll"
if errorlevel 1 pause
'@
        [System.IO.File]::WriteAllText((Join-Path $publishDirectory 'Start-RemoteHubStudio.cmd'), ($launcher -replace '\r?\n', "`r`n") + "`r`n", [System.Text.Encoding]::ASCII)
    }
    @"
$ReleaseTag — $runtime — $mode

Windows only / 仅支持 Windows。Extract every file / 请完整解压全部文件。
Open the extracted $packageDirectoryName folder to start the application.
解压后进入 $packageDirectoryName 文件夹启动程序。
Start / 启动: $launch
Runtime / 运行时: $requirement
EXE architecture / EXE 架构: $exeArchitecture
Version / 程序版本: $Version
Source commit / 源码提交: $SourceCommit

$portableNote

Portable DLLs do not make WinForms cross-platform. Linux, macOS and Windows ARM32 are not supported.
可移植 DLL 仍依赖 Windows；Linux、macOS、Windows ARM32 不受支持。
Use a Windows version supported by .NET 10. The windows7.0 TFM suffix does not promise Windows 7 support.
请使用 .NET 10 支持的 Windows 版本，目标框架中的 windows7.0 不代表支持 Windows 7。
All ZIPs are folder deployments. External remote clients must be installed separately.
所有 ZIP 都是文件夹部署；远程客户端需单独安装。data 目录保存用户数据，升级时请保留。
"@ | Set-Content -LiteralPath (Join-Path $publishDirectory 'PACKAGE-README.txt') -Encoding utf8NoBOM

    $archiveName = "$packageDirectoryName.zip"
    $archivePath = Join-Path $outputPath $archiveName
    # Archive the directory itself, not its contents: extraction creates one folder named after the ZIP.
    Compress-Archive -LiteralPath $publishDirectory -DestinationPath $archivePath -CompressionLevel Optimal -Force
    $hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $checksumLines += "$hash  $archiveName"
    $tableRows += "| $archiveName | $requirement | $launch |"
}

# This file is the authoritative asset list; publish-release.sh uploads only the verified entries.
# Explicit LF keeps sha256sum on the Linux publishing runner compatible with Windows packaging.
Set-Content -LiteralPath (Join-Path $outputPath 'SHA256SUMS.txt') -Value (($checksumLines -join "`n") + "`n") -Encoding utf8NoBOM -NoNewline
$builtAt = [DateTimeOffset]::UtcNow.ToString('u')
$buildLink = if ($env:GITHUB_RUN_ID) { "https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID" } else { 'Local build / 本地构建' }
@"
$ReleaseTag ($Channel)

- Version / 程序版本: $Version
- Built at / 构建时间: $builtAt
- Source commit / 源码提交: $SourceCommit
- Build / 构建记录: $buildLink
- Deployment mode / 部署模式: $DeploymentMode

| Package / 下载包 | Runtime / 运行时要求 | Start / 启动方式 |
| --- | --- | --- |
$($tableRows -join "`n")

Extract the complete ZIP. Self-contained packages include .NET; framework-dependent and portable packages require .NET 10 Windows Desktop Runtime.
请完整解压：self-contained 包附带运行时；framework-dependent 和可移植包需要安装 .NET 10 Windows Desktop Runtime。
Each ZIP contains one folder named after the archive (without .zip). Open that folder to start the application.
每个 ZIP 内只有一个与压缩包同名（不含 .zip）的顶级文件夹；解压后进入该文件夹启动程序。
Windows only. Portable means no fixed CPU RID, not Linux/macOS support. External remote clients must be installed separately.
仅支持 Windows；可移植表示不固定 CPU RID，不代表支持 Linux/macOS。远程客户端需单独安装。
The portable package includes an AnyCPU DLL and a native EXE; the EXE architecture is listed above. CMD/dotnet launch requires the Desktop Runtime matching the selected dotnet architecture.
可移植包同时包含 AnyCPU DLL 和原生 EXE，EXE 架构见上表；使用 CMD/dotnet 启动时需安装与所选 dotnet 架构匹配的 Desktop Runtime。
SHA256SUMS.txt verifies all packages. Normal runs preserve published stable assets; manual force publication replaces assets, build notes, and the source tag.
SHA256SUMS.txt 可校验全部安装包；普通任务保留已发布的正式版，手动强制发布会更新附件、构建说明及源码标签。
"@ | Set-Content -LiteralPath (Join-Path $outputPath 'release-notes.md') -Encoding utf8NoBOM
Write-Output "PACKAGES_OK: $($packages.Count) packages in $outputPath"
