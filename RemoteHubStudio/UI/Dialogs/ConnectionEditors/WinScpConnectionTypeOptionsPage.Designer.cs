namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

partial class WinScpConnectionTypeOptionsPage
{
    /// <summary>Creates the complete source-designer control tree. / 创建源设计器可直接加载的完整控件树。</summary>
    private void InitializeComponent()
    {
        _endpoint = new ConnectionEndpointEditor();
        _sessionOptionsGrid = new RemoteHubStudio.UI.Controls.ResponsiveFieldGrid();
        _remotePathLabel = new AntdUI.Label();
        _remotePathInput = new AntdUI.Input();
        _webDavAddressLabel = new AntdUI.Label();
        _webDavAddressInput = new AntdUI.Input();
        _layout = new System.Windows.Forms.TableLayoutPanel();
        _layout.SuspendLayout();
        _sessionOptionsGrid.SuspendLayout();
        SuspendLayout();
        // _endpoint
        _endpoint.ClientType = RemoteHubStudio.Domain.ConnectionType.WinScp;
        _endpoint.AutoSize = true;
        _endpoint.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _endpoint.Dock = System.Windows.Forms.DockStyle.Top;
        _endpoint.Margin = System.Windows.Forms.Padding.Empty;
        _endpoint.Name = "_endpoint";
        _endpoint.Size = new System.Drawing.Size(800, 150);
        _endpoint.TabIndex = 0;
        // _sessionOptionsGrid
        _sessionOptionsGrid.AutoSize = true;
        _sessionOptionsGrid.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _sessionOptionsGrid.ColumnCount = 4;
        _sessionOptionsGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
        _sessionOptionsGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        _sessionOptionsGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
        _sessionOptionsGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        _sessionOptionsGrid.Controls.Add(_remotePathLabel, 0, 0);
        _sessionOptionsGrid.Controls.Add(_remotePathInput, 1, 0);
        _sessionOptionsGrid.Controls.Add(_webDavAddressLabel, 2, 0);
        _sessionOptionsGrid.Controls.Add(_webDavAddressInput, 3, 0);
        _sessionOptionsGrid.Dock = System.Windows.Forms.DockStyle.Top;
        _sessionOptionsGrid.Margin = System.Windows.Forms.Padding.Empty;
        _sessionOptionsGrid.Name = "_sessionOptionsGrid";
        _sessionOptionsGrid.Padding = new System.Windows.Forms.Padding(4, 2, 4, 4);
        _sessionOptionsGrid.RowCount = 1;
        _sessionOptionsGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _sessionOptionsGrid.Size = new System.Drawing.Size(800, 54);
        _sessionOptionsGrid.TabIndex = 1;
        // _remotePathLabel
        _remotePathLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _remotePathLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _remotePathLabel.Name = "_remotePathLabel";
        _remotePathLabel.Text = "远程路径 / Remote path";
        _remotePathLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _remotePathInput
        _remotePathInput.Name = "_remotePathInput";
        _remotePathInput.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _remotePathInput.TabIndex = 0;
        _remotePathInput.Dock = System.Windows.Forms.DockStyle.Fill;
        _remotePathInput.Size = new System.Drawing.Size(240, 38);
        _remotePathInput.Radius = 8;
        _remotePathInput.PlaceholderText = "可选的远程路径，例如 /var/log / Optional remote path, for example /var/log";
        _remotePathInput.AllowClear = true;
        // _webDavAddressLabel
        _webDavAddressLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _webDavAddressLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _webDavAddressLabel.Name = "_webDavAddressLabel";
        _webDavAddressLabel.Text = "WebDAV 地址 / WebDAV address";
        _webDavAddressLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _webDavAddressInput
        _webDavAddressInput.Name = "_webDavAddressInput";
        _webDavAddressInput.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _webDavAddressInput.TabIndex = 1;
        _webDavAddressInput.Dock = System.Windows.Forms.DockStyle.Fill;
        _webDavAddressInput.Size = new System.Drawing.Size(240, 38);
        _webDavAddressInput.Radius = 8;
        _webDavAddressInput.PlaceholderText = "例如 https://server:443/webdav/ / For example, https://server:443/webdav/";
        _webDavAddressInput.AllowClear = true;
        // _layout
        _layout.AutoSize = true;
        _layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _layout.ColumnCount = 1;
        _layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _layout.Controls.Add(_endpoint, 0, 0);
        _layout.Controls.Add(_sessionOptionsGrid, 0, 1);
        _layout.Dock = System.Windows.Forms.DockStyle.Top;
        _layout.Margin = System.Windows.Forms.Padding.Empty;
        _layout.Name = "_layout";
        _layout.RowCount = 2;
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.Size = new System.Drawing.Size(800, 400);
        _layout.TabIndex = 0;
        // WinScpConnectionTypeOptionsPage
        AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        Controls.Add(_layout);
        Name = "WinScpConnectionTypeOptionsPage";
        Size = new System.Drawing.Size(800, 240);
        _sessionOptionsGrid.ResumeLayout(false);
        _layout.ResumeLayout(false);
        _layout.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private ConnectionEndpointEditor _endpoint = null!;
    private RemoteHubStudio.UI.Controls.ResponsiveFieldGrid _sessionOptionsGrid = null!;
    private AntdUI.Label _remotePathLabel = null!;
    private AntdUI.Input _remotePathInput = null!;
    private AntdUI.Label _webDavAddressLabel = null!;
    private AntdUI.Input _webDavAddressInput = null!;
    private System.Windows.Forms.TableLayoutPanel _layout = null!;
}
