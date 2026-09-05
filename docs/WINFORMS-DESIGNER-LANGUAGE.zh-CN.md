# WinForms 设计器语言说明

[English](WINFORMS-DESIGNER-LANGUAGE.md) | **简体中文**

RemoteHubStudio 的窗体在 `InitializeComponent()` 之后调用 `L.Apply(this)`，使用 JSON 语言包完成运行时本地化。**直接打开窗体/用户控件的源设计器时，VS 实例化基类并重放 `InitializeComponent()`，不会执行被编辑类型的构造函数。** 因此源设计器显示 `.Designer.cs` 中序列化的双语兜底文本；仅在实例化已编译的嵌入控件时，才可能执行该控件构造函数中的 JSON 本地化。不能把“构造函数内调用 `L.Apply`”当成源设计器会执行该调用的保证。

## 设计器使用哪种语言

Visual Studio 的进程外 WinForms 设计器运行在 `DesignToolsServer` 中。若实例化的已编译控件调用 `L`，它会读取设计器宿主进程的 `CultureInfo.CurrentUICulture`，并按完整 BCP-47 标识、父区域和英文的顺序回退。此规则不改变源设计器直接重放的文本常量。

这通常等于 Visual Studio 当前的显示语言，而不一定等于 Windows 的显示语言。只有把 Visual Studio 的语言设置为“跟随 Windows”时，设计器预览语言才会随当前 Windows UI 语言变化。修改 Visual Studio 或 Windows 显示语言后，应关闭所有设计器标签页并重启 Visual Studio。

运行时在 `data\language-preference.json` 中保存的语言选择不会控制设计器。设计器不会经过应用程序的 `Program` 启动流程，也不会读取这项用户偏好；这是有意的隔离，可避免个人运行设置改变团队成员打开同一窗体时的设计视图。

## 为什么内置语言包可在设计器中使用

英文和简体中文 JSON 随程序集作为嵌入资源生成。`DesignToolsServer` 加载项目程序集后即可读取这些资源，因此不依赖设计器的当前工作目录，也不要求先启动应用。

若维护者要让已编译的嵌入控件也能使用另一种语言（不改变源设计器的双语常量）：

1. 复制 `RemoteHubStudio/Languages/en.json`，以规范 BCP-47 标识命名文件，并让 JSON 的 `locale` 与文件名一致。
2. 只翻译 `strings` 下的值，保留全部键和格式占位符。
3. 保持 JSON 位于 `RemoteHubStudio/Languages` 根目录；项目文件会自动把该目录中的语言 JSON 作为嵌入资源加入程序集。仅复制到输出目录不足以保证设计器宿主能找到它。
4. 重新生成项目，关闭已打开的窗体设计器，然后重启 Visual Studio。

仓库中的 `Languages` 内容会复制到应用输出；程序目录旁的 `Languages` 与 `data\Languages` 也会在运行时加载，适合用户安装和覆盖第三方语言包。不过设计器宿主的基准目录与应用运行目录可能不同，所以外置包主要用于运行时，不应作为设计器预览的可靠来源。

## 控件键约定

`L.Apply(root, scope)` 使用以下键格式：

```text
<scope>.<control-name>.<property>
```

- 未传入 `scope` 时，使用窗体或用户控件的类型名。
- 根控件名固定为 `$this`，子控件使用其 `Name`。
- 支持 `Text`、`AccessibleName`、`AccessibleDescription`、`PlaceholderText`、`SubText`、`EmptyText` 和 `ToggleText`。
- 缺少键时保留设计器中的原始值，便于不完整语言包逐步补译，并始终可回退到内置英文。

新增窗体或控件时，请保留稳定且有意义的 `Name`，在公共无参构造函数的 `InitializeComponent()` 之后调用 `L.Apply(this)`。现有窗体的无参构造函数应继续保持不访问工作区文件、不启动外部进程，也不依赖仅运行时才存在的服务，以免设计器实例化失败。

## 资源文件与 Git

每个可在设计器中打开的窗体或连接编辑控件都应提交同名 `.resx`，即使文件目前没有资源条目。JSON 语言包取代的是本地化文本，并不取代 WinForms 的资源文件。缺少 `.resx` 时，Visual Studio 可能在打开或保存设计器时补建它，使 Git 出现新增文件。

新增 `ConnectionEditors` 页面时，请使用 `partial` 类，同时添加 `.Designer.cs` 和 `.resx`，并在构造函数中调用 `InitializeComponent()`。设计器文件必须包含完整的固定控件树：字段声明、无参创建、标签、尺寸、停靠和 `Controls.Add`。不能只设置根控件的名称和尺寸，再在构造函数中用 `AddField`、`StackControls` 或工厂创建所有内容，否则直接打开页面时仍然空白。构造函数只负责注册响应式字段、协议选择项、本地化和业务事件。项目文件已配置 `UserControl` 类型及子文件的 `DependentUpon` 关联，无需依赖个人 `.csproj.user` 文件。不要将 `.resx` 加入忽略规则。

具体参数页在 `InitializeComponent()` 后调用 `ConfigureRuntimeLayout()`，启用运行时顶部停靠和自动尺寸。基类构造函数保持固定布局：VS 在设置 `Site` 前创建基类时，`DesignMode` 和 `LicenseManager.UsageMode` 都可能尚未表示设计时，不能仅靠构造函数中的设计模式判断。这可避免空的设计器根控件收缩为零高度，或把 IDE 视口宽度写入源文件。修改基类后应重新生成并重启 VS，让进程外设计器卸载旧程序集。

`.resx` 使用 UTF-8 无 BOM，并采用当前设计器输出的空资源模板，去除旧模板注释中的行尾空白。`.gitattributes` 让 `.resx` 在工作区使用与 Windows 资源写入器一致的 CRLF，并在 Git 中仍保存为 LF，避免打开设计器时反复转换换行。修改规则后如已有设计器标签页打开，请关闭后重新打开。

## 当前限制

- `ConnectionTypeOptionsPage` 是可实例化的继承宿主，本身没有协议字段；应打开具体协议页。`ResponsiveDialogWindow` 只提供公共页头、内容和页脚，也是正常的空壳。
- 源设计器保留条件字段便于编辑；运行时会按协议、认证方式和客户端能力显示或隐藏它们。连接编辑窗体预置真实 RDP 页，其他类型在运行时切换，也可直接打开对应 `.cs` 的设计器。
- 连接列表、工作区分组、客户端安装路径等实际数据不在设计时加载。继承的 `FlowLayoutPanel` 在 WinForms 设计器中受继承编辑限制，布局容器本身应在基类中修改。
- AntdUI.Window 将 `Size` 标记为隐藏序列化属性，窗体设计器应保存 `ClientSize`。不要在设计时的 `OnShown` 中执行显示器居中或窗口大小限制。
- `ResponsiveFieldGrid` 注册字段前必须保留序列化的控件；宽度和 DPI 通知都不能清空它们。AntdUI 编辑控件还必须明确设置尺寸或填充停靠，不能依赖运行时注册来修正零尺寸。

运行 `dotnet run --project RemoteHubStudio.Tests` 会额外重放全部 21 个设计面的 `InitializeComponent`，检查控件创建、父子关联、零尺寸、父容器裁剪、DPI 变化和窗体定位。该检查补充而不替代 VS 实际预览。完整本次检查结果见 [设计器检查记录](WINFORMS-DESIGNER-AUDIT.zh-CN.md)。

语言包格式 v1 目前只验证和支持从左到右（LTR）的界面布局。可以添加 RTL 语言文本用于试验，但右到左布局、镜像停靠、图标方向和混排尚未形成兼容性承诺；正式提交 RTL 语言前需要另行完成逐窗体设计器与运行时验证。

语言包格式、字段限制、占位符规则和覆盖优先级详见 [`RemoteHubStudio/Languages/README.zh-CN.md`](../RemoteHubStudio/Languages/README.zh-CN.md)。
