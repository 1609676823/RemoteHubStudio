# RemoteHubStudio 语言包

[English](README.md) | **简体中文**

RemoteHubStudio 使用带版本的 UTF-8 JSON 语言包。程序始终内嵌 `en` 和 `zh-Hans`，并按以下顺序读取外置语言包：

1. 内嵌语言包（安全回退；内嵌英文同时是键与占位符基线）。
2. `RemoteHubStudio.exe` 旁边的 `Languages`。
3. `RemoteHubStudio.exe` 旁边的 `data/Languages`（最后加载，优先级最高）。

添加语言时，请复制 `en.json`，将文件名改为规范 BCP-47 区域标识（例如 `fr.json` 或 `pt-BR.json`），并使 JSON 中的 `locale` 与文件名一致。`schemaVersion` 保持为 `1`，只翻译 `strings` 下的值，不要改动键。区域标识必须能被 .NET 识别。

占位符必须与英文完全一致，包括出现次数、对齐方式和格式说明符；可以调整顺序。例如英文 `Copied {0} of {1:N0}` 可译为 `{1:N0} 件中已复制 {0} 件`，但不能删除 `{0}`，也不能把 `{1:N0}` 改为 `{1}`。字面大括号要写成 `{{` 和 `}}`。

加载器会拒绝损坏的 JSON、重复属性、未知根属性、文件名与 `locale` 不匹配、不支持的 schema 版本、非字符串值，以及超限的元数据或文件。未知键与占位符不兼容的字符串会被忽略，并回退到其他匹配包，最后回退到内嵌英文。语言包可以只翻译一部分键。

WinForms 设计器/运行时本地化使用 `L.Apply(root, scope)`，键格式为：

```text
<作用域>.<控件名>.<属性>
```

根控件名为 `$this`。支持 `Text`、`AccessibleName`、`AccessibleDescription`、`PlaceholderText`、`SubText`、`EmptyText` 和 `ToggleText`。省略 `scope` 时使用根控件的类型名。缺少键时保留设计器值。因此在 `InitializeComponent()` 后调用 `L.Apply(this)`，Visual Studio 的进程外 WinForms 设计器也能按宿主的当前 UI 语言预览。

提交前请用 [`language-pack.schema.json`](language-pack.schema.json) 验证。运行时安全上限为：每包 512 KiB，每个外置目录 128 个包，5,000 个字符串，键最长 256 字符，值最长 4,096 字符，语言/作者名最长 128 字符，作者最多 32 人。

中文回退会识别脚本：`zh-CN`、`zh-SG` 和 `zh-MY` 尝试 `zh-Hans`；`zh-TW`、`zh-HK` 和 `zh-MO` 尝试 `zh-Hant`。其他区域按 BCP-47 父区域逐级回退，最后使用内嵌英文。
