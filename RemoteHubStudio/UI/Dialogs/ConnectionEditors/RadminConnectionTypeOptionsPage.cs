using System.Globalization;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Localization;
using RemoteHubStudio.UI.Controls;

namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

/// <summary>Edits Radmin display and connection options. / 编辑 Radmin 显示与连接选项。</summary>
public sealed partial class RadminConnectionTypeOptionsPage : ConnectionTypeOptionsPage
{
    private const string DefaultColorDepth = "24bpp";
    private static readonly string[] AllowedColorDepths = ["24bpp", "16bpp", "8bpp", "4bpp", "2bpp", "1bpp"];
    private static readonly IReadOnlyCollection<string> OptionKeys =
    [
        "encrypt",
        "fullscreen",
        "fullScreen",
        "noFullKeyboardControl",
        "nofullkbcontrol",
        "colorDepth",
        "colorMode",
        "color_mode",
        "updates",
        "frameRate"
    ];

    /// <summary>Initializes the Radmin options page. / 初始化 Radmin 选项子页。</summary>
    public RadminConnectionTypeOptionsPage()
    {
        InitializeComponent();
        ConfigureRuntimeLayout();
        _endpoint.Configure(
            ConnectionType.Radmin,
            L.Get("ConnectionOptions.Radmin.ActionMode"),
            L.Get("ConnectionOptions.Radmin.TargetHost"),
            L.Get("ConnectionEndpoint.HostPlaceholder"),
            _ => ConnectionAuthenticationFields.None);
        _encryptSwitch.AccessibleName = L.Get("ConnectionOptions.Radmin.Encrypt");
        _fullScreenSwitch.AccessibleName = L.Get("ConnectionOptions.Common.FullScreen");
        _noFullKeyboardControlSwitch.AccessibleName = L.Get("ConnectionOptions.Radmin.DisableFullKeyboardControl");
        _colorDepthSelect.PlaceholderText = L.Get("ConnectionOptions.Common.ColorDepth");
        foreach (string colorDepth in AllowedColorDepths)
        {
            _colorDepthSelect.Items.Add(new AntdUI.SelectItem(colorDepth.ToUpperInvariant(), colorDepth));
        }

        _colorDepthSelect.SelectedValue = DefaultColorDepth;

        _encryptLabel.Text = L.Get("ConnectionOptions.Radmin.Encrypt");
        _optionsGrid.RegisterField(_encryptLabel, _encryptSwitch);
        _fullScreenLabel.Text = L.Get("ConnectionOptions.Common.FullScreen");
        _optionsGrid.RegisterField(_fullScreenLabel, _fullScreenSwitch);
        _noFullKeyboardControlLabel.Text = L.Get("ConnectionOptions.Radmin.KeyboardRestriction");
        _optionsGrid.RegisterField(_noFullKeyboardControlLabel, _noFullKeyboardControlSwitch);
        _colorDepthLabel.Text = L.Get("ConnectionOptions.Common.ColorDepth");
        _optionsGrid.RegisterField(_colorDepthLabel, _colorDepthSelect);
        _updatesLabel.Text = L.Get("ConnectionOptions.Radmin.UpdatesPerSecond");
        _optionsGrid.RegisterField(_updatesLabel, _updatesInput);
        _endpoint.ProtocolChanged += HandleProtocolChanged;
        UpdateModeDependentFields();
    }

    /// <inheritdoc />
    public override ConnectionType Type => ConnectionType.Radmin;

    /// <inheritdoc />
    public override string SectionTitle => L.Get("ConnectionOptions.Radmin.Title");

    /// <inheritdoc />
    public override IReadOnlyCollection<string> ManagedOptionKeys => OptionKeys;

    /// <inheritdoc />
    public override string SuggestedName => _endpoint.Target;

    /// <inheritdoc />
    public override void LoadFrom(ConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _endpoint.LoadFrom(profile);
        _encryptSwitch.Checked = ReadBooleanOption(profile, false, "encrypt");
        _fullScreenSwitch.Checked = ReadBooleanOption(profile, false, "fullscreen", "fullScreen");
        _noFullKeyboardControlSwitch.Checked = ReadBooleanOption(
            profile,
            false,
            "noFullKeyboardControl",
            "nofullkbcontrol");

        string colorDepth = (ReadOption(profile, "colorDepth", "colorMode", "color_mode") ?? DefaultColorDepth)
            .Trim()
            .ToLowerInvariant();
        _colorDepthSelect.SelectedValue = AllowedColorDepths.Contains(colorDepth, StringComparer.OrdinalIgnoreCase)
            ? colorDepth
            : DefaultColorDepth;
        _updatesInput.Value = ReadIntegerOption(profile, 30, 1, 100, "updates", "frameRate");
        UpdateModeDependentFields();
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

        bool usesDisplayOptions = UsesDisplayOptions(_endpoint.Protocol);
        string colorDepth = _colorDepthSelect.SelectedValue as string ?? DefaultColorDepth;
        if (usesDisplayOptions && !AllowedColorDepths.Contains(colorDepth, StringComparer.OrdinalIgnoreCase))
        {
            error = L.Get("ConnectionOptions.Radmin.Validation.ColorDepth");
            return false;
        }

        int updates = Decimal.ToInt32(_updatesInput.Value);
        if (usesDisplayOptions && (updates is < 1 or > 100))
        {
            _updatesInput.Status = AntdUI.TType.Error;
            error = L.Get("ConnectionOptions.Radmin.Validation.UpdatesPerSecond");
            return false;
        }

        RemoveManagedOptions(profile);
        WriteBooleanOption(profile, "encrypt", _encryptSwitch.Checked);
        if (usesDisplayOptions)
        {
            WriteBooleanOption(profile, "fullscreen", _fullScreenSwitch.Checked);
            WriteBooleanOption(profile, "noFullKeyboardControl", _noFullKeyboardControlSwitch.Checked);
            WriteOption(profile, "colorDepth", colorDepth.ToLowerInvariant());
            WriteOption(profile, "updates", updates.ToString(CultureInfo.InvariantCulture));
        }

        error = null;
        return true;
    }

    /// <inheritdoc />
    public override void ResetValidationState()
    {
        _endpoint.ResetValidationState();
        _updatesInput.Status = AntdUI.TType.None;
    }

    private void HandleProtocolChanged(object? sender, EventArgs e)
    {
        UpdateModeDependentFields();
        OnEditorRequirementsChanged();
    }

    private void UpdateModeDependentFields()
    {
        bool visible = UsesDisplayOptions(_endpoint.Protocol);
        _optionsGrid.SetFieldVisible(_fullScreenSwitch, visible);
        _optionsGrid.SetFieldVisible(_noFullKeyboardControlSwitch, visible);
        _optionsGrid.SetFieldVisible(_colorDepthSelect, visible);
        _optionsGrid.SetFieldVisible(_updatesInput, visible);
    }

    private static bool UsesDisplayOptions(string protocol) => protocol is "control" or "view";

    /// <inheritdoc />
    public override void UpdateCustomArgumentTemplate(string? template)
    {
        base.UpdateCustomArgumentTemplate(template);
        _endpoint.SetCustomArgumentTemplate(template);
    }
}
