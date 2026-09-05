using System.ComponentModel;
using System.Windows.Forms;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Localization;
using RemoteHubStudio.UI.Controls;

namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

/// <summary>
/// Provides the common endpoint, mode, and inline authentication surface embedded by each independent client page.
/// / 提供由各独立客户端参数页嵌入的通用目标、模式和内联认证界面。
/// </summary>
[DesignerCategory("UserControl")]
[ToolboxItem(false)]
public sealed partial class ConnectionEndpointEditor : UserControl
{
    private ConnectionType _type = ConnectionType.RemoteDesktop;
    private bool _requiresHost = true;
    private bool _showPort = true;
    private bool _requiresPort = true;
    private Func<string, ConnectionAuthenticationFields> _nativeAuthenticationFields =
        static _ => ConnectionAuthenticationFields.UsernameAndPassword;
    private string _customArgumentTemplate = string.Empty;
    private string _lastProtocol = ConnectionType.RemoteDesktop.GetDefaultProtocol();
    private bool _loading;

    /// <summary>
    /// Initializes a representative endpoint editor that the WinForms designer can construct without runtime data.
    /// / 初始化无需运行时数据即可由 WinForms 设计器构造的代表性目标编辑器。
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ConnectionEndpointEditor()
    {
        InitializeComponent();
        _fields.RegisterField(_protocolLabel, _protocolSelect);
        _fields.RegisterField(_hostLabel, _hostInput);
        _fields.RegisterField(_portLabel, _portInput);
        _fields.RegisterField(_usernameLabel, _usernameInput);
        _fields.RegisterField(_passwordLabel, _passwordInput);
        _protocolSelect.SelectedValueChanged += HandleProtocolChanged;
        PopulateProtocolChoices();
        _protocolSelect.SelectedValue = _lastProtocol;
    }

    /// <summary>Sets representative protocol/port defaults for an embedded designer preview. / 设置嵌入式设计预览的协议与端口默认值。</summary>
    [DefaultValue(ConnectionType.RemoteDesktop)]
    public ConnectionType ClientType
    {
        get => _type;
        set
        {
            if (_type == value) return;
            _type = value;
            _lastProtocol = value.GetDefaultProtocol();
            _portInput.Value = value.GetDefaultPort(_lastProtocol);
            PopulateProtocolChoices();
            _protocolSelect.SelectedValue = _lastProtocol;
        }
    }

    /// <summary>Initializes a client-specific endpoint editor. / 初始化客户端专属目标编辑器。</summary>
    public ConnectionEndpointEditor(
        ConnectionType type,
        string protocolLabel,
        string targetLabel,
        string targetPlaceholder,
        Func<string, ConnectionAuthenticationFields> nativeAuthenticationFields,
        bool showPort = true,
        bool requiresPort = true,
        bool requiresHost = true,
        string? usernameLabel = null,
        string? passwordLabel = null)
        : this()
    {
        Configure(type, protocolLabel, targetLabel, targetPlaceholder, nativeAuthenticationFields,
            showPort, requiresPort, requiresHost, usernameLabel, passwordLabel);
    }

    /// <summary>Configures a designer-created endpoint without replacing its controls. / 配置设计器创建的目标编辑器，不替换其控件。</summary>
    internal void Configure(
        ConnectionType type,
        string protocolLabel,
        string targetLabel,
        string targetPlaceholder,
        Func<string, ConnectionAuthenticationFields> nativeAuthenticationFields,
        bool showPort = true,
        bool requiresPort = true,
        bool requiresHost = true,
        string? usernameLabel = null,
        string? passwordLabel = null)
    {
        usernameLabel ??= L.Get("ConnectionEndpoint.Username");
        passwordLabel ??= L.Get("ConnectionEndpoint.Password");
        ArgumentException.ThrowIfNullOrWhiteSpace(protocolLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPlaceholder);
        ArgumentNullException.ThrowIfNull(nativeAuthenticationFields);

        _type = type;
        _showPort = showPort;
        _requiresPort = requiresPort;
        _requiresHost = requiresHost;
        _nativeAuthenticationFields = nativeAuthenticationFields;
        _lastProtocol = type.GetDefaultProtocol();

        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Dock = DockStyle.Top;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _protocolSelect.PlaceholderText = L.Get("ConnectionEndpoint.ProtocolPlaceholder");
        PopulateProtocolChoices();

        _hostInput.PlaceholderText = targetPlaceholder;
        _portInput.Value = type.GetDefaultPort(_lastProtocol);
        _usernameInput.PlaceholderText = usernameLabel;
        _passwordInput.PlaceholderText = passwordLabel;
        _fields.SetFieldLabel(_protocolSelect, protocolLabel);
        _fields.SetFieldLabel(_hostInput, targetLabel);
        _fields.SetFieldLabel(_portInput, L.Get("ConnectionEndpoint.Port"));
        _fields.SetFieldLabel(_usernameInput, usernameLabel);
        _fields.SetFieldLabel(_passwordInput, passwordLabel);

        _fields.SetFieldVisible(_protocolSelect, type.GetProtocols().Count > 1);
        _fields.SetFieldVisible(_portInput, showPort);
        if (_protocolSelect.Items.Count > 0)
        {
            _protocolSelect.SelectedValue = _lastProtocol;
        }

        RefreshAuthenticationFields();
    }

    /// <summary>Occurs when the user selects another client protocol or action mode. / 用户选择其他客户端协议或操作模式时发生。</summary>
    [Browsable(false)]
    public event EventHandler? ProtocolChanged;

    /// <summary>Gets the currently selected stable protocol identifier. / 获取当前选择的稳定协议标识。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Protocol => _protocolSelect.SelectedValue as string ?? _type.GetDefaultProtocol();

    /// <summary>Gets the current target text for quick-connect naming. / 获取当前目标文本，用于快速连接命名。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Target => _hostInput.Text.Trim();

    /// <summary>Gets the effective authentication fields after custom-argument overrides are considered. / 获取计入自定义参数覆盖后的有效认证字段。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ConnectionAuthenticationFields EffectiveAuthenticationFields => ResolveAuthenticationFields();

    /// <summary>Loads endpoint and authentication values without changing the source profile. / 加载目标与认证值且不修改源配置。</summary>
    public void LoadFrom(ConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _loading = true;
        try
        {
            bool sameClient = profile.Type == _type;
            string protocol = sameClient
                ? NormalizeProtocolForEditor(profile.Protocol)
                : _type.GetDefaultProtocol();
            bool unsupportedProtocol = sameClient && !IsSupportedProtocol(protocol);
            PopulateProtocolChoices(unsupportedProtocol ? protocol : null);
            _fields.SetFieldVisible(
                _protocolSelect,
                _type.GetProtocols().Count > 1 || unsupportedProtocol);
            if (_protocolSelect.Items.Count > 0)
            {
                _protocolSelect.SelectedValue = protocol;
                if (_protocolSelect.SelectedIndex < 0)
                {
                    _protocolSelect.SelectedIndex = 0;
                }
            }

            _lastProtocol = Protocol;
            _hostInput.Text = profile.Host ?? string.Empty;
            _portInput.Value = sameClient && profile.Port is >= 0 and <= 65535
                ? profile.Port
                : _type.GetDefaultPort(Protocol);
            _usernameInput.Text = sameClient ? profile.Username ?? string.Empty : string.Empty;
            _passwordInput.Text = sameClient ? profile.Password ?? string.Empty : string.Empty;
            _customArgumentTemplate = profile.CustomArguments ?? string.Empty;
        }
        finally
        {
            _loading = false;
        }

        RefreshAuthenticationFields();
        ResetValidationState();
    }

    /// <summary>Updates authentication-field visibility when a command override changes how values are consumed. / 命令覆盖改变参数使用方式时更新认证字段可见性。</summary>
    public void SetCustomArgumentTemplate(string? template)
    {
        _customArgumentTemplate = template ?? string.Empty;
        RefreshAuthenticationFields();
    }

    /// <summary>Re-evaluates protocol-dependent authentication controls. / 重新计算依赖协议的认证控件。</summary>
    public void RefreshAuthenticationFields()
    {
        ConnectionAuthenticationFields authenticationFields = ResolveAuthenticationFields();
        bool usesUsername = authenticationFields.HasFlag(ConnectionAuthenticationFields.Username);
        bool usesPassword = authenticationFields.HasFlag(ConnectionAuthenticationFields.Password);
        _fields.SetFieldVisible(_usernameInput, usesUsername);
        _fields.SetFieldVisible(_passwordInput, usesPassword);
    }

    /// <summary>Validates and applies only endpoint/authentication properties. / 验证并仅应用目标与认证属性。</summary>
    public bool TryApplyTo(ConnectionProfile profile, out string? error)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ResetValidationState();

        string protocol = Protocol;
        if (!IsSupportedProtocol(protocol))
        {
            _protocolSelect.Status = AntdUI.TType.Error;
            error = L.Format("ConnectionEndpoint.Validation.UnsupportedProtocol", protocol);
            return false;
        }

        string target = _hostInput.Text.Trim();
        if (_requiresHost && target.Length == 0)
        {
            _hostInput.Status = AntdUI.TType.Error;
            error = L.Get("ConnectionEndpoint.Validation.TargetRequired");
            return false;
        }

        if (target.Any(character => char.IsControl(character)))
        {
            _hostInput.Status = AntdUI.TType.Error;
            error = L.Get("ConnectionEndpoint.Validation.TargetControlCharacters");
            return false;
        }

        int port = Decimal.ToInt32(_portInput.Value);
        if (_showPort && _requiresPort && port == 0)
        {
            _portInput.Status = AntdUI.TType.Error;
            error = L.Get("ConnectionEndpoint.Validation.PortRange");
            return false;
        }

        profile.Protocol = protocol;
        profile.Host = target;
        profile.Port = _showPort ? port : 0;

        ConnectionAuthenticationFields authenticationFields = ResolveAuthenticationFields();
        if (authenticationFields == ConnectionAuthenticationFields.None)
        {
            profile.Username = string.Empty;
            profile.Password = string.Empty;
            error = null;
            return true;
        }

        profile.Username = !authenticationFields.HasFlag(ConnectionAuthenticationFields.Username)
            ? string.Empty
            : _usernameInput.Text.Trim();
        profile.Password = !authenticationFields.HasFlag(ConnectionAuthenticationFields.Password)
            ? string.Empty
            : _passwordInput.Text;
        error = null;
        return true;
    }

    /// <summary>Clears endpoint validation errors. / 清除目标验证错误。</summary>
    public void ResetValidationState()
    {
        _protocolSelect.Status = AntdUI.TType.None;
        _hostInput.Status = AntdUI.TType.None;
        _portInput.Status = AntdUI.TType.None;
    }

    private void PopulateProtocolChoices(string? unsupportedProtocol = null)
    {
        _protocolSelect.Items.Clear();
        IReadOnlyList<string> supportedProtocols = _type.GetProtocols();
        foreach (string protocol in supportedProtocols)
        {
            _protocolSelect.Items.Add(new AntdUI.SelectItem(_type.ToProtocolDisplayName(protocol), protocol));
        }

        if (string.IsNullOrEmpty(unsupportedProtocol))
        {
            return;
        }

        if (supportedProtocols.Count == 0)
        {
            _protocolSelect.Items.Add(new AntdUI.SelectItem(L.Get("ConnectionEndpoint.NoProtocol"), string.Empty));
        }

        string displayValue = string.Concat(unsupportedProtocol.Select(character => char.IsControl(character) ? '�' : character));
        if (displayValue.Length > 80)
        {
            displayValue = displayValue[..77] + "...";
        }

        _protocolSelect.Items.Add(new AntdUI.SelectItem(
            L.Format("ConnectionEndpoint.UnsupportedLegacyValue", displayValue),
            unsupportedProtocol));
    }

    private bool IsSupportedProtocol(string protocol)
    {
        IReadOnlyList<string> supportedProtocols = _type.GetProtocols();
        return supportedProtocols.Count == 0
            ? protocol.Length == 0
            : supportedProtocols.Contains(protocol, StringComparer.OrdinalIgnoreCase);
    }

    private string NormalizeProtocolForEditor(string? rawProtocol)
    {
        return _type.NormalizeProtocol(rawProtocol);
    }

    private ConnectionAuthenticationFields ResolveAuthenticationFields()
    {
        if (!string.IsNullOrWhiteSpace(_customArgumentTemplate))
        {
            ConnectionAuthenticationFields customFields = ConnectionAuthenticationFields.None;
            if (_customArgumentTemplate.Contains("{username}", StringComparison.OrdinalIgnoreCase))
            {
                customFields |= ConnectionAuthenticationFields.Username;
            }

            if (_customArgumentTemplate.Contains("{password}", StringComparison.OrdinalIgnoreCase))
            {
                customFields |= ConnectionAuthenticationFields.Password;
            }

            return customFields;
        }

        return _nativeAuthenticationFields(Protocol);
    }

    private void HandleProtocolChanged(object sender, AntdUI.ObjectNEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _protocolSelect.Status = AntdUI.TType.None;
        string protocol = Protocol;
        int oldDefaultPort = _type.GetDefaultPort(_lastProtocol);
        int newDefaultPort = _type.GetDefaultPort(protocol);
        int currentPort = Decimal.ToInt32(_portInput.Value);
        if (_showPort && (currentPort == 0 || currentPort == oldDefaultPort))
        {
            _portInput.Value = newDefaultPort;
        }

        _lastProtocol = protocol;
        RefreshAuthenticationFields();
        ProtocolChanged?.Invoke(this, EventArgs.Empty);
    }

}
