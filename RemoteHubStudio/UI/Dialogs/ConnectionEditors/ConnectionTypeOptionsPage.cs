using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;
using RemoteHubStudio.Domain;

namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

/// <summary>
/// Defines the common contract for an editor page that owns one connection type's additional options.
/// / 定义管理单个连接类型附加选项的编辑子页公共协定。
/// </summary>
/// <remarks>
/// This base intentionally remains concrete with a public constructor because the inherited WinForms designer
/// creates the base type before it creates a derived page. Runtime callers must use a concrete factory-created page.
/// / 此基类有意保持可实例化并公开无参构造，因为 WinForms 继承设计器会先创建基类型，再创建派生参数页；
/// 运行时调用方必须使用工厂创建的具体参数页。
/// </remarks>
[DesignerCategory("UserControl")]
[ToolboxItem(false)]
public partial class ConnectionTypeOptionsPage : UserControl
{
    private string _customArgumentTemplate = string.Empty;

    /// <summary>
    /// Initializes a stable root for the inherited designer; concrete pages configure runtime layout separately.
    /// / 初始化继承设计器使用的稳定根控件，具体页面另行配置运行时布局。
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ConnectionTypeOptionsPage()
    {
        InitializeComponent();
        Margin = Padding.Empty;
        Padding = Padding.Empty;
    }

    /// <summary>Enables responsive layout from a concrete page's constructor. / 从具体页面构造函数启用响应式布局。</summary>
    protected void ConfigureRuntimeLayout()
    {
        // The source designer constructs only the base class, before assigning its Site.
        // Keep this out of the base constructor: DesignMode and UsageMode can both be false there.
        // / 源设计器仅构造基类且尚未设置 Site，此时两种设计模式检测都可能为 false，故不要在基类构造函数调用。
        if (!IsDesignerHosted)
        {
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Dock = DockStyle.Top;
        }
    }

    /// <summary>Gets the connection type edited by this page. / 获取此子页编辑的连接类型。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public virtual ConnectionType Type => IsDesignerHosted ? default : throw CreateBaseContractException();

    /// <summary>Gets the bilingual section title. / 获取双语区块标题。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public virtual string SectionTitle => IsDesignerHosted ? string.Empty : throw CreateBaseContractException();

    /// <summary>
    /// Gets every canonical or legacy <see cref="ConnectionProfile.Options"/> key owned by this page.
    /// / 获取此子页管理的所有规范或旧版 <see cref="ConnectionProfile.Options"/> 键。
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public virtual IReadOnlyCollection<string> ManagedOptionKeys => IsDesignerHosted
        ? Array.Empty<string>()
        : throw CreateBaseContractException();

    /// <summary>Gets a target-derived name used by quick connect when the name is empty. / 获取快速连接名称为空时使用的目标派生名称。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public virtual string SuggestedName => string.Empty;

    /// <summary>Gets whether the shared advanced area should expose a private-key path. / 获取公共高级区是否应显示私钥路径。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public virtual bool ShowsPrivateKey => CustomArgumentsUse("{key}");

    /// <summary>Occurs when mode-dependent shared fields need to be refreshed. / 模式相关公共字段需要刷新时发生。</summary>
    [Browsable(false)]
    public event EventHandler? EditorRequirementsChanged;

    /// <summary>Loads the profile into this page without mutating it. / 将配置加载到此子页且不修改原配置。</summary>
    /// <param name="profile">Profile whose values should be displayed. / 要显示的配置。</param>
    public virtual void LoadFrom(ConnectionProfile profile)
    {
        throw CreateBaseContractException();
    }

    /// <summary>
    /// Validates the page and writes only its owned properties or option keys to the profile.
    /// / 验证子页，并仅将其管理的属性或选项键写入配置。
    /// </summary>
    /// <param name="profile">Profile to update. / 要更新的配置。</param>
    /// <param name="error">Bilingual validation error, or null on success. / 双语验证错误；成功时为 null。</param>
    /// <returns>True when validation and application succeed. / 验证与应用成功时返回 true。</returns>
    public virtual bool TryApplyTo(ConnectionProfile profile, out string? error)
    {
        throw CreateBaseContractException();
    }

    /// <summary>Clears visual validation errors. / 清除可视验证错误。</summary>
    public virtual void ResetValidationState()
    {
        throw CreateBaseContractException();
    }

    /// <summary>Notifies the page that the shared custom argument override changed. / 通知参数页公共自定义命令覆盖已更改。</summary>
    /// <param name="template">Current argument template. / 当前参数模板。</param>
    public virtual void UpdateCustomArgumentTemplate(string? template)
    {
        _customArgumentTemplate = template ?? string.Empty;
        OnEditorRequirementsChanged();
    }

    /// <summary>Raises the shared-field requirements notification. / 引发公共字段需求通知。</summary>
    protected void OnEditorRequirementsChanged()
    {
        EditorRequirementsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Checks whether the current custom template consumes one placeholder. / 检查当前自定义模板是否使用某个占位符。</summary>
    protected bool CustomArgumentsUse(string placeholder)
    {
        return !string.IsNullOrWhiteSpace(_customArgumentTemplate) &&
               _customArgumentTemplate.Contains(placeholder, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Gets whether a custom argument override is active. / 获取自定义参数覆盖是否已启用。</summary>
    protected bool HasCustomArguments => !string.IsNullOrWhiteSpace(_customArgumentTemplate);

    /// <summary>Gets whether this instance is hosted by a WinForms design surface. / 获取此实例是否由 WinForms 设计界面承载。</summary>
    private bool IsDesignerHosted =>
        LicenseManager.UsageMode == LicenseUsageMode.Designtime || Site?.DesignMode == true;

    /// <summary>Creates a consistently styled text input. / 创建样式一致的文本输入框。</summary>
    /// <param name="placeholder">Bilingual placeholder text. / 双语占位文本。</param>
    /// <returns>The configured input. / 配置后的输入框。</returns>
    protected static AntdUI.Input CreateInput(string placeholder)
    {
        return new AntdUI.Input
        {
            PlaceholderText = placeholder,
            AllowClear = true,
            Radius = 8
        };
    }

    /// <summary>Creates a text input for the shared endpoint editor. / 为公共目标编辑器创建文本输入框。</summary>
    internal static AntdUI.Input CreateTextInput(string placeholder) => CreateInput(placeholder);

    /// <summary>Creates a consistently styled selection control. / 创建样式一致的选择控件。</summary>
    /// <param name="placeholder">Bilingual placeholder text. / 双语占位文本。</param>
    /// <returns>The configured selection control. / 配置后的选择控件。</returns>
    protected static AntdUI.Select CreateSelect(string placeholder)
    {
        return new AntdUI.Select
        {
            PlaceholderText = placeholder,
            AllowClear = false,
            DropDownArrow = true,
            ListAutoWidth = true,
            Radius = 8,
            WheelModifyEnabled = false
        };
    }

    /// <summary>Creates a selection control for the shared endpoint editor. / 为公共目标编辑器创建选择控件。</summary>
    internal static AntdUI.Select CreateSelection(string placeholder) => CreateSelect(placeholder);

    /// <summary>Creates a consistently styled integer input. / 创建样式一致的整数输入框。</summary>
    /// <param name="minimum">Minimum accepted value. / 允许的最小值。</param>
    /// <param name="maximum">Maximum accepted value. / 允许的最大值。</param>
    /// <param name="value">Initial value. / 初始值。</param>
    /// <returns>The configured number input. / 配置后的数字输入框。</returns>
    protected static AntdUI.InputNumber CreateNumber(decimal minimum, decimal maximum, decimal value)
    {
        return new AntdUI.InputNumber
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = value,
            DecimalPlaces = 0,
            ShowControl = true,
            Radius = 8
        };
    }

    /// <summary>Creates an integer input for the shared endpoint editor. / 为公共目标编辑器创建整数输入框。</summary>
    internal static AntdUI.InputNumber CreateIntegerInput(decimal minimum, decimal maximum, decimal value) =>
        CreateNumber(minimum, maximum, value);

    /// <summary>Stacks auto-sized controls vertically without coupling their implementations. / 垂直堆叠自动尺寸控件且不耦合其实现。</summary>
    protected static TableLayoutPanel StackControls(params Control[] controls)
    {
        TableLayoutPanel layout = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = controls.Length
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (int index = 0; index < controls.Length; index++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(controls[index], 0, index);
        }

        return layout;
    }

    /// <summary>Creates a compact Boolean switch. / 创建紧凑的布尔开关。</summary>
    /// <param name="accessibleName">Accessible field name. / 无障碍字段名称。</param>
    /// <returns>The configured switch. / 配置后的开关。</returns>
    protected static AntdUI.Switch CreateSwitch(string accessibleName)
    {
        return new AntdUI.Switch
        {
            AccessibleName = accessibleName,
            AutoCheck = true
        };
    }

    /// <summary>
    /// Reads the first matching option with a case-insensitive fallback for externally created dictionaries.
    /// / 读取首个匹配选项，并对外部创建的字典执行不区分大小写的回退查找。
    /// </summary>
    /// <param name="profile">Profile containing options. / 包含选项的配置。</param>
    /// <param name="keys">Canonical key followed by accepted legacy aliases. / 规范键及可接受的旧版别名。</param>
    /// <returns>The option value, or null when absent. / 选项值；不存在时为 null。</returns>
    protected static string? ReadOption(ConnectionProfile profile, params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Dictionary<string, string>? options = profile.Options;
        if (options is null)
        {
            return null;
        }

        foreach (string key in keys)
        {
            if (options.TryGetValue(key, out string? direct))
            {
                return direct;
            }

            foreach (KeyValuePair<string, string> option in options)
            {
                if (string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return option.Value;
                }
            }
        }

        return null;
    }

    /// <summary>Reads a Boolean option using explicit accepted values. / 使用明确的可接受值读取布尔选项。</summary>
    /// <param name="profile">Profile containing options. / 包含选项的配置。</param>
    /// <param name="defaultValue">Fallback when no option is present or valid. / 选项不存在或无效时的回退值。</param>
    /// <param name="keys">Canonical key followed by aliases. / 规范键及别名。</param>
    /// <returns>The parsed or fallback value. / 解析值或回退值。</returns>
    protected static bool ReadBooleanOption(ConnectionProfile profile, bool defaultValue, params string[] keys)
    {
        string? value = ReadOption(profile, keys);
        return value?.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => defaultValue
        };
    }

    /// <summary>Reads a bounded integer option. / 读取有范围限制的整数选项。</summary>
    /// <param name="profile">Profile containing options. / 包含选项的配置。</param>
    /// <param name="defaultValue">Fallback value. / 回退值。</param>
    /// <param name="minimum">Inclusive minimum. / 包含的最小值。</param>
    /// <param name="maximum">Inclusive maximum. / 包含的最大值。</param>
    /// <param name="keys">Canonical key followed by aliases. / 规范键及别名。</param>
    /// <returns>The parsed or fallback value. / 解析值或回退值。</returns>
    protected static int ReadIntegerOption(
        ConnectionProfile profile,
        int defaultValue,
        int minimum,
        int maximum,
        params string[] keys)
    {
        string? value = ReadOption(profile, keys);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) &&
               parsed >= minimum && parsed <= maximum
            ? parsed
            : defaultValue;
    }

    /// <summary>
    /// Removes only this page's canonical and legacy keys while preserving every unknown option.
    /// / 仅删除此子页的规范键和旧版键，并保留所有未知选项。
    /// </summary>
    /// <param name="profile">Profile whose owned keys should be replaced. / 要替换所属键的配置。</param>
    protected void RemoveManagedOptions(ConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Options ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        HashSet<string> managedKeys = new(ManagedOptionKeys, StringComparer.OrdinalIgnoreCase);
        string[] keysToRemove = profile.Options.Keys
            .Where(key => managedKeys.Contains(key))
            .ToArray();
        foreach (string key in keysToRemove)
        {
            profile.Options.Remove(key);
        }
    }

    /// <summary>Writes one canonical option using invariant Boolean text. / 使用不变的布尔文本写入一个规范选项。</summary>
    /// <param name="profile">Profile receiving the option. / 接收选项的配置。</param>
    /// <param name="key">Canonical option key. / 规范选项键。</param>
    /// <param name="value">Boolean value. / 布尔值。</param>
    protected static void WriteBooleanOption(ConnectionProfile profile, string key, bool value)
    {
        WriteOption(profile, key, value ? "true" : "false");
    }

    /// <summary>Writes one canonical option value. / 写入一个规范选项值。</summary>
    /// <param name="profile">Profile receiving the option. / 接收选项的配置。</param>
    /// <param name="key">Canonical option key. / 规范选项键。</param>
    /// <param name="value">Option value. / 选项值。</param>
    protected static void WriteOption(ConnectionProfile profile, string key, string value)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        profile.Options ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        profile.Options[key] = value;
    }

    /// <summary>
    /// Creates the failure used when the concrete designer host is accidentally treated as a runtime editor.
    /// / 创建在仅供设计器承载的基类被误当作运行时编辑器时使用的异常。
    /// </summary>
    private static InvalidOperationException CreateBaseContractException()
    {
        return new InvalidOperationException(
            $"'{nameof(ConnectionTypeOptionsPage)}' is a designer host only. Use a concrete page from '{nameof(ConnectionTypeOptionsPageFactory)}'. / " +
            $"“{nameof(ConnectionTypeOptionsPage)}”仅用于设计器承载；请使用“{nameof(ConnectionTypeOptionsPageFactory)}”创建具体参数页。");
    }
}
