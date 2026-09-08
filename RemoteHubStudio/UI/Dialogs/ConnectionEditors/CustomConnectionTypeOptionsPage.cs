using RemoteHubStudio.Domain;
using RemoteHubStudio.Localization;

namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

/// <summary>Edits the target and optional port used by a custom command template. / 编辑自定义命令模板使用的目标及可选端口。</summary>
public sealed partial class CustomConnectionTypeOptionsPage : ConnectionTypeOptionsPage
{
    private static readonly IReadOnlyCollection<string> NoManagedOptionKeys = Array.Empty<string>();

    /// <summary>Initializes a custom-command page. / 初始化自定义命令页面。</summary>
    public CustomConnectionTypeOptionsPage()
    {
        InitializeComponent();
        ConfigureRuntimeLayout();
        _endpoint.Configure(
            ConnectionType.Custom,
            L.Get("ConnectionEndpoint.Protocol"),
            L.Get("ConnectionEndpoint.Target"),
            L.Get("ConnectionEndpoint.CommandTargetPlaceholder"),
            static _ => ConnectionAuthenticationFields.None,
            showPort: true,
            requiresPort: false,
            requiresHost: true);
        _endpoint.ProtocolChanged += HandleProtocolChanged;
    }

    /// <inheritdoc />
    public override ConnectionType Type => ConnectionType.Custom;

    /// <inheritdoc />
    public override string SectionTitle => L.Get("ConnectionOptions.Custom.Title");

    /// <inheritdoc />
    public override IReadOnlyCollection<string> ManagedOptionKeys => NoManagedOptionKeys;

    /// <inheritdoc />
    public override string SuggestedName => _endpoint.Target;

    /// <inheritdoc />
    public override bool ShowsPrivateKey => CustomArgumentsUse("{key}");

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
