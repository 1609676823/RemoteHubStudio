using RemoteHubStudio.Domain;
using RemoteHubStudio.Localization;

namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

/// <summary>Edits MobaXterm endpoint, protocol, and authentication values. / 编辑 MobaXterm 目标、协议及认证值。</summary>
public sealed partial class MobaXtermConnectionTypeOptionsPage : ConnectionTypeOptionsPage
{
    private static readonly IReadOnlyCollection<string> NoManagedOptionKeys = Array.Empty<string>();

    /// <summary>Initializes a MobaXterm page. / 初始化 MobaXterm 页面。</summary>
    public MobaXtermConnectionTypeOptionsPage()
    {
        InitializeComponent();
        ConfigureRuntimeLayout();
        _endpoint.Configure(
            ConnectionType.MobaXterm,
            L.Get("ConnectionEndpoint.Protocol"),
            L.Get("ConnectionEndpoint.TargetHost"),
            L.Get("ConnectionEndpoint.HostPlaceholder"),
            static protocol => protocol == "ssh"
                ? ConnectionAuthenticationFields.UsernameAndPassword
                : ConnectionAuthenticationFields.None);
        _endpoint.ProtocolChanged += HandleProtocolChanged;
    }

    /// <inheritdoc />
    public override ConnectionType Type => ConnectionType.MobaXterm;

    /// <inheritdoc />
    public override string SectionTitle => L.Get("ConnectionOptions.MobaXterm.Title");

    /// <inheritdoc />
    public override IReadOnlyCollection<string> ManagedOptionKeys => NoManagedOptionKeys;

    /// <inheritdoc />
    public override string SuggestedName => _endpoint.Target;

    /// <inheritdoc />
    public override bool ShowsPrivateKey => HasCustomArguments
        ? CustomArgumentsUse("{key}")
        : _endpoint.Protocol == "ssh";

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
