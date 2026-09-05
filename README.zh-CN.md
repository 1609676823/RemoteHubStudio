# RemoteHubStudio

[English](README.md) | **简体中文**

RemoteHubStudio 是一款面向 Windows 的远程连接配置管理器。它在一个便携工作区中统一管理连接与嵌套分组，并启动计算机上已经安装的远程客户端。

> RemoteHubStudio 不附带任何第三方远程客户端程序，也不授予这些客户端的许可证。请单独安装所需客户端并遵守各自的许可条款。

## 界面截图

### 连接工作区

![RemoteHubStudio 主连接工作区](docs/screenshots/main-window-en.png)

### 连接编辑器

![RemoteHubStudio 连接编辑器](docs/screenshots/connection-editor-en.png)

### 设置

![RemoteHubStudio 设置](docs/screenshots/settings-en.png)

全部截图均使用英文界面。应用同时内置简体中文，并支持添加更多 JSON 语言包。

## 快速开始

可从 [Releases](https://github.com/1609676823/RemoteHubStudio/releases) 下载正式版或每日预览版；带 `nightly` 的版本为预览版。普通用户选择对应 CPU 的 `win-x86`、`win-x64` 或 `win-arm64` **self-contained** ZIP，完整解压后运行 `RemoteHubStudio.exe`，无需另装 .NET。**framework-dependent** 包和 **win-portable** 可移植包需要 .NET 10 Windows Desktop Runtime；可移植包通过 `Start-RemoteHubStudio.cmd` 或 `dotnet RemoteHubStudio.dll` 启动。详见 [发布目标与部署模式](docs/DEPLOYMENT.zh-CN.md)。

从源码编译需要 Windows 和 .NET 10 SDK。NuGet 还原会下载 AntdUI。

```powershell
dotnet restore .\RemoteHubStudio.slnx
dotnet build .\RemoteHubStudio.slnx -c Release
dotnet run --project .\RemoteHubStudio\RemoteHubStudio.csproj -c Release
```

首次启动后，打开“**设置 → 外部客户端**”，为计划使用的客户端选择可执行文件。Microsoft Remote Desktop 使用 Windows 自带的 `mstsc.exe`；其他预定义客户端均需单独安装。

## 功能特性

- 在一个工作区中管理连接与嵌套分组。每条连接可保存适用的用户名和密码、收藏状态、到期日、备注、私钥、程序覆盖路径、自定义参数、RDP 设置和客户端专属选项。
- 可按名称、地址、类型或备注搜索；按客户端、分组、收藏或到期状态筛选；并可对多行执行状态检测或删除。
- 可新增已保存连接，也可使用“**快速连接**”而不把配置加入工作区。
- 连接编辑器采用公共窗体外壳，并为 12 种连接类型分别提供专属选项页。协议、目标、端口、认证和高级字段会随所选客户端及模式变化。
- 只有所选客户端模式实际使用认证信息时才显示用户名和密码字段。Radmin、RealVNC 以及 PuTTY、SecureCRT、MobaXterm 的 Telnet 模式会隐藏并清理不适用的认证值。
- 可配置 RDP 显示、压缩、Windows 组合键、音频、凭据提示、自动重连，以及剪贴板、驱动器、打印机、智能卡、COM、POS、摄像头和麦克风重定向。
- 可将全部数据或当前筛选视图导出为便携 JSON（`.rhs.json`），并可导入 JSON 或 CSV 数据。
- 使用无 Shell 的独立参数列表启动客户端。自定义模板会先分词，再替换 `{host}`、`{ip}`、`{port}`、`{username}`、`{key}` 和可选的 `{password}`。
- 保存时使用同卷临时文件、磁盘持久刷新与原子替换，并保留上一版本用于自动恢复。
- 可跟随 Windows 主题，或选择浅色/深色主题；支持 Per-Monitor V2 高 DPI 与响应式对话框。
- 标题栏提供独立的“最小化到托盘”按钮和普通最小化按钮。托盘右键可打开或退出，左键双击可恢复窗口；恢复时保留原来的最大化状态。设置中的“关闭窗口时收起到托盘”单独控制关闭按钮的行为。
- 重复启动时唤回已运行的窗口（包括托盘中的窗口和打开的对话框），避免同时运行多个实例。程序、窗口与托盘使用统一的原创图标，矢量源文件和多尺寸导出位于 `RemoteHubStudio/Assets`。

## 支持的客户端

| 客户端 | 协议或模式 | 程序来源 |
| --- | --- | --- |
| Microsoft Remote Desktop | RDP | Windows 自带 `mstsc.exe` |
| PuTTY | SSH、Telnet | 外部安装 |
| Xshell | SSH、Telnet、SFTP | 外部安装 |
| Xftp | SFTP、FTP | 外部安装 |
| WinSCP | SFTP、SCP、FTP、FTPS、WebDAV、WebDAVS | 外部安装 |
| SecureCRT | SSH2、SSH1、Telnet | 外部安装 |
| MobaXterm | SSH、Telnet | 外部安装 |
| VNC Viewer | TightVNC、RealVNC、UltraVNC | 外部安装 |
| Radmin Viewer | 控制、查看、Telnet、文件、关机、聊天、语音、消息 | 外部安装 |
| ToDesk | 设备连接 | 外部安装 |
| RustDesk | 远程控制、文件传输、查看摄像头、端口转发、RDP 隧道、终端 | 外部安装 |
| Custom | 用户定义的程序和参数模板 | 用户指定 |

### 程序解析顺序

请在“**设置 → 外部客户端**”中配置外部客户端。TightVNC、RealVNC 和 UltraVNC 使用独立路径，避免把某个实现的专属参数发送给错误的查看器。单条连接也可设置“**程序覆盖**”。

RemoteHubStudio 按以下顺序解析可执行文件：

1. 连接级程序覆盖路径。
2. 对应客户端或协议实现的全局设置。
3. 应用程序目录或显式 `PATH` 项中的常用程序名。

UNC 远程程序以及 `.cmd`、`.bat` 脚本会被拒绝。

### RustDesk 说明

RustDesk 连接支持可选的自建服务器、服务器公钥和强制中继。远程控制使用官方确认的 `--connect <id> --password <password>` 形式；文件传输、查看摄像头、端口转发、RDP 隧道和终端是当前官方客户端源码支持的出站模式。`--get-id`、`--set-id`、`--config` 和安装等管理命令不会作为单条连接选项暴露。

当目标包含 `?key=` 时，RemoteHubStudio 会省略自动密码传递，让 RustDesk 自行提示输入，以规避当前上游的参数组合问题。参见[官方客户端文档](https://rustdesk.com/docs/en/client/#command-line-parameters)、[维护者 CLI 示例](https://github.com/rustdesk/rustdesk/discussions/3980)、[RustDesk 1.4.9 命令分派源码](https://github.com/rustdesk/rustdesk/blob/1.4.9/src/core_main.rs#L3706-L3743)及[上游问题](https://github.com/rustdesk/rustdesk/issues/14116)。

## 界面与架构

- Windows Forms，目标为 `.NET 10`（`net10.0-windows7.0`）。RemoteHubStudio 仅支持 Windows；RDP 启动与可选的 DPAPI 保护依赖 Windows。
- 使用 AntdUI `2.4.8`，支持跟随系统、浅色与深色主题。
- 使用 Per-Monitor V2 高 DPI 模式；对话框和字段网格会根据可用宽度调整列数与滚动区域。
- 公共连接窗体仅包含名称、客户端、分组、到期日、收藏状态和备注。每个客户端分别由一个 `ConnectionTypeOptionsPage` 子类管理其协议或模式、连接目标、认证和专属设置。RDP、ToDesk 等固定单协议客户端不会重复显示协议选择器。

源代码按职责组织：

| 路径 | 职责 |
| --- | --- |
| `RemoteHubStudio/Domain` | 工作区、连接、分组、设置与 RDP 模型 |
| `RemoteHubStudio/Application` | 工作区操作、验证、限制、筛选辅助逻辑与到期规则 |
| `RemoteHubStudio/Infrastructure` | 持久化、DPAPI 保护、导入导出、客户端启动计划、状态监测与单实例协调 |
| `RemoteHubStudio/UI` | 主窗口、对话框、客户端专属编辑器、响应式控件与主题 |
| `RemoteHubStudio/Localization` 与 `RemoteHubStudio/Languages` | 运行时本地化与版本化语言包 |
| `RemoteHubStudio.Tests` | 无额外测试框架依赖的控制台回归套件 |

`Program.cs` 会初始化本地化和单实例协调，通过支持 DPAPI 的仓储加载 JSON 工作区，应用所选主题，组装各应用服务，然后打开主窗口。

## 多语言

应用内置英文和简体中文，并可通过带版本的 UTF-8 JSON 语言包添加其他区域语言。在“**设置**”中选择“**跟随系统语言**”或指定语言；保存发生变化的语言后，应用会重启，使全部窗体、菜单和组件统一切换。

运行时按以下顺序加载语言包，后加载的层会覆盖先前层：

1. 内嵌语言包；内嵌英文是安全的键与占位符基线。
2. `RemoteHubStudio.exe` 旁边的 `Languages`。
3. `RemoteHubStudio.exe` 旁边的 `data\Languages`。

贡献新区域语言时，请复制 [`RemoteHubStudio/Languages/en.json`](RemoteHubStudio/Languages/en.json)，将文件名和其中的 `locale` 改为规范 BCP-47 标识，只翻译 `strings` 下的值，并保留全部键和格式占位符。语言包格式验证、回退规则和安全上限见[语言包指南](RemoteHubStudio/Languages/README.zh-CN.md)；设计器预览行为与重载步骤见[WinForms 设计器语言说明](docs/WINFORMS-DESIGNER-LANGUAGE.zh-CN.md)。

## 安全模型

### 本地数据加密

加密**默认关闭**。此时 `workspace.json` 是可读 JSON，保存的密码可能以明文出现。请限制数据目录的访问权限。

可在“**设置 → 安全**”中启用加密。启用后，完整工作区载荷由 Windows DPAPI `CurrentUser` 保护。该保护绑定当前 Windows 用户的 DPAPI 密钥环境，不是跨账户或跨设备的便携格式，也没有单独的恢复密码；如果该 Windows 用户配置或其密钥不可用，数据可能无法恢复。

首次启用加密并保存时，应用会先用相同 DPAPI 策略原子替换 `workspace.json.bak`，再提交主文件——即使主文件恰好缺失也一样——因此不会保留旧的明文备份。该过渡保存中的备份与新主文件一致；之后成功保存时会继续保留上一加密版本。

名称精确匹配的旧版 `workspace.corrupt.*.json` 诊断文件也会在提交加密设置前原子迁移为受保护信封；迁移失败会阻止设置提交。一次迁移最多处理 32 个匹配文件和 256 MiB 聚合物理数据；超过任一限制都会失败，且不会部分提交加密设置。新版受保护诊断信封使用一条单独受保护的小型校验记录绑定原始长度和载荷哈希，因此普通加载无需再次解密完整损坏载荷。

从备份恢复并保留损坏主文件原始字节时，即使正常工作区加密仍关闭，应用也始终使用相同的 `CurrentUser` 保护器封装它们。因此诊断文件不会额外生成一份明文秘密副本，并且只能在同一 Windows 用户密钥环境中解保护。

### 自动传递密码

“**允许自动传递密码**”**默认开启**，保存的密码会自动传给支持的客户端。可在设置中关闭；关闭后，启动计划不会向客户端传递密码，请在客户端中输入密码或使用客户端自身的认证机制。关闭该设置时，包含 `{password}` 的自定义模板会被拒绝。

启用自动传递时，支持的客户端可能会在进程参数、会话 URI、进程监控工具、日志或审计记录中暴露密码。RDP 配置存在保存密码时，RemoteHubStudio 会写入由 DPAPI `CurrentUser` 保护的 `password 51` 字段，并在客户端退出后尽力删除唯一的临时 `.rdp` 文件。这不应被视为便携式秘密存储。

### 数据与秘密的导入导出

- “**导入和导出**”提供“**导出全部数据**”“**导出当前数据**”和“**导入数据**”。当前数据是应用当前搜索、类型、侧栏、收藏和到期筛选后可见的连接；导出会自动包含这些连接所属的分组及完整父分组链。
- JSON 导出默认移除连接中保存的密码。将该脱敏文件导回同名连接时，密码会随完整配置更新而清空；导入确认框会明确提示此行为。
- “**导出时包含保存的密码**”默认关闭。启用后，便携文件中的密码字段是明文，不受本地 DPAPI 设置保护。
- 备注、客户端选项和自定义参数是自由文本。RemoteHubStudio 无法可靠识别用户手动写入其中的 token 或密码，因此导出前应自行检查。
- 当前便携 JSON 信封仅包含分组和连接，不包含程序路径、窗口位置或本机安全偏好。
- 导入仍兼容 CSV v2、CSV v1 与无版本旧文件。CSV v2 可以还原 RDP 和客户端专属选项，包括可逆的电子表格公式保护。
- 导入 JSON 或 CSV 时会明确询问是否信任来源中的启动配置。默认选择“**否**”会禁用程序覆盖、自定义参数、私钥路径、可覆盖可见主机的 WinSCP 或 RustDesk 目标路由，以及 RDP 本地资源重定向。只有可信文件才应选择“**是**”。导入的分组引用只能绑定同一文件中的分组，绝不会按 GUID 绑定本地现有分组。
- 导入按裁剪后的名称进行序号式不区分大小写匹配。同名分组和连接会保留本地标识并更新为导入配置；新名称会被创建。导入文件中的重复名称或本地同名歧义会使整次导入原子失败。
- 便携 JSON 导出和 JSON/CSV 导入共享 16 MiB 文件大小上限。导出先写入同目录临时文件，验证通过后才替换目标。CSV 导入还限制记录数、列数和单字段长度。

### 工作区与内容限制

- 单个工作区最多包含 5,000 个分组和 50,000 条连接，分组嵌套最多 64 层。导入、合并、保存和加载都会执行这些限制，并用非递归线性验证拒绝缺失父分组与循环。
- 单个用户可控字符串最多 256 Ki 字符；单个工作区的全部持久化字符串合计最多 4 Mi 字符。
- `ToolPaths` 最多 64 项，每条连接最多 256 个 `Options`，整个工作区最多 100,000 个 `Options` 条目。保存、加载和便携导入导出共享这些内容预算。

## 数据位置与恢复

默认数据根目录是可执行文件旁边的 `.\data`。相对路径始终以应用程序目录为基准，而不是进程工作目录。退出应用后复制完整应用程序文件夹，即可同时迁移其数据；加密数据仍受上述 DPAPI `CurrentUser` 限制。

从使用旧版默认目录 `%LOCALAPPDATA%\RemoteHubStudio` 的版本升级时，请先退出应用，再把该目录内容复制到新可执行文件旁的 `.\data`。当前用户必须能够写入应用程序目录；便携部署时应避免受保护或只读位置。

| 路径 | 用途 |
| --- | --- |
| `workspace.json` | 当前工作区 |
| `workspace.json.bak` | 上一个成功保存的版本；首次启用加密时与当前版本一致 |
| `workspace.corrupt.<UTC 时间>-<GUID>.json` | 从备份恢复时保留的损坏主文件字节；始终是 DPAPI `CurrentUser` 受保护信封 |
| `language-preference.json` | 独立于工作区保存的界面语言选择 |
| `Languages\` | 用户维护、运行时优先级最高的语言包 |
| `temp\` | 应用临时数据目录 |
| `logs\` | 应用日志目录 |
| `.\data\temp\rdp\*.rdp` | 唯一的 RDP 启动文件；客户端退出后尽力删除，启动时只清理超过 24 小时且名称符合应用自身规则的文件 |

保存时会先在数据根目录创建唯一的同卷临时文件。只有完整写入、持久刷新到磁盘，并确认最终信封不超过 64 MiB 后才替换主文件；旧主文件会成为 `.bak`。受保护工作区的序列化明文另有 32 MiB 上限，避免在 DPAPI 与 Base64 扩展前产生无界中间分配。如果主文件无法加载，应用会验证并使用备份，同时把损坏主文件的原始字节保存在受保护诊断信封中。

## 开发与测试

核心回归检查是一个无额外测试框架依赖的控制台程序：

```powershell
dotnet run --project .\RemoteHubStudio.Tests\RemoteHubStudio.Tests.csproj -c Release
```

成功时会输出 `REMOTEHUBSTUDIO_TESTS_OK` 并返回退出代码 `0`。本仓库的回归套件应使用 `dotnet run`，而不是 `dotnet test`。

## 自动构建与发布

每天只自动运行 [All Releases 总工作流](.github/workflows/daily-release.yml)，它通过 `workflow_call` **并行**调用预览版和正式版子工作流。在同一次运行页面中可查看两个子任务及其编译、测试、发布步骤；两个子任务没有先后依赖，一个失败不会阻止另一个执行。总工作流和所有子工作流也都支持独立手动运行。

| 任务 | 工作流 | 触发方式 | 执行内容 |
| --- | --- | --- | --- |
| 总任务 | [All Releases](.github/workflows/daily-release.yml) | 每天北京时间 **08:17**，或手动 | 同时运行以下两个发布子任务 |
| 每日预览版 | [Nightly Release](.github/workflows/nightly-release.yml) | 总任务调用，或手动 | 更新 `v0.1.0-nightly` 等固定预览标签及同名附件 |
| 正式版 | [Stable Release](.github/workflows/release.yml) | 总任务调用、手动，或推送 `vX.Y.Z` 标签 | 当前版本尚未发布时构建并发布；已经发布则跳过 |
| 仅构建与测试 | [Build Release Package](.github/workflows/build-release.yml) | 发布子任务调用，或手动选择 `nightly` / `stable` | 生成可下载的 Actions Artifact，不创建标签或 Release |

**一键运行：**进入 **Actions → All Releases → Run workflow**，选择默认分支（当前为 `master`）并运行，即可同时执行两个发布子任务。只想执行其中一个时，选择 **Nightly Release** 或 **Stable Release**，同样点击 **Run workflow**。仅检查编译和打包时，选择 **Build Release Package**。预览版子任务不再单独配置定时器，避免同一天重复触发。

两个发布子任务共用构建测试流程和 [发布脚本](.github/scripts/publish-release.sh)：还原依赖、编译 Release、执行控制台回归测试，再生成 Windows x86、x64、ARM64 和可移植 ZIP，以及覆盖全部包的 `SHA256SUMS.txt`。所有包包含语言包和许可证，是否附带运行时由部署模式决定。同一渠道的总任务调用与独立手动运行共用并发锁，避免同时覆盖相同附件；不同渠道可以并行执行。

所有手动入口都提供 **deployment-mode**：`repository-default`、`self-contained`、`framework-dependent`、`both`。定时任务和标签构建读取带注释的 [release-settings.psd1](.github/release-settings.psd1)，默认独立部署并附加可移植包。可移植包始终依赖框架，当前 WinForms 应用不支持 Linux/macOS。配置方法、Windows 版本边界、部署模式取舍和本地命令见 [部署文档](docs/DEPLOYMENT.zh-CN.md)。已公开的正式版仍保留原附件，新架构或部署模式在下一正式版本生效。

**预览版：**构建默认分支（当前为 `master`），即使没有新提交也会构建；也可在 **Actions → Nightly Release → Run workflow** 选择默认分支手动运行。基础版本取自 `Directory.Build.props` 的 `<Version>`：`0.1.0` 对应 `v0.1.0-nightly`，升级为 `0.2.0` 后自动使用 `v0.2.0-nightly`，旧版本的预览标签保留最后一次构建。

标签和附件名不带日期或构建序号。例如 `RemoteHubStudio-v0.1.0-nightly-win-x64-self-contained.zip` 的下载地址保持固定；程序内部版本仍包含运行序号和重试次数（如 `0.1.0-nightly.12.1`），发布说明记录时间、源码提交和构建链接。每次成功更新时，预览标签会移动到实际构建的提交，避免源码与二进制不一致。

**正式版：**总任务或手动运行时，先读取默认分支的 `<Version>`。例如当前版本为 `0.1.0`，若 `v0.1.0` 已经公开发布，就直接跳过编译和发布，保留原版本；尚未发布则构建测试，并在成功后创建缺失的 `v0.1.0` 标签和正式 Release。若该标签已经存在但尚未发布，则构建标签指向的原始提交，不移动正式标签。版本为 `0.2.0-beta.1` 等预发布标识时跳过正式发布，仍构建预览版。

因此，**将 `<Version>` 升级为新的纯数字三段版本后，下一次总任务会自动发布对应正式版**。开发尚未稳定时，可以将版本设为 `0.2.0-beta.1`，准备正式发布时再改成 `0.2.0`。也仍然支持通过推送正式标签指定发布提交：

```powershell
git tag -a v0.1.0 -m "Release 0.1.0"
git push https://github.com/1609676823/RemoteHubStudio.git v0.1.0
```

通过标签触发时，`vX.Y.Z` 必须与该提交的源码版本完全匹配；`v0.1.0-nightly` 不会触发正式发布。构建前选定的提交和版本会再次验证，避免源码、标签与二进制不一致。正式发布失败后可重跑，已经公开的正式版始终保留原附件。准备下一版本时递增 `<Version>`，日更就会切换到新的预览标签。

编译或测试失败不会修改线上版本。替换预览附件时会暂时将 Release 设为草稿，附件和标签全部更新后再公开；若上传中途失败，该预览版会保持草稿，重跑失败的发布任务即可继续。Actions 中间产物保留 7 天，Releases 只保留每个版本最新的预览附件及各正式版。此前方案产生的日期标签不会被自动删除。

将工作流提交并推送到 GitHub 默认分支后即可启用，发布使用自动提供的 `GITHUB_TOKEN`，无需配置个人 Token。Fork 需修改总任务、预览子任务和正式子任务的仓库名条件。如 Actions 被禁用，请在 **Settings → Actions → General** 启用。滚动预览需要允许 `v*-nightly` 标签更新；仓库 **Settings → General → Releases → Enable release immutability** 应保持关闭，因为该开关会锁定新发布的预览版。脚本遇到不可变 Release 时会报错并保留原版本，不会删除重建。参见 [GitHub 不可变发布说明](https://docs.github.com/en/code-security/concepts/supply-chain-security/immutable-releases)。

修改定时时间请调整 UTC cron（当前 `17 0 * * *`）。GitHub 定时任务可能延迟；公开仓库连续 60 天无活动时会自动停用，可在 Actions 页面重新启用。参见 [GitHub 定时工作流说明](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#schedule)。

参考模式：[RustDesk](https://github.com/rustdesk/rustdesk/tree/master/.github/workflows) 将固定 `nightly` 和正式标签构建分开；[FFmpeg 官网](https://ffmpeg.org/download.html)提供源码及第三方二进制链接，其中 [BtbN](https://github.com/BtbN/FFmpeg-Builds#release-retention-policy) 提供滚动 `latest` 入口，也保留部分历史构建。本项目按基础版本设置滚动预览标签，不按天增加标签。

## 产品元数据

产品名、版本、作者、公司、描述、仓库/项目/发布地址和许可证标识集中在 [`Directory.Build.props`](Directory.Build.props)。公开项目链接指向 [GitHub](https://github.com/1609676823/RemoteHubStudio)，运行时“关于”对话框会读取生成的程序集元数据。

这是 .NET SDK 的标准构建配置机制：单项目可以将这些属性直接写在 `.csproj` 中，本仓库使用 MSBuild 自动导入的 `Directory.Build.props` 供应用和测试项目共享。`Program.cs` 负责启动，业务代码通过只读的 [`ProductInfo`](RemoteHubStudio/Configuration/ProductInfo.cs) 访问应用自身的程序集信息，不依赖测试程序或设计器的入口程序集。

- 发布时只需修改 `<Version>`（当前为 `0.1.0`，也支持 `0.2.0-beta.1`）。SDK 自动派生数字格式的 `AssemblyVersion`、`FileVersion` 和完整的 `InformationalVersion`。“关于”显示的 `ProductInfo.Version` 保留预发布标识并省略 `+` 后的构建元数据；`ProductInfo.InformationalVersion` 保留完整版本及 SDK 附加的 Git 提交号（如有），供诊断使用。
- 修改 `<RepositoryUrl>` 会同步更新默认的主页、问题跟踪、发布和许可证地址；需要独立地址时，编辑对应的 `PackageProjectUrl`、`IssuesUrl`、`PackageReleaseNotes`、`LicenseUrl` 属性。
- 修改 `<Authors>` 会同步更新默认发布者及版权中的作者名；`Company`、`Copyright`、`Description`、`PackageLicenseExpression` 也可分别配置。SDK 生成标准特性，其余信息通过 `AssemblyMetadata` 写入程序集。
- 设计器及语言包中的“关于”预览仅使用占位文本，真实信息在运行时填入，无需在 C# 或翻译文件中重复维护版本、版权或链接。数据目录、工作区格式和单实例标识属于兼容性常量，保留在代码中。

CI 可通过 `dotnet publish .\RemoteHubStudio\RemoteHubStudio.csproj -c Release -p:Version=0.2.0` 覆盖本次构建版本，无需修改源码。项目自身的 `AssemblyName` 和 `RootNamespace` 保留在各自的 `.csproj` 中。

参考：[使用项目属性生成程序集特性](https://learn.microsoft.com/en-us/dotnet/standard/assembly/set-attributes-project-file)、[使用 Directory.Build.props 共享构建配置](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory?view=vs-2022)。

## 许可证

RemoteHubStudio 按 MIT 许可证发布，详见 [`LICENSE.txt`](LICENSE.txt)。第三方远程客户端分别适用其自身许可证。

AntdUI 2.4.8 的版权声明和完整 Apache-2.0 许可证收录在 [`THIRD-PARTY-NOTICES.txt`](THIRD-PARTY-NOTICES.txt) 中。构建和发布输出会同时复制 RemoteHubStudio 许可证与第三方声明。
