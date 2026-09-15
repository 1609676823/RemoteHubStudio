#nullable disable

using System.Drawing;
using System.Windows.Forms;
using RemoteHubStudio.UI.Controls;

namespace RemoteHubStudio.UI.Dialogs;

partial class GroupEditorForm
{
    /// <summary>
    /// Creates the designer-serializable group editor control tree. / 创建可由设计器序列化的分组编辑器控件树。
    /// </summary>
    private void InitializeComponent()
    {
        _groupSection = new AntdUI.Panel();
        _sectionTitleLabel = new AntdUI.Label();
        _groupFields = new ResponsiveFieldGrid();
        _nameLabel = new AntdUI.Label();
        _nameInput = new AntdUI.Input();
        _parentLabel = new AntdUI.Label();
        _parentSelect = new AntdUI.Select();
        _colorLabel = new AntdUI.Label();
        _colorInput = new AntdUI.Input();
        _colorPreviewLabel = new AntdUI.Label();
        _colorPreview = new AntdUI.Panel();
        _sortOrderLabel = new AntdUI.Label();
        _sortOrderInput = new AntdUI.InputNumber();
        _saveButton = new AntdUI.Button();
        _cancelButton = new AntdUI.Button();
        _groupSection.SuspendLayout();
        _groupFields.SuspendLayout();
        SuspendLayout();
        // 
        // _groupSection
        // 
        _groupSection.BorderWidth = 1F;
        _groupSection.Controls.Add(_groupFields);
        _groupSection.Controls.Add(_sectionTitleLabel);
        _groupSection.Height = 300;
        _groupSection.Margin = new Padding(0, 0, 0, 12);
        _groupSection.Name = "_groupSection";
        _groupSection.Padding = new Padding(8);
        _groupSection.Radius = 10;
        _groupSection.Width = 564;
        // 
        // _sectionTitleLabel
        // 
        _sectionTitleLabel.Dock = DockStyle.Top;
        _sectionTitleLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
        _sectionTitleLabel.Height = 36;
        _sectionTitleLabel.Name = "_sectionTitleLabel";
        _sectionTitleLabel.Padding = new Padding(12, 0, 8, 0);
        _sectionTitleLabel.Text = "分组信息 / Group information";
        _sectionTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _groupFields
        // 
        _groupFields.AutoSize = true;
        _groupFields.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _groupFields.ColumnCount = 2;
        _groupFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        _groupFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _groupFields.Controls.Add(_nameLabel, 0, 0);
        _groupFields.Controls.Add(_nameInput, 1, 0);
        _groupFields.Controls.Add(_parentLabel, 0, 1);
        _groupFields.Controls.Add(_parentSelect, 1, 1);
        _groupFields.Controls.Add(_colorLabel, 0, 2);
        _groupFields.Controls.Add(_colorInput, 1, 2);
        _groupFields.Controls.Add(_colorPreviewLabel, 0, 3);
        _groupFields.Controls.Add(_colorPreview, 1, 3);
        _groupFields.Controls.Add(_sortOrderLabel, 0, 4);
        _groupFields.Controls.Add(_sortOrderInput, 1, 4);
        _groupFields.Dock = DockStyle.Top;
        _groupFields.GrowStyle = TableLayoutPanelGrowStyle.AddRows;
        _groupFields.Location = new Point(8, 44);
        _groupFields.Margin = Padding.Empty;
        _groupFields.Name = "_groupFields";
        _groupFields.Padding = new Padding(4, 2, 4, 4);
        _groupFields.RowCount = 5;
        _groupFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        _groupFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        _groupFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        _groupFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        _groupFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        _groupFields.Size = new Size(548, 246);
        _groupFields.TabIndex = 1;
        _groupFields.WideLayoutBreakpoint = 900;
        // 
        // _nameLabel
        // 
        _nameLabel.Dock = DockStyle.Fill;
        _nameLabel.Margin = new Padding(8, 5, 4, 5);
        _nameLabel.Name = "_nameLabel";
        _nameLabel.Text = "名称 / Name";
        _nameLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _nameInput
        // 
        _nameInput.AllowClear = true;
        _nameInput.Dock = DockStyle.Fill;
        _nameInput.Margin = new Padding(4, 5, 10, 5);
        _nameInput.Name = "_nameInput";
        _nameInput.PlaceholderText = "分组名称 / Group name";
        _nameInput.Radius = 8;
        _nameInput.TabIndex = 0;
        // 
        // _parentLabel
        // 
        _parentLabel.Dock = DockStyle.Fill;
        _parentLabel.Margin = new Padding(8, 5, 4, 5);
        _parentLabel.Name = "_parentLabel";
        _parentLabel.Text = "父分组 / Parent";
        _parentLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _parentSelect
        // 
        _parentSelect.AllowClear = true;
        _parentSelect.Dock = DockStyle.Fill;
        _parentSelect.DropDownArrow = true;
        _parentSelect.ListAutoWidth = true;
        _parentSelect.Margin = new Padding(4, 5, 10, 5);
        _parentSelect.Name = "_parentSelect";
        _parentSelect.PlaceholderText = "顶级分组 / Top-level group";
        _parentSelect.Radius = 8;
        _parentSelect.TabIndex = 1;
        _parentSelect.WheelModifyEnabled = false;
        // 
        // _colorLabel
        // 
        _colorLabel.Dock = DockStyle.Fill;
        _colorLabel.Margin = new Padding(8, 5, 4, 5);
        _colorLabel.Name = "_colorLabel";
        _colorLabel.Text = "强调色 / Accent";
        _colorLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _colorInput
        // 
        _colorInput.AllowClear = true;
        _colorInput.Dock = DockStyle.Fill;
        _colorInput.Margin = new Padding(4, 5, 10, 5);
        _colorInput.MaxLength = 9;
        _colorInput.Name = "_colorInput";
        _colorInput.PlaceholderText = "#1677FF";
        _colorInput.Radius = 8;
        _colorInput.TabIndex = 2;
        // 
        // _colorPreviewLabel
        // 
        _colorPreviewLabel.Dock = DockStyle.Fill;
        _colorPreviewLabel.Margin = new Padding(8, 5, 4, 5);
        _colorPreviewLabel.Name = "_colorPreviewLabel";
        _colorPreviewLabel.Text = "颜色预览 / Preview";
        _colorPreviewLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _colorPreview
        // 
        _colorPreview.BorderWidth = 1F;
        _colorPreview.Dock = DockStyle.Fill;
        _colorPreview.Height = 36;
        _colorPreview.Margin = new Padding(4, 5, 10, 5);
        _colorPreview.Name = "_colorPreview";
        _colorPreview.Radius = 8;
        // 
        // _sortOrderLabel
        // 
        _sortOrderLabel.Dock = DockStyle.Fill;
        _sortOrderLabel.Margin = new Padding(8, 5, 4, 5);
        _sortOrderLabel.Name = "_sortOrderLabel";
        _sortOrderLabel.Text = "排序 / Sort order";
        _sortOrderLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _sortOrderInput
        // 
        _sortOrderInput.DecimalPlaces = 0;
        _sortOrderInput.Dock = DockStyle.Fill;
        _sortOrderInput.Margin = new Padding(4, 5, 10, 5);
        _sortOrderInput.Maximum = 100000;
        _sortOrderInput.Minimum = -100000;
        _sortOrderInput.Name = "_sortOrderInput";
        _sortOrderInput.Radius = 8;
        _sortOrderInput.ShowControl = true;
        _sortOrderInput.TabIndex = 3;
        // 
        // _saveButton
        // 
        _saveButton.Height = 38;
        _saveButton.Margin = new Padding(8, 0, 0, 0);
        _saveButton.Name = "_saveButton";
        _saveButton.Text = "保存 / Save";
        _saveButton.Type = AntdUI.TTypeMini.Primary;
        _saveButton.Width = 112;
        // 
        // _cancelButton
        // 
        _cancelButton.Height = 38;
        _cancelButton.Margin = new Padding(8, 0, 0, 0);
        _cancelButton.Name = "_cancelButton";
        _cancelButton.Text = "取消 / Cancel";
        _cancelButton.Width = 104;
        // 
        // GroupEditorForm
        // 
        AcceptButton = _saveButton;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        CancelButton = _cancelButton;
        _contentFlow.Controls.Add(_groupSection);
        _footerFlow.Controls.Add(_saveButton);
        _footerFlow.Controls.Add(_cancelButton);
        MinimumSize = new Size(520, 400);
        _header.Text = "新增分组 / Add group";
        Text = "新增分组 / Add group";
        Name = "GroupEditorForm";
        ClientSize = new Size(620, 480);
        _groupFields.ResumeLayout(false);
        _groupSection.ResumeLayout(false);
        _groupSection.PerformLayout();
        ResumeLayout(false);
    }

    private AntdUI.Panel _groupSection;
    private AntdUI.Label _sectionTitleLabel;
    private ResponsiveFieldGrid _groupFields;
    private AntdUI.Label _nameLabel;
    private AntdUI.Input _nameInput;
    private AntdUI.Label _parentLabel;
    private AntdUI.Select _parentSelect;
    private AntdUI.Label _colorLabel;
    private AntdUI.Input _colorInput;
    private AntdUI.Label _colorPreviewLabel;
    private AntdUI.Panel _colorPreview;
    private AntdUI.Label _sortOrderLabel;
    private AntdUI.InputNumber _sortOrderInput;
    private AntdUI.Button _saveButton;
    private AntdUI.Button _cancelButton;
}

#nullable restore
