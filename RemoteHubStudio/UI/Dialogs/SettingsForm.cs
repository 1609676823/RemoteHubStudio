using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Infrastructure.Monitoring;
using RemoteHubStudio.Localization;
using RemoteHubStudio.UI.Controls;

namespace RemoteHubStudio.UI.Dialogs;

/// <summary>
/// Edits a detached application-settings copy and returns it only after confirmation. / 编辑分离的应用设置副本，并仅在确认后返回。
/// </summary>
public sealed partial class SettingsForm : ResponsiveDialogWindow
{
    private readonly AppSettings _workingCopy;
    private readonly string? _dataDirectory;
    private readonly Dictionary<string, AntdUI.Input> _toolPathInputs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes the designer-safe settings layout with default values only. / 仅使用默认值初始化设计器安全的设置布局。
    /// </summary>
    public SettingsForm()
        : base(L.Get("Settings.Title"), new Size(940, 720), new Size(680, 500))
    {
        _workingCopy = new AppSettings();
        InitializeComponent();
        L.Apply(this);
        RegisterDesignerLayout();
    }

    /// <summary>
    /// Connects serialized setting sections to the inherited responsive sizing logic. / 将已序列化设置分区接入继承的响应式尺寸逻辑。
    /// </summary>
    private void RegisterDesignerLayout()
    {
        _appearanceGrid.RegisterField(_languageLabel, _languageSelect);
        _appearanceGrid.RegisterField(_themeLabel, _themeSelect);
        _appearanceGrid.RegisterField(_sidebarLabel, _sidebarSwitch);

        _securityGrid.RegisterField(_encryptionLabel, _encryptionSwitch);
        _securityGrid.RegisterField(_unsafePasswordLabel, _unsafePasswordSwitch);
        _securityGrid.RegisterField(_includeSecretsLabel, _includeSecretsSwitch);

        _behaviorGrid.RegisterField(_trayLabel, _traySwitch);
        _behaviorGrid.RegisterField(_confirmDeleteLabel, _confirmDeleteSwitch);
        _behaviorGrid.RegisterField(_expiryDaysLabel, _expiryDaysInput);
        _behaviorGrid.RegisterField(_pingTimeoutLabel, _pingTimeoutInput);
        _behaviorGrid.RegisterField(_concurrencyLabel, _concurrencyInput);

        _toolPathGrid.RegisterField(_puttyPathLabel, _puttyPathInput);
        _toolPathGrid.RegisterField(_xshellPathLabel, _xshellPathInput);
        _toolPathGrid.RegisterField(_xftpPathLabel, _xftpPathInput);
        _toolPathGrid.RegisterField(_winscpPathLabel, _winscpPathInput);
        _toolPathGrid.RegisterField(_secureCrtPathLabel, _secureCrtPathInput);
        _toolPathGrid.RegisterField(_mobaXtermPathLabel, _mobaXtermPathInput);
        _toolPathGrid.RegisterField(_tightVncPathLabel, _tightVncPathInput);
        _toolPathGrid.RegisterField(_realVncPathLabel, _realVncPathInput);
        _toolPathGrid.RegisterField(_ultraVncPathLabel, _ultraVncPathInput);
        _toolPathGrid.RegisterField(_radminPathLabel, _radminPathInput);
        _toolPathGrid.RegisterField(_toDeskPathLabel, _toDeskPathInput);
        _toolPathGrid.RegisterField(_rustDeskPathLabel, _rustDeskPathInput);
        _toolPathGrid.RegisterField(_openDataFolderLabel, _openDataFolderButton);
        _toolPathGrid.RegisterField(_aboutLabel, _aboutButton);

        RegisterSection(_appearanceGrid, _appearanceSection);
        RegisterSection(_securityGrid, _securitySection);
        RegisterSection(_encryptionWarning, _encryptionNoticeSection);
        RegisterSection(_commandLineWarning, _commandLineNoticeSection);
        RegisterSection(_exportWarning, _exportNoticeSection);
        RegisterSection(_behaviorGrid, _behaviorSection);
        RegisterSection(_toolPathGrid, _clientsSection);
    }

    /// <summary>
    /// Initializes a settings dialog with a cloned settings object. / 使用克隆的设置对象初始化设置对话框。
    /// </summary>
    /// <param name="settings">Current settings to clone. / 要克隆的当前设置。</param>
    /// <param name="dataDirectory">Optional application data directory for the open-folder action. / 用于打开文件夹操作的可选应用数据目录。</param>
    public SettingsForm(AppSettings settings, string? dataDirectory = null)
        : this()
    {
        ArgumentNullException.ThrowIfNull(settings);

        _workingCopy = CloneSettings(settings);
        _dataDirectory = string.IsNullOrWhiteSpace(dataDirectory) ? null : Path.GetFullPath(dataDirectory);

        PopulateLanguageOptions();
        PopulateThemeOptions();
        RegisterToolPathInputs();
        LoadWorkingCopy();
        WireEvents();
        _openDataFolderButton.Enabled = _dataDirectory is not null;
        UpdateEncryptionWarning();
        UpdateCommandLineWarning();
    }

    /// <summary>
    /// Gets the validated cloned settings after an OK result. / 在结果为确定后获取已验证的克隆设置。
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AppSettings? Result { get; private set; }

    /// <summary>Gets the requested UI language to persist after the settings save succeeds. / 获取设置保存成功后要持久化的界面语言。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string LanguagePreference { get; private set; } = L.SystemLanguage;

    /// <summary>Populates language choices discovered from valid embedded and external packs. / 填充从有效内嵌及外部包发现的语言选项。</summary>
    private void PopulateLanguageOptions()
    {
        _languageSelect.Items.Add(new AntdUI.SelectItem(
            L.Get("Settings.Language.System"),
            L.SystemLanguage));
        foreach (LanguageInfo language in L.AvailableLanguages)
        {
            _languageSelect.Items.Add(new AntdUI.SelectItem(language.DisplayName, language.Code));
        }

        string selectedLanguage = L.RequestedLanguage;
        if (!selectedLanguage.Equals(L.SystemLanguage, StringComparison.OrdinalIgnoreCase) &&
            !L.AvailableLanguages.Any(language =>
                language.Code.Equals(selectedLanguage, StringComparison.OrdinalIgnoreCase)))
        {
            string fallbackDisplay = L.AvailableLanguages
                .FirstOrDefault(language =>
                    language.Code.Equals(L.CurrentLanguage, StringComparison.OrdinalIgnoreCase))
                ?.DisplayName ?? L.CurrentLanguage;
            _languageSelect.Items.Add(new AntdUI.SelectItem(
                $"{selectedLanguage} → {fallbackDisplay}",
                selectedLanguage));
        }

        _languageSelect.SelectedValue = selectedLanguage;
        if (_languageSelect.SelectedIndex < 0)
        {
            _languageSelect.SelectedValue = L.SystemLanguage;
        }
    }

    /// <summary>
    /// Adds runtime theme values without asking the WinForms source designer to serialize
    /// AntdUI-specific item objects. / 添加运行时主题值，避免 WinForms 源设计器序列化 AntdUI 专用项目对象。
    /// </summary>
    private void PopulateThemeOptions()
    {
        _themeSelect.Items.Add(new AntdUI.SelectItem(L.Get("Settings.Theme.System"), AppTheme.System));
        _themeSelect.Items.Add(new AntdUI.SelectItem(L.Get("Settings.Theme.Light"), AppTheme.Light));
        _themeSelect.Items.Add(new AntdUI.SelectItem(L.Get("Settings.Theme.Dark"), AppTheme.Dark));
    }

    /// <summary>
    /// Maps the serialized tool-path editors to their persisted setting keys. / 将已序列化的工具路径编辑器映射到持久化设置键。
    /// </summary>
    private void RegisterToolPathInputs()
    {
        _toolPathInputs.Add("putty", _puttyPathInput);
        _toolPathInputs.Add("xshell", _xshellPathInput);
        _toolPathInputs.Add("xftp", _xftpPathInput);
        _toolPathInputs.Add("winscp", _winscpPathInput);
        _toolPathInputs.Add("securecrt", _secureCrtPathInput);
        _toolPathInputs.Add("mobaxterm", _mobaXtermPathInput);
        _toolPathInputs.Add("vnc-tightvnc", _tightVncPathInput);
        _toolPathInputs.Add("vnc-realvnc", _realVncPathInput);
        _toolPathInputs.Add("vnc-ultravnc", _ultraVncPathInput);
        _toolPathInputs.Add("radmin", _radminPathInput);
        _toolPathInputs.Add("todesk", _toDeskPathInput);
        _toolPathInputs.Add("rustdesk", _rustDeskPathInput);
    }

    /// <summary>
    /// Loads cloned settings into all editors. / 将克隆设置加载到所有编辑器。
    /// </summary>
    private void LoadWorkingCopy()
    {
        _themeSelect.SelectedValue = _workingCopy.Theme;
        _encryptionSwitch.Checked = _workingCopy.EncryptionEnabled;
        _unsafePasswordSwitch.Checked = _workingCopy.AllowPasswordInCommandLine;
        _includeSecretsSwitch.Checked = _workingCopy.IncludeSecretsInExports;
        _traySwitch.Checked = _workingCopy.MinimizeToTray;
        _confirmDeleteSwitch.Checked = _workingCopy.ConfirmBeforeDelete;
        _sidebarSwitch.Checked = _workingCopy.SidebarCollapsed;
        _expiryDaysInput.Value = _workingCopy.ExpiryWarningDays;
        _pingTimeoutInput.Value = _workingCopy.PingTimeoutMilliseconds;
        _concurrencyInput.Value = _workingCopy.ConcurrentStatusChecks;

        foreach (KeyValuePair<string, AntdUI.Input> toolInput in _toolPathInputs)
        {
            if (_workingCopy.ToolPaths.TryGetValue(toolInput.Key, out string? path))
            {
                toolInput.Value.Text = path;
            }
        }

        if (_workingCopy.ToolPaths.TryGetValue("vnc", out string? legacyVncPath) &&
            GetSplitVncToolKey(legacyVncPath) is string splitVncKey &&
            _toolPathInputs.TryGetValue(splitVncKey, out AntdUI.Input? splitVncInput) &&
            string.IsNullOrWhiteSpace(splitVncInput.Text))
        {
            splitVncInput.Text = legacyVncPath;
        }
    }

    /// <summary>
    /// Maps an exact legacy VNC executable name to the matching split settings key. / 将精确的旧版 VNC 可执行文件名映射到匹配的拆分设置键。
    /// </summary>
    /// <param name="legacyPath">Legacy configured executable path or name. / 旧版配置的可执行文件路径或名称。</param>
    /// <returns>Matching split key, or null when the viewer name is unknown. / 匹配的拆分键；查看器名称未知时返回 null。</returns>
    private static string? GetSplitVncToolKey(string? legacyPath)
    {
        if (string.IsNullOrWhiteSpace(legacyPath) || legacyPath.Contains('\0'))
        {
            return null;
        }

        try
        {
            string expanded = Environment.ExpandEnvironmentVariables(legacyPath.Trim().Trim('"'));
            return Path.GetFileName(expanded).ToLowerInvariant() switch
            {
                "tvnviewer.exe" => "vnc-tightvnc",
                "vncviewer.exe" => "vnc-realvnc",
                "uvncviewer.exe" => "vnc-ultravnc",
                _ => null
            };
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    /// Connects all control events to named handlers. / 将所有控件事件连接到命名处理程序。
    /// </summary>
    private void WireEvents()
    {
        _encryptionSwitch.CheckedChanged += HandleEncryptionChanged;
        _unsafePasswordSwitch.CheckedChanged += HandleUnsafePasswordChanged;
        _puttyPathInput.SuffixClick += HandleToolBrowseClick;
        _xshellPathInput.SuffixClick += HandleToolBrowseClick;
        _xftpPathInput.SuffixClick += HandleToolBrowseClick;
        _winscpPathInput.SuffixClick += HandleToolBrowseClick;
        _secureCrtPathInput.SuffixClick += HandleToolBrowseClick;
        _mobaXtermPathInput.SuffixClick += HandleToolBrowseClick;
        _tightVncPathInput.SuffixClick += HandleToolBrowseClick;
        _realVncPathInput.SuffixClick += HandleToolBrowseClick;
        _ultraVncPathInput.SuffixClick += HandleToolBrowseClick;
        _radminPathInput.SuffixClick += HandleToolBrowseClick;
        _toDeskPathInput.SuffixClick += HandleToolBrowseClick;
        _rustDeskPathInput.SuffixClick += HandleToolBrowseClick;
        _openDataFolderButton.Click += HandleOpenDataFolderClick;
        _aboutButton.Click += HandleAboutClick;
        _saveButton.Click += HandleSaveClick;
        _cancelButton.Click += HandleCancelClick;
    }

    /// <summary>
    /// Updates the explicit encryption-state warning. / 更新明确的加密状态警告。
    /// </summary>
    private void UpdateEncryptionWarning()
    {
        _encryptionWarning.Text = _encryptionSwitch.Checked
            ? L.Get("Settings.Encryption.EnabledWarning")
            : L.Get("Settings.Encryption.DisabledWarning");
    }

    /// <summary>
    /// Updates the automatic password-passing exposure warning. / 更新自动传递密码的暴露风险警告。
    /// </summary>
    private void UpdateCommandLineWarning()
    {
        _commandLineWarning.Text = _unsafePasswordSwitch.Checked
            ? L.Get("Settings.PasswordPassing.EnabledWarning")
            : L.Get("Settings.PasswordPassing.DisabledWarning");
    }

    /// <summary>
    /// Builds a detached settings result from current editor values. / 从当前编辑器值构建分离的设置结果。
    /// </summary>
    /// <returns>The detached settings object. / 分离的设置对象。</returns>
    private AppSettings BuildResult()
    {
        Dictionary<string, string> toolPaths = new(_workingCopy.ToolPaths, StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, AntdUI.Input> toolInput in _toolPathInputs)
        {
            string path = toolInput.Value.Text.Trim();
            if (path.Length > 0)
            {
                toolPaths[toolInput.Key] = path;
            }
            else
            {
                toolPaths.Remove(toolInput.Key);
            }
        }

        return new AppSettings
        {
            Theme = _themeSelect.SelectedValue is AppTheme theme ? theme : AppTheme.System,
            EncryptionEnabled = _encryptionSwitch.Checked,
            AllowPasswordInCommandLine = _unsafePasswordSwitch.Checked,
            IncludeSecretsInExports = _includeSecretsSwitch.Checked,
            MinimizeToTray = _traySwitch.Checked,
            ConfirmBeforeDelete = _confirmDeleteSwitch.Checked,
            ExpiryWarningDays = Decimal.ToInt32(_expiryDaysInput.Value),
            PingTimeoutMilliseconds = Decimal.ToInt32(_pingTimeoutInput.Value),
            ConcurrentStatusChecks = Decimal.ToInt32(_concurrencyInput.Value),
            SidebarCollapsed = _sidebarSwitch.Checked,
            WindowBounds = _workingCopy.WindowBounds,
            ToolPaths = toolPaths
        };
    }

    /// <summary>
    /// Creates a detached settings clone so cancellation has no side effects. / 创建分离的设置克隆，使取消不产生副作用。
    /// </summary>
    /// <param name="source">Source settings. / 源设置。</param>
    /// <returns>The detached clone. / 分离克隆。</returns>
    private static AppSettings CloneSettings(AppSettings source)
    {
        return new AppSettings
        {
            Theme = source.Theme,
            EncryptionEnabled = source.EncryptionEnabled,
            AllowPasswordInCommandLine = source.AllowPasswordInCommandLine,
            IncludeSecretsInExports = source.IncludeSecretsInExports,
            MinimizeToTray = source.MinimizeToTray,
            ConfirmBeforeDelete = source.ConfirmBeforeDelete,
            ExpiryWarningDays = source.ExpiryWarningDays,
            PingTimeoutMilliseconds = source.PingTimeoutMilliseconds,
            ConcurrentStatusChecks = source.ConcurrentStatusChecks,
            SidebarCollapsed = source.SidebarCollapsed,
            WindowBounds = source.WindowBounds,
            ToolPaths = new Dictionary<string, string>(source.ToolPaths, StringComparer.OrdinalIgnoreCase)
        };
    }

    /// <summary>
    /// Refreshes encryption guidance when the switch changes. / 开关变化时刷新加密指引。
    /// </summary>
    /// <param name="sender">Event sender. / 事件发送者。</param>
    /// <param name="e">Boolean event data. / 布尔事件数据。</param>
    private void HandleEncryptionChanged(object sender, AntdUI.BoolEventArgs e)
    {
        UpdateEncryptionWarning();
    }

    /// <summary>
    /// Refreshes risk guidance and warns immediately when automatic password passing is enabled. / 启用密码自动传递时刷新风险指引并立即警告。
    /// </summary>
    /// <param name="sender">Event sender. / 事件发送者。</param>
    /// <param name="e">Boolean event data. / 布尔事件数据。</param>
    private void HandleUnsafePasswordChanged(object sender, AntdUI.BoolEventArgs e)
    {
        UpdateCommandLineWarning();
        if (e.Value)
        {
            AntdUI.Message.warn(this, L.Get("Settings.PasswordPassing.EnabledToast"));
        }
    }

    /// <summary>
    /// Opens an executable picker for the clicked tool-path input. / 为所点击的工具路径输入框打开可执行文件选择器。
    /// </summary>
    /// <param name="sender">Event sender. / 事件发送者。</param>
    /// <param name="e">Mouse event data. / 鼠标事件数据。</param>
    private void HandleToolBrowseClick(object? sender, MouseEventArgs e)
    {
        if (sender is not AntdUI.Input input)
        {
            return;
        }

        using OpenFileDialog dialog = new()
        {
            Filter = L.Get("FileDialog.ExecutableFilter"),
            CheckFileExists = true,
            Multiselect = false
        };

        if (File.Exists(input.Text))
        {
            dialog.FileName = input.Text;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            input.Text = dialog.FileName;
        }
    }

    /// <summary>
    /// Opens the configured application data directory in Windows Explorer. / 在 Windows 资源管理器中打开已配置的应用数据目录。
    /// </summary>
    /// <param name="sender">Event sender. / 事件发送者。</param>
    /// <param name="e">Event data. / 事件数据。</param>
    private void HandleOpenDataFolderClick(object? sender, EventArgs e)
    {
        if (_dataDirectory is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(_dataDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _dataDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            AntdUI.Message.error(this, L.Format("Settings.OpenDataFolderFailed", exception.Message));
        }
    }

    /// <summary>
    /// Opens product version, project links, and open-source license information. / 打开产品版本、项目链接与开源许可信息。
    /// </summary>
    /// <param name="sender">Event sender. / 事件发送者。</param>
    /// <param name="e">Event data. / 事件数据。</param>
    private void HandleAboutClick(object? sender, EventArgs e)
    {
        using AboutForm dialog = new(initializeProductInformation: true);
        dialog.ShowDialog(this);
    }

    /// <summary>
    /// Returns the edited settings clone. / 返回已编辑的设置克隆。
    /// </summary>
    /// <param name="sender">Event sender. / 事件发送者。</param>
    /// <param name="e">Event data. / 事件数据。</param>
    private void HandleSaveClick(object? sender, EventArgs e)
    {
        LanguagePreference = _languageSelect.SelectedValue as string ?? L.SystemLanguage;
        Result = BuildResult();
        CompleteDialog(DialogResult.OK);
    }

    /// <summary>
    /// Closes the settings dialog without returning changes. / 关闭设置对话框且不返回更改。
    /// </summary>
    /// <param name="sender">Event sender. / 事件发送者。</param>
    /// <param name="e">Event data. / 事件数据。</param>
    private void HandleCancelClick(object? sender, EventArgs e)
    {
        Result = null;
        CompleteDialog(DialogResult.Cancel);
    }
}
