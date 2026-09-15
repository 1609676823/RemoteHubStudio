namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

partial class RadminConnectionTypeOptionsPage
{
    /// <summary>Creates the complete source-designer control tree. / 创建源设计器可直接加载的完整控件树。</summary>
    private void InitializeComponent()
    {
        _endpoint = new ConnectionEndpointEditor();
        _optionsGrid = new RemoteHubStudio.UI.Controls.ResponsiveFieldGrid();
        _encryptLabel = new AntdUI.Label();
        _encryptSwitch = new AntdUI.Switch();
        _fullScreenLabel = new AntdUI.Label();
        _fullScreenSwitch = new AntdUI.Switch();
        _noFullKeyboardControlLabel = new AntdUI.Label();
        _noFullKeyboardControlSwitch = new AntdUI.Switch();
        _colorDepthLabel = new AntdUI.Label();
        _colorDepthSelect = new AntdUI.Select();
        _updatesLabel = new AntdUI.Label();
        _updatesInput = new AntdUI.InputNumber();
        _layout = new System.Windows.Forms.TableLayoutPanel();
        _layout.SuspendLayout();
        _optionsGrid.SuspendLayout();
        SuspendLayout();
        // _endpoint
        _endpoint.ClientType = RemoteHubStudio.Domain.ConnectionType.Radmin;
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
        _optionsGrid.Controls.Add(_encryptLabel, 0, 0);
        _optionsGrid.Controls.Add(_encryptSwitch, 1, 0);
        _optionsGrid.Controls.Add(_fullScreenLabel, 2, 0);
        _optionsGrid.Controls.Add(_fullScreenSwitch, 3, 0);
        _optionsGrid.Controls.Add(_noFullKeyboardControlLabel, 0, 1);
        _optionsGrid.Controls.Add(_noFullKeyboardControlSwitch, 1, 1);
        _optionsGrid.Controls.Add(_colorDepthLabel, 2, 1);
        _optionsGrid.Controls.Add(_colorDepthSelect, 3, 1);
        _optionsGrid.Controls.Add(_updatesLabel, 0, 2);
        _optionsGrid.Controls.Add(_updatesInput, 1, 2);
        _optionsGrid.Dock = System.Windows.Forms.DockStyle.Top;
        _optionsGrid.Margin = System.Windows.Forms.Padding.Empty;
        _optionsGrid.Name = "_optionsGrid";
        _optionsGrid.Padding = new System.Windows.Forms.Padding(4, 2, 4, 4);
        _optionsGrid.RowCount = 3;
        _optionsGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _optionsGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _optionsGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _optionsGrid.Size = new System.Drawing.Size(800, 150);
        _optionsGrid.TabIndex = 1;
        // _encryptLabel
        _encryptLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _encryptLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _encryptLabel.Name = "_encryptLabel";
        _encryptLabel.Text = "加密 / Encrypt";
        _encryptLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _encryptSwitch
        _encryptSwitch.Name = "_encryptSwitch";
        _encryptSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _encryptSwitch.TabIndex = 0;
        _encryptSwitch.AccessibleName = "加密 / Encrypt";
        _encryptSwitch.AutoCheck = true;
        _encryptSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _encryptSwitch.Size = new System.Drawing.Size(60, 32);
        // _fullScreenLabel
        _fullScreenLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _fullScreenLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _fullScreenLabel.Name = "_fullScreenLabel";
        _fullScreenLabel.Text = "全屏 / Full screen";
        _fullScreenLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _fullScreenSwitch
        _fullScreenSwitch.Name = "_fullScreenSwitch";
        _fullScreenSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _fullScreenSwitch.TabIndex = 1;
        _fullScreenSwitch.AccessibleName = "全屏 / Full screen";
        _fullScreenSwitch.AutoCheck = true;
        _fullScreenSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _fullScreenSwitch.Size = new System.Drawing.Size(60, 32);
        // _noFullKeyboardControlLabel
        _noFullKeyboardControlLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _noFullKeyboardControlLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _noFullKeyboardControlLabel.Name = "_noFullKeyboardControlLabel";
        _noFullKeyboardControlLabel.Text = "键盘限制 / Keyboard restriction";
        _noFullKeyboardControlLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _noFullKeyboardControlSwitch
        _noFullKeyboardControlSwitch.Name = "_noFullKeyboardControlSwitch";
        _noFullKeyboardControlSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _noFullKeyboardControlSwitch.TabIndex = 2;
        _noFullKeyboardControlSwitch.AccessibleName = "禁用完整键盘控制 / Disable full keyboard control";
        _noFullKeyboardControlSwitch.AutoCheck = true;
        _noFullKeyboardControlSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _noFullKeyboardControlSwitch.Size = new System.Drawing.Size(60, 32);
        // _colorDepthLabel
        _colorDepthLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _colorDepthLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _colorDepthLabel.Name = "_colorDepthLabel";
        _colorDepthLabel.Text = "色深 / Color depth";
        _colorDepthLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _colorDepthSelect
        _colorDepthSelect.Name = "_colorDepthSelect";
        _colorDepthSelect.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _colorDepthSelect.TabIndex = 3;
        _colorDepthSelect.Dock = System.Windows.Forms.DockStyle.Fill;
        _colorDepthSelect.Size = new System.Drawing.Size(240, 38);
        _colorDepthSelect.Radius = 8;
        _colorDepthSelect.PlaceholderText = "色深 / Color depth";
        _colorDepthSelect.AllowClear = false;
        _colorDepthSelect.DropDownArrow = true;
        _colorDepthSelect.ListAutoWidth = true;
        _colorDepthSelect.WheelModifyEnabled = false;
        // _updatesLabel
        _updatesLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _updatesLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _updatesLabel.Name = "_updatesLabel";
        _updatesLabel.Text = "每秒更新数 / Updates per second";
        _updatesLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _updatesInput
        _updatesInput.Name = "_updatesInput";
        _updatesInput.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _updatesInput.TabIndex = 4;
        _updatesInput.Dock = System.Windows.Forms.DockStyle.Fill;
        _updatesInput.Size = new System.Drawing.Size(240, 38);
        _updatesInput.Radius = 8;
        _updatesInput.DecimalPlaces = 0;
        _updatesInput.Maximum = 100;
        _updatesInput.Minimum = 1;
        _updatesInput.Value = 30;
        _updatesInput.ShowControl = true;
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
        // RadminConnectionTypeOptionsPage
        AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        Controls.Add(_layout);
        Name = "RadminConnectionTypeOptionsPage";
        Size = new System.Drawing.Size(800, 304);
        _optionsGrid.ResumeLayout(false);
        _layout.ResumeLayout(false);
        _layout.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private ConnectionEndpointEditor _endpoint = null!;
    private RemoteHubStudio.UI.Controls.ResponsiveFieldGrid _optionsGrid = null!;
    private AntdUI.Label _encryptLabel = null!;
    private AntdUI.Switch _encryptSwitch = null!;
    private AntdUI.Label _fullScreenLabel = null!;
    private AntdUI.Switch _fullScreenSwitch = null!;
    private AntdUI.Label _noFullKeyboardControlLabel = null!;
    private AntdUI.Switch _noFullKeyboardControlSwitch = null!;
    private AntdUI.Label _colorDepthLabel = null!;
    private AntdUI.Select _colorDepthSelect = null!;
    private AntdUI.Label _updatesLabel = null!;
    private AntdUI.InputNumber _updatesInput = null!;
    private System.Windows.Forms.TableLayoutPanel _layout = null!;
}
