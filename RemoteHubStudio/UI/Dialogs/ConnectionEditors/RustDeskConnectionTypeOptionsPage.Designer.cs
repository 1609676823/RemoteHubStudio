namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

partial class RustDeskConnectionTypeOptionsPage
{
    /// <summary>Creates the complete source-designer control tree. / 创建源设计器可直接加载的完整控件树。</summary>
    private void InitializeComponent()
    {
        _endpoint = new ConnectionEndpointEditor();
        _optionsGrid = new RemoteHubStudio.UI.Controls.ResponsiveFieldGrid();
        _serverLabel = new AntdUI.Label();
        _serverInput = new AntdUI.Input();
        _serverKeyLabel = new AntdUI.Label();
        _serverKeyInput = new AntdUI.Input();
        _forceRelayLabel = new AntdUI.Label();
        _forceRelaySwitch = new AntdUI.Switch();
        _compatibilityNote = new AntdUI.Label();
        _layout = new System.Windows.Forms.TableLayoutPanel();
        _layout.SuspendLayout();
        _optionsGrid.SuspendLayout();
        SuspendLayout();
        // _endpoint
        _endpoint.ClientType = RemoteHubStudio.Domain.ConnectionType.RustDesk;
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
        _optionsGrid.Controls.Add(_serverLabel, 0, 0);
        _optionsGrid.Controls.Add(_serverInput, 1, 0);
        _optionsGrid.Controls.Add(_serverKeyLabel, 2, 0);
        _optionsGrid.Controls.Add(_serverKeyInput, 3, 0);
        _optionsGrid.Controls.Add(_forceRelayLabel, 0, 1);
        _optionsGrid.Controls.Add(_forceRelaySwitch, 1, 1);
        _optionsGrid.Dock = System.Windows.Forms.DockStyle.Top;
        _optionsGrid.Margin = System.Windows.Forms.Padding.Empty;
        _optionsGrid.Name = "_optionsGrid";
        _optionsGrid.Padding = new System.Windows.Forms.Padding(4, 2, 4, 4);
        _optionsGrid.RowCount = 2;
        _optionsGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _optionsGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        _optionsGrid.Size = new System.Drawing.Size(800, 102);
        _optionsGrid.TabIndex = 1;
        // _serverLabel
        _serverLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _serverLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _serverLabel.Name = "_serverLabel";
        _serverLabel.Text = "服务器 / Server";
        _serverLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _serverInput
        _serverInput.Name = "_serverInput";
        _serverInput.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _serverInput.TabIndex = 0;
        _serverInput.Dock = System.Windows.Forms.DockStyle.Fill;
        _serverInput.Size = new System.Drawing.Size(240, 38);
        _serverInput.Radius = 8;
        _serverInput.PlaceholderText = "可选的自建 ID/中继服务器 / Optional self-hosted ID/relay server";
        _serverInput.AllowClear = true;
        // _serverKeyLabel
        _serverKeyLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _serverKeyLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _serverKeyLabel.Name = "_serverKeyLabel";
        _serverKeyLabel.Text = "服务器公钥 / Server key";
        _serverKeyLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _serverKeyInput
        _serverKeyInput.Name = "_serverKeyInput";
        _serverKeyInput.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _serverKeyInput.TabIndex = 1;
        _serverKeyInput.Dock = System.Windows.Forms.DockStyle.Fill;
        _serverKeyInput.Size = new System.Drawing.Size(240, 38);
        _serverKeyInput.Radius = 8;
        _serverKeyInput.PlaceholderText = "可选的服务器公钥 / Optional server public key";
        _serverKeyInput.AllowClear = true;
        // _forceRelayLabel
        _forceRelayLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _forceRelayLabel.Margin = new System.Windows.Forms.Padding(8, 5, 4, 5);
        _forceRelayLabel.Name = "_forceRelayLabel";
        _forceRelayLabel.Text = "强制中继 / Force relay";
        _forceRelayLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _forceRelaySwitch
        _forceRelaySwitch.Name = "_forceRelaySwitch";
        _forceRelaySwitch.Margin = new System.Windows.Forms.Padding(4, 5, 10, 5);
        _forceRelaySwitch.TabIndex = 2;
        _forceRelaySwitch.AccessibleName = "强制中继 / Force relay";
        _forceRelaySwitch.AutoCheck = true;
        _forceRelaySwitch.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _forceRelaySwitch.Size = new System.Drawing.Size(60, 32);
        // _compatibilityNote
        _compatibilityNote.AutoSize = false;
        _compatibilityNote.Dock = System.Windows.Forms.DockStyle.Top;
        _compatibilityNote.Size = new System.Drawing.Size(794, 84);
        _compatibilityNote.TextMultiLine = true;
        _compatibilityNote.Name = "_compatibilityNote";
        _compatibilityNote.Padding = new System.Windows.Forms.Padding(12, 8, 12, 12);
        _compatibilityNote.Text = "提示：设置服务器公钥后，RustDesk 不会自动传递一次性密码，而会由客户端提示输入。 / Note: When a server key is configured, RustDesk prompts for the one-time password instead of receiving it automatically.";
        _compatibilityNote.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // _layout
        _layout.AutoSize = true;
        _layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _layout.ColumnCount = 1;
        _layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _layout.Controls.Add(_endpoint, 0, 0);
        _layout.Controls.Add(_optionsGrid, 0, 1);
        _layout.Controls.Add(_compatibilityNote, 0, 2);
        _layout.Dock = System.Windows.Forms.DockStyle.Top;
        _layout.Margin = System.Windows.Forms.Padding.Empty;
        _layout.Name = "_layout";
        _layout.RowCount = 3;
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.Size = new System.Drawing.Size(800, 400);
        _layout.TabIndex = 0;
        // RustDeskConnectionTypeOptionsPage
        AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        Controls.Add(_layout);
        Name = "RustDeskConnectionTypeOptionsPage";
        Size = new System.Drawing.Size(800, 346);
        _optionsGrid.ResumeLayout(false);
        _layout.ResumeLayout(false);
        _layout.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private ConnectionEndpointEditor _endpoint = null!;
    private RemoteHubStudio.UI.Controls.ResponsiveFieldGrid _optionsGrid = null!;
    private AntdUI.Label _serverLabel = null!;
    private AntdUI.Input _serverInput = null!;
    private AntdUI.Label _serverKeyLabel = null!;
    private AntdUI.Input _serverKeyInput = null!;
    private AntdUI.Label _forceRelayLabel = null!;
    private AntdUI.Switch _forceRelaySwitch = null!;
    private AntdUI.Label _compatibilityNote = null!;
    private System.Windows.Forms.TableLayoutPanel _layout = null!;
}
