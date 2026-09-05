namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

partial class ConnectionEndpointEditor
{
    /// <summary>Creates the complete source-designer control tree. / 创建源设计器可直接加载的完整控件树。</summary>
    private void InitializeComponent()
    {
        _fields = new RemoteHubStudio.UI.Controls.ResponsiveFieldGrid();
        _protocolLabel = new AntdUI.Label();
        _protocolSelect = new AntdUI.Select();
        _hostLabel = new AntdUI.Label();
        _hostInput = new AntdUI.Input();
        _portLabel = new AntdUI.Label();
        _portInput = new AntdUI.InputNumber();
        _usernameLabel = new AntdUI.Label();
        _usernameInput = new AntdUI.Input();
        _passwordLabel = new AntdUI.Label();
        _passwordInput = new RemoteHubStudio.UI.Controls.PasswordInput();
        _fields.SuspendLayout();
        SuspendLayout();
        // _fields
        _fields.AutoSize = true;
        _fields.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _fields.ColumnCount = 4;
        _fields.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
        _fields.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        _fields.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
        _fields.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        _fields.Controls.Add(_protocolLabel, 0, 0);
        _fields.Controls.Add(_protocolSelect, 1, 0);
        _fields.Controls.Add(_hostLabel, 2, 0);
        _fields.Controls.Add(_hostInput, 3, 0);
        _fields.Controls.Add(_portLabel, 0, 1);
        _fields.Controls.Add(_portInput, 1, 1);
        _fields.Controls.Add(_usernameLabel, 2, 1);
        _fields.Controls.Add(_usernameInput, 3, 1);
        _fields.Controls.Add(_passwordLabel, 0, 2);
        _fields.Controls.Add(_passwordInput, 1, 2);
        _fields.Dock = System.Windows.Forms.DockStyle.Top;
        _fields.Margin = System.Windows.Forms.Padding.Empty;
        _fields.Name = "_fields";
        _fields.Padding = new System.Windows.Forms.Padding(4, 2, 4, 4);
        _fields.RowCount = 3;
        _fields.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _fields.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _fields.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _fields.Size = new System.Drawing.Size(800, 150);
        _fields.TabIndex = 0;
        // _protocolLabel
        _protocolLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _protocolLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _protocolLabel.Name = "_protocolLabel";
        _protocolLabel.Text = "协议 / Protocol";
        _protocolLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _protocolSelect
        _protocolSelect.Name = "_protocolSelect";
        _protocolSelect.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _protocolSelect.TabIndex = 0;
        _protocolSelect.Dock = System.Windows.Forms.DockStyle.Fill;
        _protocolSelect.Size = new System.Drawing.Size(240, 38);
        _protocolSelect.Radius = 8;
        _protocolSelect.PlaceholderText = "选择协议或模式 / Select a protocol or mode";
        _protocolSelect.AllowClear = false;
        _protocolSelect.DropDownArrow = true;
        _protocolSelect.ListAutoWidth = true;
        _protocolSelect.WheelModifyEnabled = false;
        // _hostLabel
        _hostLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _hostLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _hostLabel.Name = "_hostLabel";
        _hostLabel.Text = "目标主机 / Target host";
        _hostLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _hostInput
        _hostInput.Name = "_hostInput";
        _hostInput.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _hostInput.TabIndex = 1;
        _hostInput.Dock = System.Windows.Forms.DockStyle.Fill;
        _hostInput.Size = new System.Drawing.Size(240, 38);
        _hostInput.Radius = 8;
        _hostInput.PlaceholderText = "主机名或 IP 地址 / Host name or IP address";
        _hostInput.AllowClear = true;
        // _portLabel
        _portLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _portLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _portLabel.Name = "_portLabel";
        _portLabel.Text = "端口 / Port";
        _portLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _portInput
        _portInput.Name = "_portInput";
        _portInput.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _portInput.TabIndex = 2;
        _portInput.Dock = System.Windows.Forms.DockStyle.Fill;
        _portInput.Size = new System.Drawing.Size(240, 38);
        _portInput.Radius = 8;
        _portInput.DecimalPlaces = 0;
        _portInput.Maximum = 65535;
        _portInput.Minimum = 0;
        _portInput.Value = 3389;
        _portInput.ShowControl = true;
        // _usernameLabel
        _usernameLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _usernameLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _usernameLabel.Name = "_usernameLabel";
        _usernameLabel.Text = "用户名 / Username";
        _usernameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _usernameInput
        _usernameInput.Name = "_usernameInput";
        _usernameInput.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _usernameInput.TabIndex = 3;
        _usernameInput.Dock = System.Windows.Forms.DockStyle.Fill;
        _usernameInput.Size = new System.Drawing.Size(240, 38);
        _usernameInput.Radius = 8;
        _usernameInput.PlaceholderText = "用户名 / Username";
        _usernameInput.AllowClear = true;
        // _passwordLabel
        _passwordLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _passwordLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _passwordLabel.Name = "_passwordLabel";
        _passwordLabel.Text = "密码 / Password";
        _passwordLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _passwordInput
        _passwordInput.Name = "_passwordInput";
        _passwordInput.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _passwordInput.TabIndex = 4;
        _passwordInput.Dock = System.Windows.Forms.DockStyle.Fill;
        _passwordInput.Size = new System.Drawing.Size(240, 38);
        _passwordInput.Radius = 8;
        _passwordInput.PlaceholderText = "密码 / Password";
        // ConnectionEndpointEditor
        AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        Controls.Add(_fields);
        Name = "ConnectionEndpointEditor";
        Size = new System.Drawing.Size(800, 154);
        _fields.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }

    private RemoteHubStudio.UI.Controls.ResponsiveFieldGrid _fields = null!;
    private AntdUI.Label _protocolLabel = null!;
    private AntdUI.Select _protocolSelect = null!;
    private AntdUI.Label _hostLabel = null!;
    private AntdUI.Input _hostInput = null!;
    private AntdUI.Label _portLabel = null!;
    private AntdUI.InputNumber _portInput = null!;
    private AntdUI.Label _usernameLabel = null!;
    private AntdUI.Input _usernameInput = null!;
    private AntdUI.Label _passwordLabel = null!;
    private RemoteHubStudio.UI.Controls.PasswordInput _passwordInput = null!;
}
