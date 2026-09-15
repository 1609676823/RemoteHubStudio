using RemoteHubStudio.Domain;
using RemoteHubStudio.Localization;

namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

/// <summary>Edits Xshell endpoint, protocol, and authentication values. / 编辑 Xshell 目标、协议及认证值。</summary>
public sealed partial class XshellConnectionTypeOptionsPage : ConnectionTypeOptionsPage
{
    private static readonly IReadOnlyCollection<string> NoManagedOptionKeys = Array.Empty<string>();

    /// <summary>Initializes an Xshell page. / 初始化 Xshell 页面。</summary>
    public XshellConnectionTypeOptionsPage()
    {
        InitializeComponent();
        ConfigureRuntimeLayout();
        _endpoint.Configure(
            ConnectionType.Xshell,
            L.Get("ConnectionEndpoint.Protocol"),
            L.Get("ConnectionEndpoint.TargetHost"),
            L.Get("ConnectionEndpoint.HostPlaceholder"),
            static _ => ConnectionAuthenticationFields.UsernameAndPassword);
        _endpoint.ProtocolChanged += HandleProtocolChanged;
    }

    /// <inheritdoc />
    public override ConnectionType Type => ConnectionType.Xshell;

    /// <inheritdoc />
    public override string SectionTitle => L.Get("ConnectionOptions.Xshell.Title");

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
