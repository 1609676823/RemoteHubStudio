using System.ComponentModel;
using System.Text;
using System.Windows.Forms;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Localization;
using RemoteHubStudio.UI.Controls;
using RemoteHubStudio.UI.Dialogs.ConnectionEditors;

namespace RemoteHubStudio.UI.Dialogs;

/// <summary>
/// Hosts common connection fields and one independently implemented type-specific child page.
/// / 承载公共连接字段，并按连接类型显示一个独立实现的专属子页。
/// </summary>
public sealed partial class ConnectionEditorForm : ResponsiveDialogWindow
{
    private readonly ConnectionEditorMode _mode;
    private readonly ConnectionProfile _workingCopy;
    private readonly Dictionary<ConnectionType, ConnectionTypeOptionsPage> _typePages = [];
    private readonly Dictionary<ConnectionType, AntdUI.Panel> _typeSections = [];
    private readonly Dictionary<ConnectionType, string> _rawOptionDrafts = [];
    private ConnectionType _activeType;

    /// <summary>Creates an add-mode surface for the WinForms designer. / 为设计器创建新增模式界面。</summary>
    public ConnectionEditorForm()
        : base(ResolveTitle(ConnectionEditorMode.Add), new Size(980, 760), new Size(680, 500))
    {
        _mode = ConnectionEditorMode.Add;
        _workingCopy = RdpConnectionTypeOptionsPage.CreateDefaultProfile();
        _activeType = _workingCopy.Type;

        InitializeComponent();
        L.Apply(this);
        RegisterDesignerLayout();
        _typePages.Add(ConnectionType.RemoteDesktop, _rdpPage);
        _typeSections.Add(ConnectionType.RemoteDesktop, _rdpSection);
        _rawOptionDrafts.Add(ConnectionType.RemoteDesktop, string.Empty);
        _rdpPage.EditorRequirementsChanged += HandlePageRequirementsChanged;
    }

    /// <summary>Initializes an isolated add, edit, or quick-connect editor. / 初始化隔离的新增、编辑或快速连接编辑器。</summary>
    public ConnectionEditorForm(
        ConnectionProfile? profile,
        IEnumerable<ConnectionGroup>? groups,
        ConnectionEditorMode mode,
        Guid? defaultGroupId = null)
        : this()
    {
        if (mode == ConnectionEditorMode.Edit && profile is null)
        {
            throw new ArgumentNullException(nameof(profile), "Edit mode requires an existing profile. / 编辑模式需要现有配置。");
        }

        if (mode == ConnectionEditorMode.Add && profile is not null)
        {
            throw new ArgumentException("Add mode requires a new profile. / 新增模式需要新配置。", nameof(profile));
        }

        _mode = mode;
        _workingCopy = profile is null
            ? RdpConnectionTypeOptionsPage.CreateDefaultProfile()
            : CloneProfile(profile);
        if (mode == ConnectionEditorMode.Add)
        {
            _workingCopy.GroupId = defaultGroupId;
        }
        _activeType = _workingCopy.Type;

        string title = ResolveTitle(mode);
        Text = title;
        Header.Text = title;
        ConfigureDialogActions();
        PopulateStaticSelections();
        PopulateGroups(groups);
        LoadWorkingCopy();
        WireEvents();
        UpdateTypeDependentEditors();
    }

    /// <summary>Gets the validated detached profile after confirmation. / 确认后获取经过验证的分离配置。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ConnectionProfile? Result { get; private set; }

    /// <summary>Gets the editor usage mode. / 获取编辑器使用模式。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ConnectionEditorMode EditorMode => _mode;

    /// <summary>
    /// Attaches the designer-created controls to the inherited responsive hosts.
    /// Keeping custom layout calls outside InitializeComponent allows the Visual Studio
    /// source designer to parse and round-trip the generated surface reliably.
    /// / 将设计器创建的控件接入继承的响应式宿主；自定义布局调用置于 InitializeComponent 之外，
    /// 以便 Visual Studio 源设计器可靠解析和往返保存。
    /// </summary>
    private void RegisterDesignerLayout()
    {
        _basicsGrid.RegisterField(_nameLabel, _nameInput);
        _basicsGrid.RegisterField(_typeLabel, _typeSelect);
        _basicsGrid.RegisterField(_groupLabel, _groupSelect);
        _basicsGrid.RegisterField(_expiresLabel, _expiresPicker);
        _basicsGrid.RegisterField(_favoriteLabel, _favoriteSwitch);
        _basicsGrid.RegisterField(_notesLabel, _notesInput);

        _clientGrid.RegisterField(_privateKeyLabel, _privateKeyInput);
        _clientGrid.RegisterField(_executableLabel, _executableInput);
        _clientGrid.RegisterField(_argumentsLabel, _argumentsInput);
        _clientGrid.RegisterField(_clientOptionsLabel, _clientOptionsInput);

        RegisterSection(_basicsGrid, _basicsSection);
        _rdpTitle.Text = _rdpPage.SectionTitle;
        RegisterSection(_rdpPage, _rdpSection);
        RegisterSection(_clientGrid, _advancedSection);
    }

    private static string ResolveTitle(ConnectionEditorMode mode) => mode switch
    {
        ConnectionEditorMode.Edit => L.Get("ConnectionEditor.Title.Edit"),
        ConnectionEditorMode.QuickConnect => L.Get("ConnectionEditor.Title.QuickConnect"),
        _ => L.Get("ConnectionEditor.Title.Add")
    };

    /// <summary>
    /// Creates and initializes a type page the first time that type is selected.
    /// / 在某连接类型首次被选中时创建并初始化其子页。
    /// </summary>
    /// <param name="type">Type whose page is required. / 需要子页的连接类型。</param>
    /// <returns>The retained page instance for this editor session. / 此编辑会话中保留的子页实例。</returns>
    private ConnectionTypeOptionsPage EnsureTypePage(ConnectionType type)
    {
        if (_typePages.TryGetValue(type, out ConnectionTypeOptionsPage? existingPage))
        {
            return existingPage;
        }

        ConnectionTypeOptionsPage page = ConnectionTypeOptionsPageFactory.Create(type);
        if (page.Type != type)
        {
            page.Dispose();
            throw new InvalidOperationException(
                $"The options-page factory returned '{page.Type}' for '{type}'. / 选项子页工厂为“{type}”返回了“{page.Type}”。");
        }

        try
        {
            ConnectionProfile initialDraft = type == _workingCopy.Type
                ? _workingCopy
                : CreateCleanTypeDraft(type);
            page.LoadFrom(initialDraft);
            page.UpdateCustomArgumentTemplate(_argumentsInput.Text);

            string rawOptionDraft = CreateInitialRawOptionDraft(type, page);
            AntdUI.Panel section = AddSection(page.SectionTitle, page);
            section.Visible = type == ReadSelectedType();
            page.EditorRequirementsChanged += HandlePageRequirementsChanged;
            _typePages.Add(type, page);
            _typeSections.Add(type, section);
            _rawOptionDrafts.Add(type, rawOptionDraft);
            ContentHost.Controls.SetChildIndex(_advancedSection, ContentHost.Controls.Count - 1);
            return page;
        }
        catch
        {
            page.EditorRequirementsChanged -= HandlePageRequirementsChanged;
            page.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Builds an uncontaminated first-visit draft for a type other than the profile's original type.
    /// Only the reusable target and custom argument template cross the type boundary.
    /// / 为非原始类型构建无污染的首次访问草稿，仅跨类型沿用可复用的目标和自定义参数模板。
    /// </summary>
    private ConnectionProfile CreateCleanTypeDraft(ConnectionType type)
    {
        string protocol = type.GetDefaultProtocol();
        return new ConnectionProfile
        {
            Type = type,
            Protocol = protocol,
            Host = _workingCopy.Host,
            Port = type.GetDefaultPort(protocol),
            Username = string.Empty,
            Password = string.Empty,
            PrivateKeyPath = string.Empty,
            CustomArguments = _argumentsInput.Text,
            Rdp = new RdpOptions(),
            Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
    }

    /// <summary>
    /// Creates a type-local raw option draft while removing keys owned by either side of the first type transition.
    /// / 创建类型局部的原始选项草稿，并排除首次类型切换两端任一子页管理的键。
    /// </summary>
    private string CreateInitialRawOptionDraft(ConnectionType type, ConnectionTypeOptionsPage targetPage)
    {
        HashSet<string> excludedKeys = new(targetPage.ManagedOptionKeys, StringComparer.OrdinalIgnoreCase);
        if (type != _workingCopy.Type &&
            _typePages.TryGetValue(_workingCopy.Type, out ConnectionTypeOptionsPage? originalPage))
        {
            excludedKeys.UnionWith(originalPage.ManagedOptionKeys);
        }

        return FormatOptions(_workingCopy.Options, excludedKeys);
    }

    private static IEnumerable<ConnectionType> GetDisplayOrderedTypes()
    {
        foreach (ConnectionType type in Enum.GetValues<ConnectionType>())
        {
            if (type != ConnectionType.Custom)
            {
                yield return type;
            }
        }

        yield return ConnectionType.Custom;
    }

    private void PopulateStaticSelections()
    {
        foreach (ConnectionType type in GetDisplayOrderedTypes())
        {
            _typeSelect.Items.Add(new AntdUI.SelectItem(type.ToDisplayName(), type));
        }
    }

    private void PopulateGroups(IEnumerable<ConnectionGroup>? groups)
    {
        if (groups is null)
        {
            return;
        }

        foreach (ConnectionGroup group in groups)
        {
            _groupSelect.Items.Add(new AntdUI.SelectItem(group.Name, group.Id));
        }
    }

    private void LoadWorkingCopy()
    {
        _nameInput.Text = _workingCopy.Name;
        _typeSelect.SelectedValue = _workingCopy.Type;
        _groupSelect.SelectedValue = _workingCopy.GroupId;
        _expiresPicker.Value = _workingCopy.ExpiresOn;
        _favoriteSwitch.Checked = _workingCopy.IsFavorite;
        _notesInput.Text = _workingCopy.Notes;
        _privateKeyInput.Text = _workingCopy.PrivateKeyPath;
        _executableInput.Text = _workingCopy.ExecutableOverride;
        _argumentsInput.Text = _workingCopy.CustomArguments;

        ConnectionTypeOptionsPage page = EnsureTypePage(_workingCopy.Type);
        page.LoadFrom(_workingCopy);
        page.UpdateCustomArgumentTemplate(_argumentsInput.Text);
        _rawOptionDrafts[_workingCopy.Type] = CreateInitialRawOptionDraft(_workingCopy.Type, page);
        _clientOptionsInput.Text = _rawOptionDrafts[_workingCopy.Type];
    }

    private void WireEvents()
    {
        _typeSelect.SelectedValueChanged += HandleTypeChanged;
        _argumentsInput.TextChanged += HandleArgumentsChanged;
        _privateKeyInput.SuffixClick += HandlePrivateKeyBrowseClick;
        _executableInput.SuffixClick += HandleExecutableBrowseClick;
        _saveButton.Click += HandleSaveClick;
        _cancelButton.Click += HandleCancelClick;
    }

    private void ConfigureDialogActions()
    {
        if (_mode == ConnectionEditorMode.QuickConnect)
        {
            _saveButton.Text = L.Get("Common.Connect");
            _saveButton.IconSvg = "ThunderboltOutlined";
        }
    }

    private void UpdateTypeDependentEditors()
    {
        ConnectionType selectedType = ReadSelectedType();
        ConnectionTypeOptionsPage selectedPage = EnsureTypePage(selectedType);
        foreach (KeyValuePair<ConnectionType, AntdUI.Panel> entry in _typeSections)
        {
            entry.Value.Visible = entry.Key == selectedType;
        }

        _clientGrid.SetFieldVisible(_privateKeyInput, selectedPage.ShowsPrivateKey);
    }

    private ConnectionType ReadSelectedType()
    {
        return _typeSelect.SelectedValue is ConnectionType type ? type : ConnectionType.RemoteDesktop;
    }

    private static string FormatOptions(
        IReadOnlyDictionary<string, string>? options,
        IEnumerable<string>? excludedKeys)
    {
        if (options is null || options.Count == 0)
        {
            return string.Empty;
        }

        HashSet<string> excluded = excludedKeys is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(excludedKeys, StringComparer.OrdinalIgnoreCase);
        StringBuilder builder = new();
        foreach (KeyValuePair<string, string> option in options
                     .Where(item => !excluded.Contains(item.Key))
                     .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(option.Key).Append('=').Append(option.Value);
        }

        return builder.ToString();
    }

    private bool TryParseOptions(
        IReadOnlyCollection<string> managedOptionKeys,
        out Dictionary<string, string> options)
    {
        options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> managedKeys = new(managedOptionKeys, StringComparer.OrdinalIgnoreCase);
        string[] lines = _clientOptionsInput.Text.Split(
            ["\r\n", "\n"],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string line in lines)
        {
            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                _clientOptionsInput.Status = AntdUI.TType.Error;
                AntdUI.Message.error(this, L.Get("ConnectionEditor.Validation.OptionFormat"));
                return false;
            }

            string key = line[..separator].Trim();
            if (key.Length == 0)
            {
                _clientOptionsInput.Status = AntdUI.TType.Error;
                return false;
            }

            if (managedKeys.Contains(key))
            {
                _clientOptionsInput.Status = AntdUI.TType.Error;
                AntdUI.Message.error(this, L.Format("ConnectionEditor.Validation.ManagedOption", key));
                return false;
            }

            options[key] = line[(separator + 1)..].Trim();
        }

        return true;
    }

    private bool TryCreateResult(out ConnectionProfile? profile)
    {
        profile = null;
        ResetValidationState();

        ConnectionType type = ReadSelectedType();
        if (!_typePages.TryGetValue(type, out ConnectionTypeOptionsPage? page))
        {
            AntdUI.Message.error(this, L.Format("ConnectionEditor.Validation.MissingTypePage", type));
            return false;
        }

        string name = _nameInput.Text.Trim();
        if (_mode == ConnectionEditorMode.QuickConnect && name.Length == 0)
        {
            name = page.SuggestedName;
        }

        if (name.Length == 0)
        {
            _nameInput.Status = AntdUI.TType.Error;
            AntdUI.Message.error(this, L.Get("ConnectionEditor.Validation.NameRequired"));
            return false;
        }

        if (type == ConnectionType.Custom && string.IsNullOrWhiteSpace(_executableInput.Text))
        {
            _executableInput.Status = AntdUI.TType.Error;
            AntdUI.Message.error(this, L.Get("ConnectionEditor.Validation.CustomExecutableRequired"));
            return false;
        }

        if (!TryParseOptions(page.ManagedOptionKeys, out Dictionary<string, string> options))
        {
            return false;
        }

        ConnectionProfile candidate = BuildResultProfile(name, type, page, options);

        if (!page.TryApplyTo(candidate, out string? validationError))
        {
            AntdUI.Message.error(this, validationError ?? L.Get("ConnectionEditor.Validation.TypeOptionsInvalid"));
            return false;
        }

        profile = candidate;
        return true;
    }

    private ConnectionProfile BuildResultProfile(
        string name,
        ConnectionType type,
        ConnectionTypeOptionsPage page,
        Dictionary<string, string> options)
    {
        return new ConnectionProfile
        {
            Id = _workingCopy.Id,
            Name = name,
            GroupId = _groupSelect.SelectedValue is Guid groupId ? groupId : null,
            Type = type,
            Protocol = type.GetDefaultProtocol(),
            Host = string.Empty,
            Port = 0,
            Username = string.Empty,
            Password = string.Empty,
            PrivateKeyPath = page.ShowsPrivateKey ? _privateKeyInput.Text.Trim() : string.Empty,
            ExpiresOn = _expiresPicker.Value?.Date,
            Notes = _notesInput.Text,
            IsFavorite = _favoriteSwitch.Checked,
            ExecutableOverride = _executableInput.Text.Trim(),
            CustomArguments = _argumentsInput.Text,
            Rdp = CloneRdpOptions(_workingCopy.Rdp),
            Options = options,
            CreatedAtUtc = _workingCopy.CreatedAtUtc,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private void ResetValidationState()
    {
        _nameInput.Status = AntdUI.TType.None;
        _executableInput.Status = AntdUI.TType.None;
        _clientOptionsInput.Status = AntdUI.TType.None;
        foreach (ConnectionTypeOptionsPage page in _typePages.Values)
        {
            page.ResetValidationState();
        }
    }

    private static ConnectionProfile CloneProfile(ConnectionProfile source)
    {
        return new ConnectionProfile
        {
            Id = source.Id,
            Name = source.Name,
            GroupId = source.GroupId,
            Type = source.Type,
            Protocol = source.Protocol,
            Host = source.Host,
            Port = source.Port,
            Username = source.Username,
            Password = source.Password,
            PrivateKeyPath = source.PrivateKeyPath,
            ExpiresOn = source.ExpiresOn,
            Notes = source.Notes,
            IsFavorite = source.IsFavorite,
            ExecutableOverride = source.ExecutableOverride,
            CustomArguments = source.CustomArguments,
            Rdp = CloneRdpOptions(source.Rdp),
            Options = new Dictionary<string, string>(source.Options ?? [], StringComparer.OrdinalIgnoreCase),
            CreatedAtUtc = source.CreatedAtUtc,
            UpdatedAtUtc = source.UpdatedAtUtc
        };
    }

    private static RdpOptions CloneRdpOptions(RdpOptions? source)
    {
        source ??= new RdpOptions();
        return new RdpOptions
        {
            FullScreen = source.FullScreen,
            UseAllMonitors = source.UseAllMonitors,
            DesktopWidth = source.DesktopWidth,
            DesktopHeight = source.DesktopHeight,
            ColorDepth = source.ColorDepth,
            DisplayConnectionBar = source.DisplayConnectionBar,
            EnableCompression = source.EnableCompression,
            KeyboardHookMode = source.KeyboardHookMode,
            RedirectClipboard = source.RedirectClipboard,
            RedirectDrives = source.RedirectDrives,
            RedirectPrinters = source.RedirectPrinters,
            RedirectSmartCards = source.RedirectSmartCards,
            RedirectComPorts = source.RedirectComPorts,
            RedirectPosDevices = source.RedirectPosDevices,
            RedirectCameras = source.RedirectCameras,
            RedirectMicrophone = source.RedirectMicrophone,
            AudioMode = source.AudioMode,
            AdministrativeSession = source.AdministrativeSession,
            PromptForCredentials = source.PromptForCredentials,
            DisableWallpaper = source.DisableWallpaper,
            AutoReconnect = source.AutoReconnect
        };
    }

    private void HandleTypeChanged(object sender, AntdUI.ObjectNEventArgs e)
    {
        ConnectionType selectedType = ReadSelectedType();
        if (selectedType != _activeType)
        {
            _rawOptionDrafts[_activeType] = _clientOptionsInput.Text;
            EnsureTypePage(selectedType);
            _activeType = selectedType;
            _clientOptionsInput.Text = _rawOptionDrafts[selectedType];
        }

        UpdateTypeDependentEditors();
    }

    private void HandleArgumentsChanged(object? sender, EventArgs e)
    {
        foreach (ConnectionTypeOptionsPage page in _typePages.Values)
        {
            page.UpdateCustomArgumentTemplate(_argumentsInput.Text);
        }

        UpdateTypeDependentEditors();
    }

    private void HandlePageRequirementsChanged(object? sender, EventArgs e)
    {
        if (sender is ConnectionTypeOptionsPage page && page.Type == ReadSelectedType())
        {
            UpdateTypeDependentEditors();
        }
    }

    private void HandlePrivateKeyBrowseClick(object? sender, MouseEventArgs e)
    {
        using OpenFileDialog dialog = new()
        {
            Filter = L.Get("FileDialog.PrivateKeyFilter"),
            CheckFileExists = true,
            Multiselect = false
        };

        if (File.Exists(_privateKeyInput.Text))
        {
            dialog.FileName = _privateKeyInput.Text;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _privateKeyInput.Text = dialog.FileName;
        }
    }

    private void HandleExecutableBrowseClick(object? sender, MouseEventArgs e)
    {
        using OpenFileDialog dialog = new()
        {
            Filter = L.Get("FileDialog.ExecutableFilter"),
            CheckFileExists = true,
            Multiselect = false
        };

        if (File.Exists(_executableInput.Text))
        {
            dialog.FileName = _executableInput.Text;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _executableInput.Text = dialog.FileName;
        }
    }

    private void HandleSaveClick(object? sender, EventArgs e)
    {
        if (TryCreateResult(out ConnectionProfile? profile))
        {
            Result = profile;
            CompleteDialog(DialogResult.OK);
        }
    }

    private void HandleCancelClick(object? sender, EventArgs e)
    {
        Result = null;
        CompleteDialog(DialogResult.Cancel);
    }
}
