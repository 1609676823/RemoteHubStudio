# WinForms designer language guide

**English** | [简体中文](WINFORMS-DESIGNER-LANGUAGE.zh-CN.md)

RemoteHubStudio forms call `L.Apply(this)` after `InitializeComponent()` for runtime JSON localization. **The source designer instantiates the base class and replays `InitializeComponent`, without running the edited type's constructor.** Directly opened forms/pages therefore display the bilingual constants serialized in `.Designer.cs`. Compiled controls embedded in another designer may run their constructors and apply JSON localization; a constructor call alone cannot localize the edited source surface.

## Which language the designer uses

Visual Studio's out-of-process WinForms designer runs in `DesignToolsServer`. If an embedded compiled control calls `L`, it reads `CultureInfo.CurrentUICulture` from that process and falls back from the complete BCP-47 tag to its parent culture and English. This does not replace constants replayed directly by the source designer.

This normally matches Visual Studio's current display language, which is not necessarily the Windows display language. The designer preview follows the current Windows UI language only when Visual Studio's language is set to **Same as Microsoft Windows**. After changing the Visual Studio or Windows display language, close every open designer tab and restart Visual Studio.

The runtime language selection saved in `data\language-preference.json` does not control the designer. The designer does not go through the application's `Program` startup flow and does not read that user preference. This separation is intentional: an individual's runtime setting cannot change the design view that teammates see when they open the same form.

## Why embedded language packs work in the designer

The English and Simplified Chinese JSON files are embedded into the assembly as resources. Once `DesignToolsServer` loads the project assembly, it can read those resources without depending on the designer's current working directory or requiring the application to have been started first.

To make another language available to compiled embedded controls (without changing the source designer's bilingual constants):

1. Copy `RemoteHubStudio/Languages/en.json`, name the new file with a canonical BCP-47 tag, and make the JSON `locale` match the filename.
2. Translate only the values under `strings`, preserving every key and format placeholder.
3. Keep the JSON file in the `RemoteHubStudio/Languages` root. The project file automatically embeds language JSON files from this directory into the assembly. Copying a pack only to the output directory is not enough to ensure that the designer host can find it.
4. Rebuild the project, close every open form designer, and restart Visual Studio.

The repository's `Languages` content is copied to the application output. `Languages` beside the executable and `data\Languages` are also loaded at runtime, allowing users to install and override third-party language packs. However, because the designer host's base directory may differ from the running application's directory, external packs are intended primarily for runtime use and should not be treated as a reliable source for designer previews.

## Control key convention

`L.Apply(root, scope)` uses keys in this format:

```text
<scope>.<control-name>.<property>
```

- If `scope` is omitted, the form or user control's type name is used.
- The root control name is always `$this`; child controls use their `Name`.
- Supported properties are `Text`, `AccessibleName`, `AccessibleDescription`, `PlaceholderText`, `SubText`, `EmptyText`, and `ToggleText`.
- If a key is missing, the original designer value is retained, allowing an incomplete language pack to be translated incrementally while always retaining embedded English as a fallback.

When adding a form or control, keep its `Name` stable and meaningful, and call `L.Apply(this)` after `InitializeComponent()` in its public parameterless constructor. Existing forms' parameterless constructors should continue to avoid workspace file access, external process launches, and dependencies on services available only at runtime so that the designer can instantiate them successfully.

## Resource files and Git

Every form or connection-editor control that opens in the designer should include a tracked `.resx` with the same name, even when it has no resource entries yet. JSON language packs replace localized strings, not WinForms resource files. If a `.resx` is missing, Visual Studio may create it when opening or saving the designer, leaving a new file in Git.

When adding a `ConnectionEditors` page, use a `partial` class, include both `.Designer.cs` and `.resx`, and call `InitializeComponent()` from its constructor. The designer file must own the complete fixed control tree: fields, parameterless creation, labels, sizes, docking, and `Controls.Add`. Creating the contents only through constructor calls such as `AddField` or `StackControls` leaves the source surface blank. Constructors register responsive fields, localize text, populate protocol choices, and attach business events. The project already specifies the `UserControl` subtype and child-file relationships. Do not ignore `.resx` files.

Concrete options pages call `ConfigureRuntimeLayout()` after `InitializeComponent()` to enable top docking and automatic sizing at runtime. The base constructor retains a fixed layout: when VS creates it before assigning its `Site`, neither `DesignMode` nor `LicenseManager.UsageMode` may indicate design time yet, so a constructor-time check alone is insufficient. This prevents an empty designer root from shrinking to zero height or serializing the IDE viewport width into source. After changing the base class, rebuild and restart VS to unload the old assembly from the out-of-process designer.

`.resx` files use UTF-8 without a BOM and the current designer's empty resource template, with trailing whitespace removed from the old schema comments. `.gitattributes` checks out `.resx` files with CRLF to match the Windows resource writer while retaining LF in Git, avoiding repeated line-ending conversions when opening the designer. Close and reopen any existing designer tabs after changing this rule.

## Current limitations

- The concrete base `ConnectionTypeOptionsPage` intentionally contains no protocol fields. Open its concrete client pages instead. `ResponsiveDialogWindow` likewise supplies only shared dialog chrome.
- Source pages expose conditional fields for editing; runtime protocol/authentication rules control visibility. The connection form includes a real default RDP page and switches other clients at runtime.
- Workspace data and installed-client paths are not loaded by the designer. Inherited flow containers have WinForms inheritance editing restrictions; edit their layout in the base form.
- Serialize `ClientSize` for `AntdUI.Window`: its `Size` property is hidden from serialization. Do not center/clamp designer roots in `OnShown`.
- Unregistered responsive grids must preserve serialized children during both width and DPI changes. AntdUI editors need explicit sizes or fill docking before runtime field registration.

`dotnet run --project RemoteHubStudio.Tests` also replays `InitializeComponent` for all 21 source surfaces, checking creation, parenting, nonzero sizes, parent clipping, DPI changes, and window positioning. This complements actual VS preview checks. See the [Chinese audit record](WINFORMS-DESIGNER-AUDIT.zh-CN.md).

Language-pack format v1 currently validates and supports only left-to-right (LTR) interface layouts. RTL text can be added for experimentation, but right-to-left layout, mirrored docking, icon direction, and bidirectional text do not yet carry a compatibility guarantee. A production RTL language requires separate designer and runtime verification for every form.

For the language-pack format, field limits, placeholder rules, and override precedence, see [`RemoteHubStudio/Languages/README.md`](../RemoteHubStudio/Languages/README.md).
