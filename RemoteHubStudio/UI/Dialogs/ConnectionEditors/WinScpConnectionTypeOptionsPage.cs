using RemoteHubStudio.Domain;
using RemoteHubStudio.Localization;
using RemoteHubStudio.UI.Controls;

namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

/// <summary>Edits WinSCP remote-path options and normalizes legacy keys. / 编辑 WinSCP 远程路径选项并规范化旧版键。</summary>
public sealed partial class WinScpConnectionTypeOptionsPage : ConnectionTypeOptionsPage
{
    private static readonly IReadOnlyCollection<string> OptionKeys = ["remotePath", "path", "webDavAddress", "dav_address"];

    /// <summary>Initializes the WinSCP options page. / 初始化 WinSCP 选项子页。</summary>
    public WinScpConnectionTypeOptionsPage()
    {
        InitializeComponent();
        ConfigureRuntimeLayout();
        _endpoint.Configure(
            ConnectionType.WinScp,
            L.Get("ConnectionEndpoint.TransferProtocol"),
            L.Get("ConnectionEndpoint.Server"),
            L.Get("ConnectionEndpoint.HostPlaceholder"),
            _ => ConnectionAuthenticationFields.UsernameAndPassword);
        _remotePathInput.PlaceholderText = L.Get("ConnectionOptions.WinScp.RemotePathPlaceholder");
        _webDavAddressInput.PlaceholderText = L.Get("ConnectionOptions.WinScp.WebDavAddressPlaceholder");
        _remotePathLabel.Text = L.Get("ConnectionOptions.WinScp.RemotePath");
        _sessionOptionsGrid.RegisterField(_remotePathLabel, _remotePathInput);
        _webDavAddressLabel.Text = L.Get("ConnectionOptions.WinScp.WebDavAddress");
        _sessionOptionsGrid.RegisterField(_webDavAddressLabel, _webDavAddressInput);
        _endpoint.ProtocolChanged += HandleProtocolChanged;
        UpdateProtocolDependentFields();
    }

    /// <inheritdoc />
    public override ConnectionType Type => ConnectionType.WinScp;

    /// <inheritdoc />
    public override string SectionTitle => L.Get("ConnectionOptions.WinScp.Title");

    /// <inheritdoc />
    public override IReadOnlyCollection<string> ManagedOptionKeys => OptionKeys;

    /// <inheritdoc />
    public override string SuggestedName => _endpoint.Target;

    /// <inheritdoc />
    public override void LoadFrom(ConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _endpoint.LoadFrom(profile);
        _remotePathInput.Text = ReadOption(profile, "remotePath", "path") ?? string.Empty;
        _webDavAddressInput.Text = ReadOption(profile, "webDavAddress", "dav_address") ?? string.Empty;
        UpdateProtocolDependentFields();
        ResetValidationState();
    }

    /// <inheritdoc />
    public override bool TryApplyTo(ConnectionProfile profile, out string? error)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ResetValidationState();
        if (!_endpoint.TryApplyTo(profile, out error))
        {
            return false;
        }

        string remotePath = _remotePathInput.Text.Trim();
        bool webDavMode = profile.Protocol is "webdav" or "webdavs";
        string webDavAddress = webDavMode ? _webDavAddressInput.Text.Trim() : string.Empty;
        if (remotePath.Contains('\0') || remotePath.Contains('\r') || remotePath.Contains('\n'))
        {
            _remotePathInput.Status = AntdUI.TType.Error;
            error = L.Get("ConnectionOptions.WinScp.Validation.RemotePathControlCharacters");
            return false;
        }

        if (webDavAddress.Length > 0 &&
            (!Uri.TryCreate(webDavAddress, UriKind.Absolute, out Uri? parsedAddress) ||
             parsedAddress.Scheme is not ("http" or "https" or "dav" or "davs") ||
             string.IsNullOrWhiteSpace(parsedAddress.Host) ||
             !string.IsNullOrEmpty(parsedAddress.UserInfo) ||
             !string.IsNullOrEmpty(parsedAddress.Fragment)))
        {
            _webDavAddressInput.Status = AntdUI.TType.Error;
            error = L.Get("ConnectionOptions.WinScp.Validation.WebDavAddress");
            return false;
        }

        if (webDavAddress.Length > 0 &&
            !IsCompatibleWebDavScheme(profile.Protocol, new Uri(webDavAddress, UriKind.Absolute).Scheme))
        {
            _webDavAddressInput.Status = AntdUI.TType.Error;
            error = L.Get("ConnectionOptions.WinScp.Validation.WebDavScheme");
            return false;
        }

        RemoveManagedOptions(profile);
        if (remotePath.Length > 0)
        {
            WriteOption(profile, "remotePath", remotePath);
        }

        if (webDavAddress.Length > 0)
        {
            WriteOption(profile, "webDavAddress", webDavAddress);
        }

        error = null;
        return true;
    }

    private static bool IsCompatibleWebDavScheme(string protocol, string scheme)
    {
        return protocol.Trim().ToLowerInvariant() switch
        {
            "webdav" => scheme is "http" or "dav",
            "webdavs" => scheme is "https" or "davs",
            _ => false
        };
    }

    /// <inheritdoc />
    public override void ResetValidationState()
    {
        _endpoint.ResetValidationState();
        _remotePathInput.Status = AntdUI.TType.None;
        _webDavAddressInput.Status = AntdUI.TType.None;
    }

    /// <inheritdoc />
    public override void UpdateCustomArgumentTemplate(string? template)
    {
        base.UpdateCustomArgumentTemplate(template);
        _endpoint.SetCustomArgumentTemplate(template);
    }

    private void HandleProtocolChanged(object? sender, EventArgs e)
    {
        UpdateProtocolDependentFields();
        OnEditorRequirementsChanged();
    }

    private void UpdateProtocolDependentFields()
    {
        _sessionOptionsGrid.SetFieldVisible(_webDavAddressInput, _endpoint.Protocol is "webdav" or "webdavs");
    }
}
