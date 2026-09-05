using System.Windows.Forms;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Localization;
using RemoteHubStudio.UI.Controls;

namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

/// <summary>Edits RustDesk rendezvous-server, public-key, and relay preferences. / 编辑 RustDesk 会合服务器、公钥与中继偏好。</summary>
public sealed partial class RustDeskConnectionTypeOptionsPage : ConnectionTypeOptionsPage
{
    private static readonly IReadOnlyCollection<string> OptionKeys =
        ["server", "serverKey", "server_key", "forceRelay", "relay", "force_relay"];

    /// <summary>Initializes the RustDesk options page. / 初始化 RustDesk 选项子页。</summary>
    public RustDeskConnectionTypeOptionsPage()
    {
        InitializeComponent();
        ConfigureRuntimeLayout();
        _serverInput.PlaceholderText = L.Get("ConnectionOptions.RustDesk.ServerPlaceholder");
        _serverKeyInput.PlaceholderText = L.Get("ConnectionOptions.RustDesk.ServerKeyPlaceholder");
        _forceRelaySwitch.AccessibleName = L.Get("ConnectionOptions.RustDesk.ForceRelay");
        _endpoint.Configure(
            ConnectionType.RustDesk,
            L.Get("ConnectionOptions.RustDesk.ConnectionMode"),
            L.Get("ConnectionOptions.RustDesk.RemoteDevice"),
            L.Get("ConnectionOptions.RustDesk.TargetPlaceholder"),
            _ => _serverKeyInput.Text.Trim().Length == 0
                ? ConnectionAuthenticationFields.Password
                : ConnectionAuthenticationFields.None,
            showPort: false,
            requiresPort: false,
            passwordLabel: L.Get("ConnectionOptions.RustDesk.OneTimePassword"));

        _serverLabel.Text = L.Get("ConnectionEndpoint.Server");
        _optionsGrid.RegisterField(_serverLabel, _serverInput);
        _serverKeyLabel.Text = L.Get("ConnectionOptions.RustDesk.ServerKey");
        _optionsGrid.RegisterField(_serverKeyLabel, _serverKeyInput);
        _forceRelayLabel.Text = L.Get("ConnectionOptions.RustDesk.ForceRelay");
        _optionsGrid.RegisterField(_forceRelayLabel, _forceRelaySwitch);

        _compatibilityNote.Text = L.Get("ConnectionOptions.RustDesk.ServerKeyNote");

        _endpoint.ProtocolChanged += (_, _) => OnEditorRequirementsChanged();
        _serverKeyInput.TextChanged += HandleServerKeyChanged;
    }

    /// <inheritdoc />
    public override ConnectionType Type => ConnectionType.RustDesk;

    /// <inheritdoc />
    public override string SectionTitle => L.Get("ConnectionOptions.RustDesk.Title");

    /// <inheritdoc />
    public override IReadOnlyCollection<string> ManagedOptionKeys => OptionKeys;

    /// <inheritdoc />
    public override string SuggestedName => _endpoint.Target;

    /// <inheritdoc />
    public override void LoadFrom(ConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _endpoint.LoadFrom(profile);
        _serverInput.Text = ReadOption(profile, "server") ?? string.Empty;
        _serverKeyInput.Text = ReadOption(profile, "serverKey", "server_key") ?? string.Empty;
        _forceRelaySwitch.Checked = ReadBooleanOption(profile, false, "forceRelay", "relay", "force_relay");
        _endpoint.RefreshAuthenticationFields();
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

        string server = _serverInput.Text.Trim();
        string serverKey = _serverKeyInput.Text.Trim();

        if (ContainsControlCharacter(server))
        {
            _serverInput.Status = AntdUI.TType.Error;
            error = L.Get("ConnectionOptions.RustDesk.Validation.ServerControlCharacters");
            return false;
        }

        if (server.Length > 0 &&
            (server.StartsWith("-", StringComparison.Ordinal) ||
             server.Any(char.IsWhiteSpace) ||
             server.IndexOfAny(['@', '?', '#', '&', '=', '/', '\\']) >= 0))
        {
            _serverInput.Status = AntdUI.TType.Error;
            error = L.Get("ConnectionOptions.RustDesk.Validation.ServerUnsafeCharacters");
            return false;
        }

        if (ContainsControlCharacter(serverKey))
        {
            _serverKeyInput.Status = AntdUI.TType.Error;
            error = L.Get("ConnectionOptions.RustDesk.Validation.KeyControlCharacters");
            return false;
        }

        if (serverKey.Any(character => char.IsWhiteSpace(character) || character is '?' or '#' or '&' or '@' or '\\'))
        {
            _serverKeyInput.Status = AntdUI.TType.Error;
            error = L.Get("ConnectionOptions.RustDesk.Validation.KeyUnsafeCharacters");
            return false;
        }

        if (serverKey.Length > 0 && server.Length == 0)
        {
            _serverInput.Status = AntdUI.TType.Error;
            _serverKeyInput.Status = AntdUI.TType.Error;
            error = L.Get("ConnectionOptions.RustDesk.Validation.KeyRequiresServer");
            return false;
        }

        RemoveManagedOptions(profile);
        if (server.Length > 0)
        {
            WriteOption(profile, "server", server);
        }

        if (serverKey.Length > 0)
        {
            WriteOption(profile, "serverKey", serverKey);
        }

        WriteBooleanOption(profile, "forceRelay", _forceRelaySwitch.Checked);
        error = null;
        return true;
    }

    /// <inheritdoc />
    public override void ResetValidationState()
    {
        _endpoint.ResetValidationState();
        _serverInput.Status = AntdUI.TType.None;
        _serverKeyInput.Status = AntdUI.TType.None;
    }

    /// <inheritdoc />
    public override void UpdateCustomArgumentTemplate(string? template)
    {
        base.UpdateCustomArgumentTemplate(template);
        _endpoint.SetCustomArgumentTemplate(template);
    }

    private void HandleServerKeyChanged(object? sender, EventArgs e)
    {
        _endpoint.RefreshAuthenticationFields();
        OnEditorRequirementsChanged();
    }

    /// <summary>Checks for control characters that are unsafe in a client option. / 检查客户端选项中不安全的控制字符。</summary>
    private static bool ContainsControlCharacter(string value)
    {
        return value.Any(char.IsControl);
    }
}
