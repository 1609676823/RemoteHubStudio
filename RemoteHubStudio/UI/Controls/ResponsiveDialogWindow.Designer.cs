#nullable disable

namespace RemoteHubStudio.UI.Controls;

partial class ResponsiveDialogWindow
{
    /// <summary>
    /// Required method for Designer support. / 设计器支持所需的方法。
    /// </summary>
    private void InitializeComponent()
    {
        _header = new AntdUI.PageHeader();
        _scrollHost = new System.Windows.Forms.Panel();
        _contentFlow = new System.Windows.Forms.FlowLayoutPanel();
        _footerPanel = new AntdUI.Panel();
        _footerFlow = new System.Windows.Forms.FlowLayoutPanel();
        _scrollHost.SuspendLayout();
        _footerPanel.SuspendLayout();
        SuspendLayout();
        // 
        // _header
        // 
        _header.DividerShow = true;
        _header.Dock = System.Windows.Forms.DockStyle.Top;
        _header.EnableButtonTooltip = true;
        _header.EnableDoubleClickMaximize = false;
        _header.Height = 48;
        _header.Location = new System.Drawing.Point(1, 1);
        _header.MaximizeBox = false;
        _header.MinimizeBox = false;
        _header.Name = "_header";
        _header.ShowButton = true;
        _header.Size = new System.Drawing.Size(638, 48);
        _header.TabIndex = 0;
        _header.Text = "Dialog";
        // 
        // _scrollHost
        // 
        _scrollHost.AutoScroll = true;
        _scrollHost.Controls.Add(_contentFlow);
        _scrollHost.Dock = System.Windows.Forms.DockStyle.Fill;
        _scrollHost.Location = new System.Drawing.Point(1, 49);
        _scrollHost.Margin = System.Windows.Forms.Padding.Empty;
        _scrollHost.Name = "_scrollHost";
        _scrollHost.Padding = System.Windows.Forms.Padding.Empty;
        _scrollHost.Size = new System.Drawing.Size(638, 386);
        _scrollHost.TabIndex = 1;
        _scrollHost.SizeChanged += HandleScrollHostSizeChanged;
        // 
        // _contentFlow
        // 
        _contentFlow.AutoSize = true;
        _contentFlow.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _contentFlow.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
        _contentFlow.Location = System.Drawing.Point.Empty;
        _contentFlow.Margin = System.Windows.Forms.Padding.Empty;
        _contentFlow.Name = "_contentFlow";
        _contentFlow.Padding = new System.Windows.Forms.Padding(16, 14, 16, 18);
        _contentFlow.Size = new System.Drawing.Size(32, 32);
        _contentFlow.TabIndex = 0;
        _contentFlow.WrapContents = false;
        // 
        // _footerPanel
        // 
        _footerPanel.BorderWidth = 0F;
        _footerPanel.Controls.Add(_footerFlow);
        _footerPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
        _footerPanel.Height = 64;
        _footerPanel.Location = new System.Drawing.Point(1, 435);
        _footerPanel.Name = "_footerPanel";
        _footerPanel.Radius = 0;
        _footerPanel.Size = new System.Drawing.Size(638, 64);
        _footerPanel.TabIndex = 2;
        // 
        // _footerFlow
        // 
        _footerFlow.AutoSize = false;
        _footerFlow.Dock = System.Windows.Forms.DockStyle.Fill;
        _footerFlow.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
        _footerFlow.Location = System.Drawing.Point.Empty;
        _footerFlow.Name = "_footerFlow";
        _footerFlow.Padding = new System.Windows.Forms.Padding(16, 12, 16, 10);
        _footerFlow.Size = new System.Drawing.Size(638, 64);
        _footerFlow.TabIndex = 0;
        _footerFlow.WrapContents = false;
        // 
        // ResponsiveDialogWindow
        // 
        AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        ClientSize = new System.Drawing.Size(640, 500);
        Controls.Add(_scrollHost);
        Controls.Add(_footerPanel);
        Controls.Add(_header);
        MinimumSize = new System.Drawing.Size(520, 400);
        Name = "ResponsiveDialogWindow";
        Padding = new System.Windows.Forms.Padding(1);
        ShowInTaskbar = false;
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        Text = "Dialog";
        _scrollHost.ResumeLayout(false);
        _scrollHost.PerformLayout();
        _footerPanel.ResumeLayout(false);
        ResumeLayout(false);
    }

    protected AntdUI.PageHeader _header;
    private System.Windows.Forms.Panel _scrollHost;
    protected System.Windows.Forms.FlowLayoutPanel _contentFlow;
    private AntdUI.Panel _footerPanel;
    protected System.Windows.Forms.FlowLayoutPanel _footerFlow;
}
