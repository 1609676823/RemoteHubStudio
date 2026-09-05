namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

partial class VncConnectionTypeOptionsPage
{
    /// <summary>Creates the complete source-designer control tree. / 创建源设计器可直接加载的完整控件树。</summary>
    private void InitializeComponent()
    {
        _endpoint = new ConnectionEndpointEditor();
        _viewerOptionsGrid = new RemoteHubStudio.UI.Controls.ResponsiveFieldGrid();
        _fullScreenLabel = new AntdUI.Label();
        _fullScreenSwitch = new AntdUI.Switch();
        _autoReconnectLabel = new AntdUI.Label();
        _autoReconnectSwitch = new AntdUI.Switch();
        _viewOnlyLabel = new AntdUI.Label();
        _viewOnlySwitch = new AntdUI.Switch();
        _layout = new System.Windows.Forms.TableLayoutPanel();
        _layout.SuspendLayout();
        _viewerOptionsGrid.SuspendLayout();
        SuspendLayout();
        // _endpoint
        _endpoint.ClientType = RemoteHubStudio.Domain.ConnectionType.Vnc;
        _endpoint.AutoSize = true;
        _endpoint.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _endpoint.Dock = System.Windows.Forms.DockStyle.Top;
        _endpoint.Margin = System.Windows.Forms.Padding.Empty;
        _endpoint.Name = "_endpoint";
        _endpoint.Size = new System.Drawing.Size(800, 150);
        _endpoint.TabIndex = 0;
        // _viewerOptionsGrid
        _viewerOptionsGrid.AutoSize = true;
        _viewerOptionsGrid.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _viewerOptionsGrid.ColumnCount = 4;
        _viewerOptionsGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
        _viewerOptionsGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        _viewerOptionsGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
        _viewerOptionsGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        _viewerOptionsGrid.Controls.Add(_fullScreenLabel, 0, 0);
        _viewerOptionsGrid.Controls.Add(_fullScreenSwitch, 1, 0);
        _viewerOptionsGrid.Controls.Add(_autoReconnectLabel, 2, 0);
        _viewerOptionsGrid.Controls.Add(_autoReconnectSwitch, 3, 0);
        _viewerOptionsGrid.Controls.Add(_viewOnlyLabel, 0, 1);
        _viewerOptionsGrid.Controls.Add(_viewOnlySwitch, 1, 1);
        _viewerOptionsGrid.Dock = System.Windows.Forms.DockStyle.Top;
        _viewerOptionsGrid.Margin = System.Windows.Forms.Padding.Empty;
        _viewerOptionsGrid.Name = "_viewerOptionsGrid";
        _viewerOptionsGrid.Padding = new System.Windows.Forms.Padding(4, 2, 4, 4);
        _viewerOptionsGrid.RowCount = 2;
        _viewerOptionsGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _viewerOptionsGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _viewerOptionsGrid.Size = new System.Drawing.Size(800, 102);
        _viewerOptionsGrid.TabIndex = 1;
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
        // _autoReconnectLabel
        _autoReconnectLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _autoReconnectLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _autoReconnectLabel.Name = "_autoReconnectLabel";
        _autoReconnectLabel.Text = "自动重连 / Auto reconnect";
        _autoReconnectLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _autoReconnectSwitch
        _autoReconnectSwitch.Name = "_autoReconnectSwitch";
        _autoReconnectSwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _autoReconnectSwitch.TabIndex = 1;
        _autoReconnectSwitch.AccessibleName = "自动重连 / Auto reconnect";
        _autoReconnectSwitch.AutoCheck = true;
        _autoReconnectSwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _autoReconnectSwitch.Size = new System.Drawing.Size(60, 32);
        // _viewOnlyLabel
        _viewOnlyLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _viewOnlyLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _viewOnlyLabel.Name = "_viewOnlyLabel";
        _viewOnlyLabel.Text = "仅查看 / View only";
        _viewOnlyLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _viewOnlySwitch
        _viewOnlySwitch.Name = "_viewOnlySwitch";
        _viewOnlySwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _viewOnlySwitch.TabIndex = 2;
        _viewOnlySwitch.AccessibleName = "仅查看 / View only";
        _viewOnlySwitch.AutoCheck = true;
        _viewOnlySwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _viewOnlySwitch.Size = new System.Drawing.Size(60, 32);
        // _layout
        _layout.AutoSize = true;
        _layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _layout.ColumnCount = 1;
        _layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _layout.Controls.Add(_endpoint, 0, 0);
        _layout.Controls.Add(_viewerOptionsGrid, 0, 1);
        _layout.Dock = System.Windows.Forms.DockStyle.Top;
        _layout.Margin = System.Windows.Forms.Padding.Empty;
        _layout.Name = "_layout";
        _layout.RowCount = 2;
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.Size = new System.Drawing.Size(800, 400);
        _layout.TabIndex = 0;
        // VncConnectionTypeOptionsPage
        AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        Controls.Add(_layout);
        Name = "VncConnectionTypeOptionsPage";
        Size = new System.Drawing.Size(800, 256);
        _viewerOptionsGrid.ResumeLayout(false);
        _layout.ResumeLayout(false);
        _layout.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private ConnectionEndpointEditor _endpoint = null!;
    private RemoteHubStudio.UI.Controls.ResponsiveFieldGrid _viewerOptionsGrid = null!;
    private AntdUI.Label _fullScreenLabel = null!;
    private AntdUI.Switch _fullScreenSwitch = null!;
    private AntdUI.Label _autoReconnectLabel = null!;
    private AntdUI.Switch _autoReconnectSwitch = null!;
    private AntdUI.Label _viewOnlyLabel = null!;
    private AntdUI.Switch _viewOnlySwitch = null!;
    private System.Windows.Forms.TableLayoutPanel _layout = null!;
}
