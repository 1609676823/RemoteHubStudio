using RemoteHubStudio.UI.Controls;

namespace RemoteHubStudio.UI.Dialogs;

partial class ConnectionEditorForm
{
    /// <summary>
    /// Creates the designer-serializable common connection-editor shell. Type-specific
    /// pages remain runtime-only because they depend on the selected connection type.
    /// / 创建可由设计器序列化的公共连接编辑外壳；类型专属页仍按所选连接类型在运行时创建。
    /// </summary>
    private void InitializeComponent()
    {
        _basicsSection = new AntdUI.Panel();
        _basicsTitle = new AntdUI.Label();
        _basicsGrid = new ResponsiveFieldGrid();
        _nameLabel = new AntdUI.Label();
        _nameInput = new AntdUI.Input();
        _typeLabel = new AntdUI.Label();
        _typeSelect = new AntdUI.Select();
        _groupLabel = new AntdUI.Label();
        _groupSelect = new AntdUI.Select();
        _expiresLabel = new AntdUI.Label();
        _expiresPicker = new AntdUI.DatePicker();
        _favoriteLabel = new AntdUI.Label();
        _favoriteSwitch = new AntdUI.Switch();
        _notesLabel = new AntdUI.Label();
        _notesInput = new AntdUI.Input();
        _advancedSection = new AntdUI.Panel();
        _advancedTitle = new AntdUI.Label();
        _clientGrid = new ResponsiveFieldGrid();
        _privateKeyLabel = new AntdUI.Label();
        _privateKeyInput = new AntdUI.Input();
        _executableLabel = new AntdUI.Label();
        _executableInput = new AntdUI.Input();
        _argumentsLabel = new AntdUI.Label();
        _argumentsInput = new AntdUI.Input();
        _clientOptionsLabel = new AntdUI.Label();
        _clientOptionsInput = new AntdUI.Input();
        _saveButton = new AntdUI.Button();
        _cancelButton = new AntdUI.Button();
        _rdpSection = new AntdUI.Panel();
        _rdpTitle = new AntdUI.Label();
        _rdpPage = new RemoteHubStudio.UI.Dialogs.ConnectionEditors.RdpConnectionTypeOptionsPage();
        _rdpSection.SuspendLayout();
        _basicsSection.SuspendLayout();
        _basicsGrid.SuspendLayout();
        _advancedSection.SuspendLayout();
        _clientGrid.SuspendLayout();
        _contentFlow.SuspendLayout();
        _footerFlow.SuspendLayout();
        SuspendLayout();
        //
        // _basicsSection
        //
        _basicsSection.BorderWidth = 1F;
        _basicsSection.Controls.Add(_basicsGrid);
        _basicsSection.Controls.Add(_basicsTitle);
        _basicsSection.Location = new Point(16, 14);
        _basicsSection.Margin = new Padding(0, 0, 0, 12);
        _basicsSection.Name = "_basicsSection";
        _basicsSection.Padding = new Padding(8);
        _basicsSection.Radius = 10;
        _basicsSection.Size = new Size(926, 242);
        _basicsSection.TabIndex = 0;
        //
        // _basicsTitle
        //
        _basicsTitle.Dock = DockStyle.Top;
        _basicsTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
        _basicsTitle.Location = new Point(8, 8);
        _basicsTitle.Name = "_basicsTitle";
        _basicsTitle.Padding = new Padding(12, 0, 8, 0);
        _basicsTitle.Size = new Size(910, 36);
        _basicsTitle.TabIndex = 0;
        _basicsTitle.Text = "基本信息 / Basics";
        _basicsTitle.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _basicsGrid
        //
        _basicsGrid.AutoSize = true;
        _basicsGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _basicsGrid.ColumnCount = 4;
        _basicsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        _basicsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _basicsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        _basicsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _basicsGrid.Controls.Add(_nameLabel, 0, 0);
        _basicsGrid.Controls.Add(_nameInput, 1, 0);
        _basicsGrid.Controls.Add(_typeLabel, 2, 0);
        _basicsGrid.Controls.Add(_typeSelect, 3, 0);
        _basicsGrid.Controls.Add(_groupLabel, 0, 1);
        _basicsGrid.Controls.Add(_groupSelect, 1, 1);
        _basicsGrid.Controls.Add(_expiresLabel, 2, 1);
        _basicsGrid.Controls.Add(_expiresPicker, 3, 1);
        _basicsGrid.Controls.Add(_favoriteLabel, 0, 2);
        _basicsGrid.Controls.Add(_favoriteSwitch, 1, 2);
        _basicsGrid.Controls.Add(_notesLabel, 2, 2);
        _basicsGrid.Controls.Add(_notesInput, 3, 2);
        _basicsGrid.Dock = DockStyle.Top;
        _basicsGrid.Location = new Point(8, 44);
        _basicsGrid.Margin = Padding.Empty;
        _basicsGrid.Name = "_basicsGrid";
        _basicsGrid.Padding = new Padding(4, 2, 4, 4);
        _basicsGrid.RowCount = 3;
        _basicsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        _basicsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        _basicsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 96F));
        _basicsGrid.Size = new Size(910, 192);
        _basicsGrid.TabIndex = 1;
        //
        // _nameLabel
        //
        _nameLabel.Dock = DockStyle.Fill;
        _nameLabel.Margin = new Padding(8, 5, 4, 5);
        _nameLabel.Name = "_nameLabel";
        _nameLabel.TabIndex = 0;
        _nameLabel.Text = "名称 / Name";
        _nameLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _nameInput
        //
        _nameInput.AllowClear = true;
        _nameInput.Dock = DockStyle.Fill;
        _nameInput.Margin = new Padding(4, 5, 10, 5);
        _nameInput.Name = "_nameInput";
        _nameInput.PlaceholderText = "例如：生产环境数据库 / Production database";
        _nameInput.Radius = 8;
        _nameInput.TabIndex = 1;
        //
        // _typeLabel
        //
        _typeLabel.Dock = DockStyle.Fill;
        _typeLabel.Margin = new Padding(8, 5, 4, 5);
        _typeLabel.Name = "_typeLabel";
        _typeLabel.TabIndex = 2;
        _typeLabel.Text = "客户端 / Client";
        _typeLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _typeSelect
        //
        _typeSelect.AllowClear = false;
        _typeSelect.Dock = DockStyle.Fill;
        _typeSelect.DropDownArrow = true;
        _typeSelect.ListAutoWidth = true;
        _typeSelect.Margin = new Padding(4, 5, 10, 5);
        _typeSelect.Name = "_typeSelect";
        _typeSelect.PlaceholderText = "选择客户端 / Select client";
        _typeSelect.Radius = 8;
        _typeSelect.TabIndex = 3;
        _typeSelect.WheelModifyEnabled = false;
        //
        // _groupLabel
        //
        _groupLabel.Dock = DockStyle.Fill;
        _groupLabel.Margin = new Padding(8, 5, 4, 5);
        _groupLabel.Name = "_groupLabel";
        _groupLabel.TabIndex = 4;
        _groupLabel.Text = "分组 / Group";
        _groupLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _groupSelect
        //
        _groupSelect.AllowClear = true;
        _groupSelect.Dock = DockStyle.Fill;
        _groupSelect.DropDownArrow = true;
        _groupSelect.ListAutoWidth = true;
        _groupSelect.Margin = new Padding(4, 5, 10, 5);
        _groupSelect.Name = "_groupSelect";
        _groupSelect.PlaceholderText = "无分组 / No group";
        _groupSelect.Radius = 8;
        _groupSelect.TabIndex = 5;
        _groupSelect.WheelModifyEnabled = false;
        //
        // _expiresLabel
        //
        _expiresLabel.Dock = DockStyle.Fill;
        _expiresLabel.Margin = new Padding(8, 5, 4, 5);
        _expiresLabel.Name = "_expiresLabel";
        _expiresLabel.TabIndex = 6;
        _expiresLabel.Text = "到期日期 / Expires";
        _expiresLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _expiresPicker
        //
        _expiresPicker.AllowClear = true;
        _expiresPicker.Dock = DockStyle.Fill;
        _expiresPicker.Margin = new Padding(4, 5, 10, 5);
        _expiresPicker.Name = "_expiresPicker";
        _expiresPicker.PlaceholderText = "无到期日期 / No expiration";
        _expiresPicker.Radius = 8;
        _expiresPicker.TabIndex = 7;
        //
        // _favoriteLabel
        //
        _favoriteLabel.Dock = DockStyle.Fill;
        _favoriteLabel.Margin = new Padding(8, 5, 4, 5);
        _favoriteLabel.Name = "_favoriteLabel";
        _favoriteLabel.TabIndex = 8;
        _favoriteLabel.Text = "收藏 / Favorite";
        _favoriteLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _favoriteSwitch
        //
        _favoriteSwitch.Anchor = AnchorStyles.Left;
        _favoriteSwitch.AutoCheck = true;
        _favoriteSwitch.Margin = new Padding(4, 5, 10, 5);
        _favoriteSwitch.Name = "_favoriteSwitch";
        _favoriteSwitch.Size = new Size(60, 32);
        _favoriteSwitch.TabIndex = 9;
        //
        // _notesLabel
        //
        _notesLabel.Dock = DockStyle.Fill;
        _notesLabel.Margin = new Padding(8, 5, 4, 5);
        _notesLabel.Name = "_notesLabel";
        _notesLabel.TabIndex = 10;
        _notesLabel.Text = "备注 / Notes";
        _notesLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _notesInput
        //
        _notesInput.AllowClear = true;
        _notesInput.Dock = DockStyle.Fill;
        _notesInput.Height = 86;
        _notesInput.Margin = new Padding(4, 5, 10, 5);
        _notesInput.Multiline = true;
        _notesInput.Name = "_notesInput";
        _notesInput.PlaceholderText = "连接备注 / Connection notes";
        _notesInput.Radius = 8;
        _notesInput.TabIndex = 11;
        //
        // _advancedSection
        //
        _advancedSection.BorderWidth = 1F;
        _advancedSection.Controls.Add(_clientGrid);
        _advancedSection.Controls.Add(_advancedTitle);
        _advancedSection.Location = new Point(16, 268);
        _advancedSection.Margin = new Padding(0, 0, 0, 12);
        _advancedSection.Name = "_advancedSection";
        _advancedSection.Padding = new Padding(8);
        _advancedSection.Radius = 10;
        _advancedSection.Size = new Size(926, 218);
        _advancedSection.TabIndex = 1;
        //
        // _advancedTitle
        //
        _advancedTitle.Dock = DockStyle.Top;
        _advancedTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
        _advancedTitle.Location = new Point(8, 8);
        _advancedTitle.Name = "_advancedTitle";
        _advancedTitle.Padding = new Padding(12, 0, 8, 0);
        _advancedTitle.Size = new Size(910, 36);
        _advancedTitle.TabIndex = 0;
        _advancedTitle.Text = "客户端与高级覆盖 / Client and advanced overrides";
        _advancedTitle.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _clientGrid
        //
        _clientGrid.AutoSize = true;
        _clientGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _clientGrid.ColumnCount = 4;
        _clientGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        _clientGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _clientGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        _clientGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _clientGrid.Controls.Add(_privateKeyLabel, 0, 0);
        _clientGrid.Controls.Add(_privateKeyInput, 1, 0);
        _clientGrid.Controls.Add(_executableLabel, 2, 0);
        _clientGrid.Controls.Add(_executableInput, 3, 0);
        _clientGrid.Controls.Add(_argumentsLabel, 0, 1);
        _clientGrid.Controls.Add(_argumentsInput, 1, 1);
        _clientGrid.Controls.Add(_clientOptionsLabel, 2, 1);
        _clientGrid.Controls.Add(_clientOptionsInput, 3, 1);
        _clientGrid.Dock = DockStyle.Top;
        _clientGrid.Location = new Point(8, 44);
        _clientGrid.Margin = Padding.Empty;
        _clientGrid.Name = "_clientGrid";
        _clientGrid.Padding = new Padding(4, 2, 4, 4);
        _clientGrid.RowCount = 2;
        _clientGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        _clientGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
        _clientGrid.Size = new Size(910, 168);
        _clientGrid.TabIndex = 1;
        //
        // _privateKeyLabel
        //
        _privateKeyLabel.Dock = DockStyle.Fill;
        _privateKeyLabel.Margin = new Padding(8, 5, 4, 5);
        _privateKeyLabel.Name = "_privateKeyLabel";
        _privateKeyLabel.TabIndex = 0;
        _privateKeyLabel.Text = "私钥路径 / Private key";
        _privateKeyLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _privateKeyInput
        //
        _privateKeyInput.AllowClear = true;
        _privateKeyInput.Dock = DockStyle.Fill;
        _privateKeyInput.Margin = new Padding(4, 5, 10, 5);
        _privateKeyInput.Name = "_privateKeyInput";
        _privateKeyInput.PlaceholderText = "可选私钥文件 / Optional private-key file";
        _privateKeyInput.Radius = 8;
        _privateKeyInput.SuffixSvg = "FolderOpenOutlined";
        _privateKeyInput.TabIndex = 1;
        //
        // _executableLabel
        //
        _executableLabel.Dock = DockStyle.Fill;
        _executableLabel.Margin = new Padding(8, 5, 4, 5);
        _executableLabel.Name = "_executableLabel";
        _executableLabel.TabIndex = 2;
        _executableLabel.Text = "程序覆盖 / Executable";
        _executableLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _executableInput
        //
        _executableInput.AllowClear = true;
        _executableInput.Dock = DockStyle.Fill;
        _executableInput.Margin = new Padding(4, 5, 10, 5);
        _executableInput.Name = "_executableInput";
        _executableInput.PlaceholderText = "可选客户端路径覆盖 / Optional client executable override";
        _executableInput.Radius = 8;
        _executableInput.SuffixSvg = "FolderOpenOutlined";
        _executableInput.TabIndex = 3;
        //
        // _argumentsLabel
        //
        _argumentsLabel.Dock = DockStyle.Fill;
        _argumentsLabel.Margin = new Padding(8, 5, 4, 5);
        _argumentsLabel.Name = "_argumentsLabel";
        _argumentsLabel.TabIndex = 4;
        _argumentsLabel.Text = "命令参数覆盖 / Argument override";
        _argumentsLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _argumentsInput
        //
        _argumentsInput.AllowClear = true;
        _argumentsInput.Dock = DockStyle.Fill;
        _argumentsInput.Height = 86;
        _argumentsInput.Margin = new Padding(4, 5, 10, 5);
        _argumentsInput.Multiline = true;
        _argumentsInput.Name = "_argumentsInput";
        _argumentsInput.PlaceholderText = "非空时覆盖内置命令：{host} {port} {username} {password} {key}";
        _argumentsInput.Radius = 8;
        _argumentsInput.TabIndex = 5;
        //
        // _clientOptionsLabel
        //
        _clientOptionsLabel.Dock = DockStyle.Fill;
        _clientOptionsLabel.Margin = new Padding(8, 5, 4, 5);
        _clientOptionsLabel.Name = "_clientOptionsLabel";
        _clientOptionsLabel.TabIndex = 6;
        _clientOptionsLabel.Text = "未建模扩展选项 / Unmodeled options";
        _clientOptionsLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _clientOptionsInput
        //
        _clientOptionsInput.AllowClear = true;
        _clientOptionsInput.Dock = DockStyle.Fill;
        _clientOptionsInput.Height = 110;
        _clientOptionsInput.Margin = new Padding(4, 5, 10, 5);
        _clientOptionsInput.Multiline = true;
        _clientOptionsInput.Name = "_clientOptionsInput";
        _clientOptionsInput.PlaceholderText = "仅未建模扩展项：每行 key=value；专属参数请在上方设置";
        _clientOptionsInput.Radius = 8;
        _clientOptionsInput.TabIndex = 7;
        //
        // _saveButton
        //
        _saveButton.Height = 38;
        _saveButton.Margin = new Padding(8, 0, 0, 0);
        _saveButton.Name = "_saveButton";
        _saveButton.Size = new Size(112, 38);
        _saveButton.TabIndex = 0;
        _saveButton.Text = "保存 / Save";
        _saveButton.Type = AntdUI.TTypeMini.Primary;
        //
        // _cancelButton
        //
        _cancelButton.Height = 38;
        _cancelButton.Margin = new Padding(8, 0, 0, 0);
        _cancelButton.Name = "_cancelButton";
        _cancelButton.Size = new Size(104, 38);
        _cancelButton.TabIndex = 1;
        _cancelButton.Text = "取消 / Cancel";
        //
        // _rdpSection: serialize a real default page so the form designer also shows endpoint/options controls.
        // / 序列化实际的默认参数页，让窗体设计器也能显示目标与协议选项。
        _rdpPage.AutoSize = true;
        _rdpPage.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _rdpPage.Dock = DockStyle.Top;
        _rdpPage.Margin = Padding.Empty;
        _rdpPage.Name = "_rdpPage";
        _rdpPage.Size = new Size(910, 688);
        _rdpTitle.Dock = DockStyle.Top;
        _rdpTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
        _rdpTitle.Size = new Size(910, 36);
        _rdpTitle.Name = "_rdpTitle";
        _rdpTitle.Padding = new Padding(12, 0, 8, 0);
        _rdpTitle.Text = "远程桌面 / Remote Desktop";
        _rdpTitle.TextAlign = ContentAlignment.MiddleLeft;
        _rdpSection.Controls.Add(_rdpPage);
        _rdpSection.Controls.Add(_rdpTitle);
        _rdpSection.BorderWidth = 1F;
        _rdpSection.Radius = 10;
        _rdpSection.Margin = new Padding(0, 0, 0, 12);
        _rdpSection.Padding = new Padding(8);
        _rdpSection.Name = "_rdpSection";
        _rdpSection.Size = new Size(926, 740);
        _rdpSection.TabIndex = 1;
        // ConnectionEditorForm
        //
        AcceptButton = _saveButton;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        CancelButton = _cancelButton;
        _contentFlow.Controls.Add(_basicsSection);
        _contentFlow.Controls.Add(_rdpSection);
        _contentFlow.Controls.Add(_advancedSection);
        _footerFlow.Controls.Add(_saveButton);
        _footerFlow.Controls.Add(_cancelButton);
        _header.Text = "新增连接 / Add connection";
        MinimumSize = new Size(680, 500);
        Name = "ConnectionEditorForm";
        ClientSize = new Size(980, 760);
        Text = "新增连接 / Add connection";
        _basicsGrid.ResumeLayout(false);
        _basicsSection.ResumeLayout(false);
        _basicsSection.PerformLayout();
        _clientGrid.ResumeLayout(false);
        _rdpSection.ResumeLayout(false);
        _rdpSection.PerformLayout();
        _advancedSection.ResumeLayout(false);
        _advancedSection.PerformLayout();
        _contentFlow.ResumeLayout(false);
        _contentFlow.PerformLayout();
        _footerFlow.ResumeLayout(false);
        ResumeLayout(false);
    }

    private AntdUI.Panel _basicsSection = null!;
    private AntdUI.Label _basicsTitle = null!;
    private ResponsiveFieldGrid _basicsGrid = null!;
    private AntdUI.Label _nameLabel = null!;
    private AntdUI.Input _nameInput = null!;
    private AntdUI.Label _typeLabel = null!;
    private AntdUI.Select _typeSelect = null!;
    private AntdUI.Label _groupLabel = null!;
    private AntdUI.Select _groupSelect = null!;
    private AntdUI.Label _expiresLabel = null!;
    private AntdUI.DatePicker _expiresPicker = null!;
    private AntdUI.Label _favoriteLabel = null!;
    private AntdUI.Switch _favoriteSwitch = null!;
    private AntdUI.Label _notesLabel = null!;
    private AntdUI.Input _notesInput = null!;
    private AntdUI.Panel _advancedSection = null!;
    private AntdUI.Label _advancedTitle = null!;
    private ResponsiveFieldGrid _clientGrid = null!;
    private AntdUI.Label _privateKeyLabel = null!;
    private AntdUI.Input _privateKeyInput = null!;
    private AntdUI.Label _executableLabel = null!;
    private AntdUI.Input _executableInput = null!;
    private AntdUI.Label _argumentsLabel = null!;
    private AntdUI.Input _argumentsInput = null!;
    private AntdUI.Label _clientOptionsLabel = null!;
    private AntdUI.Input _clientOptionsInput = null!;
    private AntdUI.Button _saveButton = null!;
    private AntdUI.Button _cancelButton = null!;
    private AntdUI.Panel _rdpSection = null!;
    private AntdUI.Label _rdpTitle = null!;
    private RemoteHubStudio.UI.Dialogs.ConnectionEditors.RdpConnectionTypeOptionsPage _rdpPage = null!;
}
