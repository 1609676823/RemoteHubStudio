# .NET Windows CET 启动兼容通用解决方案

适用范围：使用 .NET 9、.NET 10 的 Windows 应用，包括常规 WinForms、WPF 和控制台项目。本文中的 `MyApp` 为示例名称，请替换为实际项目和程序名称。

## 1. 何时采用本方案

当应用启动失败，且错误信息明确涉及 CET，例如：

```text
Your Windows doesn't fully support CET.
```

应检查 Windows 的 CET 支持与应用宿主设置。CET（Control-flow Enforcement Technology，控制流强制技术）提供硬件栈保护；此类失败可能发生在应用进入 `Main` 之前。

先安装可用的 Windows 更新并重新测试。需要为暂时无法更新的机器提供兼容版本时，可采用下述项目级方案。不能仅凭“打不开”或退出代码 `0x80131506` 就断定是 CET 问题；还需结合实际报错。

## 2. 微软官方提供的处理方式

微软说明：从 .NET 9 开始，普通应用宿主 `apphost` 和单文件宿主 `singlefilehost` 默认标记为 CET 兼容。其“Recommended action”部分列出两种兼容处理方式：

| 官方方式 | 操作 | 适用场景 |
| --- | --- | --- |
| 修改应用项目 | 在 `.csproj` 中添加 `<CETCompat>false</CETCompat>` 并重新构建 | 能修改源码、需要分发兼容版本 |
| 配置指定程序的保护策略 | 通过 Windows 安全中心或组策略，为特定应用退出硬件栈保护 | 无法重新编译，或需要管理员统一处理已有程序 |

官方依据：[CET supported by default — Recommended action](https://learn.microsoft.com/en-us/dotnet/core/compatibility/interop/9.0/cet-support#recommended-action)。

**本文对有源码项目的实施建议是优先使用项目级配置。** 微软将上述两项列为兼容处理办法，并未要求所有 .NET 应用默认关闭 CET。

## 3. 具体修改内容

### 3.1 修改可执行应用的 `.csproj`

在公共 `PropertyGroup` 中加入以下属性；若已有同名属性，则修改原值，避免重复定义：

```xml
<PropertyGroup>
  <!-- Compatibility for Windows environments with incomplete CET support. -->
  <CETCompat>false</CETCompat>
</PropertyGroup>
```

| 修改前 | 修改后 |
| --- | --- |
| 未设置该属性，宿主按 SDK 默认行为生成 | 显式设置 `CETCompat=false`，生成不主动启用 CET 的宿主 EXE |

MSBuild 属性名不区分大小写，因此 `CetCompat` 与 `CETCompat` 等效；示例采用微软文档中的写法。属性规则见[微软：MSBuild 概述](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild)。

实施要求：

- 配置应放在最终生成 EXE 的应用项目中；只修改被引用的类库不够。
- 保留原有目标框架和业务代码；解决这类故障不要求降级 .NET。
- 若所有发布包均需兼容，应使用公共配置，并检查 `.pubxml`、共享属性文件及 CI 命令没有将其覆盖为 `true`。
- 若只提供独立兼容版，可在专用发布配置或命令行传入 `-p:CETCompat=false`，并明确区分包名。
- 重新生成宿主 EXE；只替换业务 DLL 或修改运行时 JSON 配置不能替代这一步。

本方案针对 SDK 生成的常规 `apphost` / `singlefilehost`。自定义原生宿主、Native AOT 等部署方式应单独核实其编译与保护设置。

### 3.2 安全影响与恢复方式

关闭该标志会使应用不再通过宿主默认启用 CET 硬件栈保护，属于兼容性与安全加固之间的取舍。项目配置不会修改 Windows 安全设置，也不能保证覆盖管理员强制实施的进程保护策略。

环境问题解决后，可移除该属性以恢复 SDK 默认行为，或按需要设为 `true`，重新构建、发布并验证。

## 4. 构建和发布办法

以下为 PowerShell 示例，假设已按第 3 节修改项目。多目标框架项目需在命令中额外指定实际目标，例如 `-f net10.0-windows`。构建机器应能正常运行对应版本的 .NET SDK。

### 4.1 清理并重新构建

```powershell
dotnet clean .\MyApp.csproj -c Release
dotnet build .\MyApp.csproj -c Release --no-incremental
```

以下发布模式按需要选择，并使用新的输出目录，避免混入旧版本 EXE。

### 4.2 自包含文件夹发布

```powershell
dotnet publish .\MyApp.csproj -c Release -r win-x64 --self-contained true -p:UseAppHost=true -p:PublishSingleFile=false -o .\artifacts\compat-win-x64-self-contained
```

自包含包附带所需 .NET 运行时，适合减少目标机器上的运行库安装步骤。CET 兼容仍由项目属性控制；仅设置自包含并不能替代 CET 修改。[微软：应用发布概述](https://learn.microsoft.com/en-us/dotnet/core/deploying/)。

### 4.3 依赖已安装运行库的发布

```powershell
dotnet publish .\MyApp.csproj -c Release -r win-x64 --self-contained false -p:UseAppHost=true -p:PublishSingleFile=false -o .\artifacts\compat-win-x64-framework-dependent
```

目标机器仍需安装与应用框架和进程架构匹配的运行时。WinForms / WPF 应用需要 Windows Desktop Runtime；普通 .NET Runtime 不能替代桌面运行时。[微软：在 Windows 上安装 .NET](https://learn.microsoft.com/en-us/dotnet/core/install/windows)。

### 4.4 自包含单文件发布

```powershell
dotnet publish .\MyApp.csproj -c Release -r win-x64 --self-contained true -p:UseAppHost=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\compat-win-x64-single-file
```

单文件宿主也应继承相同的 CET 配置。示例会将原生库打包供启动时解压；项目设置为外置的配置、资源和其他必要文件仍须一起分发。[微软：单文件部署](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)。

以上示例为 x64。应用及依赖支持其他架构时，可分别使用 `win-x86`、`win-arm64` 发布，并为各架构设置不同输出目录。

## 5. 目标机器上的启动办法

1. 完整解压新发布包，保留所有必要的依赖、配置和资源文件。
2. 直接运行新生成的 `MyApp.exe`，确认快捷方式指向新文件。
3. 如果使用依赖运行库的包，先安装匹配的运行时；自包含包仍需满足操作系统和原生依赖要求。
4. 在原故障机器上验证能正常进入应用，并执行必要的功能回归。

`dotnet MyApp.dll` 使用的是系统 `dotnet.exe` 宿主。若 CMD、BAT、服务或其他启动器实际执行该命令，其宿主 CET 行为不会被应用项目的属性修改。应检查最终启动入口，不能以 DLL 启动结果代替新 EXE 的兼容性验收。两种启动入口的区别见[微软：启动依赖框架的应用](https://learn.microsoft.com/en-us/dotnet/core/deploying/#launch-framework-dependent-apps)。

## 6. 无源码或构建工具自身失败时的处理

### 6.1 对指定程序配置保护策略

微软也允许为特定应用配置硬件栈保护退出策略。通过 Windows 安全中心操作时：

1. 进入“应用和浏览器控制 → 漏洞防护 → 漏洞防护设置 → 程序设置”。
2. 添加目标 EXE，优先按确切文件路径选择。
3. 在系统提供对应选项时，为该程序覆盖并关闭硬件强制堆栈保护，应用设置后重新启动程序验证。

也可由管理员使用组策略部署指定程序的策略。界面及可用选项随 Windows 版本变化；此方式属于机器上的保护配置，与修改项目重新发布是两条不同路径。

官方操作说明：[Enable exploit protection in Windows](https://learn.microsoft.com/en-us/defender-endpoint/enable-exploit-protection)。

### 6.2 Visual Studio、Roslyn、ServiceHub 或 SDK 自身崩溃

应用项目中的属性只影响该项目生成的宿主 EXE，不会修复这些独立工具进程。

应更新 Windows 和故障工具链；若构建工具仍无法运行，可先在正常构建机器或 CI 上发布兼容包，再到目标机器验证。若选择进程级保护配置，需针对实际故障工具的可执行文件单独处理。

## 7. 验证要求与适用边界

- 检查实际分发的 EXE，而不仅是 `.csproj`：可使用 PE 检查工具确认宿主未声明启用 CET，并对普通宿主、单文件宿主及实际分发架构分别核验。
- 检查打包后的 EXE 与已验证产物一致，避免分发旧文件。
- 在原故障机器上直接运行新 EXE；本机构建成功或静态标志检查不能替代实机验证。
- 执行项目原有测试与关键功能回归；记录系统版本、程序版本、架构、启动入口和测试结果。
- 该方案处理 CET 相关兼容问题，不保证所有旧版 Windows、缺失系统组件、错误架构、受限解压目录或其他启动故障都能解决。
- 操作系统支持范围应以对应 .NET 版本的官方要求为准：[Windows 上的 .NET 安装与支持要求](https://learn.microsoft.com/en-us/dotnet/core/install/windows)。
