# WinForms 设计器检查记录

检查日期：2026-09-05。范围：项目全部 7 个窗体（含公共基类）和 14 个用户控件，共 21 个源设计面，优先检查 `UI/Dialogs`。

本次在本机 Visual Studio 2026 Enterprise 18.9.2 中逐一打开设计器，结合源代码、控件树回放和运行时回归检查。项目使用 .NET SDK 10.0.400、`net10.0-windows7.0`、AntdUI 2.4.8。结论限于本机当前 VS、依赖和显示缩放环境。

## 主要问题与修复

1. **连接参数页只有空画布。** 公共目标编辑器和 12 个具体客户端页原先主要在构造函数中创建字段；直接打开源设计器不会执行该类型的构造函数。现已把固定控件创建、标签、尺寸、停靠、表格行列和父子关系移入各自的 `InitializeComponent()`，构造函数保留运行时字段注册、本地化、选择项和事件。
2. **继承窗体内容或标题缺失。** 公共页头、内容流和页脚流改为受保护字段；派生设计器直接使用继承字段添加控件、设置标题，避免通过隐藏序列化的包装属性操作设计树。
3. **Settings 的编辑控件为零尺寸。** 为 25 个输入框、选择器、开关和按钮补全尺寸或填充停靠，并设置各字段网格的设计时布局。实际检查覆盖顶部、中部和底部的外部客户端路径区域。
4. **设计画布偏移、DPI 通知清空字段。** 对话框在设计时不再执行屏幕居中与窗口范围限制；响应式字段网格在字段尚未注册或处于设计时保留序列化控件，防止 DPI/尺寸变化把设计树清空。
5. **连接编辑窗体没有协议参数预览。** 预置实际的 RDP 参数页，其他类型仍按运行时选择切换。已有连接载入时显式初始化预置页，已验证原有主机、端口、认证信息、RDP 参数和原始选项在切换客户端后仍保留。
6. **局部裁切。** 修复 RustDesk 说明文本单行撑宽、主窗体设置按钮锚定到画布外、工具栏换行后删除按钮被固定高度遮挡的问题。运行时仍使用原有响应式工具栏高度计算。
7. **设计器身份与序列化。** 将 `ConnectionAuthenticationFields` 枚举移到独立文件，使目标编辑器成为源文件中的首个类型；业务结果属性不参与设计器序列化；AntdUI 窗体使用可序列化的 `ClientSize`。

## 逐项结果

以下每项均完成 VS 实际打开及源设计树检查。“控件数”是源回放后递归子控件数，包含布局容器和嵌入控件，不含根控件；跨页面存在重复统计，不应相加作为业务字段总数。

| 设计面 | 控件数 | 结果与说明 |
| --- | ---: | --- |
| `SettingsForm` | 78 | 正常；设置编辑器、说明、客户端路径与页脚可见 |
| `ConnectionEditorForm` | 90 | 正常；基础信息、预置 RDP 页、高级设置与页脚已创建 |
| `GroupEditorForm` | 20 | 正常；名称、父分组、颜色、预览、排序和保存/取消可见 |
| `GroupManagerForm` | 14 | 正常；工具栏、空分组提示和关闭按钮可见 |
| `AboutForm` | 18 | 正常；产品信息、四个链接和关闭按钮可见，支持滚动查看 |
| `MainForm` | 25 | 正常；设置按钮、搜索、两组操作栏、分组入口、空列表和状态区可见 |
| `ResponsiveDialogWindow` | 5 | 正常打开；仅提供公共外壳，无业务内容 |
| `ConnectionEndpointEditor` | 11 | 正常；协议、目标、端口、用户名、密码字段可见 |
| `ConnectionTypeOptionsPage` | 0 | 正常打开；空白是继承宿主的预期行为 |
| `RdpConnectionTypeOptionsPage` | 56 | 正常；完整端点和 21 个 RDP 选项可见 |
| `RadminConnectionTypeOptionsPage` | 24 | 正常；端点、加密、全屏、键盘限制、色深和更新频率可见 |
| `RustDeskConnectionTypeOptionsPage` | 21 | 正常；端点、服务器、公钥、中继和多行说明可见 |
| `VncConnectionTypeOptionsPage` | 20 | 正常；端点、全屏、自动重连和仅查看开关可见 |
| `WinScpConnectionTypeOptionsPage` | 18 | 正常；端点、远程路径和 WebDAV 地址可见 |
| `PuttyConnectionTypeOptionsPage` | 12 | 正常；端点编辑器可见 |
| `MobaXtermConnectionTypeOptionsPage` | 12 | 正常；端点编辑器可见 |
| `SecureCrtConnectionTypeOptionsPage` | 12 | 正常；端点编辑器可见 |
| `ToDeskConnectionTypeOptionsPage` | 12 | 正常；端点编辑器可见 |
| `XftpConnectionTypeOptionsPage` | 12 | 正常；端点编辑器可见 |
| `XshellConnectionTypeOptionsPage` | 12 | 正常；端点编辑器可见 |
| `CustomConnectionTypeOptionsPage` | 12 | 正常；端点编辑器可见 |

另外检查了 `PasswordInput` 和 `ResponsiveFieldGrid`：它们属于代码实现的自定义控件，不是独立业务窗体。密码输入框通过嵌入的目标编辑器验证显示；字段网格通过各窗体和参数页验证显示，并单独覆盖 DPI 通知行为。

## 保留的设计时限制及原因

- 两个基类不是业务页面：`ResponsiveDialogWindow` 只有公共外壳，`ConnectionTypeOptionsPage` 本身没有字段。应打开派生窗体或具体客户端页。
- 连接列表、分组树、客户端安装路径和用户设置依赖运行时数据。设计器保留空状态和占位文本，不读取实际工作区。
- 固定窗体高度下，Settings、About、连接编辑器的全部内容不一定同时出现；内容保留在滚动区域，可操作画布右侧的滚动条查看。
- 继承的流式布局容器受 WinForms 继承设计器编辑规则约束；容器本身的结构修改应在基类中完成。嵌入的参数页内部字段应打开其独立设计器编辑。
- 独立源设计页展示可编辑的条件字段和双语常量；运行时会按协议、认证方式和能力隐藏不适用字段。编译后嵌入的控件还可能执行自身本地化，因此源页面与整窗预览的文字、默认值和可见字段不保证完全相同。详见[设计器语言说明](WINFORMS-DESIGNER-LANGUAGE.zh-CN.md)。

本次未遗留已确认的空白业务设计面、零尺寸编辑器或设计器加载错误。以上限制属于继承设计、滚动布局或运行时数据边界。

## 验证与后续防护

- `dotnet build RemoteHubStudio.slnx --no-restore`：通过，0 警告、0 错误。
- 本机 Visual Studio 自带 `MSBuild.exe RemoteHubStudio.slnx`：通过。
- `dotnet run --project RemoteHubStudio.Tests --no-restore`：通过，输出 `DESIGNER_SOURCE_OK (21 surfaces)` 和 `REMOTEHUBSTUDIO_TESTS_OK`。
- `git diff --check`：通过。

新增的 `DesignerSourceRegression` 自动发现所有具有 `InitializeComponent()` 的项目控件，在新建基类上回放设计器代码，检查固定控件初始化、父子关系、编辑器零尺寸、直接父容器裁切、DPI 通知和窗体定位。另覆盖预置 RDP 页载入既有连接及客户端切换后的数据保留。该检查补充实际 VS 预览，不模拟整个 VS 序列化引擎。

回放使用 SDK 随附的 Roslyn 编译器，不增加应用运行时依赖。设计器源文件在构建时复制到测试项目的 `obj` 目录后嵌入，避免把同一 `.Designer.cs` 作为另一项目的资源节点打开；直接跨项目引用曾在本机引发 `DesignerDocDataService.GetFileDocData` / `HRESULT E_FAIL`，改为独立副本并重启 VS 后已消失。资源显式关闭文化推断，避免 `.cs.txt` 被识别为捷克语资源。

如需重新生成本地控件树预览，可在 PowerShell 设置 `REMOTEHUB_DESIGNER_AUDIT_DIR` 为输出目录后运行测试。预览仅绘制测试中的控件树，不启动应用服务，也不替代真实 VS 检查。修改公共基类后，应重新生成并重新打开设计器；如果仍显示旧程序集内容，关闭所有设计器标签页并重启 VS。
