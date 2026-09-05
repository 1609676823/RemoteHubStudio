using RemoteHubStudio.Domain;
using RemoteHubStudio.Localization;
using RemoteHubStudio.UI.Controls;

namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

/// <summary>Edits options shared by supported VNC viewers. / 编辑受支持的 VNC 查看器共用选项。</summary>
public sealed partial class VncConnectionTypeOptionsPage : ConnectionTypeOptionsPage
{
    private static readonly IReadOnlyCollection<string> OptionKeys =
        ["fullscreen", "fullScreen", "autoReconnect", "autoreconnect", "viewOnly", "viewonly"];

    /// <summary>Initializes the VNC options page. / 初始化 VNC 选项子页。</summary>
    public VncConnectionTypeOptionsPage()
    {
        InitializeComponent();
        ConfigureRuntimeLayout();
        _endpoint.Configure(
            ConnectionType.Vnc,
            L.Get("ConnectionOptions.Vnc.Viewer"),
            L.Get("ConnectionOptions.Vnc.RemoteHost"),
            L.Get("ConnectionEndpoint.HostPlaceholder"),
            protocol => protocol is "tightvnc" or "ultravnc"
                ? ConnectionAuthenticationFields.Password
                : ConnectionAuthenticationFields.None,
            passwordLabel: L.Get("ConnectionEndpoint.AccessPassword"));
        _fullScreenSwitch.AccessibleName = L.Get("ConnectionOptions.Common.FullScreen");
        _autoReconnectSwitch.AccessibleName = L.Get("ConnectionOptions.Common.AutoReconnect");
        _viewOnlySwitch.AccessibleName = L.Get("ConnectionOptions.Vnc.ViewOnly");

        _fullScreenLabel.Text = L.Get("ConnectionOptions.Common.FullScreen");
        _viewerOptionsGrid.RegisterField(_fullScreenLabel, _fullScreenSwitch);
        _autoReconnectLabel.Text = L.Get("ConnectionOptions.Common.AutoReconnect");
        _viewerOptionsGrid.RegisterField(_autoReconnectLabel, _autoReconnectSwitch);
        _viewOnlyLabel.Text = L.Get("ConnectionOptions.Vnc.ViewOnly");
        _viewerOptionsGrid.RegisterField(_viewOnlyLabel, _viewOnlySwitch);
        _endpoint.ProtocolChanged += HandleProtocolChanged;
        UpdateViewerOptionsVisibility();
    }

    /// <inheritdoc />
    public override ConnectionType Type => ConnectionType.Vnc;

    /// <inheritdoc />
    public override string SectionTitle => L.Get("ConnectionOptions.Vnc.Title");

    /// <inheritdoc />
    public override IReadOnlyCollection<string> ManagedOptionKeys => OptionKeys;

    /// <inheritdoc />
    public override string SuggestedName => _endpoint.Target;

    /// <inheritdoc />
    public override void LoadFrom(ConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _endpoint.LoadFrom(profile);
        _fullScreenSwitch.Checked = ReadBooleanOption(profile, false, "fullscreen", "fullScreen");
        _autoReconnectSwitch.Checked = ReadBooleanOption(profile, true, "autoReconnect", "autoreconnect");
        _viewOnlySwitch.Checked = ReadBooleanOption(profile, false, "viewOnly", "viewonly");
        UpdateViewerOptionsVisibility();
    }

    /// <inheritdoc />
    public override bool TryApplyTo(ConnectionProfile profile, out string? error)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!_endpoint.TryApplyTo(profile, out error))
        {
            return false;
        }

        RemoveManagedOptions(profile);
        if (_endpoint.Protocol == "ultravnc")
        {
            WriteBooleanOption(profile, "fullscreen", _fullScreenSwitch.Checked);
            WriteBooleanOption(profile, "autoReconnect", _autoReconnectSwitch.Checked);
            WriteBooleanOption(profile, "viewOnly", _viewOnlySwitch.Checked);
        }

        error = null;
        return true;
    }

    /// <inheritdoc />
    public override void ResetValidationState()
    {
        _endpoint.ResetValidationState();
    }

    /// <inheritdoc />
    public override void UpdateCustomArgumentTemplate(string? template)
    {
        base.UpdateCustomArgumentTemplate(template);
        _endpoint.SetCustomArgumentTemplate(template);
    }

    private void HandleProtocolChanged(object? sender, EventArgs e)
    {
        UpdateViewerOptionsVisibility();
        OnEditorRequirementsChanged();
    }

    private void UpdateViewerOptionsVisibility()
    {
        _viewerOptionsGrid.Visible = _endpoint.Protocol == "ultravnc";
    }
}
