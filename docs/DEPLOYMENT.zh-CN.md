# 发布目标与部署模式

[English](DEPLOYMENT.md) | **简体中文**

RemoteHubStudio 使用 .NET 10、Windows Forms、AntdUI 和 Windows API。默认发布 **Windows x86、x64、ARM64 独立部署 ZIP，以及一个 Windows 可移植 DLL ZIP**。ZIP 都可以完整解压到可写目录使用；程序的 `data` 目录存放用户数据，更新程序时应保留。外部远程客户端仍需单独安装。

## 截图中的“目标运行时”如何选择

Visual Studio 的下拉列表是通用 RID 列表，不保证当前项目能在每个目标上运行。

| 选项 | 本项目 | 含义与启动方式 |
| --- | --- | --- |
| `win-x86` | 支持打包，默认启用 | 32 位 Windows 进程；运行 `RemoteHubStudio.exe`。也可在支持 x86 模拟的 64 位 Windows 上使用 |
| `win-x64` | 支持打包，默认启用 | Intel/AMD 64 位 Windows；普通 PC 优先选择此包 |
| `win-arm64` | 支持打包，默认启用 | ARM64 Windows；需要在 ARM64 设备上验证实际运行体验 |
| 可移植 / 不指定 RID | 支持，默认启用 | 不附带固定架构的 EXE 或运行时，发布 AnyCPU DLL；用 `dotnet RemoteHubStudio.dll` 或包中的 `Start-RemoteHubStudio.cmd` 启动 |
| `win-arm` | 不提供 | Windows ARM32，与 ARM64 不同；当前 .NET 10 Windows 桌面运行时不提供此目标 |
| `linux-x64` / `linux-arm` / `linux-arm64` | 不支持当前应用 | WinForms、DPAPI、窗口激活等依赖 Windows；换 RID 不能实现跨系统 |
| `osx-x64` / `osx-arm64` | 不支持当前应用 | 同上；若要支持 macOS/Linux，需要迁移 UI 和相关系统功能 |

**“可移植”与“免安装”是两回事：**上述所有 ZIP 都是免安装的文件夹分发。Visual Studio 的“可移植”表示没有固定 RID，本项目仍然只能运行在 Windows 上。它始终依赖框架，不能变成一个同时包含 x86、x64 和 ARM64 运行时的独立部署包。

可移植包由 `PATH` 中选中的 `dotnet` 决定进程架构；该架构必须安装 .NET 10 **Windows Desktop Runtime**。普通 `.NET Runtime` 或 ASP.NET Core Runtime 不包含 WinForms。需要明确选架构时，使用相应架构的 `dotnet.exe` 完整路径运行 DLL。独立部署的用户通常直接选择对应 CPU 的 EXE 包更方便。

## Windows 版本边界

RID 区分的是系统和 CPU，不需要为 Windows 10、11 各生成一套相同的包。应使用微软 .NET 10 支持列表中的 Windows 版本：Windows 11 的受支持版本，以及支持列表内的 Windows 10 LTSC/企业版本；不能笼统承诺所有 Windows 10 版本都仍受支持。

项目的 `net10.0-windows7.0` 中，`windows7.0` 是编译时 Windows API 版本标记，**不是 Windows 7 运行兼容承诺**。Windows 7/8/8.1 不属于当前 .NET 10 支持范围；附带运行时也无法突破该限制。Windows Server 需同时满足 .NET 支持要求和桌面 GUI 环境要求；通用 .NET 对 Server Core/Nano Server 的支持不等于本 WinForms 应用支持这些无桌面环境。

这些包的构建可在 x64 Windows 上完成。当前回归套件在 x64 构建机执行；ARM64 包的生成和内容检查不等于已经在 ARM64 实机完成 UI 验证。操作系统生命周期可能变化，以微软实时列表为准。

## 默认推荐独立部署，参数保留选择权

| 模式 | 附带 .NET | 包体积 | 适用场景 | 运行时更新方式 |
| --- | --- | --- | --- | --- |
| `self-contained`（默认） | 是 | 较大 | 面向普通开源用户、离线分发、减少首次启动的安装步骤 | 维护者更新 SDK/运行时并重新发布程序包 |
| `framework-dependent` | 否 | 较小 | 企业统一维护运行时、开发环境、频繁下载更新 | 管理员或用户更新已安装的对应架构 Desktop Runtime |
| `both` | 同时提供上述两类包 | 下载资产更多 | 同时服务普通用户与受管理环境 | 用户根据下载包选择 |

这是针对当前桌面工具的默认取舍，不代表独立部署在所有项目中都更好。独立部署仍依赖受支持的 Windows，不能理解为没有任何操作系统依赖。依赖框架通常使用系统已安装的兼容补丁版本；独立部署中的运行时不会仅因系统 .NET 更新而自动更新。

## 在 Actions 中切换

**All Releases、Nightly Release、Stable Release、Build Release Package** 的 **Run workflow** 都有 `deployment-mode` 参数：

- `repository-default`：读取仓库配置，适合保持与每天自动构建一致。
- `self-contained`：本次三个 RID 使用独立部署。
- `framework-dependent`：本次三个 RID 使用依赖框架部署。
- `both`：本次三个 RID 各生成两种部署包。

总任务把参数传给两个发布子任务，再传给共用打包流程。`IncludePortable` 开启时，每种选择都额外生成一个可移植依赖框架包；不会生成重复的可移植包。定时和标签触发没有手动参数，自动使用仓库配置。

永久修改默认值，请编辑带注释的 [`.github/release-settings.psd1`](../.github/release-settings.psd1)，提交并推送：

```powershell
@{
    DeploymentMode = 'self-contained' # 可改为 framework-dependent 或 both
    RuntimeIdentifiers = @('win-x86', 'win-x64', 'win-arm64')
    IncludePortable = $true           # 不需要可移植包时改为 $false
}
```

默认产生 4 个 ZIP；`both` 产生 7 个 ZIP。可从 `RuntimeIdentifiers` 删除不需要的架构；添加 Linux/macOS/win-arm 会被脚本拒绝。共用流程按顺序发布到独立目录，避免不同架构和模式的 DLL 混用。

## 文件名称、更新与已有版本

以 `v0.1.0-nightly` 为例：

```text
RemoteHubStudio-v0.1.0-nightly-win-x86-self-contained.zip
RemoteHubStudio-v0.1.0-nightly-win-x64-self-contained.zip
RemoteHubStudio-v0.1.0-nightly-win-arm64-self-contained.zip
RemoteHubStudio-v0.1.0-nightly-win-portable-framework-dependent.zip
SHA256SUMS.txt
```

依赖框架 RID 包以 `-framework-dependent.zip` 结尾。每个 ZIP 内都有 `PACKAGE-README.txt`，Release 说明列出各包的启动方式和运行时要求。标题仍然只使用版本标签；部署模式不会创建新的标签，也不会在文件名中加入日期。

所有架构、部署模式的正式版和预览版 ZIP 都包含一个与压缩包同名（去掉 `.zip`）的顶级文件夹。直接解压到当前目录时，程序文件会集中在该文件夹内。解压后进入文件夹，再启动 EXE 或可移植包的 CMD。例如：

```text
RemoteHubStudio-v0.1.0-nightly-win-x64-self-contained.zip
└── RemoteHubStudio-v0.1.0-nightly-win-x64-self-contained/
    ├── RemoteHubStudio.exe
    ├── RemoteHubStudio.dll
    ├── PACKAGE-README.txt
    ├── Languages/
    └── ...
```

夜间版会在同一 Release 中上传本次清单的全部包，并删除该版本过时的程序 ZIP（包括旧版单一 `win-x64.zip` 或切换模式前的包），避免下载到陈旧产物。只上传 `SHA256SUMS.txt` 中列出并校验通过的 ZIP；构建或校验失败不会发布。

**已经公开的正式版保留原附件。**改变架构或部署模式不会覆盖、补发已有正式版；需要新的正式版本号才能生成新的正式资产。想马上试用新包，可运行 Nightly Release，或运行 Build Release Package 并从 Actions Artifacts 下载。构建现有标签时，共用流程使用调用工作流对应版本的打包脚本及配置，应用代码仍来自选定的源码提交。

## 本地复现

在 Windows、PowerShell 7 和 .NET 10 SDK 环境，从仓库根目录先执行回归套件，再打包：

```powershell
dotnet run --project .\RemoteHubStudio.Tests\RemoteHubStudio.Tests.csproj -c Release
.\.github\scripts\package-release.ps1 -DeploymentMode both -ReleaseTag v0.1.0-nightly -Version 0.1.0-nightly.1.1 -Channel nightly -SourceCommit (git rev-parse HEAD) -OutputDirectory artifacts/local-packages
```

本地打包脚本只生成文件，不调用 GitHub 发布 API；建议每次验证使用新的输出目录。它会使用独立的发布子目录，并在成功后生成本次的 SHA256 清单。Windows 原生打包工具也可用，但默认 `dotnet publish` 的选项不一定与 Actions 相同，请明确指定部署模式。

当前采用多文件 ZIP；没有启用 single-file、裁剪或 Native AOT，以保持 WinForms、反射、AntdUI 和外部语言包兼容。MSI/MSIX/ClickOnce 属于另外的安装与更新方案，不是 RID 或部署模式；本次没有引入安装器、代码签名或自动升级服务。

## 官方依据

- [.NET 发布模式、Portable DLL 与 AppHost](https://learn.microsoft.com/en-us/dotnet/core/deploying/)
- [Windows Forms 概述](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/overview/)
- [.NET 10 支持的系统和架构](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)
- [Windows 运行时安装与 Desktop Runtime 区别](https://learn.microsoft.com/en-us/dotnet/core/install/windows)
- [RID 目录](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog)
