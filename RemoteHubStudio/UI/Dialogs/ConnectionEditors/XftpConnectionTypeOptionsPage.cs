using RemoteHubStudio.Domain;
using RemoteHubStudio.Localization;

namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

/// <summary>Edits Xftp endpoint, transfer protocol, and authentication values. / 编辑 Xftp 目标、传输协议及认证值。</summary>
public sealed partial class XftpConnectionTypeOptionsPage : ConnectionTypeOptionsPage
{
    private static readonly IReadOnlyCollection<string> NoManagedOptionKeys = Array.Empty<string>();

    /// <summary>Initializes an Xftp page. / 初始化 Xftp 页面。</summary>
    public XftpConnectionTypeOptionsPage()
    {
        InitializeComponent();
        ConfigureRuntimeLayout();
        _endpoint.Configure(
            ConnectionType.Xftp,
            L.Get("ConnectionEndpoint.TransferProtocol"),
            L.Get("ConnectionEndpoint.TargetHost"),
            L.Get("ConnectionEndpoint.HostPlaceholder"),
            static _ => ConnectionAuthenticationFields.UsernameAndPassword);
        _endpoint.ProtocolChanged += HandleProtocolChanged;
    }

    /// <inheritdoc />
    public override ConnectionType Type => ConnectionType.Xftp;

    /// <inheritdoc />
    public override string SectionTitle => L.Get("ConnectionOptions.Xftp.Title");

    /// <inheritdoc />
    public override IReadOnlyCollection<string> ManagedOptionKeys => NoManagedOptionKeys;

    /// <inheritdoc />
    public override string SuggestedName => _endpoint.Target;

    /// <inheritdoc />
    public override void LoadFrom(ConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _endpoint.LoadFrom(profile);
        UpdateCustomArgumentTemplate(profile.CustomArguments);
    }

    /// <inheritdoc />
    public override bool TryApplyTo(ConnectionProfile profile, out string? error)
    {
        return _endpoint.TryApplyTo(profile, out error);
    }

    /// <inheritdoc />
    public override void ResetValidationState()
    {
        _endpoint.ResetValidationState();
    }

    /// <inheritdoc />
    public override void UpdateCustomArgumentTemplate(string? template)
    {
        _endpoint.SetCustomArgumentTemplate(template);
        base.UpdateCustomArgumentTemplate(template);
    }

    private void HandleProtocolChanged(object? sender, EventArgs e)
    {
        OnEditorRequirementsChanged();
    }
}
