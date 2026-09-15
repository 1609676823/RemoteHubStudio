# RemoteHubStudio language packs

**English** | [简体中文](README.zh-CN.md)

RemoteHubStudio uses versioned UTF-8 JSON language packs. The application always embeds `en` and `zh-Hans`, and also reads external packs in this order:

1. Embedded packs (safe fallback; embedded English is the key and placeholder baseline).
2. `Languages` beside `RemoteHubStudio.exe`.
3. `data/Languages` beside `RemoteHubStudio.exe` (loaded last, so it has highest priority).

To add a language, copy `en.json`, rename it to the canonical BCP-47 locale (for example `fr.json` or `pt-BR.json`), and make the JSON `locale` match the filename. Keep `schemaVersion` at `1`, translate values under `strings`, and leave every key unchanged. The locale must be recognized by .NET.

Placeholders must exactly preserve the English format items, including repetitions, alignment, and format specifiers. Reordering is allowed. For example, English `Copied {0} of {1:N0}` may become `{1:N0} 件中已复制 {0} 件`, but `{0}` must not be removed and `{1:N0}` must not be changed to `{1}`. Literal braces must be escaped as `{{` and `}}`.

The loader rejects malformed JSON, duplicate properties, unknown root properties, filename/locale mismatches, unsupported schema versions, non-string values, and metadata or files over the documented limits. Unknown keys and strings with incompatible placeholders are ignored and fall back to another matching pack, then embedded English. A language pack may be incomplete.

For WinForms designer/runtime localization, keys used by `L.Apply(root, scope)` follow:

```text
<scope>.<control-name>.<property>
```

The root control name is `$this`. Supported properties are `Text`, `AccessibleName`, `AccessibleDescription`, `PlaceholderText`, `SubText`, `EmptyText`, and `ToggleText`. If `scope` is omitted, the root control's type name is used. Missing keys leave the designer value untouched. Calling `L.Apply(this)` after `InitializeComponent()` therefore also lets Visual Studio's out-of-process WinForms designer preview the host's current UI culture.

Validate a pack against [`language-pack.schema.json`](language-pack.schema.json) before submitting it. Runtime safety limits are 512 KiB per pack, 128 external packs per directory, 5,000 strings, 256 characters per key, 4,096 characters per value, 128 characters per language/author name, and 32 authors.

Chinese fallback is script-aware: `zh-CN`, `zh-SG`, and `zh-MY` try `zh-Hans`; `zh-TW`, `zh-HK`, and `zh-MO` try `zh-Hant`. Other locales walk their BCP-47 parent cultures and finally fall back to embedded English.
