using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Localization;
using RemoteHubStudio.UI.Controls;

namespace RemoteHubStudio.UI.Dialogs;

/// <summary>
/// Creates or edits a connection group without mutating the source instance. / 创建或编辑连接分组，且不修改源实例。
/// </summary>
public sealed partial class GroupEditorForm : ResponsiveDialogWindow
{
    private readonly ConnectionGroup _workingCopy;

    /// <summary>
    /// Initializes an empty group editor and supplies the CLR parameterless constructor required by the WinForms designer. /
    /// 初始化空分组编辑器，并提供 WinForms 设计器所需的 CLR 无参构造函数。
    /// </summary>
    public GroupEditorForm()
        : base(L.Get("GroupEditor.Title.Add"), new Size(620, 480), new Size(520, 400))
    {
        _workingCopy = new ConnectionGroup();
        InitializeComponent();
        L.Apply(this);
        RegisterDesignerLayout();
    }

    /// <summary>
    /// Initializes a group editor with an isolated copy and optional parent choices. / 使用隔离副本与可选父分组选项初始化分组编辑器。
    /// </summary>
    /// <param name="group">Existing group, or <see langword="null"/> to add one. / 现有分组；新增时为 <see langword="null"/>。</param>
    /// <param name="groups">Groups available as parents. / 可用作父级的分组。</param>
    public GroupEditorForm(ConnectionGroup? group, IEnumerable<ConnectionGroup>? groups = null)
        : this()
    {
        _workingCopy = CloneGroup(group ?? new ConnectionGroup());
        string title = group is null
            ? L.Get("GroupEditor.Title.Add")
            : L.Get("GroupEditor.Title.Edit");
        Text = title;
        Header.Text = title;
        PopulateParents(groups);
        LoadWorkingCopy();
        WireEvents();
        UpdateColorPreview();
    }

    /// <summary>
    /// Gets the validated detached group after an OK result. / 在结果为确定后获取已验证的分离分组。
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ConnectionGroup? Result { get; private set; }

    /// <summary>
    /// Registers designer-created fields with the responsive runtime grid. / 将设计器创建的字段注册到运行时响应式网格。
    /// </summary>
    private void RegisterDesignerLayout()
    {
        _groupFields.RegisterField(_nameLabel, _nameInput);
        _groupFields.RegisterField(_parentLabel, _parentSelect);
        _groupFields.RegisterField(_colorLabel, _colorInput);
        _groupFields.RegisterField(_colorPreviewLabel, _colorPreview);
        _groupFields.RegisterField(_sortOrderLabel, _sortOrderInput);
        RegisterSection(_groupFields, _groupSection);
    }

    /// <summary>
    /// Populates valid parent groups while excluding the edited group itself. / 填充有效父分组，同时排除当前编辑的分组。
    /// </summary>
    /// <param name="groups">Available groups. / 可用分组。</param>
    private void PopulateParents(IEnumerable<ConnectionGroup>? groups)
    {
        if (groups is null)
        {
            return;
        }

        Dictionary<Guid, ConnectionGroup> groupById = [];
        foreach (ConnectionGroup group in groups)
        {
            groupById[group.Id] = group;
        }

        foreach (ConnectionGroup group in groupById.Values)
        {
            if (group.Id != _workingCopy.Id && !IsDescendantOf(group, _workingCopy.Id, groupById))
            {
                _parentSelect.Items.Add(new AntdUI.SelectItem(group.Name, group.Id));
            }
        }
    }

    /// <summary>
    /// Determines whether a candidate group is below the edited group and would create a cycle. / 确定候选分组是否位于当前编辑分组之下并会形成循环。
    /// </summary>
    /// <param name="candidate">Candidate parent group. / 候选父分组。</param>
    /// <param name="ancestorId">Edited group identifier. / 当前编辑分组标识。</param>
    /// <param name="groups">Groups indexed by identifier. / 按标识索引的分组。</param>
    /// <returns><see langword="true"/> when the candidate is a descendant. / 候选项为后代时返回 <see langword="true"/>。</returns>
    private static bool IsDescendantOf(
        ConnectionGroup candidate,
        Guid ancestorId,
        IReadOnlyDictionary<Guid, ConnectionGroup> groups)
    {
        HashSet<Guid> visited = [];
        ConnectionGroup current = candidate;
        while (current.ParentId is Guid parentId)
        {
            if (parentId == ancestorId)
            {
                return true;
            }

            if (!visited.Add(parentId) || !groups.TryGetValue(parentId, out ConnectionGroup? parent))
            {
                return false;
            }

            current = parent;
        }

        return false;
    }

    /// <summary>
    /// Loads the isolated group values into editors. / 将隔离的分组值加载到编辑器。
    /// </summary>
    private void LoadWorkingCopy()
    {
        _nameInput.Text = _workingCopy.Name;
        _parentSelect.SelectedValue = _workingCopy.ParentId;
        _colorInput.Text = _workingCopy.Color;
        _sortOrderInput.Value = _workingCopy.SortOrder;
    }

    /// <summary>
    /// Connects all editor events to named handlers. / 将所有编辑器事件连接到命名处理程序。
    /// </summary>
    private void WireEvents()
    {
        _colorInput.TextChanged += HandleColorTextChanged;
        _saveButton.Click += HandleSaveClick;
        _cancelButton.Click += HandleCancelClick;
    }

    /// <summary>
    /// Updates the color preview when a valid HTML color is entered. / 输入有效 HTML 颜色时更新颜色预览。
    /// </summary>
    private void UpdateColorPreview()
    {
        if (TryParseHtmlColor(_colorInput.Text.Trim(), out Color color))
        {
            _colorPreview.Back = color;
        }
    }

    /// <summary>
    /// Validates a strict #RRGGBB or #AARRGGBB HTML color. / 验证严格的 #RRGGBB 或 #AARRGGBB HTML 颜色。
    /// </summary>
    /// <param name="value">Color text. / 颜色文本。</param>
    /// <param name="color">Parsed color when valid. / 有效时的已解析颜色。</param>
    /// <returns><see langword="true"/> when the color is valid. / 颜色有效时返回 <see langword="true"/>。</returns>
    private static bool TryParseHtmlColor(string value, out Color color)
    {
        color = Color.Empty;
        if ((value.Length != 7 && value.Length != 9) || value[0] != '#')
        {
            return false;
        }

        if (!uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        color = ColorTranslator.FromHtml(value);
        return true;
    }

    /// <summary>
    /// Creates an isolated group copy. / 创建隔离的分组副本。
    /// </summary>
    /// <param name="source">Source group. / 源分组。</param>
    /// <returns>The detached copy. / 分离副本。</returns>
    private static ConnectionGroup CloneGroup(ConnectionGroup source)
    {
        return new ConnectionGroup
        {
            Id = source.Id,
            Name = source.Name,
            ParentId = source.ParentId,
            Color = source.Color,
            SortOrder = source.SortOrder
        };
    }

    /// <summary>
    /// Refreshes the preview after color text changes. / 颜色文本变化后刷新预览。
    /// </summary>
    /// <param name="sender">Event sender. / 事件发送者。</param>
    /// <param name="e">Event data. / 事件数据。</param>
    private void HandleColorTextChanged(object? sender, EventArgs e)
    {
        UpdateColorPreview();
    }

    /// <summary>
    /// Validates and returns a detached group. / 验证并返回分离的分组。
    /// </summary>
    /// <param name="sender">Event sender. / 事件发送者。</param>
    /// <param name="e">Event data. / 事件数据。</param>
    private void HandleSaveClick(object? sender, EventArgs e)
    {
        _nameInput.Status = AntdUI.TType.None;
        _colorInput.Status = AntdUI.TType.None;
        string name = _nameInput.Text.Trim();
        string colorText = _colorInput.Text.Trim();

        if (name.Length == 0)
        {
            _nameInput.Status = AntdUI.TType.Error;
            AntdUI.Message.error(this, L.Get("GroupEditor.Validation.NameRequired"));
            return;
        }

        if (!TryParseHtmlColor(colorText, out _))
        {
            _colorInput.Status = AntdUI.TType.Error;
            AntdUI.Message.error(this, L.Get("GroupEditor.Validation.ColorFormat"));
            return;
        }

        Result = new ConnectionGroup
        {
            Id = _workingCopy.Id,
            Name = name,
            ParentId = _parentSelect.SelectedValue is Guid parentId ? parentId : null,
            Color = colorText.ToUpperInvariant(),
            SortOrder = Decimal.ToInt32(_sortOrderInput.Value)
        };
        CompleteDialog(DialogResult.OK);
    }

    /// <summary>
    /// Closes the group editor without returning changes. / 关闭分组编辑器且不返回更改。
    /// </summary>
    /// <param name="sender">Event sender. / 事件发送者。</param>
    /// <param name="e">Event data. / 事件数据。</param>
    private void HandleCancelClick(object? sender, EventArgs e)
    {
        Result = null;
        CompleteDialog(DialogResult.Cancel);
    }
}
