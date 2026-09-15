using RemoteHubStudio.Domain;
using RemoteHubStudio.Localization;

namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

/// <summary>Edits a ToDesk device identifier and access password. / 编辑 ToDesk 设备 ID 及访问密码。</summary>
public sealed partial class ToDeskConnectionTypeOptionsPage : ConnectionTypeOptionsPage
{
    private static readonly IReadOnlyCollection<string> NoManagedOptionKeys = Array.Empty<string>();

    /// <summary>Initializes a ToDesk page. / 初始化 ToDesk 页面。</summary>
    public ToDeskConnectionTypeOptionsPage()
    {
        InitializeComponent();
        ConfigureRuntimeLayout();
        _endpoint.Configure(
            ConnectionType.ToDesk,
            L.Get("ConnectionEndpoint.Mode"),
            L.Get("ConnectionEndpoint.DeviceId"),
            L.Get("ConnectionOptions.ToDesk.DeviceIdPlaceholder"),
            static _ => ConnectionAuthenticationFields.Password,
            showPort: false,
            requiresPort: false,
            passwordLabel: L.Get("ConnectionEndpoint.AccessPassword"));
        _endpoint.ProtocolChanged += HandleProtocolChanged;
    }

    /// <inheritdoc />
    public override ConnectionType Type => ConnectionType.ToDesk;

    /// <inheritdoc />
    public override string SectionTitle => L.Get("ConnectionOptions.ToDesk.Title");

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
