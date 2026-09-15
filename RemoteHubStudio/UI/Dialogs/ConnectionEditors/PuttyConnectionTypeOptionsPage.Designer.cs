namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

partial class PuttyConnectionTypeOptionsPage
{
    /// <summary>Creates the complete source-designer control tree. / 创建源设计器可直接加载的完整控件树。</summary>
    private void InitializeComponent()
    {
        _endpoint = new ConnectionEndpointEditor();
        SuspendLayout();
        // _endpoint
        _endpoint.ClientType = RemoteHubStudio.Domain.ConnectionType.Putty;
        _endpoint.AutoSize = true;
        _endpoint.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _endpoint.Dock = System.Windows.Forms.DockStyle.Top;
        _endpoint.Margin = System.Windows.Forms.Padding.Empty;
        _endpoint.Name = "_endpoint";
        _endpoint.Size = new System.Drawing.Size(800, 150);
        _endpoint.TabIndex = 0;
        // PuttyConnectionTypeOptionsPage
        AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        Controls.Add(_endpoint);
        Name = "PuttyConnectionTypeOptionsPage";
        Size = new System.Drawing.Size(800, 240);
        ResumeLayout(false);
        PerformLayout();
    }

    private ConnectionEndpointEditor _endpoint = null!;
}
