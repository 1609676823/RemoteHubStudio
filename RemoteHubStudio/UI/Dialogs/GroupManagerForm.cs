using RemoteHubStudio.Application;
using RemoteHubStudio.Configuration;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Localization;
using RemoteHubStudio.UI.Controls;

namespace RemoteHubStudio.UI.Dialogs;

/// <summary>
/// Manages arbitrarily nested connection groups through AntdUI. / 通过 AntdUI 管理任意层级的连接分组。
/// </summary>
public sealed partial class GroupManagerForm : ResponsiveDialogWindow
{
    private readonly WorkspaceService _workspace = null!;
    private List<Guid> _rowOrder = [];
    private bool _operationInProgress;
    private bool _restoringSelection;

    /// <summary>
    /// Initializes the designer-safe visual tree without accessing a workspace. / 初始化设计器安全的可视树，不访问工作区。
    /// </summary>
    public GroupManagerForm()
        : base(L.Get("GroupManager.Title"), new Size(840, 620), new Size(600, 460))
    {
        InitializeComponent();
        L.Apply(this);
        ConfigureTableColumns();
        RegisterDesignerLayout();
    }

    /// <summary>
    /// Configures AntdUI columns outside InitializeComponent so the Visual Studio
    /// designer only has to parse standard control/property statements.
    /// / 在 InitializeComponent 之外配置 AntdUI 列，使 Visual Studio 设计器只需解析标准控件和属性语句。
    /// </summary>
    private void ConfigureTableColumns()
    {
        _table.Columns = new AntdUI.ColumnCollection
        {
            new AntdUI.Column("Name", L.Get("GroupManager.Table.Name")).SetWidth("32%").SetMinWidth(150),
            new AntdUI.Column("Parent", L.Get("GroupManager.Table.Parent")).SetWidth("32%").SetMinWidth(150),
            new AntdUI.Column("Color", L.Get("GroupManager.Table.Color")).SetWidth(100),
            new AntdUI.Column("SortOrder", L.Get("GroupManager.Table.Order")).SetWidthFill().SetMinWidth(90)
        };
    }

    /// <summary>
    /// Connects the serialized content panel to the inherited responsive sizing logic. / 将已序列化内容面板接入继承的响应式尺寸逻辑。
    /// </summary>
    private void RegisterDesignerLayout()
    {
        RegisterSection(_content, _section);
    }

    /// <summary>
    /// Initializes the connection group manager. / 初始化连接分组管理器。
    /// </summary>
    /// <param name="workspace">Initialized workspace service. / 已初始化的工作区服务。</param>
    public GroupManagerForm(WorkspaceService workspace)
        : this()
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        WireEvents();
        RefreshRows();
    }

    /// <summary>
    /// Wires named table and toolbar event handlers. / 绑定命名的表格与工具栏事件处理器。
    /// </summary>
    private void WireEvents()
    {
        _table.SelectIndexChanged += HandleSelectionChanged;
        _table.CellDoubleClick += HandleCellDoubleClick;
        _addButton.Click += HandleCommandClick;
        _editButton.Click += HandleCommandClick;
        _deleteButton.Click += HandleCommandClick;
        _closeButton.Click += HandleCommandClick;
        FormClosing += HandleFormClosing;
    }

    /// <summary>
    /// Reloads group rows with resolved parent names. / 使用已解析的父分组名称重新加载分组行。
    /// </summary>
    private void RefreshRows(Guid? preferredSelection = null)
    {
        Guid? selectedId = preferredSelection ?? GetSelectedGroupId();
        IReadOnlyList<ConnectionGroup> groups = _workspace.GetGroups();
        Dictionary<Guid, string> names = groups.ToDictionary(group => group.Id, group => group.Name);
        GroupRow[] rows = groups
            .OrderBy(group => group.SortOrder)
            .ThenBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new GroupRow
            {
                Id = group.Id,
                Name = group.Name,
                Parent = group.ParentId is Guid parentId
                    ? names.GetValueOrDefault(parentId, "—")
                    : L.Get("Common.TopLevel"),
                Color = group.Color,
                SortOrder = group.SortOrder.ToString()
            })
            .ToArray();
        _rowOrder = rows.Select(row => row.Id).ToList();
        _restoringSelection = true;
        try
        {
            _table.DataSource = rows;
            _table.SelectedIndexs = [];
            GroupRow? selectedRow = selectedId is Guid id
                ? rows.FirstOrDefault(row => row.Id == id)
                : null;
            if (selectedRow is not null)
            {
                _table.SetSelected(selectedRow, false);
            }
        }
        finally
        {
            _restoringSelection = false;
        }

        UpdateActionState();
    }

    /// <summary>
    /// Returns the group represented by AntdUI's current native row selection. / 返回 AntdUI 当前原生行选择所表示的分组。
    /// </summary>
    /// <returns>The selected group identifier, or null. / 选中的分组标识，或 null。</returns>
    private Guid? GetSelectedGroupId()
    {
        IReadOnlyList<Guid> selectedIds = ConnectionSelectionLogic.ResolveOneBasedSelection(
            _rowOrder,
            _table.SelectedIndexsReal());
        return selectedIds.Count > 0 ? selectedIds[0] : null;
    }

    /// <summary>
    /// Enables group row actions only when a row is selected. / 仅在选中分组行时启用操作。
    /// </summary>
    private void UpdateActionState()
    {
        bool hasSelection = GetSelectedGroupId().HasValue;
        _table.Enabled = !_operationInProgress;
        _addButton.Enabled = !_operationInProgress;
        _editButton.Enabled = !_operationInProgress && hasSelection;
        _deleteButton.Enabled = !_operationInProgress && hasSelection;
        _closeButton.Enabled = !_operationInProgress;
    }

    /// <summary>
    /// Refreshes actions when AntdUI changes its native group-row selection. / AntdUI 更改原生分组行选择时刷新操作。
    /// </summary>
    /// <param name="sender">Table event source. / 表格事件源。</param>
    /// <param name="e">Selection-change event data. / 选择变更事件数据。</param>
    private void HandleSelectionChanged(object? sender, EventArgs e)
    {
        if (_operationInProgress || _restoringSelection)
        {
            return;
        }

        UpdateActionState();
    }

    /// <summary>
    /// Opens the selected group when its row is double-clicked. / 双击分组行时打开选中分组。
    /// </summary>
    /// <param name="sender">Table event source. / 表格事件源。</param>
    /// <param name="e">Double-clicked cell data. / 双击单元格数据。</param>
    private async void HandleCellDoubleClick(object sender, AntdUI.TableClickEventArgs e)
    {
        if (!_operationInProgress && e.Record is GroupRow row)
        {
            _table.SetSelected(row, false);
            await ExecuteManagedOperationAsync(EditSelectedGroupAsync);
        }
    }

    /// <summary>
    /// Dispatches group manager toolbar commands. / 分派分组管理工具栏命令。
    /// </summary>
    /// <param name="sender">Command button. / 命令按钮。</param>
    /// <param name="e">Click event data. / 单击事件数据。</param>
    private async void HandleCommandClick(object? sender, EventArgs e)
    {
        string command = (sender as Control)?.Tag as string ?? string.Empty;
        if (command == "close")
        {
            if (!_operationInProgress)
            {
                CompleteDialog(DialogResult.OK);
            }

            return;
        }

        Func<Task>? operation = command switch
        {
            "add" => AddGroupAsync,
            "edit" => EditSelectedGroupAsync,
            "delete" => DeleteSelectedGroupAsync,
            _ => null
        };
        if (operation is not null)
        {
            await ExecuteManagedOperationAsync(operation);
        }
    }

    /// <summary>
    /// Opens the add dialog and commits the confirmed group. / 打开新增对话框并提交已确认的分组。
    /// </summary>
    /// <returns>A task that completes after the add workflow. / 新增流程完成后结束的任务。</returns>
    private async Task AddGroupAsync()
    {
        using GroupEditorForm editor = new(null, _workspace.GetGroups());
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Result is ConnectionGroup group)
        {
            ConnectionGroup committed = await _workspace.AddGroupAsync(group);
            if (!CanUpdateUi())
            {
                return;
            }

            RefreshRows(committed.Id);
            AntdUI.Message.success(this, L.Get("GroupManager.Message.Added"));
        }
    }

    /// <summary>
    /// Opens the editor for the selected group. / 为选中分组打开编辑器。
    /// </summary>
    /// <returns>A task that completes after the edit workflow. / 编辑流程完成后结束的任务。</returns>
    private async Task EditSelectedGroupAsync()
    {
        if (GetSelectedGroupId() is not Guid id || _workspace.GetGroup(id) is not ConnectionGroup group)
        {
            return;
        }

        using GroupEditorForm editor = new(group, _workspace.GetGroups());
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Result is ConnectionGroup result)
        {
            await CommitGroupEditAsync(result);
        }
    }

    /// <summary>
    /// Commits a group edit initiated by a synchronous modal dialog. / 提交由同步模态对话框发起的分组编辑。
    /// </summary>
    /// <param name="group">Edited group. / 已编辑分组。</param>
    /// <returns>A task that represents the durable edit commit. / 表示持久编辑提交的任务。</returns>
    private async Task CommitGroupEditAsync(ConnectionGroup group)
    {
        await _workspace.UpdateGroupAsync(group);
        if (CanUpdateUi())
        {
            RefreshRows(group.Id);
            AntdUI.Message.success(this, L.Get("GroupManager.Message.Updated"));
        }
    }

    /// <summary>
    /// Confirms and deletes the selected group while preserving its children and connections. / 确认并删除选中分组，同时保留其子分组与连接。
    /// </summary>
    /// <returns>A task that completes after the delete workflow. / 删除流程完成后结束的任务。</returns>
    private async Task DeleteSelectedGroupAsync()
    {
        if (GetSelectedGroupId() is not Guid id || _workspace.GetGroup(id) is not ConnectionGroup group)
        {
            return;
        }

        if (_workspace.GetSettings().ConfirmBeforeDelete && !ConfirmGroupDeletion(group.Name))
        {
            return;
        }

        await _workspace.DeleteGroupAsync(id);
        if (!CanUpdateUi())
        {
            return;
        }

        RefreshRows();
        AntdUI.Message.success(this, L.Get("GroupManager.Message.Deleted"));
    }

    /// <summary>
    /// Requests confirmation before removing a group and reparenting its contents. / 在删除分组并重新指定其内容的父级前请求确认。
    /// </summary>
    /// <param name="groupName">Name of the group that will be removed. / 将被删除的分组名称。</param>
    /// <returns>True when deletion is approved. / 批准删除时返回 true。</returns>
    private bool ConfirmGroupDeletion(string groupName)
    {
        string nameSummary = ConnectionSelectionLogic.BuildDeletionNameSummary(
            [groupName],
            maximumNames: 1,
            separator: L.Get("Common.ListSeparator"),
            unnamed: L.Get("Common.Unnamed"),
            formatRemaining: count => L.Format("Common.MoreItems", count));
        return MessageBox.Show(
            this,
            L.Format("GroupManager.Delete.Confirmation", nameSummary),
            ProductInfo.Name,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning) == DialogResult.Yes;
    }

    /// <summary>
    /// Runs one mutation while preventing reentry and suppressing UI access after disposal. / 在阻止重入的同时运行一次变更，并在窗体处置后禁止访问 UI。
    /// </summary>
    /// <param name="operation">Asynchronous manager operation. / 异步管理操作。</param>
    /// <returns>A task that completes after the operation and UI cleanup. / 操作及 UI 清理完成后结束的任务。</returns>
    private async Task ExecuteManagedOperationAsync(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_operationInProgress || !CanUpdateUi())
        {
            return;
        }

        SetOperationInProgress(true);
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            if (CanUpdateUi())
            {
                AntdUI.Message.error(this, L.Format("Error.OperationFailedWithDetails", exception.Message));
            }
        }
        finally
        {
            if (CanUpdateUi())
            {
                SetOperationInProgress(false);
            }
        }
    }

    /// <summary>
    /// Updates the mutation guard and all controls that can initiate or interrupt work. / 更新变更防护状态以及可启动或中断工作的全部控件。
    /// </summary>
    /// <param name="inProgress">Whether an asynchronous mutation is active. / 是否存在活动的异步变更。</param>
    private void SetOperationInProgress(bool inProgress)
    {
        _operationInProgress = inProgress;
        UpdateActionState();
    }

    /// <summary>
    /// Determines whether manager controls can still be updated safely. / 判断是否仍可安全更新管理器控件。
    /// </summary>
    /// <returns>True while the form is alive and not disposing. / 窗体存活且未正在处置时返回 true。</returns>
    private bool CanUpdateUi()
    {
        return !IsDisposed && !Disposing;
    }

    /// <summary>
    /// Prevents the manager from closing while a durable mutation is incomplete. / 在持久变更尚未完成时阻止关闭管理器。
    /// </summary>
    /// <param name="sender">Manager form. / 管理器窗体。</param>
    /// <param name="e">Form-closing event data. / 窗体关闭事件数据。</param>
    private void HandleFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_operationInProgress)
        {
            e.Cancel = true;
        }
    }

    /// <summary>
    /// Provides resolved group fields to the table. / 向表格提供已解析的分组字段。
    /// </summary>
    private sealed class GroupRow
    {
        /// <summary>Gets or initializes the group identifier. / 获取或初始化分组标识。</summary>
        public Guid Id { get; init; }

        /// <summary>Gets or initializes the group name. / 获取或初始化分组名称。</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets or initializes the parent name. / 获取或初始化父分组名称。</summary>
        public string Parent { get; init; } = string.Empty;

        /// <summary>Gets or initializes the accent color. / 获取或初始化强调色。</summary>
        public string Color { get; init; } = string.Empty;

        /// <summary>Gets or initializes the sort order text. / 获取或初始化排序文本。</summary>
        public string SortOrder { get; init; } = string.Empty;
    }
}
