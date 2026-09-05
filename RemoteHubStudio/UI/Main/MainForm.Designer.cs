using System.ComponentModel;

namespace RemoteHubStudio.UI.Main;

partial class MainForm
{
    /// <summary>
    /// Creates the designer-serializable main-window control tree. / 创建可由设计器序列化的主窗口控件树。
    /// </summary>
    private void InitializeComponent()
    {
        components = new Container();
        _toolTip = new ToolTip(components);
        _header = new AntdUI.PageHeader();
        _settingsButton = new AntdUI.Button();
        _minimizeToTrayButton = new AntdUI.Button();
        _sidebar = new AntdUI.Panel();
        _navigation = new AntdUI.Menu();
        _sidebarActions = new FlowLayoutPanel();
        _groupsButton = new AntdUI.Button();
        _contentPanel = new System.Windows.Forms.Panel();
        _toolbar = new FlowLayoutPanel();
        _searchInput = new AntdUI.Input();
        _typeFilter = new AntdUI.Select();
        _toolbarSpacer = new System.Windows.Forms.Panel();
        _addButton = new AntdUI.Button();
        _connectButton = new AntdUI.Button();
        _editButton = new AntdUI.Button();
        _deleteButton = new AntdUI.Button();
        _secondaryToolbar = new FlowLayoutPanel();
        _favoriteFilterButton = new AntdUI.Button();
        _expiringFilterButton = new AntdUI.Button();
        _secondaryToolbarSpacer = new System.Windows.Forms.Panel();
        _quickButton = new AntdUI.Button();
        _statusButton = new AntdUI.Button();
        _transferButton = new AntdUI.Button();
        _moreButton = new AntdUI.Button();
        _connectionTable = new AntdUI.Table();
        _viewStatus = new AntdUI.Label();
        _transferMenu = new ContextMenuStrip(components);
        _toolbarOverflowMenu = new ContextMenuStrip(components);
        _trayMenu = new ContextMenuStrip(components);
        _notifyIcon = new NotifyIcon(components);
        _sidebar.SuspendLayout();
        _sidebarActions.SuspendLayout();
        _contentPanel.SuspendLayout();
        _toolbar.SuspendLayout();
        _secondaryToolbar.SuspendLayout();
        SuspendLayout();
        //
        // _header
        //
        _header.Size = new Size(1180, 58);
        _header.Controls.Add(_settingsButton);
        _header.Controls.Add(_minimizeToTrayButton);
        _header.DividerShow = true;
        _header.Dock = DockStyle.Top;
        _header.EnableButtonTooltip = true;
        _header.EnableDoubleClickMaximize = true;
        _header.Height = 58;
        _header.IconSvg = "CloudServerOutlined";
        _header.MaximizeBox = true;
        _header.MinimizeBox = true;
        _header.Name = "_header";
        _header.ShowButton = true;
        _header.ShowIcon = true;
        _header.SubText = "远程连接工作台 · Remote connection workspace";
        _header.Text = "RemoteHubStudio";
        //
        // _settingsButton
        //
        _settingsButton.AccessibleName = "设置 / Settings";
        _settingsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _settingsButton.AutoSize = false;
        _settingsButton.Ghost = true;
        _settingsButton.Height = 36;
        _settingsButton.IconSvg = "SettingOutlined";
        _settingsButton.Location = new Point(918, 11);
        _settingsButton.Name = "_settingsButton";
        _settingsButton.Shape = AntdUI.TShape.Circle;
        _settingsButton.TabIndex = 0;
        _settingsButton.Tag = "settings";
        _settingsButton.Width = 36;
        _toolTip.SetToolTip(_settingsButton, "设置 / Settings");
        //
        // _minimizeToTrayButton (the standard header minimize button stays enabled)
        //
        _minimizeToTrayButton.AccessibleName = "最小化到托盘 / Minimize to tray";
        _minimizeToTrayButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _minimizeToTrayButton.Ghost = true;
        _minimizeToTrayButton.IconSvg = "VerticalAlignBottomOutlined";
        _minimizeToTrayButton.Location = new Point(962, 11);
        _minimizeToTrayButton.Name = "_minimizeToTrayButton";
        _minimizeToTrayButton.Shape = AntdUI.TShape.Circle;
        _minimizeToTrayButton.Size = new Size(36, 36);
        _minimizeToTrayButton.TabIndex = 1;
        _toolTip.SetToolTip(_minimizeToTrayButton, "最小化到托盘 / Minimize to tray");
        //
        // _sidebar
        //
        _sidebar.BorderWidth = 0F;
        _sidebar.Controls.Add(_navigation);
        _sidebar.Controls.Add(_sidebarActions);
        _sidebar.Dock = DockStyle.Left;
        _sidebar.Name = "_sidebar";
        _sidebar.Padding = new Padding(8);
        _sidebar.Width = 232;
        //
        // _navigation
        //
        _navigation.AutoCollapse = false;
        _navigation.Dock = DockStyle.Fill;
        _navigation.Mode = AntdUI.TMenuMode.Inline;
        _navigation.Name = "_navigation";
        _navigation.Radius = 8;
        _navigation.Unique = true;
        //
        // _sidebarActions
        //
        _sidebarActions.Controls.Add(_groupsButton);
        _sidebarActions.Dock = DockStyle.Bottom;
        _sidebarActions.FlowDirection = FlowDirection.LeftToRight;
        _sidebarActions.Height = 54;
        _sidebarActions.Name = "_sidebarActions";
        _sidebarActions.Padding = new Padding(0, 8, 0, 0);
        _sidebarActions.WrapContents = true;
        //
        // _groupsButton
        //
        _groupsButton.AutoSize = false;
        _groupsButton.AutoSizeMode = AntdUI.TAutoSize.Width;
        _groupsButton.Height = 38;
        _groupsButton.IconSvg = "FolderOutlined";
        _groupsButton.Margin = new Padding(0, 0, 8, 4);
        _groupsButton.Name = "_groupsButton";
        _groupsButton.Radius = 8;
        _groupsButton.Tag = "groups";
        _groupsButton.Text = "分组 / Groups";
        _groupsButton.Type = AntdUI.TTypeMini.Default;
        _groupsButton.Width = 102;
        //
        // _contentPanel
        //
        _contentPanel.Controls.Add(_connectionTable);
        _contentPanel.Controls.Add(_viewStatus);
        _contentPanel.Controls.Add(_secondaryToolbar);
        _contentPanel.Controls.Add(_toolbar);
        _contentPanel.Dock = DockStyle.Fill;
        _contentPanel.Name = "_contentPanel";
        _contentPanel.Padding = new Padding(16, 12, 16, 10);
        //
        // _toolbar
        //
        _toolbar.Controls.Add(_searchInput);
        _toolbar.Controls.Add(_typeFilter);
        _toolbar.Controls.Add(_toolbarSpacer);
        _toolbar.Controls.Add(_addButton);
        _toolbar.Controls.Add(_connectButton);
        _toolbar.Controls.Add(_editButton);
        _toolbar.Controls.Add(_deleteButton);
        _toolbar.Dock = DockStyle.Top;
        _toolbar.AutoSize = true;
        _toolbar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _toolbar.FlowDirection = FlowDirection.LeftToRight;
        _toolbar.Height = 56;
        _toolbar.Name = "_toolbar";
        _toolbar.Padding = new Padding(0, 4, 0, 8);
        _toolbar.WrapContents = true;
        //
        // _searchInput
        //
        _searchInput.AllowClear = true;
        _searchInput.Height = 38;
        _searchInput.Name = "_searchInput";
        _searchInput.PlaceholderText = "搜索名称、地址、类型或备注 / Search connections";
        _searchInput.PrefixSvg = "SearchOutlined";
        _searchInput.Radius = 8;
        _searchInput.Width = 300;
        //
        // _typeFilter
        //
        _typeFilter.DropDownArrow = true;
        _typeFilter.Height = 38;
        _typeFilter.ListAutoWidth = true;
        _typeFilter.Name = "_typeFilter";
        _typeFilter.PlaceholderText = "全部类型 / All types";
        _typeFilter.Radius = 8;
        _typeFilter.WheelModifyEnabled = false;
        _typeFilter.Width = 174;
        //
        // _secondaryToolbar
        //
        _secondaryToolbar.Controls.Add(_favoriteFilterButton);
        _secondaryToolbar.Controls.Add(_expiringFilterButton);
        _secondaryToolbar.Controls.Add(_secondaryToolbarSpacer);
        _secondaryToolbar.Controls.Add(_quickButton);
        _secondaryToolbar.Controls.Add(_statusButton);
        _secondaryToolbar.Controls.Add(_transferButton);
        _secondaryToolbar.Controls.Add(_moreButton);
        _secondaryToolbar.Dock = DockStyle.Top;
        _secondaryToolbar.AutoSize = true;
        _secondaryToolbar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _secondaryToolbar.FlowDirection = FlowDirection.LeftToRight;
        _secondaryToolbar.Height = 48;
        _secondaryToolbar.Name = "_secondaryToolbar";
        _secondaryToolbar.Padding = new Padding(3, 0, 0, 6);
        _secondaryToolbar.WrapContents = true;
        //
        // _favoriteFilterButton
        //
        _favoriteFilterButton.AccessibleName = "仅看收藏 / Favorites only";
        _favoriteFilterButton.AutoSize = true;
        _favoriteFilterButton.AutoSizeMode = AntdUI.TAutoSize.Width;
        _favoriteFilterButton.AutoToggle = true;
        _favoriteFilterButton.Height = 38;
        _favoriteFilterButton.IconSvg = "StarOutlined";
        _favoriteFilterButton.Margin = new Padding(0, 0, 8, 4);
        _favoriteFilterButton.Name = "_favoriteFilterButton";
        _favoriteFilterButton.Radius = 8;
        _favoriteFilterButton.Tag = "favorite-filter";
        _favoriteFilterButton.Text = "收藏 / Favorites";
        _favoriteFilterButton.ToggleIconSvg = "StarFilled";
        _favoriteFilterButton.ToggleText = "收藏 / Favorites";
        _favoriteFilterButton.ToggleType = AntdUI.TTypeMini.Primary;
        _toolTip.SetToolTip(_favoriteFilterButton, "筛选收藏连接 / Filter favorites");
        //
        // _expiringFilterButton
        //
        _expiringFilterButton.AccessibleName = "仅看即将到期 / Expiring only";
        _expiringFilterButton.AutoSize = true;
        _expiringFilterButton.AutoSizeMode = AntdUI.TAutoSize.Width;
        _expiringFilterButton.AutoToggle = true;
        _expiringFilterButton.Height = 38;
        _expiringFilterButton.IconSvg = "ClockCircleOutlined";
        _expiringFilterButton.Margin = new Padding(0, 0, 8, 4);
        _expiringFilterButton.Name = "_expiringFilterButton";
        _expiringFilterButton.Radius = 8;
        _expiringFilterButton.Tag = "expiring-filter";
        _expiringFilterButton.Text = "即将到期 / Expiring";
        _expiringFilterButton.ToggleIconSvg = "ClockCircleFilled";
        _expiringFilterButton.ToggleText = "即将到期 / Expiring";
        _expiringFilterButton.ToggleType = AntdUI.TTypeMini.Primary;
        _toolTip.SetToolTip(_expiringFilterButton, "筛选已到期及即将到期连接 / Filter expired and expiring connections");
        //
        // _secondaryToolbarSpacer
        //
        _secondaryToolbarSpacer.Height = 38;
        _secondaryToolbarSpacer.Margin = new Padding(0);
        _secondaryToolbarSpacer.Name = "_secondaryToolbarSpacer";
        _secondaryToolbarSpacer.Width = 0;
        //
        // _toolbarSpacer
        //
        _toolbarSpacer.Height = 38;
        _toolbarSpacer.Margin = new Padding(0);
        _toolbarSpacer.Name = "_toolbarSpacer";
        _toolbarSpacer.Width = 0;
        //
        // toolbar buttons
        //
        _addButton.AutoSize = true;
        _addButton.AutoSizeMode = AntdUI.TAutoSize.Width;
        _addButton.Height = 38;
        _addButton.IconSvg = "PlusOutlined";
        _addButton.Margin = new Padding(0, 0, 8, 4);
        _addButton.Name = "_addButton";
        _addButton.Radius = 8;
        _addButton.Tag = "add";
        _addButton.Text = "新增 / Add";
        _addButton.Type = AntdUI.TTypeMini.Primary;

        _quickButton.AutoSize = true;
        _quickButton.AutoSizeMode = AntdUI.TAutoSize.Width;
        _quickButton.Height = 38;
        _quickButton.IconSvg = "ThunderboltOutlined";
        _quickButton.Margin = new Padding(0, 0, 8, 4);
        _quickButton.Name = "_quickButton";
        _quickButton.Radius = 8;
        _quickButton.Tag = "quick";
        _quickButton.Text = "快速连接 / Quick";

        _connectButton.AutoSize = true;
        _connectButton.AutoSizeMode = AntdUI.TAutoSize.Width;
        _connectButton.Height = 38;
        _connectButton.IconSvg = "PlayCircleOutlined";
        _connectButton.Margin = new Padding(0, 0, 8, 4);
        _connectButton.Name = "_connectButton";
        _connectButton.Radius = 8;
        _connectButton.Tag = "connect";
        _connectButton.Text = "连接 / Connect";
        _connectButton.Type = AntdUI.TTypeMini.Primary;

        _editButton.AutoSize = true;
        _editButton.AutoSizeMode = AntdUI.TAutoSize.Width;
        _editButton.Height = 38;
        _editButton.IconSvg = "EditOutlined";
        _editButton.Margin = new Padding(0, 0, 8, 4);
        _editButton.Name = "_editButton";
        _editButton.Radius = 8;
        _editButton.Tag = "edit";
        _editButton.Text = "编辑 / Edit";

        _deleteButton.AutoSize = true;
        _deleteButton.AutoSizeMode = AntdUI.TAutoSize.Width;
        _deleteButton.Height = 38;
        _deleteButton.IconSvg = "DeleteOutlined";
        _deleteButton.Margin = new Padding(0, 0, 8, 4);
        _deleteButton.Name = "_deleteButton";
        _deleteButton.Radius = 8;
        _deleteButton.Tag = "delete";
        _deleteButton.Text = "删除 / Delete";

        _statusButton.AutoSize = true;
        _statusButton.AutoSizeMode = AntdUI.TAutoSize.Width;
        _statusButton.Height = 38;
        _statusButton.IconSvg = "RadarChartOutlined";
        _statusButton.Margin = new Padding(0, 0, 8, 4);
        _statusButton.Name = "_statusButton";
        _statusButton.Radius = 8;
        _statusButton.Tag = "check";
        _statusButton.Text = "检测 / Check";

        _transferButton.AutoSize = true;
        _transferButton.AutoSizeMode = AntdUI.TAutoSize.Width;
        _transferButton.Height = 38;
        _transferButton.IconSvg = "SwapOutlined";
        _transferButton.Margin = new Padding(0, 0, 8, 4);
        _transferButton.Name = "_transferButton";
        _transferButton.Radius = 8;
        _transferButton.Tag = "transfer";
        _transferButton.Text = "导入导出 / Transfer";

        _moreButton.AutoSize = true;
        _moreButton.AutoSizeMode = AntdUI.TAutoSize.Width;
        _moreButton.Height = 38;
        _moreButton.IconSvg = "EllipsisOutlined";
        _moreButton.Margin = new Padding(0, 0, 8, 4);
        _moreButton.Name = "_moreButton";
        _moreButton.Radius = 8;
        _moreButton.Tag = "more";
        _moreButton.Text = "更多 / More";
        _moreButton.Visible = false;
        //
        // _connectionTable
        //
        _connectionTable.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;
        _connectionTable.Dock = DockStyle.Fill;
        _connectionTable.EmptyText = "没有匹配的连接 / No matching connections";
        _connectionTable.EnableHeaderResizing = true;
        _connectionTable.FixedHeader = true;
        _connectionTable.LostFocusClearSelection = false;
        _connectionTable.MultipleRows = true;
        _connectionTable.Name = "_connectionTable";
        _connectionTable.RowHeight = 46;
        _connectionTable.VirtualMode = true;
        //
        // _viewStatus
        //
        _viewStatus.Dock = DockStyle.Bottom;
        _viewStatus.Height = 28;
        _viewStatus.Name = "_viewStatus";
        _viewStatus.Text = "全部连接 / All connections";
        _viewStatus.TextAlign = ContentAlignment.MiddleLeft;
        //
        // menus and tray icon
        //
        _transferMenu.Name = "_transferMenu";
        _transferMenu.ShowImageMargin = false;
        _toolbarOverflowMenu.Name = "_toolbarOverflowMenu";
        _toolbarOverflowMenu.ShowImageMargin = false;
        _trayMenu.Name = "_trayMenu";
        _trayMenu.ShowImageMargin = false;
        _notifyIcon.Text = "RemoteHubStudio";
        _notifyIcon.Visible = false;
        //
        // MainForm
        //
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1180, 760);
        Controls.Add(_contentPanel);
        Controls.Add(_sidebar);
        Controls.Add(_header);
        MinimumSize = new Size(720, 480);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "RemoteHubStudio";
        _sidebar.ResumeLayout(false);
        _sidebarActions.ResumeLayout(false);
        _contentPanel.ResumeLayout(false);
        _toolbar.ResumeLayout(false);
        _secondaryToolbar.ResumeLayout(false);
        ResumeLayout(false);
    }

}
