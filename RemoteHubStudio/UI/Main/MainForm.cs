using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Win32;
using RemoteHubStudio.Application;
using RemoteHubStudio.Configuration;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Infrastructure;
using RemoteHubStudio.Infrastructure.ImportExport;
using RemoteHubStudio.Infrastructure.Launch;
using RemoteHubStudio.Infrastructure.Monitoring;
using RemoteHubStudio.Infrastructure.Persistence;
using RemoteHubStudio.Localization;
using RemoteHubStudio.UI.Dialogs;
using RemoteHubStudio.UI.Theme;
using RemoteHubStudio.UI.Branding;

namespace RemoteHubStudio.UI.Main;

/// <summary>
/// Hosts the responsive AntdUI connection workspace and coordinates all user workflows. / 承载响应式 AntdUI 连接工作区，并协调全部用户工作流。
/// </summary>
public sealed partial class MainForm : AntdUI.Window
{
    private const string AllView = "view:all";
    private const string GroupViewPrefix = "group:";
    private const string FavoriteButtonId = "favorite-toggle";

    private readonly WorkspaceService _workspace = null!;
    private readonly ConnectionLaunchService _launchService = null!;
    private readonly WorkspaceTransferService _transferService = null!;
    private readonly ConnectionStatusService _statusService = null!;
    private readonly ExpirationService _expirationService = null!;
    private readonly AppDataPaths _paths = null!;
    private readonly SingleInstanceCoordinator _singleInstance = null!;
    private readonly ConnectionStatusBatchState _statusBatchState = new();

    private AntdUI.PageHeader _header = null!;
    private AntdUI.Panel _sidebar = null!;
    private AntdUI.Menu _navigation = null!;
    private FlowLayoutPanel _sidebarActions = null!;
    private FlowLayoutPanel _toolbar = null!;
    private FlowLayoutPanel _secondaryToolbar = null!;
    private System.Windows.Forms.Panel _toolbarSpacer = null!;
    private System.Windows.Forms.Panel _secondaryToolbarSpacer = null!;
    private System.Windows.Forms.Panel _contentPanel = null!;
    private AntdUI.Input _searchInput = null!;
    private AntdUI.Select _typeFilter = null!;
    private AntdUI.Button _favoriteFilterButton = null!;
    private AntdUI.Button _expiringFilterButton = null!;
    private AntdUI.Button _addButton = null!;
    private AntdUI.Button _quickButton = null!;
    private AntdUI.Button _connectButton = null!;
    private AntdUI.Button _editButton = null!;
    private AntdUI.Button _deleteButton = null!;
    private AntdUI.Button _statusButton = null!;
    private AntdUI.Button _transferButton = null!;
    private AntdUI.Button _settingsButton = null!;
    private AntdUI.Button _minimizeToTrayButton = null!;
    private AntdUI.Button _moreButton = null!;
    private AntdUI.Button _groupsButton = null!;
    private AntdUI.Table _connectionTable = null!;
    private AntdUI.Label _viewStatus = null!;
    private ContextMenuStrip _transferMenu = null!;
    private ContextMenuStrip _toolbarOverflowMenu = null!;
    private ContextMenuStrip _trayMenu = null!;
    private ToolTip _toolTip = null!;
    private NotifyIcon _notifyIcon = null!;
    private System.ComponentModel.IContainer? components;

    private List<ConnectionTableRow> _visibleRows = [];
    private List<Guid> _visibleConnectionOrder = [];
    private HashSet<Guid> _visibleConnectionIds = [];
    private int _totalConnectionCount;
    private string _activeView = AllView;
    private ConnectionType? _activeType;
    private CancellationTokenSource? _statusCancellation;
    private bool _restoringConnectionSelection;
    private bool _primaryOperationInFlight;
    private int _windowBoundsSaveCount;
    private bool _exitRequestedAfterOperation;
    private bool _shutdownRequested;
    private bool _forceExit;
    private bool _recoveryNoticeShown;
    private bool _runtimeInitialized;
    private bool _systemEventsSubscribed;
    private FormWindowState _windowStateBeforeMinimize = FormWindowState.Normal;

    /// <summary>
    /// Creates the static visual tree used by the WinForms designer. Runtime services are attached by the dependency constructor. / 创建供 WinForms 设计器使用的静态视觉树；运行时服务由依赖构造函数连接。
    /// </summary>
    public MainForm()
    {
        InitializeComponent();
        ApplyBranding();
        L.Apply(this);
        ApplyLocalizedToolTips();
    }

    /// <summary>
    /// Initializes the main workspace with fully constructed application services. / 使用已完整构造的应用服务初始化主工作区。
    /// </summary>
    /// <param name="workspace">Initialized workspace service. / 已初始化的工作区服务。</param>
    /// <param name="launchService">Connection launcher. / 连接启动器。</param>
    /// <param name="transferService">Import and export service. / 导入导出服务。</param>
    /// <param name="statusService">Reachability status service. / 可达性状态服务。</param>
    /// <param name="expirationService">Expiration classification service. / 到期分类服务。</param>
    /// <param name="paths">Application-owned data paths. / 应用拥有的数据路径。</param>
    /// <param name="singleInstance">Single-instance coordinator. / 单实例协调器。</param>
    public MainForm(
        WorkspaceService workspace,
        ConnectionLaunchService launchService,
        WorkspaceTransferService transferService,
        ConnectionStatusService statusService,
        ExpirationService expirationService,
        AppDataPaths paths,
        SingleInstanceCoordinator singleInstance)
    {
        InitializeComponent();
        ApplyBranding();
        L.Apply(this);
        ApplyLocalizedToolTips();

        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _launchService = launchService ?? throw new ArgumentNullException(nameof(launchService));
        _transferService = transferService ?? throw new ArgumentNullException(nameof(transferService));
        _statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));
        _expirationService = expirationService ?? throw new ArgumentNullException(nameof(expirationService));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _singleInstance = singleInstance ?? throw new ArgumentNullException(nameof(singleInstance));

        _runtimeInitialized = true;
        // The source designer uses flow auto-sizing; runtime layout measures wrapped rows explicitly.
        // / 源设计器使用流式自动高度；运行时响应式布局会显式测量折行高度。
        _toolbar.AutoSize = false;
        _secondaryToolbar.AutoSize = false;
        ApplyInitialWindowBounds(_workspace.GetSettings().WindowBounds);
        ThemeManager.Apply(_workspace.GetSettings().Theme);

        ConfigureTable();
        PopulateTypeFilter();
        BuildTransferMenu();
        BuildToolbarOverflowMenu();
        BuildTrayMenu();
        ApplyWindowTheme();
        WireEvents();
        SystemEvents.UserPreferenceChanged += HandleUserPreferenceChanged;
        _systemEventsSubscribed = true;
        _notifyIcon.Icon = AppIcons.Tray;
        _notifyIcon.Visible = true;
        RebuildNavigation();
        RefreshConnectionTable();
        ApplyResponsiveLayout();
    }

    /// <summary>Gets whether the process should relaunch after this window closes. / 获取此窗口关闭后是否应重新启动进程。</summary>
    internal bool RestartRequested { get; private set; }

    private void ApplyBranding()
    {
        Icon = AppIcons.Application;
        _header.IconSvg = AppIcons.LogoSvg;
    }

    /// <summary>Localizes component-provided tooltips that are not part of the WinForms control tree. / 本地化不属于 WinForms 控件树的组件提示。</summary>
    private void ApplyLocalizedToolTips()
    {
        _toolTip.SetToolTip(_settingsButton, L.Get("Main.ToolTip.Settings"));
        _toolTip.SetToolTip(_minimizeToTrayButton, L.Get("Main.ToolTip.MinimizeToTray"));
        _toolTip.SetToolTip(_favoriteFilterButton, L.Get("Main.ToolTip.Favorites"));
        _toolTip.SetToolTip(_expiringFilterButton, L.Get("Main.ToolTip.Expiring"));
    }

    /// <summary>
    /// Configures connection table columns and responsive width constraints. / 配置连接表格列与响应式宽度约束。
    /// </summary>
    private void ConfigureTable()
    {
        _connectionTable.Columns = new AntdUI.ColumnCollection
        {
            new AntdUI.Column(nameof(ConnectionTableRow.Favorite), "★").SetWidth(42),
            new AntdUI.Column(nameof(ConnectionTableRow.Name), L.Get("Main.Table.Name")).SetWidth("21%").SetMinWidth(150),
            new AntdUI.Column(nameof(ConnectionTableRow.Type), L.Get("Main.Table.Client")).SetWidth("18%").SetMinWidth(130),
            new AntdUI.Column(nameof(ConnectionTableRow.Address), L.Get("Main.Table.Address")).SetWidth("20%").SetMinWidth(145),
            new AntdUI.Column(nameof(ConnectionTableRow.Group), L.Get("Main.Table.Group")).SetWidth("15%").SetMinWidth(110),
            new AntdUI.Column(nameof(ConnectionTableRow.Username), L.Get("Main.Table.User")).SetWidth("14%").SetMinWidth(100),
            new AntdUI.Column(nameof(ConnectionTableRow.Status), L.Get("Main.Table.Status")).SetWidth(130),
            new AntdUI.Column(nameof(ConnectionTableRow.Expiration), L.Get("Main.Table.Expiration")).SetWidthFill().SetMinWidth(130)
        };
    }

    /// <summary>
    /// Populates the type filter with all supported clients. / 使用全部支持的客户端填充类型筛选器。
    /// </summary>
    private void PopulateTypeFilter()
    {
        _typeFilter.Items.Add(new AntdUI.SelectItem(L.Get("Main.Filter.AllTypes"), "all"));
        foreach (ConnectionType type in Enum.GetValues<ConnectionType>())
        {
            _typeFilter.Items.Add(new AntdUI.SelectItem(type.ToDisplayName(), type));
        }

        _typeFilter.SelectedIndex = 0;
    }

    /// <summary>
    /// Builds the import and export context menu. / 构建导入导出上下文菜单。
    /// </summary>
    private void BuildTransferMenu()
    {
        _transferMenu.Items.Add(CreateMenuItem(L.Get("Main.Transfer.ExportAll"), "export-all"));
        _transferMenu.Items.Add(CreateMenuItem(L.Get("Main.Transfer.ExportCurrent"), "export-current"));
        _transferMenu.Items.Add(new ToolStripSeparator());
        _transferMenu.Items.Add(CreateMenuItem(L.Get("Main.Transfer.Import"), "import"));
    }

    /// <summary>
    /// Builds the low-height overflow menu for secondary toolbar commands. / 构建低高度布局下承载次要工具栏命令的溢出菜单。
    /// </summary>
    private void BuildToolbarOverflowMenu()
    {
        _toolbarOverflowMenu.Items.Add(CreateMenuItem(L.Get("Main.Command.QuickConnection"), "quick", HandleToolbarClick));
        _toolbarOverflowMenu.Items.Add(CreateMenuItem(L.Get("Main.Command.CheckStatus"), "check", HandleToolbarClick));
        _toolbarOverflowMenu.Items.Add(CreateMenuItem(L.Get("Main.Command.ImportExport"), "transfer", HandleToolbarClick));
    }

    /// <summary>
    /// Builds the system tray context menu. / 构建系统托盘上下文菜单。
    /// </summary>
    private void BuildTrayMenu()
    {
        _trayMenu.Items.Add(CreateMenuItem(L.Format("Main.Tray.Open", ProductInfo.Name), "tray-open", HandleTrayMenuClick));
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(CreateMenuItem(L.Get("Common.Exit"), "tray-exit", HandleTrayMenuClick));
        _notifyIcon.ContextMenuStrip = _trayMenu;
    }

    /// <summary>
    /// Creates a context-menu command item. / 创建上下文菜单命令项。
    /// </summary>
    /// <param name="text">Menu text. / 菜单文本。</param>
    /// <param name="command">Command tag. / 命令标记。</param>
    /// <param name="handler">Optional click handler. / 可选单击处理器。</param>
    /// <returns>Configured menu item. / 配置后的菜单项。</returns>
    private ToolStripMenuItem CreateMenuItem(string text, string command, EventHandler? handler = null)
    {
        ToolStripMenuItem item = new(text) { Tag = command };
        item.Click += handler ?? HandleTransferMenuClick;
        return item;
    }

    /// <summary>
    /// Wires all named event handlers used by the main window. / 绑定主窗口使用的全部命名事件处理器。
    /// </summary>
    private void WireEvents()
    {
        _workspace.Changed += HandleWorkspaceChanged;
        _singleInstance.ActivationRequested += HandleActivationRequested;
        _navigation.SelectChanged += HandleNavigationChanged;
        _searchInput.TextChanged += HandleSearchChanged;
        _typeFilter.SelectedValueChanged += HandleTypeFilterChanged;
        _connectionTable.SelectIndexChanged += HandleConnectionSelectionChanged;
        _connectionTable.CellButtonClick += HandleConnectionCellButtonClick;
        _connectionTable.CellDoubleClick += HandleConnectionCellDoubleClick;
        _favoriteFilterButton.Click += HandleToolbarClick;
        _expiringFilterButton.Click += HandleToolbarClick;
        _addButton.Click += HandleToolbarClick;
        _quickButton.Click += HandleToolbarClick;
        _connectButton.Click += HandleToolbarClick;
        _editButton.Click += HandleToolbarClick;
        _deleteButton.Click += HandleToolbarClick;
        _statusButton.Click += HandleToolbarClick;
        _transferButton.Click += HandleToolbarClick;
        _settingsButton.Click += HandleToolbarClick;
        _moreButton.Click += HandleToolbarClick;
        _groupsButton.Click += HandleToolbarClick;
        _minimizeToTrayButton.Click += HandleMinimizeToTrayClick;
        _notifyIcon.MouseDoubleClick += HandleNotifyIconDoubleClick;
        Resize += HandleWindowStateChanged;
        ClientSizeChanged += HandleClientSizeChanged;
        ResizeEnd += HandleResizeEnd;
        FormClosing += HandleFormClosing;
    }

    /// <summary>
    /// Rebuilds navigation items from the latest nested group snapshot. / 根据最新的嵌套分组快照重建导航项。
    /// </summary>
    private void RebuildNavigation()
    {
        _navigation.Items.Clear();
        _navigation.Items.Add(CreateNavigationItem(L.Get("Main.Navigation.AllConnections"), AllView, "AppstoreOutlined"));

        AntdUI.MenuItem groupsRoot = CreateNavigationItem(L.Get("Main.Navigation.Groups"), "groups-root", "FolderOutlined");
        IReadOnlyList<ConnectionGroup> groups = _workspace.GetGroups();
        IReadOnlyDictionary<Guid, List<ConnectionGroup>> childrenByParent = BuildGroupChildrenLookup(groups);
        AddGroupNavigationItems(groupsRoot, null, childrenByParent, []);
        if (groupsRoot.Sub.Count > 0)
        {
            _navigation.Items.Add(groupsRoot);
        }
    }

    /// <summary>
    /// Creates one navigation item and restores its selected state. / 创建一个导航项并恢复其选中状态。
    /// </summary>
    /// <param name="text">Navigation text. / 导航文本。</param>
    /// <param name="id">Stable navigation identifier. / 稳定导航标识。</param>
    /// <param name="iconSvg">AntdUI icon name. / AntdUI 图标名称。</param>
    /// <returns>Configured navigation item. / 配置后的导航项。</returns>
    private AntdUI.MenuItem CreateNavigationItem(string text, string id, string iconSvg)
    {
        return new AntdUI.MenuItem(text, iconSvg) { ID = id, Select = string.Equals(_activeView, id, StringComparison.Ordinal) };
    }

    /// <summary>
    /// Recursively appends group items while guarding against corrupt cycles. / 递归追加分组项，同时防止损坏数据形成循环。
    /// </summary>
    /// <param name="parentItem">Parent menu item. / 父菜单项。</param>
    /// <param name="parentId">Parent group identifier. / 父分组标识。</param>
    /// <param name="childrenByParent">Groups indexed by parent identifier. / 按父分类标识索引的分类。</param>
    /// <param name="visited">Cycle guard. / 循环保护集合。</param>
    private void AddGroupNavigationItems(
        AntdUI.MenuItem parentItem,
        Guid? parentId,
        IReadOnlyDictionary<Guid, List<ConnectionGroup>> childrenByParent,
        HashSet<Guid> visited)
    {
        if (!childrenByParent.TryGetValue(parentId ?? Guid.Empty, out List<ConnectionGroup>? children))
        {
            return;
        }

        foreach (ConnectionGroup group in children)
        {
            if (!visited.Add(group.Id))
            {
                continue;
            }

            string id = GroupViewPrefix + group.Id.ToString("D");
            AntdUI.MenuItem item = CreateNavigationItem(group.Name, id, "FolderOutlined");
            AddGroupNavigationItems(item, group.Id, childrenByParent, visited);
            parentItem.Sub.Add(item);
        }
    }

    /// <summary>
    /// Builds and sorts a linear-time parent-to-children index for navigation and filtering. / 为导航与筛选构建并排序线性时间的父分类到子分类索引。
    /// </summary>
    /// <param name="groups">Complete validated group collection. / 完整且已经验证的分类集合。</param>
    /// <returns>Child lists keyed by parent identifier; an empty identifier represents roots. / 按父标识索引的子分类列表；空标识表示根分类。</returns>
    private static IReadOnlyDictionary<Guid, List<ConnectionGroup>> BuildGroupChildrenLookup(
        IReadOnlyList<ConnectionGroup> groups)
    {
        Dictionary<Guid, List<ConnectionGroup>> childrenByParent = new(groups.Count);
        foreach (ConnectionGroup group in groups)
        {
            Guid parentKey = group.ParentId ?? Guid.Empty;
            if (!childrenByParent.TryGetValue(parentKey, out List<ConnectionGroup>? children))
            {
                children = [];
                childrenByParent.Add(parentKey, children);
            }

            children.Add(group);
        }

        foreach (List<ConnectionGroup> children in childrenByParent.Values)
        {
            children.Sort(static (left, right) =>
            {
                int sortOrderComparison = left.SortOrder.CompareTo(right.SortOrder);
                return sortOrderComparison != 0
                    ? sortOrderComparison
                    : StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name);
            });
        }

        return childrenByParent;
    }

    /// <summary>
    /// Refreshes filtering, table projections, and selection state. / 刷新筛选、表格投影与选择状态。
    /// </summary>
    private void RefreshConnectionTable()
    {
        IReadOnlyList<Guid> previouslySelectedIds = GetSelectedConnectionIds();
        IReadOnlyList<ConnectionProfile> connections = _workspace.GetConnections();
        _totalConnectionCount = connections.Count;
        IReadOnlyList<ConnectionGroup> groups = _workspace.GetGroups();
        Dictionary<Guid, ConnectionGroup> groupLookup = groups.ToDictionary(group => group.Id);
        HashSet<Guid>? groupFilter = ResolveActiveGroupFilter(groups);
        string query = _searchInput.Text.Trim();
        AppSettings settings = _workspace.GetSettings();

        IEnumerable<ConnectionProfile> filtered = connections.Where(profile => MatchesActiveView(profile, groupFilter, settings));
        if (_activeType is ConnectionType type)
        {
            filtered = filtered.Where(profile => profile.Type == type);
        }

        if (query.Length > 0)
        {
            filtered = filtered.Where(profile => MatchesSearch(profile, query, groupLookup));
        }

        _visibleRows = filtered
            .OrderByDescending(profile => profile.IsFavorite)
            .ThenBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(profile => CreateTableRow(profile, groupLookup, settings))
            .ToList();
        _visibleConnectionOrder = _visibleRows.Select(row => row.Id).ToList();
        _visibleConnectionIds = _visibleConnectionOrder.ToHashSet();
        _connectionTable.DataSource = _visibleRows.ToArray();

        RestoreConnectionSelection(previouslySelectedIds);
        UpdateActionState();
    }

    /// <summary>
    /// Reads the native table selection and discards identifiers that are stale, hidden, or deleted. / 读取原生表格选择，并丢弃陈旧、隐藏或已删除的标识。
    /// </summary>
    /// <returns>Visible existing connection identifiers in native selection order. / 按原生选择顺序返回可见且存在的连接标识。</returns>
    private IReadOnlyList<Guid> GetSelectedConnectionIds()
    {
        // AntdUI 2.4.8 returns no row objects from SelectedsReal() until table sorting is active.
        // SelectedIndexsReal() consistently reports one-based source indices in both sorted and unsorted views.
        // AntdUI 2.4.8 在表格尚未排序时不会从 SelectedsReal() 返回行对象；真实索引 API 在两种状态下都可用。
        return ConnectionSelectionLogic.ResolveOneBasedSelection(
            _visibleConnectionOrder,
            _connectionTable.SelectedIndexsReal());
    }

    /// <summary>
    /// Clears native table selection and refreshes all selection-dependent actions. / 清除原生表格选择，并刷新全部依赖选择的操作。
    /// </summary>
    private void ClearConnectionSelection()
    {
        _restoringConnectionSelection = true;
        try
        {
            _connectionTable.SelectedIndexs = [];
        }
        finally
        {
            _restoringConnectionSelection = false;
        }

        UpdateActionState();
    }

    /// <summary>
    /// Restores a reconciled native selection and its row highlighting after table projection changes. / 表格投影变化后恢复已对齐的原生选择及行高亮。
    /// </summary>
    /// <param name="selectedIds">Connection identifiers requested for restoration. / 请求恢复的连接标识。</param>
    private void RestoreConnectionSelection(IEnumerable<Guid> selectedIds)
    {
        ArgumentNullException.ThrowIfNull(selectedIds);
        int[] nativeIndices = ConnectionSelectionLogic.BuildOneBasedVisibleIndices(
            _visibleConnectionOrder,
            selectedIds);

        _restoringConnectionSelection = true;
        try
        {
            _connectionTable.SelectedIndexs = nativeIndices;
        }
        finally
        {
            _restoringConnectionSelection = false;
        }

        UpdateActionState();
    }

    /// <summary>
    /// Selects and highlights exactly one visible connection. / 选择并高亮恰好一条可见连接。
    /// </summary>
    /// <param name="id">Connection identifier to select. / 要选择的连接标识。</param>
    private void SelectConnection(Guid id)
    {
        RestoreConnectionSelection([id]);
    }

    /// <summary>
    /// Resolves the selected group and all of its descendants. / 解析选中的分组及其全部后代分组。
    /// </summary>
    /// <param name="groups">All connection groups. / 全部连接分组。</param>
    /// <returns>Allowed group identifiers, or null when no group filter is active. / 允许的分组标识；无分组筛选时为 null。</returns>
    private HashSet<Guid>? ResolveActiveGroupFilter(IReadOnlyList<ConnectionGroup> groups)
    {
        if (!_activeView.StartsWith(GroupViewPrefix, StringComparison.Ordinal) ||
            !Guid.TryParse(_activeView[GroupViewPrefix.Length..], out Guid selectedGroupId))
        {
            return null;
        }

        HashSet<Guid> result = [selectedGroupId];
        IReadOnlyDictionary<Guid, List<ConnectionGroup>> childrenByParent = BuildGroupChildrenLookup(groups);
        Queue<Guid> pending = new();
        pending.Enqueue(selectedGroupId);
        while (pending.Count > 0)
        {
            Guid parentId = pending.Dequeue();
            if (!childrenByParent.TryGetValue(parentId, out List<ConnectionGroup>? children))
            {
                continue;
            }

            foreach (ConnectionGroup child in children)
            {
                if (result.Add(child.Id))
                {
                    pending.Enqueue(child.Id);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Tests whether a profile belongs in the current navigation view. / 测试配置是否属于当前导航视图。
    /// </summary>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="groupFilter">Active group identifiers. / 当前分组标识集合。</param>
    /// <param name="settings">Current settings. / 当前设置。</param>
    /// <returns>True when the profile should remain visible. / 配置应保持可见时返回 true。</returns>
    private bool MatchesActiveView(ConnectionProfile profile, HashSet<Guid>? groupFilter, AppSettings settings)
    {
        if (_favoriteFilterButton.Toggle && !profile.IsFavorite)
        {
            return false;
        }

        if (_expiringFilterButton.Toggle)
        {
            ExpirationState state = _expirationService.Classify(profile, DateTime.Today, settings.ExpiryWarningDays);
            if (state is not (ExpirationState.Expired or ExpirationState.Today or ExpirationState.ExpiringSoon))
            {
                return false;
            }
        }

        return groupFilter is null || profile.GroupId is Guid groupId && groupFilter.Contains(groupId);
    }

    /// <summary>
    /// Performs culture-insensitive fuzzy matching across visible non-secret fields. / 在可见的非秘密字段中执行不区分区域的模糊匹配。
    /// </summary>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="query">Search query. / 搜索文本。</param>
    /// <param name="groups">Group lookup. / 分组索引。</param>
    /// <returns>True when any searchable field contains the query. / 任一可搜索字段包含文本时返回 true。</returns>
    private static bool MatchesSearch(
        ConnectionProfile profile,
        string query,
        IReadOnlyDictionary<Guid, ConnectionGroup> groups)
    {
        StringComparison comparison = StringComparison.CurrentCultureIgnoreCase;
        string groupName = profile.GroupId is Guid groupId ? groups.GetValueOrDefault(groupId)?.Name ?? string.Empty : string.Empty;
        return profile.Name.Contains(query, comparison) ||
               profile.Host.Contains(query, comparison) ||
               profile.Port.ToString().Contains(query, comparison) ||
               profile.Type.ToDisplayName().Contains(query, comparison) ||
               profile.Protocol.Contains(query, comparison) ||
               profile.Notes.Contains(query, comparison) ||
               groupName.Contains(query, comparison) ||
               profile.Username.Contains(query, comparison);
    }

    /// <summary>
    /// Projects one domain profile into a non-secret connection table row. / 将一个领域配置投影为不含秘密的连接表格行。
    /// </summary>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="groups">Group lookup. / 分组索引。</param>
    /// <param name="settings">Current settings. / 当前设置。</param>
    /// <returns>Table row projection. / 表格行投影。</returns>
    private ConnectionTableRow CreateTableRow(
        ConnectionProfile profile,
        IReadOnlyDictionary<Guid, ConnectionGroup> groups,
        AppSettings settings)
    {
        return new ConnectionTableRow
        {
            Id = profile.Id,
            Favorite = CreateFavoriteButton(profile),
            Name = profile.Name,
            Type = profile.Type.ToDisplayName(),
            Address = FormatEndpoint(profile),
            Group = ResolveGroupPath(profile.GroupId, groups),
            Username = profile.Username,
            Status = FormatStatus(profile.Id),
            Expiration = FormatExpiration(profile, settings),
            Notes = profile.Notes
        };
    }

    /// <summary>
    /// Creates the one-click favorite affordance rendered in the leading table column. / 创建显示在表格首列中的一键收藏控件。
    /// </summary>
    /// <param name="profile">Connection represented by the row. / 该行表示的连接。</param>
    /// <returns>A compact star button with state-specific icon and tooltip. / 带状态图标和提示的紧凑星标按钮。</returns>
    private static AntdUI.CellButton CreateFavoriteButton(ConnectionProfile profile)
    {
        return new AntdUI.CellButton(FavoriteButtonId)
        {
            Ghost = true,
            IconRatio = 0.72F,
            IconSvg = profile.IsFavorite ? "StarFilled" : "StarOutlined",
            Shape = AntdUI.TShape.Circle,
            Tooltip = profile.IsFavorite
                ? L.Get("Main.Favorite.Remove")
                : L.Get("Main.Favorite.Add"),
            Fore = profile.IsFavorite ? AntdUI.Style.Get(AntdUI.Colour.Warning) : null
        };
    }

    /// <summary>
    /// Formats a network endpoint while preserving IPv6 readability. / 格式化网络端点，同时保持 IPv6 可读性。
    /// </summary>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <returns>Formatted endpoint. / 格式化后的端点。</returns>
    private static string FormatEndpoint(ConnectionProfile profile)
    {
        string host = profile.Host;
        if (IPAddress.TryParse(host, out IPAddress? address) && address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            host = $"[{host}]";
        }

        return profile.Port > 0 ? $"{host}:{profile.Port}" : host;
    }

    /// <summary>
    /// Resolves a nested group path while guarding against invalid references. / 解析嵌套分组路径，同时防止无效引用。
    /// </summary>
    /// <param name="groupId">Optional leaf group identifier. / 可选叶分组标识。</param>
    /// <param name="groups">Group lookup. / 分组索引。</param>
    /// <returns>Slash-separated path or the ungrouped label. / 斜线分隔路径或未分组标签。</returns>
    private static string ResolveGroupPath(Guid? groupId, IReadOnlyDictionary<Guid, ConnectionGroup> groups)
    {
        if (groupId is not Guid currentId)
        {
            return L.Get("Common.Ungrouped");
        }

        List<string> names = [];
        HashSet<Guid> visited = [];
        while (visited.Add(currentId) && groups.TryGetValue(currentId, out ConnectionGroup? group))
        {
            names.Add(group.Name);
            if (group.ParentId is not Guid parentId)
            {
                break;
            }

            currentId = parentId;
        }

        names.Reverse();
        return names.Count == 0 ? L.Get("Common.Ungrouped") : string.Join(" / ", names);
    }

    /// <summary>
    /// Formats the latest conservative reachability state. / 格式化最近一次保守可达性状态。
    /// </summary>
    /// <param name="connectionId">Connection identifier. / 连接标识。</param>
    /// <returns>Bilingual status text. / 双语状态文本。</returns>
    private string FormatStatus(Guid connectionId)
    {
        if (!_statusBatchState.TryGetStatus(connectionId, out ConnectionStatus? status) || status is null)
        {
            return L.Get("Status.NotChecked");
        }

        return status.State switch
        {
            ReachabilityState.Checking => L.Get("Status.Checking"),
            ReachabilityState.NotApplicable => L.Get("Status.NotApplicable"),
            ReachabilityState.Reachable => status.LatencyMilliseconds is long latency
                ? L.Format("Status.ReachableWithLatency", latency)
                : L.Get("Status.Reachable"),
            ReachabilityState.NoIcmpResponse => L.Get("Status.NoIcmpResponse"),
            ReachabilityState.InvalidAddress => L.Get("Status.InvalidAddress"),
            ReachabilityState.Error => L.Get("Status.Error"),
            _ => L.Get("Status.NotChecked")
        };
    }

    /// <summary>
    /// Formats expiration state and remaining days. / 格式化到期状态与剩余天数。
    /// </summary>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="settings">Current settings. / 当前设置。</param>
    /// <returns>Bilingual expiration text. / 双语到期文本。</returns>
    private string FormatExpiration(ConnectionProfile profile, AppSettings settings)
    {
        int? days = _expirationService.GetRemainingDays(profile, DateTime.Today);
        return _expirationService.Classify(profile, DateTime.Today, settings.ExpiryWarningDays) switch
        {
            ExpirationState.NotSet => "—",
            ExpirationState.Expired => L.Format("Expiration.ExpiredDays", Math.Abs(days ?? 0)),
            ExpirationState.Today => L.Get("Expiration.Today"),
            ExpirationState.ExpiringSoon => L.Format("Expiration.RemainingDays", days ?? 0),
            _ => profile.ExpiresOn?.ToString("yyyy-MM-dd") ?? "—"
        };
    }

    /// <summary>
    /// Enables connection actions according to current selection and workload. / 根据当前选择与工作负载启用连接操作。
    /// </summary>
    private void UpdateActionState()
    {
        int selectedCount = GetSelectedConnectionIds().Count;
        bool hasSelection = selectedCount > 0;
        bool canConnect = !_primaryOperationInFlight && selectedCount == 1;
        _connectButton.Enabled = canConnect;
        _editButton.Enabled = !_primaryOperationInFlight && selectedCount == 1;
        _deleteButton.Enabled = !_primaryOperationInFlight && hasSelection;
        _addButton.Type = canConnect ? AntdUI.TTypeMini.Default : AntdUI.TTypeMini.Primary;
        _connectButton.Type = canConnect ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
        string favoriteFilterStatus = _favoriteFilterButton.Toggle
            ? L.Get("Main.ViewStatus.FavoritesOnlySuffix")
            : string.Empty;
        string expiringFilterStatus = _expiringFilterButton.Toggle
            ? L.Get("Main.ViewStatus.ExpiringOnlySuffix")
            : string.Empty;
        _viewStatus.Text = L.Format(
            "Main.ViewStatus.Summary",
            _visibleRows.Count,
            _totalConnectionCount,
            selectedCount) + favoriteFilterStatus + expiringFilterStatus;
    }

    /// <summary>
    /// Applies menu, title-bar, toolbar, and table breakpoints using logical DPI-independent width. / 使用与 DPI 无关的逻辑宽度应用菜单、标题栏、工具栏与表格断点。
    /// </summary>
    private void ApplyResponsiveLayout()
    {
        // Visible includes ancestor visibility. Measuring while the form is hidden
        // treats every toolbar item as absent and expands the spacers to a full row.
        // Minimized client bounds are likewise not the restored layout's dimensions.
        // / 窗口隐藏时子控件的 Visible 也为 false，会将占位空隙误算为整行宽度；
        // 最小化时的客户区尺寸同样不能用于计算恢复后的布局。
        if (!_runtimeInitialized || !Visible || WindowState == FormWindowState.Minimized)
        {
            return;
        }

        float scale = DeviceDpi <= 0 ? 1F : DeviceDpi / 96F;
        float logicalWidth = ClientSize.Width / scale;
        float logicalHeight = ClientSize.Height / scale;
        ApplyResponsiveMinimumSize();
        bool compactSidebar = logicalWidth < 1080 || _workspace.GetSettings().SidebarCollapsed;
        _navigation.SetCollapsed(compactSidebar);
        _sidebar.Width = ScaleLogical(compactSidebar ? 72 : 232);
        _groupsButton.Text = compactSidebar
            ? L.Get("Main.Command.Groups.Compact")
            : L.Get("Main.Command.Groups");
        _groupsButton.Width = ScaleLogical(compactSidebar ? 52 : 102);
        _header.SubText = logicalWidth < 900F
            ? string.Empty
            : L.Get("Main.Header.Subtitle");
        _settingsButton.Size = new Size(ScaleLogical(36), ScaleLogical(36));
        _settingsButton.Location = new Point(
            Math.Max(ScaleLogical(8), _header.ClientSize.Width - ScaleLogical(262)),
            ScaleLogical(11));
        _minimizeToTrayButton.Size = new Size(ScaleLogical(36), ScaleLogical(36));
        _minimizeToTrayButton.Location = new Point(
            Math.Max(ScaleLogical(48), _header.ClientSize.Width - ScaleLogical(218)),
            ScaleLogical(11));

        int toolbarAvailableWidth = Math.Max(1, ClientSize.Width - _sidebar.Width - ScaleLogical(32));
        float logicalToolbarWidth = toolbarAvailableWidth / scale;
        MainResponsiveLayoutPlan layoutPlan = MainResponsiveLayoutLogic.CreatePlan(logicalToolbarWidth, logicalHeight);
        _searchInput.Width = ScaleLogical(layoutPlan.SearchWidth);
        _typeFilter.Width = ScaleLogical(layoutPlan.TypeFilterWidth);
        ApplyToolbarLabelMode(layoutPlan.CompactToolbarText);
        ApplyToolbarOverflowMode(layoutPlan.UseToolbarOverflow);
        UpdateToolbarSpacer(_toolbar, _toolbarSpacer, toolbarAvailableWidth);
        UpdateToolbarSpacer(_secondaryToolbar, _secondaryToolbarSpacer, toolbarAvailableWidth);
        int measuredToolbarHeight = MainResponsiveLayoutLogic.CalculateWrappedHeight(
            Math.Max(1, toolbarAvailableWidth - _toolbar.Padding.Horizontal),
            _toolbar.Padding.Vertical,
            MeasureToolbarItemOuterSizes(_toolbar));
        int measuredSecondaryToolbarHeight = MainResponsiveLayoutLogic.CalculateWrappedHeight(
            Math.Max(1, toolbarAvailableWidth - _secondaryToolbar.Padding.Horizontal),
            _secondaryToolbar.Padding.Vertical,
            MeasureToolbarItemOuterSizes(_secondaryToolbar));
        _toolbar.Height = Math.Max(ScaleLogical(layoutPlan.ToolbarHeight), measuredToolbarHeight);
        _secondaryToolbar.Height = Math.Max(ScaleLogical(layoutPlan.SecondaryToolbarHeight), measuredSecondaryToolbarHeight);
        _connectionTable.MinimumSize = new Size(0, ScaleLogical(layoutPlan.MinimumTableHeight));

        bool showSecondaryColumns = logicalWidth >= 900;
        SetColumnVisibility(nameof(ConnectionTableRow.Group), showSecondaryColumns);
        SetColumnVisibility(nameof(ConnectionTableRow.Username), showSecondaryColumns);
    }

    /// <summary>
    /// Measures every visible toolbar control including its DPI-scaled margins. / 测量每个可见工具栏控件及其按 DPI 缩放的外边距。
    /// </summary>
    /// <returns>Outer sizes in flow order. / 按流式顺序排列的外部尺寸。</returns>
    private static IReadOnlyList<Size> MeasureToolbarItemOuterSizes(FlowLayoutPanel toolbar)
    {
        List<Size> itemSizes = new(toolbar.Controls.Count);
        foreach (Control control in toolbar.Controls)
        {
            if (!control.Visible)
            {
                continue;
            }

            Size contentSize = control.AutoSize
                ? control.GetPreferredSize(Size.Empty)
                : control.Size;
            int width = Math.Max(1, contentSize.Width) + control.Margin.Horizontal;
            int height = Math.Max(1, Math.Max(control.Height, contentSize.Height)) + control.Margin.Vertical;
            itemSizes.Add(new Size(width, height));
        }

        return itemSizes;
    }

    /// <summary>
    /// Uses a flexible row-local gap to align commands to the right whenever one row is available. / 当控件可容纳于一行时，使用行内弹性间距将命令右对齐。
    /// </summary>
    /// <param name="toolbar">Toolbar row receiving the flexible gap. / 使用弹性间距的工具栏行。</param>
    /// <param name="spacer">Flexible spacer in that row. / 该行中的弹性间距。</param>
    /// <param name="toolbarAvailableWidth">Physical toolbar width inside the content padding. / 内容内边距中的工具栏物理宽度。</param>
    private void UpdateToolbarSpacer(
        FlowLayoutPanel toolbar,
        Control spacer,
        int toolbarAvailableWidth)
    {
        spacer.Width = 0;
        int occupiedWidth = toolbar.Padding.Horizontal;
        foreach (Control control in toolbar.Controls)
        {
            if (!control.Visible || ReferenceEquals(control, spacer))
            {
                continue;
            }

            Size contentSize = control.AutoSize
                ? control.GetPreferredSize(Size.Empty)
                : control.Size;
            occupiedWidth += Math.Max(1, contentSize.Width) + control.Margin.Horizontal;
        }

        spacer.Width = Math.Max(0, toolbarAvailableWidth - occupiedWidth - ScaleLogical(4));
    }

    /// <summary>
    /// Switches toolbar labels to concise localized text on narrow screens while preserving accessible names. / 在窄屏上切换为简洁的本地化工具栏标签，并保留无障碍名称。
    /// </summary>
    /// <param name="compact">Whether compact labels should be used. / 是否使用紧凑标签。</param>
    private void ApplyToolbarLabelMode(bool compact)
    {
        _searchInput.PlaceholderText = compact
            ? L.Get("Main.Search.CompactPlaceholder")
            : L.Get("Main.Search.Placeholder");
        _favoriteFilterButton.Text = compact
            ? L.Get("Main.Filter.Favorites.Compact")
            : L.Get("Main.Filter.Favorites");
        _favoriteFilterButton.ToggleText = _favoriteFilterButton.Text;
        _expiringFilterButton.Text = compact
            ? L.Get("Main.Filter.Expiring.Compact")
            : L.Get("Main.Filter.Expiring");
        _expiringFilterButton.ToggleText = _expiringFilterButton.Text;
        _addButton.Text = L.Get(compact ? "Common.Add.Compact" : "Common.Add");
        _quickButton.Text = L.Get(compact ? "Main.Command.Quick.Compact" : "Main.Command.Quick");
        _connectButton.Text = L.Get(compact ? "Common.Connect.Compact" : "Common.Connect");
        _editButton.Text = L.Get(compact ? "Common.Edit.Compact" : "Common.Edit");
        _deleteButton.Text = L.Get(compact ? "Common.Delete.Compact" : "Common.Delete");
        _statusButton.Text = L.Get(compact ? "Main.Command.Check.Compact" : "Main.Command.Check");
        _transferButton.Text = L.Get(compact ? "Main.Command.Transfer.Compact" : "Main.Command.Transfer");
        _moreButton.Text = L.Get(compact ? "Common.More.Compact" : "Common.More");

        _favoriteFilterButton.AccessibleName = L.Get("Main.Filter.Favorites.AccessibleName");
        _expiringFilterButton.AccessibleName = L.Get("Main.Filter.Expiring.AccessibleName");
        _addButton.AccessibleName = L.Get("Common.Add");
        _quickButton.AccessibleName = L.Get("Main.Command.QuickConnection");
        _connectButton.AccessibleName = L.Get("Common.Connect");
        _editButton.AccessibleName = L.Get("Common.Edit");
        _deleteButton.AccessibleName = L.Get("Common.Delete");
        _statusButton.AccessibleName = L.Get("Main.Command.CheckStatus");
        _transferButton.AccessibleName = L.Get("Main.Command.ImportExport");
        _settingsButton.AccessibleName = L.Get("Common.Settings");
        _moreButton.AccessibleName = L.Get("Main.Command.MoreCommands");
    }

    /// <summary>
    /// Moves secondary commands into the More menu when vertical space cannot accommodate the wrapped toolbar. / 当垂直空间无法容纳换行工具栏时，将次要命令移入“更多”菜单。
    /// </summary>
    /// <param name="useOverflow">Whether the low-height overflow presentation is active. / 是否启用低高度溢出呈现。</param>
    private void ApplyToolbarOverflowMode(bool useOverflow)
    {
        _quickButton.Visible = !useOverflow;
        _statusButton.Visible = !useOverflow;
        _transferButton.Visible = !useOverflow;
        _moreButton.Visible = useOverflow;
    }

    /// <summary>
    /// Applies a DPI-scaled logical minimum that never exceeds the current monitor working area. / 应用按 DPI 缩放的逻辑最小尺寸，并确保不超过当前显示器工作区。
    /// </summary>
    private void ApplyResponsiveMinimumSize()
    {
        Rectangle workingArea = Screen.FromControl(this).WorkingArea;
        int horizontalMargin = ScaleLogical(16);
        int verticalMargin = ScaleLogical(8);
        int maximumWidth = Math.Max(1, workingArea.Width - horizontalMargin);
        int maximumHeight = Math.Max(1, workingArea.Height - verticalMargin);
        MinimumSize = new Size(
            Math.Min(ScaleLogical(720), maximumWidth),
            Math.Min(ScaleLogical(480), maximumHeight));
    }

    /// <summary>
    /// Converts a logical 96-DPI measurement to physical pixels for the form's current monitor. / 将 96 DPI 逻辑尺寸转换为主窗体当前显示器的物理像素。
    /// </summary>
    /// <param name="logicalPixels">Logical pixel measurement at 96 DPI. / 96 DPI 下的逻辑像素尺寸。</param>
    /// <returns>Rounded physical pixel measurement. / 四舍五入后的物理像素尺寸。</returns>
    private int ScaleLogical(int logicalPixels)
    {
        float scale = DeviceDpi <= 0 ? 1F : DeviceDpi / 96F;
        return Math.Max(0, (int)Math.Round(logicalPixels * scale, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Toggles one table column by its binding key. / 按绑定键切换一个表格列。
    /// </summary>
    /// <param name="key">Column binding key. / 列绑定键。</param>
    /// <param name="visible">Desired visibility. / 期望可见性。</param>
    private void SetColumnVisibility(string key, bool visible)
    {
        foreach (AntdUI.Column column in _connectionTable.Columns)
        {
            if (string.Equals(column.Key, key, StringComparison.Ordinal))
            {
                column.Visible = visible;
                break;
            }
        }
    }

    /// <summary>
    /// Restores persisted bounds on the best-overlapping monitor while keeping the whole window reachable. / 在交叠最多的显示器上恢复持久化边界，同时使整个窗口保持可达。
    /// </summary>
    /// <param name="bounds">Persisted normal bounds. / 持久化的正常边界。</param>
    private void ApplyInitialWindowBounds(Rectangle bounds)
    {
        Screen? targetScreen = Screen.AllScreens
            .OrderByDescending(screen => IntersectionArea(screen.WorkingArea, bounds))
            .FirstOrDefault(screen => IntersectionArea(screen.WorkingArea, bounds) > 0)
            ?? Screen.PrimaryScreen;
        Rectangle workingArea = targetScreen?.WorkingArea ?? Screen.FromPoint(Cursor.Position).WorkingArea;
        StartPosition = FormStartPosition.Manual;
        Bounds = MainResponsiveLayoutLogic.ClampWindowBounds(
            bounds,
            workingArea,
            MinimumSize,
            new Size(1280, 800),
            ScaleLogical(8));
    }

    /// <summary>
    /// Returns the physical intersection area of two rectangles for monitor selection. / 返回两个矩形的物理交集面积，以便选择显示器。
    /// </summary>
    /// <param name="left">First rectangle. / 第一个矩形。</param>
    /// <param name="right">Second rectangle. / 第二个矩形。</param>
    /// <returns>Intersection area in physical square pixels. / 以物理平方像素表示的交集面积。</returns>
    private static long IntersectionArea(Rectangle left, Rectangle right)
    {
        Rectangle intersection = Rectangle.Intersect(left, right);
        return (long)intersection.Width * intersection.Height;
    }

    /// <summary>
    /// Keeps a normal window completely reachable after display or DPI topology changes. / 在显示器或 DPI 拓扑变化后，使正常窗口保持完全可达。
    /// </summary>
    private void ClampCurrentWindowToWorkingArea()
    {
        if (WindowState != FormWindowState.Normal)
        {
            return;
        }

        Rectangle workingArea = Screen.FromControl(this).WorkingArea;
        Bounds = MainResponsiveLayoutLogic.ClampWindowBounds(
            Bounds,
            workingArea,
            MinimumSize,
            new Size(ScaleLogical(1280), ScaleLogical(800)),
            ScaleLogical(8));
    }

    /// <summary>
    /// Handles a navigation selection and refreshes the visible connection view. / 处理导航选择并刷新可见连接视图。
    /// </summary>
    /// <param name="sender">Navigation menu. / 导航菜单。</param>
    /// <param name="e">Selected menu item data. / 选中菜单项数据。</param>
    private void HandleNavigationChanged(object sender, AntdUI.MenuSelectEventArgs e)
    {
        if (e.Value.ID is not string selectedId || selectedId == "groups-root")
        {
            return;
        }

        _activeView = selectedId;
        ClearConnectionSelection();
        RefreshConnectionTable();
    }

    /// <summary>
    /// Refreshes the table when search text changes. / 搜索文本变化时刷新表格。
    /// </summary>
    /// <param name="sender">Search input. / 搜索输入框。</param>
    /// <param name="e">Text-change event data. / 文本变化事件数据。</param>
    private void HandleSearchChanged(object? sender, EventArgs e)
    {
        RefreshConnectionTable();
    }

    /// <summary>
    /// Applies a newly selected connection-type filter. / 应用新选择的连接类型筛选。
    /// </summary>
    /// <param name="sender">Type selector. / 类型选择器。</param>
    /// <param name="e">Selected value event data. / 选中值事件数据。</param>
    private void HandleTypeFilterChanged(object sender, AntdUI.ObjectNEventArgs e)
    {
        _activeType = e.Value is ConnectionType type ? type : null;
        RefreshConnectionTable();
    }

    /// <summary>
    /// Refreshes action state when AntdUI changes its native row selection. / AntdUI 更改原生行选择时刷新操作状态。
    /// </summary>
    /// <param name="sender">Connection table. / 连接表格。</param>
    /// <param name="e">Selection-change event data. / 选择变更事件数据。</param>
    private void HandleConnectionSelectionChanged(object? sender, EventArgs e)
    {
        if (_restoringConnectionSelection)
        {
            return;
        }

        UpdateActionState();
    }

    /// <summary>
    /// Persists a star-button change without requiring the connection editor. / 无需打开连接编辑器即可持久化星标更改。
    /// </summary>
    /// <param name="sender">Connection table. / 连接表格。</param>
    /// <param name="e">Clicked cell-button data. / 被点击的单元格按钮数据。</param>
    private async void HandleConnectionCellButtonClick(object sender, AntdUI.TableButtonEventArgs e)
    {
        if (e.Button != MouseButtons.Left ||
            e.Record is not ConnectionTableRow row ||
            !string.Equals(e.Column?.Key, nameof(ConnectionTableRow.Favorite), StringComparison.Ordinal) ||
            !string.Equals(e.Btn.Id, FavoriteButtonId, StringComparison.Ordinal))
        {
            return;
        }

        await RunPrimaryOperationAsync(
            () => ToggleFavoriteAsync(row.Id),
            L.Get("Error.FavoriteUpdateFailed"));
    }

    /// <summary>
    /// Launches the double-clicked connection. / 启动双击的连接。
    /// </summary>
    /// <param name="sender">Connection table. / 连接表格。</param>
    /// <param name="e">Double-clicked cell data. / 双击单元格数据。</param>
    private void HandleConnectionCellDoubleClick(object sender, AntdUI.TableClickEventArgs e)
    {
        if (_primaryOperationInFlight || !CanUpdateUi())
        {
            return;
        }

        if (e.Record is ConnectionTableRow row &&
            !string.Equals(e.Column?.Key, nameof(ConnectionTableRow.Favorite), StringComparison.Ordinal))
        {
            SelectConnection(row.Id);
            LaunchSelectedConnection();
        }
    }

    /// <summary>
    /// Dispatches toolbar and sidebar button commands. / 分派工具栏与侧栏按钮命令。
    /// </summary>
    /// <param name="sender">Command button. / 命令按钮。</param>
    /// <param name="e">Click event data. / 单击事件数据。</param>
    private async void HandleToolbarClick(object? sender, EventArgs e)
    {
        string command = sender switch
        {
            Control control => control.Tag as string ?? string.Empty,
            ToolStripItem item => item.Tag as string ?? string.Empty,
            _ => string.Empty
        };
        if (!CanUpdateUi() || _primaryOperationInFlight && command != "check")
        {
            return;
        }

        try
        {
            switch (command)
            {
                case "add":
                    await RunPrimaryOperationAsync(AddConnectionAsync, L.Get("Error.AddConnectionFailed"));
                    break;
                case "quick":
                    QuickConnect();
                    break;
                case "connect":
                    LaunchSelectedConnection();
                    break;
                case "edit":
                    await RunPrimaryOperationAsync(EditSelectedConnectionAsync, L.Get("Error.EditConnectionFailed"));
                    break;
                case "delete":
                    await RunPrimaryOperationAsync(DeleteSelectedConnectionsAsync, L.Get("Error.DeleteConnectionFailed"));
                    break;
                case "favorite-filter":
                case "expiring-filter":
                    ClearConnectionSelection();
                    RefreshConnectionTable();
                    break;
                case "check":
                    await CheckVisibleConnectionsAsync();
                    break;
                case "transfer":
                    Control transferAnchor = _transferButton.Visible ? _transferButton : _moreButton;
                    _transferMenu.Show(transferAnchor, new Point(0, transferAnchor.Height));
                    break;
                case "more":
                    _toolbarOverflowMenu.Show(_moreButton, new Point(0, _moreButton.Height));
                    break;
                case "settings":
                    await RunPrimaryOperationAsync(OpenSettingsAsync, L.Get("Error.SettingsSaveFailed"));
                    break;
                case "groups":
                    await RunPrimaryOperationAsync(
                        () => RunSynchronousDialogAsync(OpenGroupManager),
                        L.Get("Error.GroupOperationFailed"));
                    break;
            }
        }
        catch (Exception exception)
        {
            ShowError(L.Get("Error.OperationFailed"), exception);
        }
    }

    /// <summary>
    /// Runs one modal synchronous action through the common awaitable operation pipeline. / 通过通用可等待操作管线运行一个模态同步操作。
    /// </summary>
    /// <param name="action">Modal action to run. / 要运行的模态操作。</param>
    /// <returns>An already completed task after the modal action closes. / 模态操作关闭后已完成的任务。</returns>
    private static Task RunSynchronousDialogAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Serializes one user-initiated primary operation, disables conflicting commands, and safely handles deferred exit. / 串行执行一项用户发起的主要操作、禁用冲突命令，并安全处理延迟退出。
    /// </summary>
    /// <param name="operation">Operation to execute on the UI context. / 要在 UI 上下文执行的操作。</param>
    /// <param name="errorTitle">Bilingual title used when the operation fails. / 操作失败时使用的双语标题。</param>
    /// <returns>A task that completes after UI state has been restored or exit has been scheduled. / UI 状态恢复或已安排退出后完成的任务。</returns>
    private async Task RunPrimaryOperationAsync(Func<Task> operation, string errorTitle)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_primaryOperationInFlight || !CanUpdateUi())
        {
            return;
        }

        _primaryOperationInFlight = true;
        SetPrimaryOperationState(busy: true);
        bool operationSucceeded = false;
        try
        {
            await operation();
            operationSucceeded = true;
        }
        catch (Exception exception)
        {
            _exitRequestedAfterOperation = false;
            ShowError(errorTitle, exception);
        }
        finally
        {
            _primaryOperationInFlight = false;
            if (CanUpdateUi())
            {
                SetPrimaryOperationState(busy: false);
            }

            if (operationSucceeded)
            {
                ScheduleDeferredExitIfReady();
            }
        }
    }

    /// <summary>
    /// Schedules a requested real exit after every primary operation and pending bounds save has completed. / 在全部主要操作与待处理边界保存完成后安排所请求的真正退出。
    /// </summary>
    private void ScheduleDeferredExitIfReady()
    {
        if (!_exitRequestedAfterOperation ||
            _primaryOperationInFlight ||
            _windowBoundsSaveCount > 0 ||
            !CanUpdateUi())
        {
            return;
        }

        _exitRequestedAfterOperation = false;
        _forceExit = true;
        BeginInvoke(Close);
    }

    /// <summary>
    /// Enables or disables commands that could conflict with a primary save, import, export, or modal edit. / 启用或禁用可能与主要保存、导入、导出或模态编辑冲突的命令。
    /// </summary>
    /// <param name="busy">Whether a primary operation is active. / 是否有主要操作正在进行。</param>
    private void SetPrimaryOperationState(bool busy)
    {
        bool enabled = !busy;
        _addButton.Enabled = enabled;
        _quickButton.Enabled = enabled;
        _connectButton.Enabled = enabled;
        _editButton.Enabled = enabled;
        _deleteButton.Enabled = enabled;
        _transferButton.Enabled = enabled;
        _settingsButton.Enabled = enabled;
        _favoriteFilterButton.Enabled = enabled;
        _expiringFilterButton.Enabled = enabled;
        _groupsButton.Enabled = enabled;
        _moreButton.Enabled = enabled;
        _toolbarOverflowMenu.Enabled = enabled;
        _connectionTable.Enabled = enabled;
        if (!busy)
        {
            UpdateActionState();
        }
    }

    /// <summary>
    /// Toggles and persists the favorite state of one saved connection. / 切换并持久化一条已保存连接的收藏状态。
    /// </summary>
    /// <param name="connectionId">Connection identifier. / 连接标识。</param>
    private async Task ToggleFavoriteAsync(Guid connectionId)
    {
        ConnectionProfile? profile = _workspace.GetConnection(connectionId);
        if (profile is null)
        {
            return;
        }

        profile.IsFavorite = !profile.IsFavorite;
        await _workspace.UpdateConnectionAsync(profile);
    }

    /// <summary>
    /// Opens the add editor and persists the confirmed connection. / 打开新增编辑器并持久化已确认的连接。
    /// </summary>
    private async Task AddConnectionAsync()
    {
        Guid? defaultGroupId = _activeView.StartsWith(GroupViewPrefix, StringComparison.Ordinal) &&
            Guid.TryParse(_activeView[GroupViewPrefix.Length..], out Guid selectedGroupId)
                ? selectedGroupId
                : null;
        using ConnectionEditorForm editor = new(null, _workspace.GetGroups(), ConnectionEditorMode.Add, defaultGroupId);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.Result is not ConnectionProfile profile)
        {
            return;
        }

        ConnectionProfile committed = await _workspace.AddConnectionAsync(profile);
        if (!CanUpdateUi())
        {
            return;
        }

        RefreshConnectionTable();
        SelectConnection(committed.Id);
        AntdUI.Message.success(this, L.Get("Main.Message.ConnectionAdded"));
    }

    /// <summary>
    /// Opens the quick-connect editor and launches without saving. / 打开快速连接编辑器并在不保存的情况下启动。
    /// </summary>
    private void QuickConnect()
    {
        using ConnectionEditorForm editor = new(null, _workspace.GetGroups(), ConnectionEditorMode.QuickConnect);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Result is ConnectionProfile profile)
        {
            LaunchProfile(profile);
        }
    }

    /// <summary>
    /// Opens the selected connection editor and persists changes. / 打开选中连接编辑器并持久化更改。
    /// </summary>
    private async Task EditSelectedConnectionAsync()
    {
        if (GetSingleSelectedProfile() is not ConnectionProfile profile)
        {
            return;
        }

        using ConnectionEditorForm editor = new(profile, _workspace.GetGroups(), ConnectionEditorMode.Edit);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Result is ConnectionProfile result)
        {
            await _workspace.UpdateConnectionAsync(result);
            if (!CanUpdateUi())
            {
                return;
            }

            AntdUI.Message.success(this, L.Get("Main.Message.ConnectionUpdated"));
        }
    }

    /// <summary>
    /// Launches the only selected saved connection. / 启动唯一选中的已保存连接。
    /// </summary>
    private void LaunchSelectedConnection()
    {
        if (GetSingleSelectedProfile() is ConnectionProfile profile)
        {
            LaunchProfile(profile);
        }
    }

    /// <summary>
    /// Builds and starts a connection using its inline authentication values and current security settings. / 使用连接内联认证信息与当前安全设置构建并启动连接。
    /// </summary>
    /// <param name="profile">Connection profile to launch. / 要启动的连接配置。</param>
    private void LaunchProfile(ConnectionProfile profile)
    {
        try
        {
            _launchService.Launch(profile, _workspace.GetSettings());
            AntdUI.Message.success(this, L.Format("Main.Message.LaunchingConnection", profile.Name));
        }
        catch (Exception exception) when (exception is LaunchValidationException or InvalidOperationException or IOException)
        {
            ShowError(L.Get("Error.UnableToLaunch"), exception);
        }
    }

    /// <summary>
    /// Surfaces automatic backup recovery after the main window is ready to host notifications. / 主窗口可显示通知后，向用户呈现自动备份恢复结果。
    /// </summary>
    /// <param name="e">Shown event data. / 显示事件数据。</param>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (!_runtimeInitialized)
        {
            return;
        }

        ApplyResponsiveLayout();
        ClampCurrentWindowToWorkingArea();
        if (!_recoveryNoticeShown && _workspace.RecoveredFromBackup)
        {
            _recoveryNoticeShown = true;
            AntdUI.Message.warn(this, L.Get("Main.Message.BackupRestored"));
        }
    }

    /// <summary>Remeasures toolbar controls after every return from the tray, once their inherited visibility is restored. / 每次从托盘恢复、子控件重新可见后，重新测量工具栏布局。</summary>
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        ApplyResponsiveLayout();
    }

    /// <summary>
    /// Reapplies logical breakpoints after Windows moves the form to a monitor with a different DPI. / Windows 将窗体移到不同 DPI 的显示器后重新应用逻辑断点。
    /// </summary>
    /// <param name="e">Per-monitor DPI change data. / 每显示器 DPI 变化数据。</param>
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        if (!_runtimeInitialized)
        {
            return;
        }

        ApplyResponsiveLayout();
        ClampCurrentWindowToWorkingArea();
    }

    /// <summary>
    /// Returns the only selected saved connection, otherwise shows guidance. / 返回唯一选中的已保存连接，否则显示操作指引。
    /// </summary>
    /// <returns>Selected profile or null. / 选中的配置或 null。</returns>
    private ConnectionProfile? GetSingleSelectedProfile()
    {
        IReadOnlyList<Guid> selectedIds = GetSelectedConnectionIds();
        if (selectedIds.Count != 1)
        {
            AntdUI.Message.warn(this, L.Get("Main.Message.SelectOneConnection"));
            return null;
        }

        return _workspace.GetConnection(selectedIds[0]);
    }

    /// <summary>
    /// Confirms and atomically deletes all connections in the native table selection. / 确认并以原子方式删除原生表格选择中的全部连接。
    /// </summary>
    private async Task DeleteSelectedConnectionsAsync()
    {
        HashSet<Guid> selectedIds = GetSelectedConnectionIds().ToHashSet();
        IReadOnlyList<ConnectionProfile> selectedProfiles = _workspace.GetConnections()
            .Where(profile => selectedIds.Contains(profile.Id))
            .ToArray();
        if (selectedProfiles.Count == 0)
        {
            return;
        }

        AppSettings settings = _workspace.GetSettings();
        if (settings.ConfirmBeforeDelete)
        {
            string nameSummary = ConnectionSelectionLogic.BuildDeletionNameSummary(
                selectedProfiles.Select(profile => profile.Name),
                separator: L.Get("Common.ListSeparator"),
                unnamed: L.Get("Common.Unnamed"),
                formatRemaining: count => L.Format("Common.MoreItems", count));
            DialogResult result = MessageBox.Show(
                this,
                L.Format("Main.DeleteConnections.Confirmation", selectedProfiles.Count, nameSummary),
                ProductInfo.Name,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result != DialogResult.Yes)
            {
                return;
            }
        }

        Guid[] ids = selectedProfiles.Select(profile => profile.Id).ToArray();
        int deletedCount = await _workspace.DeleteConnectionsAsync(ids);
        if (!CanUpdateUi())
        {
            return;
        }

        ClearConnectionSelection();
        AntdUI.Message.success(this, L.Format("Main.Message.ConnectionsDeleted", deletedCount));
    }

    /// <summary>
    /// Checks all currently visible network connections with bounded concurrency. / 使用有界并发检测当前可见的全部网络连接。
    /// </summary>
    private async Task CheckVisibleConnectionsAsync()
    {
        CancellationTokenSource? previousCancellation = _statusCancellation;
        _statusCancellation = null;
        previousCancellation?.Cancel();

        HashSet<Guid> visibleIds = new(_visibleConnectionIds);
        IReadOnlyList<ConnectionProfile> profiles = _workspace.GetConnections()
            .Where(profile => visibleIds.Contains(profile.Id))
            .ToArray();
        long generation = _statusBatchState.BeginBatch(profiles.Select(profile => profile.Id));
        if (profiles.Count == 0)
        {
            _statusButton.Loading = false;
            RefreshConnectionTable();
            return;
        }

        CancellationTokenSource batchCancellation = new();
        _statusCancellation = batchCancellation;
        RefreshConnectionTable();
        _statusButton.Loading = true;
        AppSettings settings = _workspace.GetSettings();
        try
        {
            IReadOnlyList<ConnectionStatus> results = await _statusService.CheckManyAsync(
                profiles,
                settings.PingTimeoutMilliseconds,
                settings.ConcurrentStatusChecks,
                cancellationToken: batchCancellation.Token);

            if (IsCurrentStatusBatch(generation, batchCancellation))
            {
                _statusBatchState.TryApplyResults(generation, results);
                AntdUI.Message.success(this, L.Get("Main.Message.StatusCheckCompleted"));
            }
        }
        catch (OperationCanceledException) when (batchCancellation.IsCancellationRequested)
        {
        }
        catch (Exception) when (!IsCurrentStatusBatch(generation, batchCancellation))
        {
        }
        finally
        {
            if (IsCurrentStatusBatch(generation, batchCancellation))
            {
                _statusCancellation = null;
                _statusBatchState.TryFinishBatch(generation);
                _statusButton.Loading = false;
                RefreshConnectionTable();
            }

            batchCancellation.Dispose();
        }
    }

    /// <summary>
    /// Tests whether a status callback still belongs to the active, undisposed batch. / 检查状态回调是否仍属于活动且未处置的批次。
    /// </summary>
    /// <param name="generation">Generation captured when the batch started. / 批次启动时捕获的代数。</param>
    /// <param name="batchCancellation">Cancellation source owned by that batch. / 该批次拥有的取消源。</param>
    /// <returns>True only for the batch currently allowed to update UI state. / 仅当该批次当前允许更新 UI 状态时返回 true。</returns>
    private bool IsCurrentStatusBatch(long generation, CancellationTokenSource batchCancellation)
    {
        return CanUpdateUi() &&
               generation == _statusBatchState.CurrentGeneration &&
               ReferenceEquals(_statusCancellation, batchCancellation);
    }

    /// <summary>
    /// Tests whether the main window can safely receive continuation, event, or notification updates. / 检查主窗口是否可安全接收延续、事件或通知更新。
    /// </summary>
    /// <returns>True only while the form is alive, handled, and not shutting down. / 仅当窗体存活、已创建句柄且未关闭时返回 true。</returns>
    private bool CanUpdateUi()
    {
        return !_shutdownRequested && !IsDisposed && !Disposing && IsHandleCreated;
    }

    /// <summary>
    /// Opens settings, persists the confirmed clone, and reapplies theme and layout. / 打开设置、持久化已确认副本，并重新应用主题与布局。
    /// </summary>
    private async Task OpenSettingsAsync()
    {
        using SettingsForm dialog = new(_workspace.GetSettings(), _paths.DataDirectory);
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Result is AppSettings settings)
        {
            await _workspace.UpdateSettingsAsync(settings);
            if (!CanUpdateUi())
            {
                return;
            }

            bool languageChanged = !string.Equals(
                dialog.LanguagePreference,
                L.RequestedLanguage,
                StringComparison.OrdinalIgnoreCase);
            bool languageSaveFailed = false;
            if (languageChanged && !new LanguagePreferenceStore(_paths).Save(dialog.LanguagePreference))
            {
                AntdUI.Message.error(this, L.Get("Settings.Language.SaveFailed"));
                languageChanged = false;
                languageSaveFailed = true;
            }

            ThemeManager.Apply(settings.Theme);
            ApplyWindowTheme();
            ApplyResponsiveLayout();
            Invalidate(true);
            if (languageChanged)
            {
                RestartRequested = true;
                _forceExit = true;
                Close();
            }
            else if (!languageSaveFailed)
            {
                AntdUI.Message.success(this, L.Get("Main.Message.SettingsSaved"));
            }
        }
    }

    /// <summary>
    /// Synchronizes the AntdUI window, native layout hosts, menus, and tooltips with the active palette. / 将 AntdUI 窗口、原生布局容器、菜单与提示同步到当前色板。
    /// </summary>
    private void ApplyWindowTheme()
    {
        ThemeManager.ApplyTo(this, _transferMenu, _toolbarOverflowMenu, _trayMenu);
        ThemePalette palette = ThemeManager.CurrentPalette;
        _toolTip.BackColor = palette.ElevatedBackground;
        _toolTip.ForeColor = palette.TextPrimary;
    }

    /// <summary>
    /// Re-resolves System mode when Windows changes its application color preference. / Windows 更改应用颜色偏好时重新解析跟随系统模式。
    /// </summary>
    /// <param name="sender">System event source. / 系统事件源。</param>
    /// <param name="e">Changed preference category. / 已更改的偏好类别。</param>
    private void HandleUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.Color or UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle) ||
            !_runtimeInitialized ||
            _workspace.GetSettings().Theme != AppTheme.System)
        {
            return;
        }

        void ReapplySystemTheme()
        {
            if (!CanUpdateUi() || _workspace.GetSettings().Theme != AppTheme.System)
            {
                return;
            }

            ThemeManager.Apply(AppTheme.System);
            ApplyWindowTheme();
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(ReapplySystemTheme);
            }
            catch (InvalidOperationException) when (!CanUpdateUi())
            {
            }

            return;
        }

        ReapplySystemTheme();
    }

    /// <summary>
    /// Opens the nested group manager. / 打开嵌套分组管理器。
    /// </summary>
    private void OpenGroupManager()
    {
        using GroupManagerForm dialog = new(_workspace);
        dialog.ShowDialog(this);
    }

    /// <summary>
    /// Dispatches an import or export menu command. / 分派导入或导出菜单命令。
    /// </summary>
    /// <param name="sender">Menu item. / 菜单项。</param>
    /// <param name="e">Click event data. / 单击事件数据。</param>
    private async void HandleTransferMenuClick(object? sender, EventArgs e)
    {
        if (!CanUpdateUi() || _primaryOperationInFlight)
        {
            return;
        }

        string command = (sender as ToolStripItem)?.Tag as string ?? string.Empty;
        switch (command)
        {
            case "export-all":
                await RunPrimaryOperationAsync(ExportAllDataAsync, L.Get("Error.DataExportFailed"));
                break;
            case "export-current":
                await RunPrimaryOperationAsync(ExportCurrentDataAsync, L.Get("Error.CurrentDataExportFailed"));
                break;
            case "import":
                await RunPrimaryOperationAsync(ImportFileAsync, L.Get("Error.ImportFailed"));
                break;
        }
    }

    /// <summary>
    /// Imports and atomically upserts a RemoteHubStudio JSON or CSV file by name. / 导入 RemoteHubStudio JSON 或 CSV 文件，并按名称原子更新或新增。
    /// </summary>
    private async Task ImportFileAsync()
    {
        using OpenFileDialog dialog = new()
        {
            Title = L.Get("Main.Transfer.Import"),
            Filter = $"{ProductInfo.Name} (*{ProductInfo.WorkspaceExportExtension};*.json;*.csv)|*{ProductInfo.WorkspaceExportExtension};*.json;*.csv|JSON (*{ProductInfo.WorkspaceExportExtension};*.json)|*{ProductInfo.WorkspaceExportExtension};*.json|CSV (*.csv)|*.csv",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        DialogResult launchTrust = ConfirmImportedLaunchConfiguration();
        if (launchTrust == DialogResult.Cancel)
        {
            return;
        }

        bool trustLaunchConfiguration = launchTrust == DialogResult.Yes;

        AppDataDocument imported;
        if (dialog.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            ImportResult csvResult = await _transferService.ImportCsvAsync(dialog.FileName, trustLaunchConfiguration);
            if (!CanUpdateUi())
            {
                return;
            }

            imported = new AppDataDocument { Groups = csvResult.Groups, Connections = csvResult.Connections };
            if (csvResult.SkippedRowCount > 0)
            {
                AntdUI.Message.warn(this, L.Format("Import.SkippedInvalidRows", csvResult.SkippedRowCount));
            }

            if (csvResult.ModifiedRowCount > 0)
            {
                AntdUI.Message.info(this, L.Format("Import.DisabledActiveSettings", csvResult.ModifiedRowCount));
            }
        }
        else
        {
            imported = await _transferService.ImportJsonAsync(dialog.FileName, trustLaunchConfiguration);
            if (!CanUpdateUi())
            {
                return;
            }
        }

        WorkspaceImportSummary summary = await _workspace.MergeAsync(imported);
        if (!CanUpdateUi())
        {
            return;
        }

        AntdUI.Message.success(
            this,
            L.Format(
                "Import.Completed",
                summary.CreatedConnectionCount,
                summary.UpdatedConnectionCount));
    }

    /// <summary>
    /// Asks whether executable overrides and custom arguments from an import source are explicitly trusted. / 询问是否明确信任导入源中的程序覆盖与自定义参数。
    /// </summary>
    /// <returns>Yes to preserve, No to disable safely, or Cancel to stop importing. / 选“是”保留，选“否”安全禁用，选“取消”停止导入。</returns>
    private DialogResult ConfirmImportedLaunchConfiguration()
    {
        return MessageBox.Show(
            this,
            L.Get("Import.TrustConfirmation"),
            L.Format("Import.TrustTitle", ProductInfo.Name),
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
    }

    /// <summary>
    /// Exports every portable group and connection. / 导出全部可移植分类与连接。
    /// </summary>
    private Task ExportAllDataAsync()
    {
        return ExportDataAsync(
            _workspace.GetSnapshot(),
            "all",
            L.Get("Main.Transfer.ExportAll"),
            L.Get("Export.AllCompleted"));
    }

    /// <summary>
    /// Exports the connections in the current filtered view and their required dependencies. / 导出当前筛选视图中的连接及其必需依赖项。
    /// </summary>
    private Task ExportCurrentDataAsync()
    {
        AppDataDocument current = WorkspaceExportProjector.Create(
            _workspace.GetSnapshot(),
            _visibleConnectionOrder);
        if (current.Connections.Count == 0)
        {
            AntdUI.Message.info(this, L.Get("Export.NoCurrentConnections"));
            return Task.CompletedTask;
        }

        return ExportDataAsync(
            current,
            "current",
            L.Get("Main.Transfer.ExportCurrent"),
            L.Format("Export.CurrentCompleted", current.Connections.Count));
    }

    /// <summary>
    /// Exports one self-contained portable JSON document using the configured secret policy. / 使用已配置的秘密策略导出一份自包含便携 JSON 文档。
    /// </summary>
    /// <param name="document">Detached data to export. / 要导出的独立数据。</param>
    /// <param name="fileNameSuffix">Scope suffix used in the suggested file name. / 建议文件名所用的范围后缀。</param>
    /// <param name="dialogTitle">Save dialog title. / 保存对话框标题。</param>
    /// <param name="successMessage">Success notification. / 成功通知。</param>
    private async Task ExportDataAsync(
        AppDataDocument document,
        string fileNameSuffix,
        string dialogTitle,
        string successMessage)
    {
        AppSettings settings = _workspace.GetSettings();
        if (!ConfirmSecretExportWhenNeeded(settings.IncludeSecretsInExports))
        {
            return;
        }

        using SaveFileDialog dialog = new()
        {
            Title = dialogTitle,
            Filter = $"{ProductInfo.Name} (*{ProductInfo.WorkspaceExportExtension})|*{ProductInfo.WorkspaceExportExtension}|JSON (*.json)|*.json",
            FileName = $"{ProductInfo.Name}-{fileNameSuffix}-{DateTime.Now:yyyyMMdd-HHmm}{ProductInfo.WorkspaceExportExtension}",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            await _transferService.ExportJsonAsync(document, dialog.FileName, settings.IncludeSecretsInExports);
            if (!CanUpdateUi())
            {
                return;
            }

            AntdUI.Message.success(this, successMessage);
        }
    }

    /// <summary>
    /// Requires explicit confirmation when an export is configured to contain passwords. / 当导出配置为包含密码时要求明确确认。
    /// </summary>
    /// <param name="includeSecrets">Whether the export would contain passwords. / 导出是否会包含密码。</param>
    /// <returns>True when export may continue. / 可以继续导出时返回 true。</returns>
    private bool ConfirmSecretExportWhenNeeded(bool includeSecrets)
    {
        if (!includeSecrets)
        {
            return true;
        }

        return MessageBox.Show(
            this,
            L.Get("Export.PlaintextPasswordWarning"),
            ProductInfo.Name,
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning) == DialogResult.OK;
    }

    /// <summary>
    /// Responds to committed workspace revisions and safely marshals back to the UI thread. / 响应已提交的工作区版本，并安全切回 UI 线程。
    /// </summary>
    /// <param name="sender">Workspace service. / 工作区服务。</param>
    /// <param name="e">Committed change details. / 已提交变更详情。</param>
    private void HandleWorkspaceChanged(object? sender, WorkspaceChangedEventArgs e)
    {
        if (!CanUpdateUi())
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action<WorkspaceChangedEventArgs>(RefreshWorkspaceState), e);
            }
            catch (InvalidOperationException) when (!CanUpdateUi())
            {
            }

            return;
        }

        RefreshWorkspaceState(e);
    }

    /// <summary>
    /// Refreshes navigation and connection projections after a workspace commit. / 工作区提交后刷新导航与连接投影。
    /// </summary>
    /// <param name="change">Committed workspace change that triggered the refresh. / 触发刷新的已提交工作区变更。</param>
    private void RefreshWorkspaceState(WorkspaceChangedEventArgs change)
    {
        if (!CanUpdateUi())
        {
            return;
        }

        if (change.Kind == WorkspaceChangeKind.GroupDeleted &&
            change.EntityId is Guid deletedGroupId &&
            string.Equals(_activeView, $"{GroupViewPrefix}{deletedGroupId}", StringComparison.Ordinal))
        {
            _activeView = AllView;
        }

        RebuildNavigation();
        RefreshConnectionTable();
    }

    /// <summary>
    /// Marshals a later-process activation request to the main UI thread. / 将后续进程的激活请求切换到主 UI 线程。
    /// </summary>
    /// <param name="sender">Single-instance coordinator. / 单实例协调器。</param>
    /// <param name="e">Activation event data. / 激活事件数据。</param>
    private void HandleActivationRequested(object? sender, EventArgs e)
    {
        if (CanUpdateUi())
        {
            try
            {
                BeginInvoke(ActivateMainWindow);
            }
            catch (InvalidOperationException) when (!CanUpdateUi())
            {
            }
        }
    }

    /// <summary>
    /// Restores, shows, and activates the main window. / 恢复、显示并激活主窗口。
    /// </summary>
    private void ActivateMainWindow()
    {
        if (!CanUpdateUi())
        {
            return;
        }

        FormWindowState restoreState = _windowStateBeforeMinimize;
        ShowInTaskbar = true;
        Show();
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = restoreState;
        }

        BringToFront();
        Activate();
        WindowActivation.Activate(Handle);
    }

    /// <summary>Hides the window while keeping services and the tray menu available. / 隐藏窗口，同时保留服务和托盘菜单。</summary>
    private void MinimizeToTray()
    {
        if (!CanUpdateUi()) return;
        _notifyIcon.Visible = true;
        Hide();
        ShowInTaskbar = false;
    }

    private void HandleMinimizeToTrayClick(object? sender, EventArgs e) => MinimizeToTray();

    private void HandleWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != FormWindowState.Minimized)
        {
            _windowStateBeforeMinimize = WindowState;
        }
    }

    /// <summary>
    /// Restores the main window when the tray icon is double-clicked. / 双击托盘图标时恢复主窗口。
    /// </summary>
    /// <param name="sender">Tray icon. / 托盘图标。</param>
    /// <param name="e">Mouse event data. / 鼠标事件数据。</param>
    private void HandleNotifyIconDoubleClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) ActivateMainWindow();
    }

    /// <summary>
    /// Dispatches system-tray open and exit commands. / 分派系统托盘打开与退出命令。
    /// </summary>
    /// <param name="sender">Tray menu item. / 托盘菜单项。</param>
    /// <param name="e">Click event data. / 单击事件数据。</param>
    private void HandleTrayMenuClick(object? sender, EventArgs e)
    {
        string command = (sender as ToolStripItem)?.Tag as string ?? string.Empty;
        if (command == "tray-open")
        {
            ActivateMainWindow();
        }
        else if (command == "tray-exit")
        {
            _forceExit = true;
            Close();
        }
    }

    /// <summary>
    /// Reapplies responsive breakpoints after the client area changes. / 客户区变化后重新应用响应式断点。
    /// </summary>
    /// <param name="sender">Main form. / 主窗体。</param>
    /// <param name="e">Size-change event data. / 尺寸变化事件数据。</param>
    private void HandleClientSizeChanged(object? sender, EventArgs e)
    {
        ApplyResponsiveLayout();
    }

    /// <summary>
    /// Persists normal window bounds after interactive resizing completes. / 交互式调整大小完成后持久化正常窗口边界。
    /// </summary>
    /// <param name="sender">Main form. / 主窗体。</param>
    /// <param name="e">Resize event data. / 调整大小事件数据。</param>
    private async void HandleResizeEnd(object? sender, EventArgs e)
    {
        if (WindowState != FormWindowState.Normal)
        {
            return;
        }

        _windowBoundsSaveCount++;
        try
        {
            await _workspace.UpdateWindowBoundsAsync(Bounds);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
        }
        finally
        {
            _windowBoundsSaveCount--;
            ScheduleDeferredExitIfReady();
        }
    }

    /// <summary>
    /// Minimizes user close requests to the tray when enabled, or performs a real exit. / 启用时将用户关闭请求最小化到托盘，否则执行真正退出。
    /// </summary>
    /// <param name="sender">Main form. / 主窗体。</param>
    /// <param name="e">Form-closing data. / 窗体关闭数据。</param>
    private void HandleFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_forceExit && e.CloseReason == CloseReason.UserClosing && _workspace.GetSettings().MinimizeToTray)
        {
            e.Cancel = true;
            MinimizeToTray();
            _notifyIcon.ShowBalloonTip(2000, ProductInfo.Name, L.Get("Main.Tray.StillRunning"), ToolTipIcon.Info);
            return;
        }

        if ((_primaryOperationInFlight || _windowBoundsSaveCount > 0) &&
            e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            if (!_exitRequestedAfterOperation)
            {
                AntdUI.Message.info(this, L.Get("Main.Message.FinishingSaveBeforeExit"));
            }

            _forceExit = false;
            _exitRequestedAfterOperation = true;
            return;
        }

        _shutdownRequested = true;
        _statusBatchState.BeginBatch([]);
        _statusCancellation?.Cancel();
        _notifyIcon.Visible = false;
    }

    /// <summary>
    /// Displays a non-sensitive bilingual error notification. / 显示不含敏感信息的双语错误通知。
    /// </summary>
    /// <param name="title">Error title. / 错误标题。</param>
    /// <param name="exception">Failure to summarize. / 要概述的错误。</param>
    private void ShowError(string title, Exception exception)
    {
        if (CanUpdateUi())
        {
            AntdUI.Notification.error(this, title, exception.Message);
        }
    }

    /// <summary>
    /// Releases status checks, tray resources, menus, and event subscriptions. / 释放状态检测、托盘资源、菜单与事件订阅。
    /// </summary>
    /// <param name="disposing">Whether managed resources should be released. / 是否应释放托管资源。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _shutdownRequested = true;
            _exitRequestedAfterOperation = false;
            if (_runtimeInitialized)
            {
                _workspace.Changed -= HandleWorkspaceChanged;
                _singleInstance.ActivationRequested -= HandleActivationRequested;
            }

            if (_systemEventsSubscribed)
            {
                SystemEvents.UserPreferenceChanged -= HandleUserPreferenceChanged;
                _systemEventsSubscribed = false;
            }

            _statusBatchState.BeginBatch([]);
            CancellationTokenSource? statusCancellation = _statusCancellation;
            _statusCancellation = null;
            statusCancellation?.Cancel();
            statusCancellation?.Dispose();
            if (_notifyIcon is not null)
            {
                _notifyIcon.Visible = false;
            }

            components?.Dispose();
        }

        base.Dispose(disposing);
    }
}
