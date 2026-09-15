namespace RemoteHubStudio.UI.Dialogs;

partial class GroupManagerForm
{
    /// <summary>
    /// Creates the complete designer-serializable group manager visual tree. / 创建可由设计器完整序列化的分组管理器可视树。
    /// </summary>
    private void InitializeComponent()
    {
        _table = new AntdUI.Table();
        _addButton = new AntdUI.Button();
        _editButton = new AntdUI.Button();
        _deleteButton = new AntdUI.Button();
        _closeButton = new AntdUI.Button();
        _toolbar = new FlowLayoutPanel();
        _content = new System.Windows.Forms.Panel();
        _section = new AntdUI.Panel();
        _sectionTitle = new AntdUI.Label();
        SuspendLayout();
        _section.SuspendLayout();
        _content.SuspendLayout();
        _toolbar.SuspendLayout();

        _table.Dock = DockStyle.Fill;
        _table.FixedHeader = true;
        _table.VirtualMode = true;
        _table.LostFocusClearSelection = false;
        _table.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;
        _table.RowHeight = 42;
        _table.EmptyText = "暂无分组 / No groups";
        _table.Name = "groupTable";

        _addButton.Text = "新增 / Add";
        _addButton.Tag = "add";
        _addButton.Type = AntdUI.TTypeMini.Primary;
        _addButton.Width = 108;
        _addButton.Height = 36;
        _addButton.Radius = 8;
        _addButton.Name = "addButton";

        _editButton.Text = "编辑 / Edit";
        _editButton.Tag = "edit";
        _editButton.Width = 108;
        _editButton.Height = 36;
        _editButton.Radius = 8;
        _editButton.Enabled = false;
        _editButton.Name = "editButton";

        _deleteButton.Text = "删除 / Delete";
        _deleteButton.Tag = "delete";
        _deleteButton.Type = AntdUI.TTypeMini.Error;
        _deleteButton.Width = 108;
        _deleteButton.Height = 36;
        _deleteButton.Radius = 8;
        _deleteButton.Enabled = false;
        _deleteButton.Name = "deleteButton";

        _closeButton.Text = "关闭 / Close";
        _closeButton.Tag = "close";
        _closeButton.Width = 108;
        _closeButton.Height = 38;
        _closeButton.Radius = 8;
        _closeButton.Margin = new Padding(8, 0, 0, 0);
        _closeButton.Name = "closeButton";

        _toolbar.Dock = DockStyle.Top;
        _toolbar.Height = 48;
        _toolbar.Padding = new Padding(0, 4, 0, 4);
        _toolbar.FlowDirection = FlowDirection.LeftToRight;
        _toolbar.WrapContents = false;
        _toolbar.Name = "groupToolbar";
        _toolbar.Controls.Add(_addButton);
        _toolbar.Controls.Add(_editButton);
        _toolbar.Controls.Add(_deleteButton);

        _content.Dock = DockStyle.Top;
        _content.Height = 430;
        _content.Name = "groupContent";
        _content.Controls.Add(_table);
        _content.Controls.Add(_toolbar);

        _sectionTitle.Dock = DockStyle.Top;
        _sectionTitle.Height = 36;
        _sectionTitle.Text = "连接分组 / Connection groups";
        _sectionTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
        _sectionTitle.TextAlign = ContentAlignment.MiddleLeft;
        _sectionTitle.Padding = new Padding(12, 0, 8, 0);
        _sectionTitle.Name = "groupSectionTitle";

        _section.Radius = 10;
        _section.BorderWidth = 1F;
        _section.Margin = new Padding(0, 0, 0, 12);
        _section.Padding = new Padding(8);
        _section.Width = 780;
        _section.Height = 482;
        _section.Name = "groupSection";
        _section.Controls.Add(_content);
        _section.Controls.Add(_sectionTitle);

        _contentFlow.Controls.Add(_section);
        _footerFlow.Controls.Add(_closeButton);
        _header.Text = "分组管理 / Groups";
        MinimumSize = new Size(600, 460);
        ClientSize = new Size(840, 620);
        Name = "GroupManagerForm";
        Text = "分组管理 / Groups";

        _toolbar.ResumeLayout(false);
        _content.ResumeLayout(false);
        _section.ResumeLayout(false);
        ResumeLayout(false);
    }

    private AntdUI.Table _table = null!;
    private AntdUI.Button _addButton = null!;
    private AntdUI.Button _editButton = null!;
    private AntdUI.Button _deleteButton = null!;
    private AntdUI.Button _closeButton = null!;
    private FlowLayoutPanel _toolbar = null!;
    private System.Windows.Forms.Panel _content = null!;
    private AntdUI.Panel _section = null!;
    private AntdUI.Label _sectionTitle = null!;
}
