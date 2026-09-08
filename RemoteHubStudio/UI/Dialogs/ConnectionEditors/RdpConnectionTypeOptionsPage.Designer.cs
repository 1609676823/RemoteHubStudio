namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

partial class RdpConnectionTypeOptionsPage
{
    /// <summary>Creates the complete source-designer control tree. / 创建源设计器可直接加载的完整控件树。</summary>
    private void InitializeComponent()
    {
        _endpoint = new ConnectionEndpointEditor();
        _optionsGrid = new RemoteHubStudio.UI.Controls.ResponsiveFieldGrid();
        _fullScreenLabel = new AntdUI.Label();
        _fullScreenSwitch = new AntdUI.Switch();
        _allMonitorsLabel = new AntdUI.Label();
        _allMonitorsSwitch = new AntdUI.Switch();
        _desktopWidthLabel = new AntdUI.Label();
        _desktopWidthInput = new AntdUI.InputNumber();
        _desktopHeightLabel = new AntdUI.Label();
        _desktopHeightInput = new AntdUI.InputNumber();
        _colorDepthLabel = new AntdUI.Label();
        _colorDepthSelect = new AntdUI.Select();
        _displayConnectionBarLabel = new AntdUI.Label();
        _displayConnectionBarSwitch = new AntdUI.Switch();
        _compressionLabel = new AntdUI.Label();
        _compressionSwitch = new AntdUI.Switch();
        _keyboardHookLabel = new AntdUI.Label();
        _keyboardHookSelect = new AntdUI.Select();
        _clipboardLabel = new AntdUI.Label();
        _clipboardSwitch = new AntdUI.Switch();
        _drivesLabel = new AntdUI.Label();
        _drivesSwitch = new AntdUI.Switch();
        _printersLabel = new AntdUI.Label();
        _printersSwitch = new AntdUI.Switch();
        _smartCardsLabel = new AntdUI.Label();
        _smartCardsSwitch = new AntdUI.Switch();
        _comPortsLabel = new AntdUI.Label();
        _comPortsSwitch = new AntdUI.Switch();
        _posDevicesLabel = new AntdUI.Label();
        _posDevicesSwitch = new AntdUI.Switch();
        _camerasLabel = new AntdUI.Label();
        _camerasSwitch = new AntdUI.Switch();
        _microphoneLabel = new AntdUI.Label();
        _microphoneSwitch = new AntdUI.Switch();
        _audioModeLabel = new AntdUI.Label();
        _audioModeSelect = new AntdUI.Select();
        _administrativeSessionLabel = new AntdUI.Label();
        _administrativeSessionSwitch = new AntdUI.Switch();
        _promptForCredentialsLabel = new AntdUI.Label();
        _promptForCredentialsSwitch = new AntdUI.Switch();
        _disableWallpaperLabel = new AntdUI.Label();
        _disableWallpaperSwitch = new AntdUI.Switch();
        _autoReconnectLabel = new AntdUI.Label();
        _autoReconnectSwitch = new AntdUI.Switch();
        _layout = new System.Windows.Forms.TableLayoutPanel();
        _layout.SuspendLayout();
        _optionsGrid.SuspendLayout();
        SuspendLayout();
        // _endpoint
        _endpoint.ClientType = RemoteHubStudio.Domain.ConnectionType.RemoteDesktop;
        _endpoint.AutoSize = true;
        _endpoint.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _endpoint.Dock = System.Windows.Forms.DockStyle.Top;
        _endpoint.Margin = System.Windows.Forms.Padding.Empty;
        _endpoint.Name = "_endpoint";
        _endpoint.Size = new System.Drawing.Size(800, 150);
        _endpoint.TabIndex = 0;
        // _optionsGrid
        _optionsGrid.AutoSize = true;
        _optionsGrid.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _optionsGrid.ColumnCount = 4;
        _optionsGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
        _optionsGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        _optionsGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
        _optionsGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        _optionsGrid.Controls.Add(_fullScreenLabel, 0, 0);
        _optionsGrid.Controls.Add(_fullScreenSwitch, 1, 0);
        _optionsGrid.Controls.Add(_allMonitorsLabel, 2, 0);
        _optionsGrid.Controls.Add(_allMonitorsSwitch, 3, 0);
        _optionsGrid.Controls.Add(_desktopWidthLabel, 0, 1);
        _optionsGrid.Controls.Add(_desktopWidthInput, 1, 1);
        _optionsGrid.Controls.Add(_desktopHeightLabel, 2, 1);
        _optionsGrid.Controls.Add(_desktopHeightInput, 3, 1);
        _optionsGrid.Controls.Add(_colorDepthLabel, 0, 2);
        _optionsGrid.Controls.Add(_colorDepthSelect, 1, 2);
        _optionsGrid.Controls.Add(_displayConnectionBarLabel, 2, 2);
        _optionsGrid.Controls.Add(_displayConnectionBarSwitch, 3, 2);
        _optionsGrid.Controls.Add(_compressionLabel, 0, 3);
        _optionsGrid.Controls.Add(_compressionSwitch, 1, 3);
        _optionsGrid.Controls.Add(_keyboardHookLabel, 2, 3);
        _optionsGrid.Controls.Add(_keyboardHookSelect, 3, 3);
        _optionsGrid.Controls.Add(_clipboardLabel, 0, 4);
        _optionsGrid.Controls.Add(_clipboardSwitch, 1, 4);
        _optionsGrid.Controls.Add(_drivesLabel, 2, 4);
        _optionsGrid.Controls.Add(_drivesSwitch, 3, 4);
        _optionsGrid.Controls.Add(_printersLabel, 0, 5);
        _optionsGrid.Controls.Add(_printersSwitch, 1, 5);
        _optionsGrid.Controls.Add(_smartCardsLabel, 2, 5);
        _optionsGrid.Controls.Add(_smartCardsSwitch, 3, 5);
        _optionsGrid.Controls.Add(_comPortsLabel, 0, 6);
        _optionsGrid.Controls.Add(_comPortsSwitch, 1, 6);
        _optionsGrid.Controls.Add(_posDevicesLabel, 2, 6);
        _optionsGrid.Controls.Add(_posDevicesSwitch, 3, 6);
        _optionsGrid.Controls.Add(_camerasLabel, 0, 7);
        _optionsGrid.Controls.Add(_camerasSwitch, 1, 7);
        _optionsGrid.Controls.Add(_microphoneLabel, 2, 7);
        _optionsGrid.Controls.Add(_microphoneSwitch, 3, 7);
        _optionsGrid.Controls.Add(_audioModeLabel, 0, 8);
        _optionsGrid.Controls.Add(_audioModeSelect, 1, 8);
        _optionsGrid.Controls.Add(_administrativeSessionLabel, 2, 8);
        _optionsGrid.Controls.Add(_administrativeSessionSwitch, 3, 8);
        _optionsGrid.Controls.Add(_promptForCredentialsLabel, 0, 9);
        _optionsGrid.Controls.Add(_promptForCredentialsSwitch, 1, 9);
        _optionsGrid.Controls.Add(_disableWallpaperLabel, 2, 9);
        _optionsGrid.Controls.Add(_disableWallpaperSwitch, 3, 9);
        _optionsGrid.Controls.Add(_autoReconnectLabel, 0, 10);
        _optionsGrid.Controls.Add(_autoReconnectSwitch, 1, 10);
        _optionsGrid.Dock = System.Windows.Forms.DockStyle.Top;
        _optionsGrid.Margin = System.Windows.Forms.Padding.Empty;
        _optionsGrid.Name = "_optionsGrid";
        _optionsGrid.Padding = new System.Windows.Forms.Padding(4, 2, 4, 4);
        _optionsGrid.RowCount = 11;
        _optionsGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _optionsGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _optionsGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _optionsGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _optionsGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _optionsGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _optionsGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _optionsGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _optionsGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _optionsGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _optionsGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _optionsGrid.Size = new System.Drawing.Size(800, 534);
        _optionsGrid.TabIndex = 1;
        // _fullScreenLabel
        _fullScreenLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _fullScreenLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _fullScreenLabel.Name = "_fullScreenLabel";
        _fullScreenLabel.Text = "全屏 / Full screen";
        _fullScreenLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _fullScreenSwitch
        _fullScreenSwitch.Name = "_fullScreenSwitch";
        _fullScreenSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _fullScreenSwitch.TabIndex = 0;
        _fullScreenSwitch.AccessibleName = "全屏 / Full screen";
        _fullScreenSwitch.AutoCheck = true;
        _fullScreenSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _fullScreenSwitch.Size = new System.Drawing.Size(60, 32);
        // _allMonitorsLabel
        _allMonitorsLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _allMonitorsLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _allMonitorsLabel.Name = "_allMonitorsLabel";
        _allMonitorsLabel.Text = "全部显示器 / All monitors";
        _allMonitorsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _allMonitorsSwitch
        _allMonitorsSwitch.Name = "_allMonitorsSwitch";
        _allMonitorsSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _allMonitorsSwitch.TabIndex = 1;
        _allMonitorsSwitch.AccessibleName = "全部显示器 / All monitors";
        _allMonitorsSwitch.AutoCheck = true;
        _allMonitorsSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _allMonitorsSwitch.Size = new System.Drawing.Size(60, 32);
        // _desktopWidthLabel
        _desktopWidthLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _desktopWidthLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _desktopWidthLabel.Name = "_desktopWidthLabel";
        _desktopWidthLabel.Text = "桌面宽度 / Desktop width";
        _desktopWidthLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _desktopWidthInput
        _desktopWidthInput.Name = "_desktopWidthInput";
        _desktopWidthInput.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _desktopWidthInput.TabIndex = 2;
        _desktopWidthInput.Dock = System.Windows.Forms.DockStyle.Fill;
        _desktopWidthInput.Size = new System.Drawing.Size(240, 38);
        _desktopWidthInput.Radius = 8;
        _desktopWidthInput.DecimalPlaces = 0;
        _desktopWidthInput.Maximum = 16384;
        _desktopWidthInput.Minimum = 320;
        _desktopWidthInput.Value = 1440;
        _desktopWidthInput.ShowControl = true;
        // _desktopHeightLabel
        _desktopHeightLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _desktopHeightLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _desktopHeightLabel.Name = "_desktopHeightLabel";
        _desktopHeightLabel.Text = "桌面高度 / Desktop height";
        _desktopHeightLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _desktopHeightInput
        _desktopHeightInput.Name = "_desktopHeightInput";
        _desktopHeightInput.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _desktopHeightInput.TabIndex = 3;
        _desktopHeightInput.Dock = System.Windows.Forms.DockStyle.Fill;
        _desktopHeightInput.Size = new System.Drawing.Size(240, 38);
        _desktopHeightInput.Radius = 8;
        _desktopHeightInput.DecimalPlaces = 0;
        _desktopHeightInput.Maximum = 16384;
        _desktopHeightInput.Minimum = 200;
        _desktopHeightInput.Value = 900;
        _desktopHeightInput.ShowControl = true;
        // _colorDepthLabel
        _colorDepthLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _colorDepthLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _colorDepthLabel.Name = "_colorDepthLabel";
        _colorDepthLabel.Text = "色深 / Color depth";
        _colorDepthLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _colorDepthSelect
        _colorDepthSelect.Name = "_colorDepthSelect";
        _colorDepthSelect.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _colorDepthSelect.TabIndex = 4;
        _colorDepthSelect.Dock = System.Windows.Forms.DockStyle.Fill;
        _colorDepthSelect.Size = new System.Drawing.Size(240, 38);
        _colorDepthSelect.Radius = 8;
        _colorDepthSelect.PlaceholderText = "色深 / Color depth";
        _colorDepthSelect.AllowClear = false;
        _colorDepthSelect.DropDownArrow = true;
        _colorDepthSelect.ListAutoWidth = true;
        _colorDepthSelect.WheelModifyEnabled = false;
        // _displayConnectionBarLabel
        _displayConnectionBarLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _displayConnectionBarLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _displayConnectionBarLabel.Name = "_displayConnectionBarLabel";
        _displayConnectionBarLabel.Text = "连接栏 / Connection bar";
        _displayConnectionBarLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _displayConnectionBarSwitch
        _displayConnectionBarSwitch.Name = "_displayConnectionBarSwitch";
        _displayConnectionBarSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _displayConnectionBarSwitch.TabIndex = 5;
        _displayConnectionBarSwitch.AccessibleName = "显示连接栏 / Display connection bar";
        _displayConnectionBarSwitch.AutoCheck = true;
        _displayConnectionBarSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _displayConnectionBarSwitch.Size = new System.Drawing.Size(60, 32);
        // _compressionLabel
        _compressionLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _compressionLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _compressionLabel.Name = "_compressionLabel";
        _compressionLabel.Text = "压缩 / Compression";
        _compressionLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _compressionSwitch
        _compressionSwitch.Name = "_compressionSwitch";
        _compressionSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _compressionSwitch.TabIndex = 6;
        _compressionSwitch.AccessibleName = "启用压缩 / Enable compression";
        _compressionSwitch.AutoCheck = true;
        _compressionSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _compressionSwitch.Size = new System.Drawing.Size(60, 32);
        // _keyboardHookLabel
        _keyboardHookLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _keyboardHookLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _keyboardHookLabel.Name = "_keyboardHookLabel";
        _keyboardHookLabel.Text = "键盘钩子 / Keyboard hook";
        _keyboardHookLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _keyboardHookSelect
        _keyboardHookSelect.Name = "_keyboardHookSelect";
        _keyboardHookSelect.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _keyboardHookSelect.TabIndex = 7;
        _keyboardHookSelect.Dock = System.Windows.Forms.DockStyle.Fill;
        _keyboardHookSelect.Size = new System.Drawing.Size(240, 38);
        _keyboardHookSelect.Radius = 8;
        _keyboardHookSelect.PlaceholderText = "Windows 组合键 / Windows key combinations";
        _keyboardHookSelect.AllowClear = false;
        _keyboardHookSelect.DropDownArrow = true;
        _keyboardHookSelect.ListAutoWidth = true;
        _keyboardHookSelect.WheelModifyEnabled = false;
        // _clipboardLabel
        _clipboardLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _clipboardLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _clipboardLabel.Name = "_clipboardLabel";
        _clipboardLabel.Text = "剪贴板 / Clipboard";
        _clipboardLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _clipboardSwitch
        _clipboardSwitch.Name = "_clipboardSwitch";
        _clipboardSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _clipboardSwitch.TabIndex = 8;
        _clipboardSwitch.AccessibleName = "剪贴板重定向 / Clipboard redirection";
        _clipboardSwitch.AutoCheck = true;
        _clipboardSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _clipboardSwitch.Size = new System.Drawing.Size(60, 32);
        // _drivesLabel
        _drivesLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _drivesLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _drivesLabel.Name = "_drivesLabel";
        _drivesLabel.Text = "驱动器 / Drives";
        _drivesLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _drivesSwitch
        _drivesSwitch.Name = "_drivesSwitch";
        _drivesSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _drivesSwitch.TabIndex = 9;
        _drivesSwitch.AccessibleName = "驱动器重定向 / Drive redirection";
        _drivesSwitch.AutoCheck = true;
        _drivesSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _drivesSwitch.Size = new System.Drawing.Size(60, 32);
        // _printersLabel
        _printersLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _printersLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _printersLabel.Name = "_printersLabel";
        _printersLabel.Text = "打印机 / Printers";
        _printersLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _printersSwitch
        _printersSwitch.Name = "_printersSwitch";
        _printersSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _printersSwitch.TabIndex = 10;
        _printersSwitch.AccessibleName = "打印机重定向 / Printer redirection";
        _printersSwitch.AutoCheck = true;
        _printersSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _printersSwitch.Size = new System.Drawing.Size(60, 32);
        // _smartCardsLabel
        _smartCardsLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _smartCardsLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _smartCardsLabel.Name = "_smartCardsLabel";
        _smartCardsLabel.Text = "智能卡 / Smart cards";
        _smartCardsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _smartCardsSwitch
        _smartCardsSwitch.Name = "_smartCardsSwitch";
        _smartCardsSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _smartCardsSwitch.TabIndex = 11;
        _smartCardsSwitch.AccessibleName = "智能卡重定向 / Smart-card redirection";
        _smartCardsSwitch.AutoCheck = true;
        _smartCardsSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _smartCardsSwitch.Size = new System.Drawing.Size(60, 32);
        // _comPortsLabel
        _comPortsLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _comPortsLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _comPortsLabel.Name = "_comPortsLabel";
        _comPortsLabel.Text = "串行端口 / COM ports";
        _comPortsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _comPortsSwitch
        _comPortsSwitch.Name = "_comPortsSwitch";
        _comPortsSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _comPortsSwitch.TabIndex = 12;
        _comPortsSwitch.AccessibleName = "串行端口重定向 / COM-port redirection";
        _comPortsSwitch.AutoCheck = true;
        _comPortsSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _comPortsSwitch.Size = new System.Drawing.Size(60, 32);
        // _posDevicesLabel
        _posDevicesLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _posDevicesLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _posDevicesLabel.Name = "_posDevicesLabel";
        _posDevicesLabel.Text = "POS 设备 / POS devices";
        _posDevicesLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _posDevicesSwitch
        _posDevicesSwitch.Name = "_posDevicesSwitch";
        _posDevicesSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _posDevicesSwitch.TabIndex = 13;
        _posDevicesSwitch.AccessibleName = "POS 设备重定向 / POS-device redirection";
        _posDevicesSwitch.AutoCheck = true;
        _posDevicesSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _posDevicesSwitch.Size = new System.Drawing.Size(60, 32);
        // _camerasLabel
        _camerasLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _camerasLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _camerasLabel.Name = "_camerasLabel";
        _camerasLabel.Text = "摄像头 / Cameras";
        _camerasLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _camerasSwitch
        _camerasSwitch.Name = "_camerasSwitch";
        _camerasSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _camerasSwitch.TabIndex = 14;
        _camerasSwitch.AccessibleName = "摄像头重定向 / Camera redirection";
        _camerasSwitch.AutoCheck = true;
        _camerasSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _camerasSwitch.Size = new System.Drawing.Size(60, 32);
        // _microphoneLabel
        _microphoneLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _microphoneLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _microphoneLabel.Name = "_microphoneLabel";
        _microphoneLabel.Text = "麦克风 / Microphone";
        _microphoneLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _microphoneSwitch
        _microphoneSwitch.Name = "_microphoneSwitch";
        _microphoneSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _microphoneSwitch.TabIndex = 15;
        _microphoneSwitch.AccessibleName = "麦克风重定向 / Microphone redirection";
        _microphoneSwitch.AutoCheck = true;
        _microphoneSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _microphoneSwitch.Size = new System.Drawing.Size(60, 32);
        // _audioModeLabel
        _audioModeLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _audioModeLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _audioModeLabel.Name = "_audioModeLabel";
        _audioModeLabel.Text = "音频 / Audio";
        _audioModeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _audioModeSelect
        _audioModeSelect.Name = "_audioModeSelect";
        _audioModeSelect.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _audioModeSelect.TabIndex = 16;
        _audioModeSelect.Dock = System.Windows.Forms.DockStyle.Fill;
        _audioModeSelect.Size = new System.Drawing.Size(240, 38);
        _audioModeSelect.Radius = 8;
        _audioModeSelect.PlaceholderText = "远程音频 / Remote audio";
        _audioModeSelect.AllowClear = false;
        _audioModeSelect.DropDownArrow = true;
        _audioModeSelect.ListAutoWidth = true;
        _audioModeSelect.WheelModifyEnabled = false;
        // _administrativeSessionLabel
        _administrativeSessionLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _administrativeSessionLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _administrativeSessionLabel.Name = "_administrativeSessionLabel";
        _administrativeSessionLabel.Text = "管理会话 / Admin session";
        _administrativeSessionLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _administrativeSessionSwitch
        _administrativeSessionSwitch.Name = "_administrativeSessionSwitch";
        _administrativeSessionSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _administrativeSessionSwitch.TabIndex = 17;
        _administrativeSessionSwitch.AccessibleName = "管理会话 / Administrative session";
        _administrativeSessionSwitch.AutoCheck = true;
        _administrativeSessionSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _administrativeSessionSwitch.Size = new System.Drawing.Size(60, 32);
        // _promptForCredentialsLabel
        _promptForCredentialsLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _promptForCredentialsLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _promptForCredentialsLabel.Name = "_promptForCredentialsLabel";
        _promptForCredentialsLabel.Text = "连接时提示输入凭据 / Prompt for credentials when connecting";
        _promptForCredentialsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _promptForCredentialsSwitch
        _promptForCredentialsSwitch.Name = "_promptForCredentialsSwitch";
        _promptForCredentialsSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _promptForCredentialsSwitch.TabIndex = 18;
        _promptForCredentialsSwitch.AccessibleName = "连接时提示输入凭据 / Prompt for credentials when connecting";
        _promptForCredentialsSwitch.AutoCheck = true;
        _promptForCredentialsSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _promptForCredentialsSwitch.Size = new System.Drawing.Size(60, 32);
        // _disableWallpaperLabel
        _disableWallpaperLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _disableWallpaperLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _disableWallpaperLabel.Name = "_disableWallpaperLabel";
        _disableWallpaperLabel.Text = "禁用壁纸 / Disable wallpaper";
        _disableWallpaperLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _disableWallpaperSwitch
        _disableWallpaperSwitch.Name = "_disableWallpaperSwitch";
        _disableWallpaperSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _disableWallpaperSwitch.TabIndex = 19;
        _disableWallpaperSwitch.AccessibleName = "禁用壁纸 / Disable wallpaper";
        _disableWallpaperSwitch.AutoCheck = true;
        _disableWallpaperSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _disableWallpaperSwitch.Size = new System.Drawing.Size(60, 32);
        // _autoReconnectLabel
        _autoReconnectLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _autoReconnectLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _autoReconnectLabel.Name = "_autoReconnectLabel";
        _autoReconnectLabel.Text = "自动重连 / Auto reconnect";
        _autoReconnectLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _autoReconnectSwitch
        _autoReconnectSwitch.Name = "_autoReconnectSwitch";
        _autoReconnectSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _autoReconnectSwitch.TabIndex = 20;
        _autoReconnectSwitch.AccessibleName = "自动重连 / Auto reconnect";
        _autoReconnectSwitch.AutoCheck = true;
        _autoReconnectSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _autoReconnectSwitch.Size = new System.Drawing.Size(60, 32);
        // _layout
        _layout.AutoSize = true;
        _layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _layout.ColumnCount = 1;
        _layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _layout.Controls.Add(_endpoint, 0, 0);
        _layout.Controls.Add(_optionsGrid, 0, 1);
        _layout.Dock = System.Windows.Forms.DockStyle.Top;
        _layout.Margin = System.Windows.Forms.Padding.Empty;
        _layout.Name = "_layout";
        _layout.RowCount = 2;
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.Size = new System.Drawing.Size(800, 400);
        _layout.TabIndex = 0;
        // RdpConnectionTypeOptionsPage
        AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        Controls.Add(_layout);
        Name = "RdpConnectionTypeOptionsPage";
        Size = new System.Drawing.Size(800, 688);
        _optionsGrid.ResumeLayout(false);
        _layout.ResumeLayout(false);
        _layout.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private ConnectionEndpointEditor _endpoint = null!;
    private RemoteHubStudio.UI.Controls.ResponsiveFieldGrid _optionsGrid = null!;
    private AntdUI.Label _fullScreenLabel = null!;
    private AntdUI.Switch _fullScreenSwitch = null!;
    private AntdUI.Label _allMonitorsLabel = null!;
    private AntdUI.Switch _allMonitorsSwitch = null!;
    private AntdUI.Label _desktopWidthLabel = null!;
    private AntdUI.InputNumber _desktopWidthInput = null!;
    private AntdUI.Label _desktopHeightLabel = null!;
    private AntdUI.InputNumber _desktopHeightInput = null!;
    private AntdUI.Label _colorDepthLabel = null!;
    private AntdUI.Select _colorDepthSelect = null!;
    private AntdUI.Label _displayConnectionBarLabel = null!;
    private AntdUI.Switch _displayConnectionBarSwitch = null!;
    private AntdUI.Label _compressionLabel = null!;
    private AntdUI.Switch _compressionSwitch = null!;
    private AntdUI.Label _keyboardHookLabel = null!;
    private AntdUI.Select _keyboardHookSelect = null!;
    private AntdUI.Label _clipboardLabel = null!;
    private AntdUI.Switch _clipboardSwitch = null!;
    private AntdUI.Label _drivesLabel = null!;
    private AntdUI.Switch _drivesSwitch = null!;
    private AntdUI.Label _printersLabel = null!;
    private AntdUI.Switch _printersSwitch = null!;
    private AntdUI.Label _smartCardsLabel = null!;
    private AntdUI.Switch _smartCardsSwitch = null!;
    private AntdUI.Label _comPortsLabel = null!;
    private AntdUI.Switch _comPortsSwitch = null!;
    private AntdUI.Label _posDevicesLabel = null!;
    private AntdUI.Switch _posDevicesSwitch = null!;
    private AntdUI.Label _camerasLabel = null!;
    private AntdUI.Switch _camerasSwitch = null!;
    private AntdUI.Label _microphoneLabel = null!;
    private AntdUI.Switch _microphoneSwitch = null!;
    private AntdUI.Label _audioModeLabel = null!;
    private AntdUI.Select _audioModeSelect = null!;
    private AntdUI.Label _administrativeSessionLabel = null!;
    private AntdUI.Switch _administrativeSessionSwitch = null!;
    private AntdUI.Label _promptForCredentialsLabel = null!;
    private AntdUI.Switch _promptForCredentialsSwitch = null!;
    private AntdUI.Label _disableWallpaperLabel = null!;
    private AntdUI.Switch _disableWallpaperSwitch = null!;
    private AntdUI.Label _autoReconnectLabel = null!;
    private AntdUI.Switch _autoReconnectSwitch = null!;
    private System.Windows.Forms.TableLayoutPanel _layout = null!;
}
