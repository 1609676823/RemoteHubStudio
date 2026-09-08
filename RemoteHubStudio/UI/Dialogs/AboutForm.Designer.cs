#nullable disable

namespace RemoteHubStudio.UI.Dialogs;

partial class AboutForm
{
    /// <summary>
    /// Required method for Designer support. / 设计器支持所需的方法。
    /// </summary>
    private void InitializeComponent()
    {
        _identitySection = new AntdUI.Panel();
        _identityLayout = new System.Windows.Forms.TableLayoutPanel();
        _productNameLabel = new AntdUI.Label();
        _detailsLabel = new AntdUI.Label();
        _identityTitleLabel = new AntdUI.Label();
        _linksSection = new AntdUI.Panel();
        _linksFlow = new System.Windows.Forms.FlowLayoutPanel();
        _projectLinkButton = new AntdUI.Button();
        _issuesLinkButton = new AntdUI.Button();
        _releasesLinkButton = new AntdUI.Button();
        _licenseLinkButton = new AntdUI.Button();
        _linksTitleLabel = new AntdUI.Label();
        _closeButton = new AntdUI.Button();
        _identitySection.SuspendLayout();
        _identityLayout.SuspendLayout();
        _linksSection.SuspendLayout();
        _linksFlow.SuspendLayout();
        _contentFlow.SuspendLayout();
        _footerFlow.SuspendLayout();
        SuspendLayout();
        // 
        // _identitySection
        // 
        _identitySection.BorderWidth = 1F;
        _identitySection.Controls.Add(_identityLayout);
        _identitySection.Controls.Add(_identityTitleLabel);
        _identitySection.Location = new System.Drawing.Point(16, 14);
        _identitySection.Margin = new System.Windows.Forms.Padding(0, 0, 0, 12);
        _identitySection.Name = "_identitySection";
        _identitySection.Padding = new System.Windows.Forms.Padding(8);
        _identitySection.Radius = 10;
        _identitySection.Size = new System.Drawing.Size(590, 240);
        _identitySection.TabIndex = 0;
        // 
        // _identityLayout
        // 
        _identityLayout.AutoSize = true;
        _identityLayout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _identityLayout.ColumnCount = 1;
        _identityLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _identityLayout.Controls.Add(_productNameLabel, 0, 0);
        _identityLayout.Controls.Add(_detailsLabel, 0, 1);
        _identityLayout.Dock = System.Windows.Forms.DockStyle.Top;
        _identityLayout.Location = new System.Drawing.Point(8, 44);
        _identityLayout.Name = "_identityLayout";
        _identityLayout.Padding = new System.Windows.Forms.Padding(12);
        _identityLayout.RowCount = 2;
        _identityLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 52F));
        _identityLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 112F));
        _identityLayout.Size = new System.Drawing.Size(574, 188);
        _identityLayout.TabIndex = 1;
        // 
        // _productNameLabel
        // 
        _productNameLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _productNameLabel.Font = new System.Drawing.Font("Segoe UI", 22F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        _productNameLabel.Location = new System.Drawing.Point(15, 12);
        _productNameLabel.Name = "_productNameLabel";
        _productNameLabel.Size = new System.Drawing.Size(544, 52);
        _productNameLabel.TabIndex = 0;
        _productNameLabel.Text = "产品名称 / Product name";
        _productNameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // 
        // _detailsLabel
        // 
        _detailsLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _detailsLabel.Location = new System.Drawing.Point(15, 64);
        _detailsLabel.Name = "_detailsLabel";
        _detailsLabel.Size = new System.Drawing.Size(544, 112);
        _detailsLabel.TabIndex = 1;
        _detailsLabel.Text = "版本 / Version: —\r\n发布者 / Publisher: —\r\n许可证 / License: —\r\n版权 / Copyright: —";
        _detailsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        _detailsLabel.TextMultiLine = true;
        // 
        // _identityTitleLabel
        // 
        _identityTitleLabel.Dock = System.Windows.Forms.DockStyle.Top;
        _identityTitleLabel.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        _identityTitleLabel.Height = 36;
        _identityTitleLabel.Location = new System.Drawing.Point(8, 8);
        _identityTitleLabel.Name = "_identityTitleLabel";
        _identityTitleLabel.Padding = new System.Windows.Forms.Padding(12, 0, 8, 0);
        _identityTitleLabel.Size = new System.Drawing.Size(574, 36);
        _identityTitleLabel.TabIndex = 0;
        _identityTitleLabel.Text = "产品信息 / Product information";
        _identityTitleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // 
        // _linksSection
        // 
        _linksSection.BorderWidth = 1F;
        _linksSection.Controls.Add(_linksFlow);
        _linksSection.Controls.Add(_linksTitleLabel);
        _linksSection.Location = new System.Drawing.Point(16, 266);
        _linksSection.Margin = new System.Windows.Forms.Padding(0, 0, 0, 12);
        _linksSection.Name = "_linksSection";
        _linksSection.Padding = new System.Windows.Forms.Padding(8);
        _linksSection.Radius = 10;
        _linksSection.Size = new System.Drawing.Size(590, 176);
        _linksSection.TabIndex = 1;
        // 
        // _linksFlow
        // 
        _linksFlow.AutoSize = true;
        _linksFlow.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _linksFlow.Controls.Add(_projectLinkButton);
        _linksFlow.Controls.Add(_issuesLinkButton);
        _linksFlow.Controls.Add(_releasesLinkButton);
        _linksFlow.Controls.Add(_licenseLinkButton);
        _linksFlow.Dock = System.Windows.Forms.DockStyle.Top;
        _linksFlow.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
        _linksFlow.Location = new System.Drawing.Point(8, 44);
        _linksFlow.Name = "_linksFlow";
        _linksFlow.Padding = new System.Windows.Forms.Padding(8);
        _linksFlow.Size = new System.Drawing.Size(574, 124);
        _linksFlow.TabIndex = 1;
        _linksFlow.WrapContents = true;
        // 
        // _projectLinkButton
        // 
        _projectLinkButton.Height = 42;
        _projectLinkButton.IconSvg = "HomeOutlined";
        _projectLinkButton.Location = new System.Drawing.Point(14, 14);
        _projectLinkButton.Margin = new System.Windows.Forms.Padding(6);
        _projectLinkButton.Name = "_projectLinkButton";
        _projectLinkButton.Size = new System.Drawing.Size(220, 42);
        _projectLinkButton.TabIndex = 0;
        _projectLinkButton.Text = "项目主页 / Project";
        _projectLinkButton.Click += HandleOpenLinkClick;
        // 
        // _issuesLinkButton
        // 
        _issuesLinkButton.Height = 42;
        _issuesLinkButton.IconSvg = "BugOutlined";
        _issuesLinkButton.Location = new System.Drawing.Point(246, 14);
        _issuesLinkButton.Margin = new System.Windows.Forms.Padding(6);
        _issuesLinkButton.Name = "_issuesLinkButton";
        _issuesLinkButton.Size = new System.Drawing.Size(220, 42);
        _issuesLinkButton.TabIndex = 1;
        _issuesLinkButton.Text = "问题跟踪 / Issues";
        _issuesLinkButton.Click += HandleOpenLinkClick;
        // 
        // _releasesLinkButton
        // 
        _releasesLinkButton.Height = 42;
        _releasesLinkButton.IconSvg = "CloudDownloadOutlined";
        _releasesLinkButton.Location = new System.Drawing.Point(14, 68);
        _releasesLinkButton.Margin = new System.Windows.Forms.Padding(6);
        _releasesLinkButton.Name = "_releasesLinkButton";
        _releasesLinkButton.Size = new System.Drawing.Size(220, 42);
        _releasesLinkButton.TabIndex = 2;
        _releasesLinkButton.Text = "版本发布 / Releases";
        _releasesLinkButton.Click += HandleOpenLinkClick;
        // 
        // _licenseLinkButton
        // 
        _licenseLinkButton.Height = 42;
        _licenseLinkButton.IconSvg = "FileProtectOutlined";
        _licenseLinkButton.Location = new System.Drawing.Point(246, 68);
        _licenseLinkButton.Margin = new System.Windows.Forms.Padding(6);
        _licenseLinkButton.Name = "_licenseLinkButton";
        _licenseLinkButton.Size = new System.Drawing.Size(220, 42);
        _licenseLinkButton.TabIndex = 3;
        _licenseLinkButton.Text = "开源许可 / License";
        _licenseLinkButton.Click += HandleOpenLinkClick;
        // 
        // _linksTitleLabel
        // 
        _linksTitleLabel.Dock = System.Windows.Forms.DockStyle.Top;
        _linksTitleLabel.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        _linksTitleLabel.Height = 36;
        _linksTitleLabel.Location = new System.Drawing.Point(8, 8);
        _linksTitleLabel.Name = "_linksTitleLabel";
        _linksTitleLabel.Padding = new System.Windows.Forms.Padding(12, 0, 8, 0);
        _linksTitleLabel.Size = new System.Drawing.Size(574, 36);
        _linksTitleLabel.TabIndex = 0;
        _linksTitleLabel.Text = "开源与支持 / Open source and support";
        _linksTitleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // 
        // _closeButton
        // 
        _closeButton.Height = 38;
        _closeButton.Location = new System.Drawing.Point(506, 12);
        _closeButton.Margin = new System.Windows.Forms.Padding(8, 0, 0, 0);
        _closeButton.Name = "_closeButton";
        _closeButton.Size = new System.Drawing.Size(108, 38);
        _closeButton.TabIndex = 0;
        _closeButton.Text = "关闭 / Close";
        _closeButton.Type = AntdUI.TTypeMini.Primary;
        _closeButton.Click += HandleCloseClick;
        // 
        // AboutForm
        // 
        AcceptButton = _closeButton;
        AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        CancelButton = _closeButton;
        ClientSize = new System.Drawing.Size(640, 500);
        _contentFlow.Controls.Add(_identitySection);
        _contentFlow.Controls.Add(_linksSection);
        _footerFlow.Controls.Add(_closeButton);
        _header.Text = "关于 / About";
        MinimumSize = new System.Drawing.Size(520, 400);
        Name = "AboutForm";
        Text = "关于 / About";
        _identitySection.ResumeLayout(false);
        _identitySection.PerformLayout();
        _identityLayout.ResumeLayout(false);
        _linksSection.ResumeLayout(false);
        _linksSection.PerformLayout();
        _linksFlow.ResumeLayout(false);
        _contentFlow.ResumeLayout(false);
        _contentFlow.PerformLayout();
        _footerFlow.ResumeLayout(false);
        ResumeLayout(false);
    }

    private AntdUI.Panel _identitySection;
    private System.Windows.Forms.TableLayoutPanel _identityLayout;
    private AntdUI.Label _productNameLabel;
    private AntdUI.Label _detailsLabel;
    private AntdUI.Label _identityTitleLabel;
    private AntdUI.Panel _linksSection;
    private System.Windows.Forms.FlowLayoutPanel _linksFlow;
    private AntdUI.Button _projectLinkButton;
    private AntdUI.Button _issuesLinkButton;
    private AntdUI.Button _releasesLinkButton;
    private AntdUI.Button _licenseLinkButton;
    private AntdUI.Label _linksTitleLabel;
    private AntdUI.Button _closeButton;
}
