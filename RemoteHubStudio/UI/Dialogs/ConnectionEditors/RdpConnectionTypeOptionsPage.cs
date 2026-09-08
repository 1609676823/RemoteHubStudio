using RemoteHubStudio.Domain;
using RemoteHubStudio.Localization;
using RemoteHubStudio.UI.Controls;

namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

/// <summary>Edits all supported Remote Desktop file and launch options. / 编辑所有受支持的远程桌面文件与启动选项。</summary>
public sealed partial class RdpConnectionTypeOptionsPage : ConnectionTypeOptionsPage
{
    private static readonly IReadOnlyCollection<string> NoManagedOptionKeys = Array.Empty<string>();

    /// <summary>Initializes the complete RDP options page. / 初始化完整的 RDP 选项子页。</summary>
    public RdpConnectionTypeOptionsPage()
    {
        InitializeComponent();
        ConfigureRuntimeLayout();
        _endpoint.Configure(
            ConnectionType.RemoteDesktop,
            L.Get("ConnectionEndpoint.Protocol"),
            L.Get("ConnectionOptions.Rdp.Computer"),
            L.Get("ConnectionEndpoint.HostPlaceholder"),
            _ => ConnectionAuthenticationFields.UsernameAndPassword);
        _fullScreenSwitch.AccessibleName = L.Get("ConnectionOptions.Common.FullScreen");
        _allMonitorsSwitch.AccessibleName = L.Get("ConnectionOptions.Rdp.AllMonitors");

        _colorDepthSelect.PlaceholderText = L.Get("ConnectionOptions.Common.ColorDepth");
        foreach (int depth in new[] { 15, 16, 24, 32 })
        {
            _colorDepthSelect.Items.Add(new AntdUI.SelectItem($"{depth} bit", depth));
        }

        _displayConnectionBarSwitch.AccessibleName = L.Get("ConnectionOptions.Rdp.DisplayConnectionBar");
        _compressionSwitch.AccessibleName = L.Get("ConnectionOptions.Rdp.EnableCompression");
        _keyboardHookSelect.PlaceholderText = L.Get("ConnectionOptions.Rdp.WindowsKeyCombinations");
        _keyboardHookSelect.Items.Add(new AntdUI.SelectItem(L.Get("ConnectionOptions.Rdp.Keyboard.Local"), RdpKeyboardHookMode.Local));
        _keyboardHookSelect.Items.Add(new AntdUI.SelectItem(L.Get("ConnectionOptions.Rdp.Keyboard.Remote"), RdpKeyboardHookMode.Remote));
        _keyboardHookSelect.Items.Add(new AntdUI.SelectItem(L.Get("ConnectionOptions.Rdp.Keyboard.FullScreenOnly"), RdpKeyboardHookMode.FullScreenOnly));

        _clipboardSwitch.AccessibleName = L.Get("ConnectionOptions.Rdp.ClipboardRedirection");
        _drivesSwitch.AccessibleName = L.Get("ConnectionOptions.Rdp.DriveRedirection");
        _printersSwitch.AccessibleName = L.Get("ConnectionOptions.Rdp.PrinterRedirection");
        _smartCardsSwitch.AccessibleName = L.Get("ConnectionOptions.Rdp.SmartCardRedirection");
        _comPortsSwitch.AccessibleName = L.Get("ConnectionOptions.Rdp.ComPortRedirection");
        _posDevicesSwitch.AccessibleName = L.Get("ConnectionOptions.Rdp.PosDeviceRedirection");
        _camerasSwitch.AccessibleName = L.Get("ConnectionOptions.Rdp.CameraRedirection");
        _microphoneSwitch.AccessibleName = L.Get("ConnectionOptions.Rdp.MicrophoneRedirection");

        _audioModeSelect.PlaceholderText = L.Get("ConnectionOptions.Rdp.RemoteAudio");
        _audioModeSelect.Items.Add(new AntdUI.SelectItem(L.Get("ConnectionOptions.Rdp.Audio.Local"), RdpAudioMode.Local));
        _audioModeSelect.Items.Add(new AntdUI.SelectItem(L.Get("ConnectionOptions.Rdp.Audio.Remote"), RdpAudioMode.Remote));
        _audioModeSelect.Items.Add(new AntdUI.SelectItem(L.Get("ConnectionOptions.Rdp.Audio.Disabled"), RdpAudioMode.Disabled));

        _administrativeSessionSwitch.AccessibleName = L.Get("ConnectionOptions.Rdp.AdministrativeSession");
        _promptForCredentialsSwitch.AccessibleName = L.Get("ConnectionOptions.Rdp.PromptForCredentials");
        _disableWallpaperSwitch.AccessibleName = L.Get("ConnectionOptions.Rdp.DisableWallpaper");
        _autoReconnectSwitch.AccessibleName = L.Get("ConnectionOptions.Common.AutoReconnect");

        _fullScreenLabel.Text = L.Get("ConnectionOptions.Common.FullScreen");
        _optionsGrid.RegisterField(_fullScreenLabel, _fullScreenSwitch);
        _allMonitorsLabel.Text = L.Get("ConnectionOptions.Rdp.AllMonitors");
        _optionsGrid.RegisterField(_allMonitorsLabel, _allMonitorsSwitch);
        _desktopWidthLabel.Text = L.Get("ConnectionOptions.Rdp.DesktopWidth");
        _optionsGrid.RegisterField(_desktopWidthLabel, _desktopWidthInput);
        _desktopHeightLabel.Text = L.Get("ConnectionOptions.Rdp.DesktopHeight");
        _optionsGrid.RegisterField(_desktopHeightLabel, _desktopHeightInput);
        _colorDepthLabel.Text = L.Get("ConnectionOptions.Common.ColorDepth");
        _optionsGrid.RegisterField(_colorDepthLabel, _colorDepthSelect);
        _displayConnectionBarLabel.Text = L.Get("ConnectionOptions.Rdp.ConnectionBar");
        _optionsGrid.RegisterField(_displayConnectionBarLabel, _displayConnectionBarSwitch);
        _compressionLabel.Text = L.Get("ConnectionOptions.Rdp.Compression");
        _optionsGrid.RegisterField(_compressionLabel, _compressionSwitch);
        _keyboardHookLabel.Text = L.Get("ConnectionOptions.Rdp.KeyboardHook");
        _optionsGrid.RegisterField(_keyboardHookLabel, _keyboardHookSelect);
        _clipboardLabel.Text = L.Get("ConnectionOptions.Rdp.Clipboard");
        _optionsGrid.RegisterField(_clipboardLabel, _clipboardSwitch);
        _drivesLabel.Text = L.Get("ConnectionOptions.Rdp.Drives");
        _optionsGrid.RegisterField(_drivesLabel, _drivesSwitch);
        _printersLabel.Text = L.Get("ConnectionOptions.Rdp.Printers");
        _optionsGrid.RegisterField(_printersLabel, _printersSwitch);
        _smartCardsLabel.Text = L.Get("ConnectionOptions.Rdp.SmartCards");
        _optionsGrid.RegisterField(_smartCardsLabel, _smartCardsSwitch);
        _comPortsLabel.Text = L.Get("ConnectionOptions.Rdp.ComPorts");
        _optionsGrid.RegisterField(_comPortsLabel, _comPortsSwitch);
        _posDevicesLabel.Text = L.Get("ConnectionOptions.Rdp.PosDevices");
        _optionsGrid.RegisterField(_posDevicesLabel, _posDevicesSwitch);
        _camerasLabel.Text = L.Get("ConnectionOptions.Rdp.Cameras");
        _optionsGrid.RegisterField(_camerasLabel, _camerasSwitch);
        _microphoneLabel.Text = L.Get("ConnectionOptions.Rdp.Microphone");
        _optionsGrid.RegisterField(_microphoneLabel, _microphoneSwitch);
        _audioModeLabel.Text = L.Get("ConnectionOptions.Rdp.Audio");
        _optionsGrid.RegisterField(_audioModeLabel, _audioModeSelect);
        _administrativeSessionLabel.Text = L.Get("ConnectionOptions.Rdp.AdminSession");
        _optionsGrid.RegisterField(_administrativeSessionLabel, _administrativeSessionSwitch);
        _promptForCredentialsLabel.Text = L.Get("ConnectionOptions.Rdp.PromptForCredentials");
        _optionsGrid.RegisterField(_promptForCredentialsLabel, _promptForCredentialsSwitch);
        _disableWallpaperLabel.Text = L.Get("ConnectionOptions.Rdp.DisableWallpaper");
        _optionsGrid.RegisterField(_disableWallpaperLabel, _disableWallpaperSwitch);
        _autoReconnectLabel.Text = L.Get("ConnectionOptions.Common.AutoReconnect");
        _optionsGrid.RegisterField(_autoReconnectLabel, _autoReconnectSwitch);

        _fullScreenSwitch.CheckedChanged += HandleFullScreenChanged;
        LoadFrom(CreateDefaultProfile());
    }

    /// <summary>Creates defaults for a new RDP editor draft. / 创建新 RDP 编辑草稿的默认配置。</summary>
    internal static ConnectionProfile CreateDefaultProfile() => new()
    {
        Type = ConnectionType.RemoteDesktop,
        Username = "Administrator",
        Rdp = new RdpOptions { AudioMode = RdpAudioMode.Remote }
    };

    /// <inheritdoc />
    public override ConnectionType Type => ConnectionType.RemoteDesktop;

    /// <inheritdoc />
    public override string SectionTitle => L.Get("ConnectionOptions.Rdp.Title");

    /// <inheritdoc />
    public override IReadOnlyCollection<string> ManagedOptionKeys => NoManagedOptionKeys;

    /// <inheritdoc />
    public override string SuggestedName => _endpoint.Target;

    /// <inheritdoc />
    public override void LoadFrom(ConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _endpoint.LoadFrom(profile);
        RdpOptions options = profile.Rdp ?? new RdpOptions();
        _fullScreenSwitch.Checked = options.FullScreen;
        _allMonitorsSwitch.Checked = options.UseAllMonitors;
        _desktopWidthInput.Value = Math.Clamp(options.DesktopWidth, 320, 16384);
        _desktopHeightInput.Value = Math.Clamp(options.DesktopHeight, 200, 16384);
        _colorDepthSelect.SelectedValue = options.ColorDepth is 15 or 16 or 24 or 32 ? options.ColorDepth : 32;
        _displayConnectionBarSwitch.Checked = options.DisplayConnectionBar;
        _compressionSwitch.Checked = options.EnableCompression;
        _keyboardHookSelect.SelectedValue = Enum.IsDefined(options.KeyboardHookMode)
            ? options.KeyboardHookMode
            : RdpKeyboardHookMode.FullScreenOnly;
        _clipboardSwitch.Checked = options.RedirectClipboard;
        _drivesSwitch.Checked = options.RedirectDrives;
        _printersSwitch.Checked = options.RedirectPrinters;
        _smartCardsSwitch.Checked = options.RedirectSmartCards;
        _comPortsSwitch.Checked = options.RedirectComPorts;
        _posDevicesSwitch.Checked = options.RedirectPosDevices;
        _camerasSwitch.Checked = options.RedirectCameras;
        _microphoneSwitch.Checked = options.RedirectMicrophone;
        _audioModeSelect.SelectedValue = Enum.IsDefined(options.AudioMode) ? options.AudioMode : RdpAudioMode.Local;
        _administrativeSessionSwitch.Checked = options.AdministrativeSession;
        _promptForCredentialsSwitch.Checked = options.PromptForCredentials;
        _disableWallpaperSwitch.Checked = options.DisableWallpaper;
        _autoReconnectSwitch.Checked = options.AutoReconnect;
        UpdateWindowedDesktopEditors();
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

        int desktopWidth = Decimal.ToInt32(_desktopWidthInput.Value);
        int desktopHeight = Decimal.ToInt32(_desktopHeightInput.Value);
        if (desktopWidth is < 320 or > 16384)
        {
            _desktopWidthInput.Status = AntdUI.TType.Error;
            error = L.Get("ConnectionOptions.Rdp.Validation.DesktopWidth");
            return false;
        }

        if (desktopHeight is < 200 or > 16384)
        {
            _desktopHeightInput.Status = AntdUI.TType.Error;
            error = L.Get("ConnectionOptions.Rdp.Validation.DesktopHeight");
            return false;
        }

        RdpOptions options = profile.Rdp ??= new RdpOptions();
        options.FullScreen = _fullScreenSwitch.Checked;
        options.UseAllMonitors = _allMonitorsSwitch.Checked;
        options.DesktopWidth = desktopWidth;
        options.DesktopHeight = desktopHeight;
        options.ColorDepth = _colorDepthSelect.SelectedValue is int colorDepth && colorDepth is 15 or 16 or 24 or 32
            ? colorDepth
            : 32;
        options.DisplayConnectionBar = _displayConnectionBarSwitch.Checked;
        options.EnableCompression = _compressionSwitch.Checked;
        options.KeyboardHookMode = _keyboardHookSelect.SelectedValue is RdpKeyboardHookMode keyboardHook && Enum.IsDefined(keyboardHook)
            ? keyboardHook
            : RdpKeyboardHookMode.FullScreenOnly;
        options.RedirectClipboard = _clipboardSwitch.Checked;
        options.RedirectDrives = _drivesSwitch.Checked;
        options.RedirectPrinters = _printersSwitch.Checked;
        options.RedirectSmartCards = _smartCardsSwitch.Checked;
        options.RedirectComPorts = _comPortsSwitch.Checked;
        options.RedirectPosDevices = _posDevicesSwitch.Checked;
        options.RedirectCameras = _camerasSwitch.Checked;
        options.RedirectMicrophone = _microphoneSwitch.Checked;
        options.AudioMode = _audioModeSelect.SelectedValue is RdpAudioMode audioMode && Enum.IsDefined(audioMode)
            ? audioMode
            : RdpAudioMode.Local;
        options.AdministrativeSession = _administrativeSessionSwitch.Checked;
        options.PromptForCredentials = _promptForCredentialsSwitch.Checked;
        options.DisableWallpaper = _disableWallpaperSwitch.Checked;
        options.AutoReconnect = _autoReconnectSwitch.Checked;
        error = null;
        return true;
    }

    /// <inheritdoc />
    public override void ResetValidationState()
    {
        _endpoint.ResetValidationState();
        _desktopWidthInput.Status = AntdUI.TType.None;
        _desktopHeightInput.Status = AntdUI.TType.None;
    }

    /// <inheritdoc />
    public override void UpdateCustomArgumentTemplate(string? template)
    {
        base.UpdateCustomArgumentTemplate(template);
        _endpoint.SetCustomArgumentTemplate(template);
    }

    /// <summary>Updates fields whose availability depends on full-screen mode. / 根据全屏模式更新相关字段可用性。</summary>
    private void HandleFullScreenChanged(object sender, AntdUI.BoolEventArgs e)
    {
        UpdateWindowedDesktopEditors();
    }

    /// <summary>Allows explicit dimensions only in windowed mode and multimon only in full screen. / 仅在窗口模式允许尺寸，并仅在全屏模式允许多显示器。</summary>
    private void UpdateWindowedDesktopEditors()
    {
        bool windowed = !_fullScreenSwitch.Checked;
        _desktopWidthInput.Enabled = windowed;
        _desktopHeightInput.Enabled = windowed;
        _allMonitorsSwitch.Enabled = !windowed;
    }
}
