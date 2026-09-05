using System.Text;
using System.Text.Json;
using System.Drawing;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Windows.Forms;
using RemoteHubStudio.Application;
using RemoteHubStudio.Configuration;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Infrastructure.ImportExport;
using RemoteHubStudio.Infrastructure.Launch;
using RemoteHubStudio.Infrastructure.Monitoring;
using RemoteHubStudio.Infrastructure.Persistence;
using RemoteHubStudio.Infrastructure.Security;
using RemoteHubStudio.Localization;
using RemoteHubStudio.UI.Controls;
using RemoteHubStudio.UI.Dialogs;
using RemoteHubStudio.UI.Dialogs.ConnectionEditors;
using RemoteHubStudio.UI.Main;

namespace RemoteHubStudio.Tests;

/// <summary>
/// Runs dependency-free regression checks for core RemoteHubStudio behavior. / 为 RemoteHubStudio 核心行为运行无外部依赖的回归检查。
/// </summary>
internal static class Program
{
    /// <summary>
    /// Runs all regression checks and returns a process exit code. / 运行全部回归检查并返回进程退出代码。
    /// </summary>
    /// <returns>Zero on success; otherwise one. / 成功返回零，否则返回一。</returns>
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.FirstOrDefault() == "--single-instance-probe") return SingleInstanceRegression.RunProbe(args);
            SingleInstanceRegression.Run();
            TrayWindowRegression.Run();
            TestSettingsDefaults();
            LocalizationRegression.Run();
            ProductInfoRegression.Run();
            TestDesignerLocalization();
            ThemeRegression.Run();
            TestDesignerForms();
            TestDesignerConnectionEditors();
            DesignerSourceRegression.Run();
            TestConnectionTableSelectionBridge();
            AppDataPathsRegression.Run();
            TestCsvCodec();
            await TestWorkspaceJsonTransferAsync();
            WorkspaceExportProjectorRegression.Run();
            await TestLegacyCsvTransferAsync();
            await TestCsvFormulaProtectionAsync();
            await TestCsvExtendedOptionsRoundTripAsync();
            await ImportLimitRegression.RunAsync();
            GroupGraphLimitRegression.Run();
            WorkspaceLimitsRegression.Run();
            TestExpirationRules();
            TestLaunchPlans();
            await TestProcessCleanupAsync();
            await TestStatusRulesAsync();
            ConnectionStatusBatchRegression.Run();
            await TestPersistenceAndEncryptionAsync();
            await ProtectedArtifactRegression.RunAsync();
            await RepositorySizeLimitRegression.RunAsync();
            await ContentBoundaryRegression.RunAsync();
            await TestWorkspaceGraphRecoveryAsync();
            await NameBasedImportRegression.RunAsync();
            await TestMaximumConnectionMergeAsync();
            await TestAtomicConnectionDeletionAsync();
            await TestConcurrentWindowBoundsPatchAsync();
            TestConnectionSelectionLogic();
            TestMainResponsiveLayoutLogic();
            TestResponsiveFieldGridSwitchSizing();
            TestResponsiveFieldGridEditorSizing();
            TestConnectionTypeOptionPages();
            Console.WriteLine("REMOTEHUBSTUDIO_TESTS_OK");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    /// <summary>
    /// Verifies the intended defaults for storage, password passing, and exports. / 验证存储、密码传递及导出的预期默认值。
    /// </summary>
    private static void TestSettingsDefaults()
    {
        AppSettings settings = new();
        Assert(!settings.EncryptionEnabled, "Storage encryption must be opt-in. / 本地存储加密必须由用户主动启用。");
        Assert(settings.AllowPasswordInCommandLine, "Automatic password passing must be enabled by default. / 密码自动传递必须默认启用。");
        Assert(!settings.IncludeSecretsInExports, "Secret export must be opt-in. / 秘密导出必须由用户主动启用。");
    }

    /// <summary>
    /// Verifies designer-safe construction resolves embedded strings from the active UI culture.
    /// / 验证设计器安全构造会按当前 UI 区域从内嵌语言包解析文本。
    /// </summary>
    private static void TestDesignerLocalization()
    {
        Exception? threadFailure = null;
        Thread designerThread = new(() =>
        {
            try
            {
                Assert(
                    Thread.CurrentThread.GetApartmentState() == ApartmentState.STA,
                    "Designer localization tests must run in a single-threaded apartment. / 设计器本地化测试必须在单线程单元中运行。");

                L.SetLanguage("en");
                using SettingsForm englishForm =
                    LicenseManager.CreateWithContext(typeof(SettingsForm), new DesignerSmokeLicenseContext()) as SettingsForm
                    ?? throw new InvalidOperationException("The English designer settings form was not created. / 未创建设计器英文设置窗体。");
                AntdUI.Label englishLanguageLabel = typeof(SettingsForm)
                    .GetField("_languageLabel", BindingFlags.Instance | BindingFlags.NonPublic)?
                    .GetValue(englishForm) as AntdUI.Label
                    ?? throw new InvalidOperationException("The designer language label was not initialized. / 设计器语言标签未初始化。");
                Assert(
                    englishForm.Text == "Settings" && englishLanguageLabel.Text == "Language",
                    "The WinForms designer did not apply the embedded English pack. / WinForms 设计器未应用内嵌英文包。");
                string originalEnglishText = englishLanguageLabel.Text ?? string.Empty;
                L.Apply(englishForm, "NoKeysInAnyPack");
                Assert(
                    englishLanguageLabel.Text == originalEnglishText,
                    "L.Apply replaced a designer property for a missing key. / L.Apply 在键缺失时覆盖了设计器属性。");

                L.SetLanguage("zh-CN");
                using SettingsForm chineseForm =
                    LicenseManager.CreateWithContext(typeof(SettingsForm), new DesignerSmokeLicenseContext()) as SettingsForm
                    ?? throw new InvalidOperationException("The Chinese designer settings form was not created. / 未创建设计器中文设置窗体。");
                AntdUI.Label chineseLanguageLabel = typeof(SettingsForm)
                    .GetField("_languageLabel", BindingFlags.Instance | BindingFlags.NonPublic)?
                    .GetValue(chineseForm) as AntdUI.Label
                    ?? throw new InvalidOperationException("The designer language label was not initialized. / 设计器语言标签未初始化。");
                Assert(
                    chineseForm.Text == "设置" && chineseLanguageLabel.Text == "语言",
                    "The WinForms designer did not apply the embedded Simplified Chinese pack. / WinForms 设计器未应用内嵌简体中文包。");

                using SettingsForm settingsForm = new(new AppSettings());
                AntdUI.Select languageSelect = typeof(SettingsForm)
                    .GetField("_languageSelect", BindingFlags.Instance | BindingFlags.NonPublic)?
                    .GetValue(settingsForm) as AntdUI.Select
                    ?? throw new InvalidOperationException("The language selector was not initialized. / 语言选择器未初始化。");
                Assert(
                    languageSelect.SelectedValue as string == "zh-CN",
                    "Opening settings rewrote a regional language request to its fallback pack. / 打开设置时区域语言请求被改写成了回退语言包。");
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                L.SetLanguage(L.SystemLanguage);
            }
        })
        {
            IsBackground = true,
            Name = "RemoteHubStudio designer localization test"
        };
        designerThread.SetApartmentState(ApartmentState.STA);
        designerThread.Start();

        if (!designerThread.Join(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("WinForms designer localization test timed out. / WinForms 设计器本地化测试超时。");
        }

        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    /// <summary>
    /// Verifies every concrete application form can be created by the WinForms designer on an STA thread without displaying a window.
    /// / 验证每个具体应用窗体都能由 WinForms 设计器在 STA 线程上创建，且不显示窗口。
    /// </summary>
    private static void TestDesignerForms()
    {
        Type[] formTypes = typeof(MainForm).Assembly.GetTypes()
            .Where(type =>
                !type.IsAbstract &&
                !type.ContainsGenericParameters &&
                typeof(Form).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        Assert(formTypes.Length > 0, "No application forms were discovered for designer testing. / 未发现可用于设计器测试的应用窗体。");

        Exception? threadFailure = null;
        string currentFormType = "<not started>";
        Thread designerThread = new(() =>
        {
            try
            {
                Assert(
                    Thread.CurrentThread.GetApartmentState() == ApartmentState.STA,
                    "Designer form tests must run in a single-threaded apartment. / 设计器窗体测试必须在单线程单元中运行。");

                foreach (Type formType in formTypes)
                {
                    currentFormType = formType.FullName ?? formType.Name;
                    ConstructorInfo? constructor = formType.GetConstructor(Type.EmptyTypes);
                    Assert(
                        constructor is not null,
                        $"Form '{currentFormType}' has no public parameterless constructor for the WinForms designer. / 窗体“{currentFormType}”没有供 WinForms 设计器使用的公共无参构造函数。");

                    using Form form = LicenseManager.CreateWithContext(formType, new DesignerSmokeLicenseContext()) as Form
                        ?? throw new InvalidOperationException(
                            $"Designer construction returned an unexpected instance for '{currentFormType}'. / 设计器构造“{currentFormType}”时返回了意外实例。");

                    Assert(
                        !form.Visible,
                        $"Designer construction unexpectedly displayed '{currentFormType}'. / 设计器构造意外显示了“{currentFormType}”。");
                    Assert(
                        form.Controls.Count > 0,
                        $"Form '{currentFormType}' created no designer controls. / 窗体“{currentFormType}”未创建任何设计器控件。");

                    FieldInfo[] designerFields = formType.GetFields(
                            BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic |
                            BindingFlags.DeclaredOnly)
                        .Where(field =>
                            typeof(Control).IsAssignableFrom(field.FieldType) ||
                            typeof(IComponent).IsAssignableFrom(field.FieldType))
                        .ToArray();
                    foreach (FieldInfo field in designerFields)
                    {
                        Assert(
                            field.GetValue(form) is not null,
                            $"Designer field '{currentFormType}.{field.Name}' was not initialized. / 设计器字段“{currentFormType}.{field.Name}”未初始化。");
                    }

                    Queue<Control> pending = new();
                    HashSet<Control> visited = new(ReferenceEqualityComparer.Instance);
                    pending.Enqueue(form);
                    while (pending.Count > 0)
                    {
                        Control control = pending.Dequeue();
                        if (!visited.Add(control))
                        {
                            continue;
                        }

                        control.CreateControl();
                        _ = control.Handle;
                        control.PerformLayout();
                        if (control is AntdUI.Select select)
                        {
                            Assert(
                                !select.WheelModifyEnabled,
                                $"Selection control '{currentFormType}.{select.Name}' allows mouse-wheel value changes. / " +
                                $"下拉选择器“{currentFormType}.{select.Name}”仍允许鼠标滚轮切换值。");
                        }

                        if (form is SettingsForm && control is AntdUI.InputNumber inputNumber)
                        {
                            Assert(
                                !inputNumber.WheelModifyEnabled,
                                $"Settings number input '{currentFormType}.{inputNumber.Name}' allows mouse-wheel value changes. / " +
                                $"设置页数值输入框“{currentFormType}.{inputNumber.Name}”仍允许鼠标滚轮改值。");
                        }

                        foreach (Control child in control.Controls)
                        {
                            pending.Enqueue(child);
                        }
                    }

                    form.PerformLayout();
                    if (form is MainForm)
                    {
                        VerifyMainFilterToolbarLayout(form, formType);
                    }

                    Assert(
                        visited.Count > 1,
                        $"Form '{currentFormType}' exposed an empty control tree. / 窗体“{currentFormType}”的控件树为空。");
                    Assert(
                        !form.Visible,
                        $"Handle creation unexpectedly displayed '{currentFormType}'. / 创建句柄时意外显示了“{currentFormType}”。");
                }
            }
            catch (Exception exception)
            {
                threadFailure = new InvalidOperationException(
                    $"WinForms designer smoke test failed while processing '{currentFormType}'. / WinForms 设计器烟雾测试在处理“{currentFormType}”时失败。",
                    exception);
            }
        })
        {
            IsBackground = true,
            Name = "RemoteHubStudio designer smoke test"
        };
        designerThread.SetApartmentState(ApartmentState.STA);
        designerThread.Start();

        if (!designerThread.Join(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException(
                $"WinForms designer smoke test timed out while processing '{currentFormType}'. / WinForms 设计器烟雾测试在处理“{currentFormType}”时超时。");
        }

        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    /// <summary>
    /// Verifies the main quick filters occupy a dedicated second row in their intended order. / 验证主界面快捷筛选器按预期顺序位于独立第二行。
    /// </summary>
    /// <param name="form">Designer-created main form. / 设计器创建的主窗体。</param>
    /// <param name="formType">Concrete form type. / 具体窗体类型。</param>
    private static void VerifyMainFilterToolbarLayout(Form form, Type formType)
    {
        FlowLayoutPanel primaryToolbar = formType
            .GetField("_toolbar", BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(form) as FlowLayoutPanel
            ?? throw new InvalidOperationException("Main toolbar was not initialized. / 主工具栏未初始化。");
        FlowLayoutPanel secondaryToolbar = formType
            .GetField("_secondaryToolbar", BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(form) as FlowLayoutPanel
            ?? throw new InvalidOperationException("Secondary toolbar was not initialized. / 第二行工具栏未初始化。");
        AntdUI.Button favoriteFilter = formType
            .GetField("_favoriteFilterButton", BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(form) as AntdUI.Button
            ?? throw new InvalidOperationException("Favorite filter was not initialized. / 收藏筛选按钮未初始化。");
        AntdUI.Button expiringFilter = formType
            .GetField("_expiringFilterButton", BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(form) as AntdUI.Button
            ?? throw new InvalidOperationException("Expiring filter was not initialized. / 即将到期筛选按钮未初始化。");
        AntdUI.Table connectionTable = formType
            .GetField("_connectionTable", BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(form) as AntdUI.Table
            ?? throw new InvalidOperationException("Connection table was not initialized. / 连接表格未初始化。");

        Assert(
            primaryToolbar.Parent == secondaryToolbar.Parent && primaryToolbar.Bottom <= secondaryToolbar.Top,
            "The quick-filter row is not below the primary toolbar. / 快捷筛选行未位于主工具栏下方。");
        Assert(
            secondaryToolbar.Parent == connectionTable.Parent && secondaryToolbar.Bottom <= connectionTable.Top,
            "The quick-filter row overlaps the connection table. / 快捷筛选行与连接表格重叠。");
        Assert(
            favoriteFilter.Parent == secondaryToolbar && expiringFilter.Parent == secondaryToolbar,
            "Favorite and expiring filters are not grouped in the secondary toolbar. / 收藏和即将到期筛选按钮未归入第二行工具栏。");
        Assert(
            favoriteFilter.Top == expiringFilter.Top &&
            favoriteFilter.Left < expiringFilter.Left &&
            expiringFilter.Left - favoriteFilter.Right <= 16,
            "Favorite and expiring filters are not adjacent on the same row. / 收藏和即将到期筛选按钮未在同一行相邻排列。");

        VerifyMainQuickFilterBehavior(form, formType, favoriteFilter, expiringFilter);
    }

    /// <summary>
    /// Verifies favorite, expiration, and group filters compose as an intersection. / 验证收藏、到期与分组筛选按交集组合。
    /// </summary>
    /// <param name="form">Designer-created main form. / 设计器创建的主窗体。</param>
    /// <param name="formType">Concrete form type. / 具体窗体类型。</param>
    /// <param name="favoriteFilter">Favorite toggle. / 收藏筛选按钮。</param>
    /// <param name="expiringFilter">Expiration toggle. / 到期筛选按钮。</param>
    private static void VerifyMainQuickFilterBehavior(
        Form form,
        Type formType,
        AntdUI.Button favoriteFilter,
        AntdUI.Button expiringFilter)
    {
        formType.GetField("_expirationService", BindingFlags.Instance | BindingFlags.NonPublic)?
            .SetValue(form, new ExpirationService());
        MethodInfo matchesActiveView = formType
            .GetMethod("MatchesActiveView", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Main quick-filter predicate was not found. / 未找到主界面快捷筛选谓词。");
        AppSettings settings = new() { ExpiryWarningDays = 30 };
        Guid visibleGroupId = Guid.NewGuid();
        HashSet<Guid> visibleGroup = [visibleGroupId];
        ConnectionProfile matching = new()
        {
            IsFavorite = true,
            ExpiresOn = DateTime.Today.AddDays(3),
            GroupId = visibleGroupId
        };

        favoriteFilter.Toggle = true;
        expiringFilter.Toggle = true;
        bool Matches(ConnectionProfile profile, HashSet<Guid>? groupFilter = null)
        {
            return matchesActiveView.Invoke(form, [profile, groupFilter, settings]) is true;
        }

        Assert(Matches(matching, visibleGroup), "Combined favorite and expiration filters rejected a matching connection. / 收藏与到期组合筛选拒绝了匹配连接。");
        Assert(!Matches(new ConnectionProfile { ExpiresOn = DateTime.Today.AddDays(3), GroupId = visibleGroupId }, visibleGroup), "Combined filters retained a non-favorite connection. / 组合筛选保留了未收藏连接。");
        Assert(!Matches(new ConnectionProfile { IsFavorite = true, ExpiresOn = DateTime.Today.AddDays(31), GroupId = visibleGroupId }, visibleGroup), "Combined filters retained a healthy connection. / 组合筛选保留了未临期连接。");
        Assert(!Matches(new ConnectionProfile { IsFavorite = true, ExpiresOn = DateTime.Today.AddDays(3), GroupId = Guid.NewGuid() }, visibleGroup), "Combined filters ignored the selected group. / 组合筛选忽略了所选分组。");
    }

    /// <summary>
    /// Verifies every concrete connection-editor UserControl has a designer-safe public constructor and dynamic control tree.
    /// / 验证每个具体连接编辑 UserControl 都具有设计器安全的公共构造函数和动态控件树。
    /// </summary>
    private static void TestDesignerConnectionEditors()
    {
        Type rootType = typeof(ConnectionTypeOptionsPage);
        Type[] controlTypes = rootType.Assembly.GetTypes()
            .Where(type =>
                !type.IsAbstract &&
                !type.ContainsGenericParameters &&
                type.Namespace == rootType.Namespace &&
                typeof(UserControl).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        Assert(
            controlTypes.Length >= Enum.GetValues<ConnectionType>().Length + 2,
            "Not every connection-editor UserControl was discovered for designer testing. / 未发现全部可用于设计器测试的连接编辑 UserControl。");

        Exception? threadFailure = null;
        string currentControlType = "<not started>";
        Thread designerThread = new(() =>
        {
            try
            {
                Assert(
                    Thread.CurrentThread.GetApartmentState() == ApartmentState.STA,
                    "Designer UserControl tests must run in a single-threaded apartment. / 设计器 UserControl 测试必须在单线程单元中运行。");

                foreach (Type controlType in controlTypes)
                {
                    currentControlType = controlType.FullName ?? controlType.Name;
                    using Stream? resource = controlType.Assembly.GetManifestResourceStream($"{currentControlType}.resources");
                    Assert(
                        resource is not null,
                        $"Designer resource is missing for '{currentControlType}'. / “{currentControlType}”缺少设计器资源。");
                    Assert(
                        controlType.GetMethod("InitializeComponent", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) is not null,
                        $"Designer initialization is missing for '{currentControlType}'. / “{currentControlType}”缺少设计器初始化方法。");
                    DesignerCategoryAttribute? designerCategory = TypeDescriptor.GetAttributes(controlType)
                        [typeof(DesignerCategoryAttribute)] as DesignerCategoryAttribute;
                    Assert(
                        designerCategory?.Category == "UserControl",
                        $"UserControl '{currentControlType}' does not expose the WinForms UserControl designer category. / " +
                        $"UserControl“{currentControlType}”未公开 WinForms UserControl 设计器类别。");
                    Assert(
                        controlType.GetConstructor(Type.EmptyTypes) is not null,
                        $"UserControl '{currentControlType}' has no public parameterless constructor for the WinForms designer. / " +
                        $"UserControl“{currentControlType}”没有供 WinForms 设计器使用的公共无参构造函数。");

                    using UserControl control = LicenseManager.CreateWithContext(
                            controlType,
                            new DesignerSmokeLicenseContext()) as UserControl
                        ?? throw new InvalidOperationException(
                            $"Designer construction returned an unexpected instance for '{currentControlType}'. / " +
                            $"设计器构造“{currentControlType}”时返回了意外实例。");

                    Assert(
                        control.Name == controlType.Name,
                        $"Designer initialization did not set the name of '{currentControlType}'. / “{currentControlType}”未执行设计器名称初始化。");
                    if (control is ConnectionTypeOptionsPage)
                    {
                        Assert(
                            !control.AutoSize && control.Dock == DockStyle.None && control.Width > 0 && control.Height > 0,
                            $"Designer root '{currentControlType}' is using runtime auto-layout. / 设计器根控件“{currentControlType}”启用了运行时自动布局。");
                    }

                    FieldInfo[] controlFields = controlType.GetFields(
                            BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic |
                            BindingFlags.DeclaredOnly)
                        .Where(field => typeof(Control).IsAssignableFrom(field.FieldType))
                        .ToArray();
                    foreach (FieldInfo field in controlFields)
                    {
                        Assert(
                            field.GetValue(control) is not null,
                            $"Designer field '{currentControlType}.{field.Name}' was not initialized. / " +
                            $"设计器字段“{currentControlType}.{field.Name}”未初始化。");
                    }

                    string[] runtimeOnlyPropertyNames = typeof(ConnectionTypeOptionsPage).IsAssignableFrom(controlType)
                        ? ["Type", "SectionTitle", "ManagedOptionKeys", "SuggestedName", "ShowsPrivateKey"]
                        : ["Protocol", "Target", "EffectiveAuthenticationFields"];
                    PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(control);
                    foreach (string propertyName in runtimeOnlyPropertyNames)
                    {
                        PropertyDescriptor? property = properties[propertyName];
                        Assert(
                            property is not null &&
                            !property.IsBrowsable &&
                            property.SerializationVisibility == DesignerSerializationVisibility.Hidden,
                            $"Runtime-only property '{currentControlType}.{propertyName}' is visible or serializable in the designer. / " +
                            $"仅供运行时使用的属性“{currentControlType}.{propertyName}”仍可在设计器中浏览或序列化。");
                    }

                    Queue<Control> pending = new();
                    HashSet<Control> visited = new(ReferenceEqualityComparer.Instance);
                    pending.Enqueue(control);
                    while (pending.Count > 0)
                    {
                        Control current = pending.Dequeue();
                        if (!visited.Add(current))
                        {
                            continue;
                        }

                        current.CreateControl();
                        _ = current.Handle;
                        current.PerformLayout();
                        foreach (Control child in current.Controls)
                        {
                            pending.Enqueue(child);
                        }
                    }

                    if (controlType != rootType)
                    {
                        Assert(
                            visited.Count > 1,
                            $"UserControl '{currentControlType}' exposed an empty dynamic control tree. / " +
                            $"UserControl“{currentControlType}”的动态控件树为空。");
                    }
                }

                using ConnectionTypeOptionsPage designerHost = new();
                Assert(
                    !designerHost.AutoSize && designerHost.Dock == DockStyle.None && designerHost.Height > 0,
                    "The inherited designer base changed layout before its design-time Site was assigned. / 继承设计器基类在设置设计时 Site 前改变了布局。");
                bool rejectedRuntimeContractUse = false;
                try
                {
                    _ = designerHost.Type;
                }
                catch (InvalidOperationException)
                {
                    rejectedRuntimeContractUse = true;
                }

                Assert(
                    rejectedRuntimeContractUse,
                    "The concrete designer host silently accepted runtime editor use. / 可实例化设计器基类静默接受了运行时编辑器用法。");
            }
            catch (Exception exception)
            {
                threadFailure = new InvalidOperationException(
                    $"WinForms connection-editor designer smoke test failed while processing '{currentControlType}'. / " +
                    $"WinForms 连接编辑器设计烟雾测试在处理“{currentControlType}”时失败。",
                    exception);
            }
        })
        {
            IsBackground = true,
            Name = "RemoteHubStudio connection-editor designer smoke test"
        };
        designerThread.SetApartmentState(ApartmentState.STA);
        designerThread.Start();

        if (!designerThread.Join(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException(
                $"WinForms connection-editor designer smoke test timed out while processing '{currentControlType}'. / " +
                $"WinForms 连接编辑器设计烟雾测试在处理“{currentControlType}”时超时。");
        }

        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    /// <summary>
    /// Verifies the main window resolves AntdUI's normal unsorted native selection to the selected connection. / 验证主窗口可将 AntdUI 常规未排序的原生选择解析为已选连接。
    /// </summary>
    private static void TestConnectionTableSelectionBridge()
    {
        Exception? threadFailure = null;
        Thread selectionThread = new(() =>
        {
            try
            {
                using MainForm form = new();
                Type formType = typeof(MainForm);
                AntdUI.Table table = formType
                    .GetField("_connectionTable", BindingFlags.Instance | BindingFlags.NonPublic)?
                    .GetValue(form) as AntdUI.Table
                    ?? throw new InvalidOperationException("Main connection table was not initialized. / 主连接表格未初始化。");
                AntdUI.Button connectButton = formType
                    .GetField("_connectButton", BindingFlags.Instance | BindingFlags.NonPublic)?
                    .GetValue(form) as AntdUI.Button
                    ?? throw new InvalidOperationException("Main connect button was not initialized. / 主连接按钮未初始化。");
                Guid selectedId = Guid.NewGuid();
                formType.GetField("_visibleConnectionOrder", BindingFlags.Instance | BindingFlags.NonPublic)?
                    .SetValue(form, new List<Guid> { selectedId });
                formType.GetField("_visibleConnectionIds", BindingFlags.Instance | BindingFlags.NonPublic)?
                    .SetValue(form, new HashSet<Guid> { selectedId });
                table.DataSource = new[] { new ConnectionTableRow { Id = selectedId, Name = "Selection regression" } };
                table.SelectedIndexs = [1];

                IReadOnlyList<Guid> selectedIds = formType
                    .GetMethod("GetSelectedConnectionIds", BindingFlags.Instance | BindingFlags.NonPublic)?
                    .Invoke(form, null) as IReadOnlyList<Guid>
                    ?? throw new InvalidOperationException("Main selection bridge returned no identifier list. / 主选择桥接未返回标识列表。");
                Assert(
                    selectedIds.SequenceEqual([selectedId]),
                    "An unsorted AntdUI table selection was not resolved to the selected connection. / AntdUI 未排序表格的选择未能解析为已选连接。");

                MethodInfo updateActionState = formType.GetMethod("UpdateActionState", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Main action-state updater was not found. / 未找到主操作状态更新方法。");
                updateActionState.Invoke(form, null);
                Assert(connectButton.Enabled, "The connect button stayed disabled after selecting one connection. / 选中一条连接后连接按钮仍处于禁用状态。");
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
        })
        {
            IsBackground = true,
            Name = "RemoteHubStudio connection selection test"
        };
        selectionThread.SetApartmentState(ApartmentState.STA);
        selectionThread.Start();
        if (!selectionThread.Join(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("Connection selection regression test timed out. / 连接选择回归测试超时。");
        }

        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    /// <summary>
    /// Verifies quoted commas, quotes, and multiline fields round-trip through CSV. / 验证逗号、引号及多行字段能够通过 CSV 往返。
    /// </summary>
    private static void TestCsvCodec()
    {
        IReadOnlyList<IReadOnlyList<string?>> source =
        [
            ["Name", "Notes"],
            ["db,primary", "line 1\r\n\"quoted\" line 2"]
        ];
        string encoded = CsvCodec.Encode(source);
        IReadOnlyList<IReadOnlyList<string>> decoded = CsvCodec.Decode(encoded);
        Assert(decoded.Count == 2, "CSV row count changed. / CSV 行数发生变化。");
        Assert(decoded[1][0] == "db,primary", "CSV comma escaping failed. / CSV 逗号转义失败。");
        Assert(decoded[1][1] == "line 1\r\n\"quoted\" line 2", "CSV multiline escaping failed. / CSV 多行转义失败。");
    }

    /// <summary>
    /// Verifies switches stay compact and vertically independent from a tall editor in the same responsive row. / 验证开关保持紧凑，且不会被同一响应式行中的高编辑器纵向拉伸。
    /// </summary>
    private static void TestResponsiveFieldGridSwitchSizing()
    {
        using ResponsiveFieldGrid grid = new() { Size = new Size(904, 300) };
        using AntdUI.Switch compactSwitch = new()
        {
            CheckedText = "A deliberately long state description that must not stretch the switch",
            UnCheckedText = "A deliberately long state description that must not stretch the switch"
        };
        using AntdUI.Input tallInput = new() { Height = 86, Multiline = true };
        using AntdUI.Input stretchableInput = new();

        grid.AddField("紧凑开关 / Compact switch", compactSwitch);
        grid.AddField("多行输入 / Multiline input", tallInput);
        grid.AddField("普通输入 / Standard input", stretchableInput);
        grid.PerformLayout();

        float scale = grid.DeviceDpi <= 0 ? 1F : grid.DeviceDpi / 96F;
        int expectedSwitchWidth = (int)Math.Round(60F * scale, MidpointRounding.AwayFromZero);
        int expectedSwitchHeight = (int)Math.Round(32F * scale, MidpointRounding.AwayFromZero);
        Assert(compactSwitch.Dock == DockStyle.None && compactSwitch.Anchor == AnchorStyles.Left,
            "A responsive field switch was still stretched by its table cell. / 响应式字段开关仍被表格单元格拉伸。");
        Assert(compactSwitch.Size == new Size(expectedSwitchWidth, expectedSwitchHeight),
            "A responsive field switch lost its standard 60x32 logical proportions. / 响应式字段开关未保持标准的 60x32 逻辑比例。");
        Assert(stretchableInput.Dock == DockStyle.Fill,
            "The compact-switch exception stopped ordinary inputs from filling their cells. / 紧凑开关特例导致普通输入框不再填满单元格。");
        Assert(compactSwitch.AccessibleName == "紧凑开关 / Compact switch",
            "The compact switch did not inherit an accessible field name. / 紧凑开关未继承无障碍字段名称。");
    }

    /// <summary>
    /// Verifies that incrementally adding fields cannot make the first ordinary editor row taller than later rows.
    /// / 验证逐项添加字段时，首个普通编辑器行不会比后续行更高。
    /// </summary>
    private static void TestResponsiveFieldGridEditorSizing()
    {
        using ResponsiveFieldGrid grid = new() { Width = 904 };
        using AntdUI.Input firstInput = new();
        using AntdUI.Select secondInput = new();
        using AntdUI.Input thirdInput = new();
        using AntdUI.Select fourthInput = new();

        grid.AddField("第一项 / First", firstInput);
        grid.AddField("第二项 / Second", secondInput);
        grid.AddField("第三项 / Third", thirdInput);
        grid.AddField("第四项 / Fourth", fourthInput);
        grid.PerformLayout();

        float scale = grid.DeviceDpi <= 0 ? 1F : grid.DeviceDpi / 96F;
        int expectedRowHeight = (int)Math.Round(48F * scale, MidpointRounding.AwayFromZero);
        int expectedEditorHeight = expectedRowHeight - firstInput.Margin.Vertical;
        Assert(
            firstInput.Height == expectedEditorHeight && secondInput.Height == expectedEditorHeight &&
            thirdInput.Height == expectedEditorHeight && fourthInput.Height == expectedEditorHeight,
            $"Responsive grid single-line editor heights were " +
            $"{firstInput.Height}/{secondInput.Height}/{thirdInput.Height}/{fourthInput.Height}; " +
            $"expected {expectedEditorHeight}. / 响应式网格的单行控件高度或比例不一致。");

        grid.SetFieldVisible(secondInput, false);
        grid.SetFieldVisible(fourthInput, false);
        grid.PerformLayout();
        Assert(grid.RowCount == 1 && !grid.Controls.Contains(secondInput) && !grid.Controls.Contains(fourthInput),
            "Hidden responsive fields left empty layout rows behind. / 响应式字段隐藏后仍留下了空白布局行。");
        grid.SetFieldVisible(secondInput, true);
        grid.SetFieldVisible(fourthInput, true);
        Assert(grid.Controls.Contains(secondInput) && grid.Controls.Contains(fourthInput),
            "A responsive field could not be shown again. / 响应式字段隐藏后无法重新显示。");

        AntdUI.Input detachedInput = new();
        ResponsiveFieldGrid disposableGrid = new();
        disposableGrid.AddField("Detached", detachedInput);
        disposableGrid.SetFieldVisible(detachedInput, false);
        disposableGrid.Dispose();
        Assert(detachedInput.IsDisposed,
            "A hidden responsive editor was not disposed with its grid. / 隐藏的响应式编辑控件未随网格释放。");
    }

    /// <summary>
    /// Verifies every connection type has an isolated child editor and legacy option aliases normalize without losing unknown values.
    /// / 验证每种连接类型均有独立子编辑器，且旧选项别名能在不丢失未知值的情况下规范化。
    /// </summary>
    private static void TestConnectionTypeOptionPages()
    {
        Assert((int)ConnectionType.Custom == 10 && (int)ConnectionType.RustDesk == 11,
            "Adding RustDesk changed a persisted legacy enum value. / 新增 RustDesk 改变了旧连接类型的持久化枚举值。");

        IReadOnlyDictionary<ConnectionType, ConnectionTypeOptionsPage> pages = ConnectionTypeOptionsPageFactory.CreateAll();
        try
        {
            ConnectionType[] types = Enum.GetValues<ConnectionType>();
            Assert(pages.Count == types.Length && types.All(type => pages.TryGetValue(type, out ConnectionTypeOptionsPage? page) && page.Type == type),
                "The type-specific editor factory does not cover every connection type. / 类型专属编辑器工厂未覆盖全部连接类型。");
            Assert(pages.Values.Select(page => page.GetType()).Distinct().Count() == types.Length,
                "Multiple connection types still share one concrete editor class. / 多个连接类型仍在共用同一个具体编辑器类。");
            Assert(ConnectionType.Putty.GetDefaultPort("telnet") == 23 &&
                   ConnectionType.Xftp.GetDefaultPort("ftp") == 21 &&
                   ConnectionType.WinScp.GetDefaultPort("webdav") == 80 &&
                   ConnectionType.WinScp.GetDefaultPort("webdavs") == 443,
                "Protocol-specific default ports are incorrect. / 协议专属默认端口不正确。");

            foreach (ConnectionType type in types)
            {
                IReadOnlyList<string> protocols = type.GetProtocols();
                if (protocols.Count == 0)
                {
                    continue;
                }

                foreach (string protocol in protocols)
                {
                    ConnectionProfile source = new()
                    {
                        Type = type,
                        Protocol = protocol,
                        Host = type is ConnectionType.ToDesk or ConnectionType.RustDesk ? "123456789" : "host.example",
                        Port = type.GetDefaultPort(protocol),
                        Username = "matrix-user",
                        Password = "matrix-secret"
                    };
                    ConnectionProfile target = new() { Type = type };
                    pages[type].LoadFrom(source);
                    Assert(pages[type].TryApplyTo(target, out string? protocolError),
                        protocolError ?? $"The {type}/{protocol} editor rejected its own supported mode. / {type}/{protocol} 参数页拒绝了自身支持的模式。");
                    Assert(target.Protocol == protocol,
                        $"The {type}/{protocol} editor did not preserve its selected mode. / {type}/{protocol} 参数页未保留所选模式。");

                    ConnectionAuthenticationFields expectedAuthentication = type switch
                    {
                        ConnectionType.RemoteDesktop or
                        ConnectionType.Xshell or
                        ConnectionType.Xftp or
                        ConnectionType.WinScp => ConnectionAuthenticationFields.UsernameAndPassword,
                        ConnectionType.Putty when protocol == "ssh" => ConnectionAuthenticationFields.UsernameAndPassword,
                        ConnectionType.SecureCrt when protocol is "ssh1" or "ssh2" => ConnectionAuthenticationFields.UsernameAndPassword,
                        ConnectionType.MobaXterm when protocol == "ssh" => ConnectionAuthenticationFields.UsernameAndPassword,
                        ConnectionType.Vnc when protocol is "tightvnc" or "ultravnc" => ConnectionAuthenticationFields.Password,
                        ConnectionType.ToDesk or ConnectionType.RustDesk => ConnectionAuthenticationFields.Password,
                        _ => ConnectionAuthenticationFields.None
                    };
                    string expectedUsername = expectedAuthentication.HasFlag(ConnectionAuthenticationFields.Username)
                        ? "matrix-user"
                        : string.Empty;
                    string expectedPassword = expectedAuthentication.HasFlag(ConnectionAuthenticationFields.Password)
                        ? "matrix-secret"
                        : string.Empty;
                    Assert(target.Username == expectedUsername && target.Password == expectedPassword,
                        $"The {type}/{protocol} editor did not preserve or clear inline authentication values correctly. / {type}/{protocol} 参数页未正确保留或清理内联认证信息。");
                }
            }

            Assert(ConnectionType.SecureCrt.NormalizeProtocol("ssh") == "ssh2" &&
                   ConnectionType.ToDesk.NormalizeProtocol("device") == "todesk" &&
                   ConnectionType.WinScp.NormalizeProtocol("https") == "webdavs" &&
                   ConnectionType.Radmin.NormalizeProtocol("文字聊天") == "chat" &&
                   ConnectionType.RustDesk.NormalizeProtocol("camera") == "view-camera",
                "Legacy protocol aliases are not centralized consistently. / 旧协议别名未得到一致的集中规范化。");

            pages[ConnectionType.RustDesk].LoadFrom(new ConnectionProfile
            {
                Type = ConnectionType.RustDesk,
                Protocol = "file",
                Host = "123456789",
                Port = 0
            });
            ConnectionProfile legacyRustDeskMode = new() { Type = ConnectionType.RustDesk };
            Assert(pages[ConnectionType.RustDesk].TryApplyTo(legacyRustDeskMode, out string? legacyRustDeskError),
                legacyRustDeskError ?? "RustDesk editor rejected a legacy mode alias. / RustDesk 参数页拒绝了旧模式别名。");
            Assert(legacyRustDeskMode.Protocol == "file-transfer",
                "RustDesk editor silently changed a legacy file-transfer alias to the default mode. / RustDesk 参数页将旧文件传输别名静默改成了默认模式。");

            pages[ConnectionType.WinScp].LoadFrom(new ConnectionProfile
            {
                Type = ConnectionType.WinScp,
                Protocol = "http",
                Host = "dav.example",
                Port = 80
            });
            ConnectionProfile legacyWinScpMode = new() { Type = ConnectionType.WinScp };
            Assert(pages[ConnectionType.WinScp].TryApplyTo(legacyWinScpMode, out string? legacyWinScpError),
                legacyWinScpError ?? "WinSCP editor rejected a legacy WebDAV alias. / WinSCP 参数页拒绝了旧 WebDAV 别名。");
            Assert(legacyWinScpMode.Protocol == "webdav" && legacyWinScpMode.Port == 80,
                "WinSCP editor did not preserve a legacy HTTP WebDAV session. / WinSCP 参数页未保留旧 HTTP WebDAV 会话。");

            RdpConnectionTypeOptionsPage unsupportedProtocolPage = (RdpConnectionTypeOptionsPage)pages[ConnectionType.RemoteDesktop];
            unsupportedProtocolPage.LoadFrom(new ConnectionProfile
            {
                Type = ConnectionType.RemoteDesktop,
                Protocol = "future-rdp-mode",
                Host = "rdp.example",
                Port = 3389
            });
            ConnectionEndpointEditor unsupportedEndpoint =
                typeof(RdpConnectionTypeOptionsPage)
                    .GetField("_endpoint", BindingFlags.Instance | BindingFlags.NonPublic)?
                    .GetValue(unsupportedProtocolPage) as ConnectionEndpointEditor
                ?? throw new InvalidOperationException("RDP endpoint editor was not found. / 未找到 RDP 目标编辑器。");
            AntdUI.Select unsupportedProtocolSelect =
                typeof(ConnectionEndpointEditor)
                    .GetField("_protocolSelect", BindingFlags.Instance | BindingFlags.NonPublic)?
                    .GetValue(unsupportedEndpoint) as AntdUI.Select
                ?? throw new InvalidOperationException("Protocol selector was not found. / 未找到协议选择器。");
            Assert(unsupportedProtocolSelect.SelectedValue as string == "future-rdp-mode" &&
                   unsupportedProtocolSelect.Parent is not null &&
                   unsupportedProtocolSelect.Items.OfType<AntdUI.SelectItem>().Any(item =>
                       item.Text == L.Format("ConnectionEndpoint.UnsupportedLegacyValue", "future-rdp-mode")),
                "A same-client unsupported legacy protocol was not retained in a visible temporary option. / 同客户端未支持的旧协议未保留为可见临时选项。");

            ConnectionProfile rejectedUnsupportedProtocol = new() { Type = ConnectionType.RemoteDesktop };
            Assert(!unsupportedProtocolPage.TryApplyTo(rejectedUnsupportedProtocol, out string? unsupportedProtocolError) &&
                   unsupportedProtocolError == L.Format(
                       "ConnectionEndpoint.Validation.UnsupportedProtocol",
                       "future-rdp-mode"),
                "The editor silently saved an unsupported legacy protocol. / 编辑器静默保存了未支持的旧协议。");
            unsupportedProtocolSelect.SelectedValue = "rdp";
            ConnectionProfile correctedProtocol = new() { Type = ConnectionType.RemoteDesktop };
            Assert(unsupportedProtocolPage.TryApplyTo(correctedProtocol, out string? correctedProtocolError),
                correctedProtocolError ?? "The editor rejected a supported replacement protocol. / 编辑器拒绝了替换后的受支持协议。");
            Assert(correctedProtocol.Protocol == "rdp",
                "The corrected supported protocol was not saved. / 修正后的受支持协议未被保存。");

            pages[ConnectionType.Putty].LoadFrom(new ConnectionProfile
            {
                Type = ConnectionType.RemoteDesktop,
                Protocol = "rdp",
                Host = "host.example",
                Port = 3389
            });
            ConnectionProfile switchedPutty = new() { Type = ConnectionType.Putty };
            Assert(pages[ConnectionType.Putty].TryApplyTo(switchedPutty, out string? switchedPuttyError),
                switchedPuttyError ?? "PuTTY editor rejected a client switch. / PuTTY 参数页拒绝了客户端切换。");
            Assert(switchedPutty.Protocol == "ssh" && switchedPutty.Port == 22,
                "Switching from RDP to PuTTY retained the unrelated RDP port. / 从 RDP 切换到 PuTTY 后仍保留了无关的 RDP 端口。");

            ConnectionProfile rustDeskSource = new()
            {
                Type = ConnectionType.RustDesk,
                Protocol = "connect",
                Host = "123456789",
                Port = 0,
                Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["server"] = "relay.example:21118",
                    ["server_key"] = "abc+/=",
                    ["relay"] = "1",
                    ["futureOption"] = "preserve-me"
                }
            };
            ConnectionProfile rustDeskTarget = new()
            {
                Type = ConnectionType.RustDesk,
                Protocol = "connect",
                Host = "123456789",
                Port = 0,
                Options = new Dictionary<string, string>(rustDeskSource.Options, StringComparer.OrdinalIgnoreCase)
            };
            pages[ConnectionType.RustDesk].LoadFrom(rustDeskSource);
            Assert(pages[ConnectionType.RustDesk].TryApplyTo(rustDeskTarget, out string? rustDeskError), rustDeskError ?? "RustDesk editor rejected valid options. / RustDesk 编辑器拒绝了有效选项。");
            Assert(rustDeskTarget.Options["serverKey"] == "abc+/=" && rustDeskTarget.Options["forceRelay"] == "true" &&
                   !rustDeskTarget.Options.ContainsKey("server_key") && !rustDeskTarget.Options.ContainsKey("relay") &&
                   rustDeskTarget.Options["futureOption"] == "preserve-me",
                "RustDesk aliases were not normalized or an unknown option was lost. / RustDesk 别名未规范化或未知选项丢失。");

            ConnectionProfile invalidKeyOnly = new()
            {
                Type = ConnectionType.RustDesk,
                Protocol = "connect",
                Host = "123456789",
                Port = 0,
                Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["serverKey"] = "abc" }
            };
            pages[ConnectionType.RustDesk].LoadFrom(invalidKeyOnly);
            Assert(!pages[ConnectionType.RustDesk].TryApplyTo(new ConnectionProfile { Type = ConnectionType.RustDesk }, out _),
                "The RustDesk editor accepted a server key without a server. / RustDesk 编辑器接受了没有服务器地址的公钥。");

            ConnectionProfile mismatchedWebDav = new()
            {
                Type = ConnectionType.WinScp,
                Protocol = "webdav",
                Host = "dav.example",
                Port = 80,
                Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["webDavAddress"] = "https://dav.example/root/"
                }
            };
            pages[ConnectionType.WinScp].LoadFrom(mismatchedWebDav);
            Assert(!pages[ConnectionType.WinScp].TryApplyTo(
                    new ConnectionProfile { Type = ConnectionType.WinScp },
                    out _),
                "The WinSCP editor accepted an encrypted WebDAV URI for an unencrypted WebDAV mode. / WinSCP 编辑器接受了与非加密 WebDAV 模式不匹配的加密 URI。");
            ConnectionProfile matchingWebDav = new()
            {
                Type = ConnectionType.WinScp,
                Protocol = "webdavs",
                Host = "dav.example",
                Port = 443,
                Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["webDavAddress"] = "https://dav.example/root/"
                }
            };
            pages[ConnectionType.WinScp].LoadFrom(matchingWebDav);
            Assert(pages[ConnectionType.WinScp].TryApplyTo(
                    new ConnectionProfile { Type = ConnectionType.WinScp },
                    out string? webDavError),
                webDavError ?? "The WinSCP editor rejected a matching WebDAVS URI. / WinSCP 编辑器拒绝了匹配的 WebDAVS URI。");

            ConnectionProfile rdpSource = new()
            {
                Type = ConnectionType.RemoteDesktop,
                Protocol = "rdp",
                Host = "rdp.example",
                Port = 3389,
                Rdp = new RdpOptions
                {
                    DisplayConnectionBar = false,
                    EnableCompression = false,
                    KeyboardHookMode = RdpKeyboardHookMode.Remote,
                    RedirectComPorts = true,
                    RedirectPosDevices = true,
                    RedirectCameras = true
                }
            };
            ConnectionProfile rdpTarget = new() { Type = ConnectionType.RemoteDesktop, Rdp = new RdpOptions() };
            pages[ConnectionType.RemoteDesktop].LoadFrom(rdpSource);
            Assert(pages[ConnectionType.RemoteDesktop].TryApplyTo(rdpTarget, out string? rdpError), rdpError ?? "RDP editor rejected valid options. / RDP 编辑器拒绝了有效选项。");
            Assert(!rdpTarget.Rdp.DisplayConnectionBar && !rdpTarget.Rdp.EnableCompression &&
                   rdpTarget.Rdp.KeyboardHookMode == RdpKeyboardHookMode.Remote &&
                   rdpTarget.Rdp.RedirectComPorts && rdpTarget.Rdp.RedirectPosDevices && rdpTarget.Rdp.RedirectCameras,
                "The RDP child editor did not apply every extended option. / RDP 子编辑器未应用全部扩展选项。");

            using VncConnectionTypeOptionsPage realVncPage = new();
            realVncPage.LoadFrom(new ConnectionProfile
            {
                Type = ConnectionType.Vnc,
                Protocol = "realvnc",
                Host = "vnc.example",
                Port = 5900,
                Username = "unused-user",
                Password = "unused-secret"
            });
            ConnectionProfile realVncTarget = new() { Type = ConnectionType.Vnc };
            Assert(realVncPage.TryApplyTo(realVncTarget, out string? realVncError), realVncError ?? "RealVNC editor rejected a valid target. / RealVNC 编辑器拒绝了有效目标。");
            Assert(realVncTarget.Username.Length == 0 && realVncTarget.Password.Length == 0,
                "RealVNC retained hidden authentication values that its launch builder never consumes. / RealVNC 保留了启动器不会使用的隐藏认证信息。");

            using VncConnectionTypeOptionsPage tightVncPage = new();
            tightVncPage.LoadFrom(new ConnectionProfile
            {
                Type = ConnectionType.Vnc,
                Protocol = "tightvnc",
                Host = "vnc.example",
                Port = 5900,
                Username = "unused-user",
                Password = "inline-vnc-secret"
            });
            ConnectionProfile tightVncTarget = new() { Type = ConnectionType.Vnc };
            Assert(tightVncPage.TryApplyTo(tightVncTarget, out string? tightVncError), tightVncError ?? "TightVNC editor rejected a valid target. / TightVNC 编辑器拒绝了有效目标。");
            Assert(tightVncTarget.Username.Length == 0 && tightVncTarget.Password == "inline-vnc-secret",
                "The password-only VNC mode did not preserve its inline password. / 仅密码 VNC 模式未保留内联密码。");

            using XshellConnectionTypeOptionsPage accountPage = new();
            accountPage.LoadFrom(new ConnectionProfile
            {
                Type = ConnectionType.Xshell,
                Protocol = "ssh",
                Host = "shell.example",
                Port = 22,
                Username = "inline-user",
                Password = "inline-secret"
            });
            ConnectionProfile accountTarget = new() { Type = ConnectionType.Xshell };
            Assert(accountPage.TryApplyTo(accountTarget, out string? accountError) &&
                   accountTarget.Username == "inline-user" &&
                   accountTarget.Password == "inline-secret",
                accountError ?? "An account-based client did not preserve its inline authentication values. / 账号型客户端未保留内联认证信息。");

            using ToDeskConnectionTypeOptionsPage devicePage = new();
            devicePage.LoadFrom(new ConnectionProfile
            {
                Type = ConnectionType.ToDesk,
                Protocol = "todesk",
                Host = "123456789",
                Username = "unused-user",
                Password = "inline-device-secret"
            });
            ConnectionProfile deviceTarget = new() { Type = ConnectionType.ToDesk };
            Assert(devicePage.TryApplyTo(deviceTarget, out string? deviceError) &&
                   deviceTarget.Username.Length == 0 &&
                   deviceTarget.Password == "inline-device-secret",
                deviceError ?? "A password-only client did not preserve its inline password. / 仅密码客户端未保留内联密码。");

            using CustomConnectionTypeOptionsPage customAuthenticationPage = new();
            customAuthenticationPage.LoadFrom(new ConnectionProfile
            {
                Type = ConnectionType.Custom,
                Host = "custom.example",
                Password = "inline-custom-secret",
                CustomArguments = "--secret {password}"
            });
            ConnectionProfile customAuthenticationTarget = new() { Type = ConnectionType.Custom };
            Assert(customAuthenticationPage.TryApplyTo(customAuthenticationTarget, out string? customAuthenticationError) &&
                   customAuthenticationTarget.Password == "inline-custom-secret",
                customAuthenticationError ?? "A custom password placeholder did not preserve its inline password. / 自定义密码占位符未保留内联密码。");
            customAuthenticationPage.UpdateCustomArgumentTemplate("--no-auth");
            ConnectionProfile customNoAuthenticationTarget = new() { Type = ConnectionType.Custom };
            Assert(customAuthenticationPage.TryApplyTo(customNoAuthenticationTarget, out string? customNoAuthenticationError) &&
                   customNoAuthenticationTarget.Username.Length == 0 &&
                   customNoAuthenticationTarget.Password.Length == 0,
                customNoAuthenticationError ?? "A custom template without authentication placeholders retained inline authentication values. / 不含认证占位符的自定义模板仍保留内联认证信息。");

            using PuttyConnectionTypeOptionsPage puttyPrivateKeyPage = new();
            puttyPrivateKeyPage.LoadFrom(new ConnectionProfile
            {
                Type = ConnectionType.Putty,
                Protocol = "ssh",
                Host = "putty.example",
                Port = 22
            });
            Assert(puttyPrivateKeyPage.ShowsPrivateKey,
                "Native PuTTY SSH hid its private-key field. / PuTTY 原生 SSH 隐藏了私钥字段。");
            puttyPrivateKeyPage.UpdateCustomArgumentTemplate("--batch");
            Assert(!puttyPrivateKeyPage.ShowsPrivateKey,
                "A custom PuTTY template without {key} exposed an unused private-key field. / 不含 {key} 的 PuTTY 自定义模板显示了无用私钥字段。");
            puttyPrivateKeyPage.UpdateCustomArgumentTemplate("--identity {key}");
            Assert(puttyPrivateKeyPage.ShowsPrivateKey,
                "A custom PuTTY template using {key} hid its private-key field. / 使用 {key} 的 PuTTY 自定义模板隐藏了私钥字段。");

            using RustDeskConnectionTypeOptionsPage keyedRustDeskPage = new();
            keyedRustDeskPage.LoadFrom(new ConnectionProfile
            {
                Type = ConnectionType.RustDesk,
                Protocol = "connect",
                Host = "123456789",
                Password = "unused-secret",
                Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["server"] = "relay.example:21118",
                    ["serverKey"] = "server-public-key"
                }
            });
            ConnectionProfile keyedRustDeskTarget = new() { Type = ConnectionType.RustDesk };
            Assert(keyedRustDeskPage.TryApplyTo(keyedRustDeskTarget, out string? keyedRustDeskError) &&
                   keyedRustDeskTarget.Password.Length == 0,
                keyedRustDeskError ?? "RustDesk retained a password while a server key disables automatic password passing. / 配置服务器公钥后 RustDesk 仍保留了密码。");

            using RadminConnectionTypeOptionsPage radminPage = new();
            radminPage.LoadFrom(new ConnectionProfile
            {
                Type = ConnectionType.Radmin,
                Protocol = "control",
                Host = "radmin.example",
                Port = 4899,
                Username = "unused-user",
                Password = "unused-secret"
            });
            ConnectionProfile radminTarget = new() { Type = ConnectionType.Radmin };
            Assert(radminPage.TryApplyTo(radminTarget, out string? radminError), radminError ?? "Radmin editor rejected a valid target. / Radmin 编辑器拒绝了有效目标。");
            Assert(radminTarget.Username.Length == 0 && radminTarget.Password.Length == 0,
                "Radmin retained authentication values that are never used by its launcher. / Radmin 保留了启动器从不使用的认证信息。");

            ConnectionProfile radminFileSource = new()
            {
                Type = ConnectionType.Radmin,
                Protocol = "file",
                Host = "radmin.example",
                Port = 4899,
                Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["encrypt"] = "true",
                    ["fullscreen"] = "true",
                    ["noFullKeyboardControl"] = "true",
                    ["colorDepth"] = "16bpp",
                    ["updates"] = "60",
                    ["futureOption"] = "preserve-me"
                }
            };
            radminPage.LoadFrom(radminFileSource);
            ConnectionProfile radminFileTarget = new()
            {
                Type = ConnectionType.Radmin,
                Options = new Dictionary<string, string>(radminFileSource.Options, StringComparer.OrdinalIgnoreCase)
            };
            Assert(radminPage.TryApplyTo(radminFileTarget, out string? radminFileError),
                radminFileError ?? "Radmin file-mode editor rejected a valid target. / Radmin 文件模式参数页拒绝了有效目标。");
            Assert(radminFileTarget.Options.TryGetValue("encrypt", out string? encrypt) && encrypt == "true" &&
                   !radminFileTarget.Options.ContainsKey("fullscreen") &&
                   !radminFileTarget.Options.ContainsKey("noFullKeyboardControl") &&
                   !radminFileTarget.Options.ContainsKey("colorDepth") &&
                   !radminFileTarget.Options.ContainsKey("updates") &&
                   radminFileTarget.Options["futureOption"] == "preserve-me",
                "Radmin non-display mode retained display-only options or lost an unknown option. / Radmin 非显示模式保留了显示专属选项或丢失了未知选项。");
        }
        finally
        {
            foreach (ConnectionTypeOptionsPage page in pages.Values)
            {
                page.Dispose();
            }
        }

        using ConnectionEditorForm editor = new();
        Assert(editor.EditorMode == ConnectionEditorMode.Add,
            "The composed connection editor failed to initialize in add mode. / 组合式连接编辑器未能以新增模式初始化。");
    }

    /// <summary>
    /// Verifies portable envelopes, legacy raw JSON, schema limits, size limits, and imported launch sanitization. / 验证便携信封、旧版原始 JSON、架构与大小限制及导入启动配置清理。
    /// </summary>
    private static async Task TestWorkspaceJsonTransferAsync()
    {
        using TemporaryDirectoryScope scope = new();
        WorkspaceTransferService service = new();
        string exportedPath = System.IO.Path.Combine(scope.Path, "portable.rhs.json");
        AppDataDocument source = new()
        {
            Connections =
            [
                new ConnectionProfile
                {
                    Name = "Portable",
                    Type = ConnectionType.Custom,
                    Host = "portable.example",
                    Port = 0,
                    Password = "  whitespace-secret  ",
                    ExecutableOverride = "powershell.exe",
                    CustomArguments = "-NoProfile -Command malicious",
                    PrivateKeyPath = @"C:\Users\example\.ssh\id_ed25519",
                    Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["webDavAddress"] = "https://hidden-after-type-change.example/",
                        ["serverKey"] = "hidden-after-type-change-key",
                        ["customSafeOption"] = "preserve-me"
                    }
                },
                new ConnectionProfile
                {
                    Name = "Imported RDP",
                    Type = ConnectionType.RemoteDesktop,
                    Protocol = "rdp",
                    Host = "rdp.example",
                    Port = 3389,
                    Rdp = new RdpOptions
                    {
                        RedirectClipboard = true,
                        RedirectDrives = true,
                        RedirectPrinters = true,
                        RedirectSmartCards = true,
                        RedirectComPorts = true,
                        RedirectPosDevices = true,
                        RedirectCameras = true,
                        RedirectMicrophone = true,
                        AdministrativeSession = true
                    }
                },
                new ConnectionProfile
                {
                    Name = "Imported WinSCP route",
                    Type = ConnectionType.WinScp,
                    Protocol = "webdavs",
                    Host = "visible.example",
                    Port = 443,
                    Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["dav_address"] = "https://hidden.example/root/",
                        ["remotePath"] = "/safe-path"
                    }
                },
                new ConnectionProfile
                {
                    Name = "Imported RustDesk route",
                    Type = ConnectionType.RustDesk,
                    Protocol = "connect",
                    Host = "123456789",
                    Port = 0,
                    Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["server"] = "hidden.example:21118",
                        ["server_key"] = "public-key",
                        ["relay"] = "true",
                        ["futureOption"] = "preserve-me"
                    }
                }
            ]
        };
        source.Settings.ToolPaths["putty"] = @"C:\Users\example\Tools\putty.exe";

        await service.ExportJsonAsync(source, exportedPath, includeSecrets: true);
        using (JsonDocument exported = JsonDocument.Parse(await File.ReadAllTextAsync(exportedPath)))
        {
            JsonElement root = exported.RootElement;
            Assert(root.GetProperty("format").GetString() == ProductInfo.WorkspaceFormatId, "Portable JSON format identifier is wrong. / 便携 JSON 格式标识错误。");
            Assert(root.GetProperty("schema").GetInt32() == 1, "Portable JSON envelope schema is wrong. / 便携 JSON 信封架构错误。");
            Assert(root.TryGetProperty("exportedAt", out _), "Portable JSON export timestamp is missing. / 便携 JSON 缺少导出时间。");
            Assert(root.TryGetProperty("data", out JsonElement data), "Portable JSON data object is missing. / 便携 JSON 缺少数据对象。");
            Assert(!data.TryGetProperty("settings", out _), "Portable JSON exposed machine-local settings. / 便携 JSON 暴露了本机设置。");
        }

        AppDataDocument imported = await service.ImportJsonAsync(exportedPath);
        ConnectionProfile importedProfile = imported.Connections.Single(connection => connection.Name == "Portable");
        Assert(importedProfile.Password == "  whitespace-secret  ", "JSON import changed password whitespace. / JSON 导入改变了密码空白。");
        Assert(importedProfile.ExecutableOverride.Length == 0 && importedProfile.CustomArguments.Length == 0 && importedProfile.PrivateKeyPath.Length == 0 &&
               !importedProfile.Options.ContainsKey("webDavAddress") && !importedProfile.Options.ContainsKey("serverKey") &&
               importedProfile.Options["customSafeOption"] == "preserve-me",
            "Untrusted JSON import retained active or cross-type endpoint configuration, or removed a harmless extension. / 不可信 JSON 导入保留了主动启动或跨类型目标配置，或误删了无害扩展项。");
        RdpOptions importedRdp = imported.Connections.Single(connection => connection.Name == "Imported RDP").Rdp;
        Assert(!importedRdp.RedirectClipboard && !importedRdp.RedirectDrives && !importedRdp.RedirectPrinters && !importedRdp.RedirectSmartCards && !importedRdp.RedirectComPorts && !importedRdp.RedirectPosDevices && !importedRdp.RedirectCameras && !importedRdp.RedirectMicrophone && !importedRdp.AdministrativeSession, "Untrusted JSON import retained RDP local-resource exposure. / 不可信 JSON 导入保留了 RDP 本地资源暴露设置。");
        ConnectionProfile importedWinScp = imported.Connections.Single(connection => connection.Name == "Imported WinSCP route");
        ConnectionProfile importedRustDesk = imported.Connections.Single(connection => connection.Name == "Imported RustDesk route");
        Assert(!importedWinScp.Options.ContainsKey("dav_address") && importedWinScp.Options["remotePath"] == "/safe-path" &&
               !importedRustDesk.Options.ContainsKey("server") && !importedRustDesk.Options.ContainsKey("server_key") &&
               !importedRustDesk.Options.ContainsKey("relay") && importedRustDesk.Options["futureOption"] == "preserve-me",
            "Untrusted JSON import retained hidden endpoint-routing options or removed harmless extensions. / 不可信 JSON 导入保留了隐藏目标路由选项，或误删了无害扩展项。");

        AppDataDocument trustedImport = await service.ImportJsonAsync(exportedPath, trustLaunchConfiguration: true);
        ConnectionProfile trustedProfile = trustedImport.Connections.Single(connection => connection.Name == "Portable");
        Assert(trustedProfile.ExecutableOverride == "powershell.exe" && trustedProfile.CustomArguments.Contains("malicious", StringComparison.Ordinal) && trustedProfile.PrivateKeyPath.EndsWith("id_ed25519", StringComparison.Ordinal) &&
               trustedProfile.Options.ContainsKey("webDavAddress") && trustedProfile.Options.ContainsKey("serverKey"),
            "Trusted JSON import did not preserve launch or cross-type endpoint configuration. / 可信 JSON 导入未保留启动或跨类型目标配置。");
        Assert(trustedImport.Connections.Single(connection => connection.Name == "Imported RDP").Rdp.RedirectDrives, "Trusted JSON import did not preserve RDP redirection. / 可信 JSON 导入未保留 RDP 重定向。");
        Assert(trustedImport.Connections.Single(connection => connection.Name == "Imported WinSCP route").Options.ContainsKey("dav_address") &&
               trustedImport.Connections.Single(connection => connection.Name == "Imported RustDesk route").Options.ContainsKey("server"),
            "Trusted JSON import did not preserve endpoint-routing options. / 可信 JSON 导入未保留目标路由选项。");

        string redactedPath = System.IO.Path.Combine(scope.Path, "redacted.rhs.json");
        await service.ExportJsonAsync(source, redactedPath, includeSecrets: false);
        AppDataDocument redacted = await service.ImportJsonAsync(redactedPath);
        Assert(redacted.Connections.Single(connection => connection.Name == "Portable").Password.Length == 0, "Portable JSON password redaction failed. / 便携 JSON 密码清除失败。");

        string legacyPath = System.IO.Path.Combine(scope.Path, "legacy.json");
        await File.WriteAllTextAsync(legacyPath, """
            {"schemaVersion":1,"connections":[{"name":"Legacy","host":"legacy.example","password":"  legacy secret  ","executableOverride":"cmd.exe","customArguments":"/c whoami"}]}
            """);
        AppDataDocument legacy = await service.ImportJsonAsync(legacyPath);
        Assert(legacy.Connections.Single().Password == "  legacy secret  ", "Legacy JSON password whitespace changed. / 旧版 JSON 密码空白发生变化。");
        Assert(legacy.Connections.Single().ExecutableOverride.Length == 0 && legacy.Connections.Single().CustomArguments.Length == 0, "Legacy JSON retained executable launch configuration. / 旧版 JSON 保留了可执行启动配置。");

        string rustDeskProtocolPath = System.IO.Path.Combine(scope.Path, "rustdesk-protocol-defaults.json");
        await File.WriteAllTextAsync(rustDeskProtocolPath, """
            {"schemaVersion":1,"connections":[
              {"name":"Missing RustDesk protocol","type":"rustDesk","host":"123456789","port":0},
              {"name":"Explicit RustDesk RDP","type":"rustDesk","protocol":"rdp","host":"123456789","port":0}
            ]}
            """);
        AppDataDocument rustDeskProtocols = await service.ImportJsonAsync(rustDeskProtocolPath);
        Assert(rustDeskProtocols.Connections.Single(connection => connection.Name == "Missing RustDesk protocol").Protocol == "connect" &&
               rustDeskProtocols.Connections.Single(connection => connection.Name == "Explicit RustDesk RDP").Protocol == "rdp",
            "JSON protocol migration did not distinguish a missing RustDesk mode from an explicit RDP tunnel. / JSON 协议迁移未区分 RustDesk 缺省模式与显式 RDP 隧道。");

        await AssertInvalidImportAsync(service, scope.Path, "empty.json", "{}", "Empty JSON object was accepted. / 空 JSON 对象被接受。");
        await AssertInvalidImportAsync(service, scope.Path, "local.json", "{\"format\":\"remotehubstudio-workspace\",\"schemaVersion\":1,\"protection\":\"none\",\"data\":{}}", "Local workspace envelope was accepted. / 本地工作区信封被接受。");
        await AssertInvalidImportAsync(service, scope.Path, "unknown.json", "{\"format\":\"unknown-workspace\",\"schema\":1,\"exportedAt\":\"2026-09-02T00:00:00Z\",\"data\":{}}", "Unknown workspace format was accepted. / 未知工作区格式被接受。");
        await AssertInvalidImportAsync(service, scope.Path, "future-envelope.json", $"{{\"format\":\"{ProductInfo.WorkspaceFormatId}\",\"schema\":2,\"exportedAt\":\"2026-09-02T00:00:00Z\",\"data\":{{}}}}", "Future portable envelope was accepted. / 未来版便携信封被接受。");
        await AssertInvalidImportAsync(service, scope.Path, "future-data.json", "{\"schemaVersion\":2,\"connections\":[]}", "Future workspace data was accepted. / 未来版工作区数据被接受。");

        string oversizedPath = System.IO.Path.Combine(scope.Path, "oversized.json");
        await using (FileStream oversized = new(oversizedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            oversized.SetLength(64L * 1024L * 1024L + 1L);
        }

        await AssertThrowsAsync<InvalidDataException>(() => service.ImportJsonAsync(oversizedPath), "Oversized JSON was accepted. / 超大 JSON 被接受。");
    }

    /// <summary>
    /// Verifies legacy combined client labels, protocol precedence, whitespace, required fields, ports, and launch sanitization. / 验证旧版组合客户端标签、协议优先级、空白、必需字段、端口及启动配置清理。
    /// </summary>
    private static async Task TestLegacyCsvTransferAsync()
    {
        using TemporaryDirectoryScope scope = new();
        WorkspaceTransferService service = new();
        string csvPath = System.IO.Path.Combine(scope.Path, "legacy.csv");
        IReadOnlyList<IReadOnlyList<string?>> rows =
        [
            ["Name", "Type", "Protocol", "Host", "Port", "Password", "Notes", "Executable", "Arguments"],
            ["Putty", "Putty-telnet", "", "putty.example", "23", "  padded password  ", "  padded notes  ", "powershell.exe", "  -Command whoami  "],
            ["Explicit", "Xshell-sftp", "telnet", "explicit.example", "23", "", "", "", ""],
            ["Radmin", "Radmin-仅限查看", "", "radmin.example", "4899", "", "", "", ""],
            ["VNC", "VNC-realvnc", "", "vnc.example", "5900", "", "", "", ""],
            ["WinSCP HTTPS", "Winscp-https", "", "dav.example", "443", "", "", "", ""],
            ["WinSCP HTTP", "Winscp-http", "", "dav-http.example", "80", "", "", "", ""],
            ["SecureCRT SSH1", "SecureCrt-ssh1", "", "ssh1.example", "22", "", "", "", ""],
            ["SecureCRT SSH", "SecureCrt", "ssh", "ssh2.example", "22", "", "", "", ""],
            ["MobaXterm", "Mobaxterm-telnet", "", "moba.example", "23", "", "", "", ""],
            ["RustDesk", "RustDesk-file-transfer", "", "123456789", "0", "", "", "", ""],
            ["Legacy custom", "Putty-自定义", "", "custom.example", "22", "", "", "", "-ssh {host}"],
            ["Default PuTTY Telnet", "Putty-telnet", "", "putty-default.example", "", "", "", "", ""],
            ["Default Xftp FTP", "Xftp-ftp", "", "xftp-default.example", "", "", "", "", ""],
            ["Default WinSCP FTPS", "WinSCP-ftps", "", "ftps-default.example", "", "", "", "", ""],
            ["Default WinSCP WebDAV", "WinSCP-webdav", "", "dav-default.example", "", "", "", "", ""],
            ["Default WinSCP WebDAVS", "WinSCP-webdavs", "", "davs-default.example", "", "", "", "", ""],
            ["", "Putty-ssh", "", "missing-name.example", "22", "", "", "", ""],
            ["Missing host", "Putty-ssh", "", "", "22", "", "", "", ""],
            ["Zero port", "Putty-ssh", "", "zero.example", "0", "", "", "", ""]
        ];
        await File.WriteAllTextAsync(csvPath, CsvCodec.Encode(rows));

        ImportResult result = await service.ImportCsvAsync(csvPath);
        Assert(result.Connections.Count == 16, "CSV invalid-row filtering changed the valid count. / CSV 无效行过滤后的有效数量错误。");
        Assert(result.SkippedRowCount == 3 && result.ModifiedRowCount == 2, "CSV skipped and modified row counts were conflated. / CSV 跳过行与修改行计数被混淆。");
        Assert(result.Warnings.Count >= 5, "CSV warnings omitted invalid or sanitized rows. / CSV warning 遗漏无效或已清理行。");
        Assert(FindConnection(result, "Putty").Protocol == "telnet", "PuTTY combined protocol was not parsed. / PuTTY 组合协议未解析。");
        Assert(FindConnection(result, "Explicit").Protocol == "telnet", "Explicit Protocol did not override the combined label. / 显式 Protocol 未覆盖组合标签。");
        Assert(FindConnection(result, "Radmin").Protocol == "view", "Radmin localized action was not mapped. / Radmin 本地化动作未映射。");
        Assert(FindConnection(result, "VNC").Protocol == "realvnc", "VNC combined protocol was not parsed. / VNC 组合协议未解析。");
        Assert(FindConnection(result, "WinSCP HTTPS").Protocol == "webdavs" && FindConnection(result, "WinSCP HTTP").Protocol == "webdav", "WinSCP HTTP aliases were not mapped. / WinSCP HTTP 别名未映射。");
        Assert(FindConnection(result, "SecureCRT SSH1").Protocol == "ssh1" && FindConnection(result, "SecureCRT SSH").Protocol == "ssh2", "SecureCRT SSH aliases were not mapped. / SecureCRT SSH 别名未映射。");
        Assert(FindConnection(result, "MobaXterm").Protocol == "telnet", "MobaXterm combined protocol was not parsed. / MobaXterm 组合协议未解析。");
        Assert(FindConnection(result, "RustDesk").Type == ConnectionType.RustDesk && FindConnection(result, "RustDesk").Protocol == "file-transfer", "RustDesk combined protocol was not parsed. / RustDesk 组合协议未解析。");
        Assert(FindConnection(result, "Legacy custom").Protocol == "ssh", "Legacy custom arguments did not retain the client's default protocol. / 旧版自定义参数未保留客户端默认协议。");
        Assert(FindConnection(result, "Default PuTTY Telnet").Port == 23 &&
               FindConnection(result, "Default Xftp FTP").Port == 21 &&
               FindConnection(result, "Default WinSCP FTPS").Port == 990 &&
               FindConnection(result, "Default WinSCP WebDAV").Port == 80 &&
               FindConnection(result, "Default WinSCP WebDAVS").Port == 443,
            "CSV blank ports did not use their normalized protocol defaults. / CSV 空端口未使用规范化协议的默认端口。");
        ConnectionProfile putty = FindConnection(result, "Putty");
        Assert(putty.Password == "  padded password  ", "CSV import trimmed password whitespace. / CSV 导入裁剪了密码空白。");
        Assert(putty.Notes == "  padded notes  ", "CSV import trimmed notes whitespace. / CSV 导入裁剪了备注空白。");
        Assert(putty.ExecutableOverride.Length == 0 && putty.CustomArguments.Length == 0, "CSV import retained executable launch configuration. / CSV 导入保留了可执行启动配置。");

        ImportResult trustedResult = await service.ImportCsvAsync(csvPath, trustLaunchConfiguration: true);
        ConnectionProfile trustedPutty = FindConnection(trustedResult, "Putty");
        Assert(trustedPutty.ExecutableOverride == "powershell.exe" && trustedPutty.CustomArguments.Contains("whoami", StringComparison.Ordinal), "Trusted CSV import did not preserve launch configuration. / 可信 CSV 导入未保留启动配置。");
        Assert(FindConnection(trustedResult, "Legacy custom").CustomArguments == "-ssh {host}", "Trusted legacy custom arguments were not preserved. / 可信旧版自定义参数未保留。");

        string unrelatedPath = System.IO.Path.Combine(scope.Path, "unrelated.csv");
        await File.WriteAllTextAsync(unrelatedPath, "First,Second\r\nvalue,value");
        await AssertThrowsAsync<InvalidDataException>(() => service.ImportCsvAsync(unrelatedPath), "Unrelated CSV headers were accepted. / 无关 CSV 表头被接受。");

        string missingTypePath = System.IO.Path.Combine(scope.Path, "missing-type.csv");
        await File.WriteAllTextAsync(missingTypePath, "Name,Host\r\nserver,server.example");
        await AssertThrowsAsync<InvalidDataException>(() => service.ImportCsvAsync(missingTypePath), "CSV without a Type header was accepted. / 缺少类型表头的 CSV 被接受。");
    }

    /// <summary>
    /// Verifies CSV export neutralizes every supported spreadsheet formula prefix. / 验证 CSV 导出中所有支持的电子表格公式前缀均被中和。
    /// </summary>
    private static async Task TestCsvFormulaProtectionAsync()
    {
        using TemporaryDirectoryScope scope = new();
        string csvPath = System.IO.Path.Combine(scope.Path, "safe.csv");
        Guid groupId = Guid.NewGuid();
        AppDataDocument document = new()
        {
            Groups = [new ConnectionGroup { Id = groupId, Name = "'=group" }],
            Connections =
            [
                new ConnectionProfile
                {
                    Name = "=cmd",
                    GroupId = groupId,
                    Type = ConnectionType.Custom,
                    Protocol = "+protocol",
                    Host = "-host",
                    Port = 0,
                    Username = "@user",
                    Password = "\tsecret",
                    Notes = "\rnote",
                    CustomArguments = "\nargs"
                }
            ]
        };
        await new WorkspaceTransferService().ExportCsvAsync(document, csvPath, includeSecrets: true);
        IReadOnlyList<IReadOnlyList<string>> decoded = CsvCodec.Decode(await File.ReadAllTextAsync(csvPath));
        IReadOnlyList<string> row = decoded[1];
        Assert(row[0] == "'=cmd" && row[3] == "'+protocol" && row[4] == "'-host" && row[6] == "'@user" && row[7] == "'\tsecret" && row[10] == "'\rnote" && row[13] == "'\nargs", "CSV spreadsheet formula protection is incomplete. / CSV 电子表格公式防护不完整。");
        Assert(row[1] == "''=group" && row[14] == "2", "CSV reversible escape or format marker is missing. / CSV 可逆转义或格式标记缺失。");

        ImportResult roundTrip = await new WorkspaceTransferService().ImportCsvAsync(csvPath, trustLaunchConfiguration: true);
        ConnectionProfile restored = roundTrip.Connections.Single();
        Assert(restored.Name == "=cmd" && restored.Protocol == "+protocol" && restored.Host == "-host" && restored.Username == "@user" && restored.Password == "\tsecret" && restored.Notes == "\rnote" && restored.CustomArguments == "\nargs", "CSV spreadsheet protection was not losslessly reversible. / CSV 电子表格防护未能无损逆转。");
        Assert(roundTrip.Groups.Single().Name == "'=group", "CSV leading-apostrophe escaping was ambiguous. / CSV 首撤号转义存在歧义。");
    }

    /// <summary>
    /// Verifies CSV version 2 preserves type-specific client options and every extended RDP field.
    /// / 验证 CSV 版本 2 会保留类型专属客户端选项及全部扩展 RDP 字段。
    /// </summary>
    private static async Task TestCsvExtendedOptionsRoundTripAsync()
    {
        using TemporaryDirectoryScope scope = new();
        string csvPath = System.IO.Path.Combine(scope.Path, "extended.csv");
        AppDataDocument document = new()
        {
            Connections =
            [
                new ConnectionProfile
                {
                    Name = "RustDesk extended",
                    Type = ConnectionType.RustDesk,
                    Protocol = "connect",
                    Host = "123456789",
                    Port = 0,
                    Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["server"] = "relay.example:21118",
                        ["serverKey"] = "abc+/=",
                        ["forceRelay"] = "true"
                    }
                },
                new ConnectionProfile
                {
                    Name = "RDP extended",
                    Type = ConnectionType.RemoteDesktop,
                    Protocol = "rdp",
                    Host = "rdp.example",
                    Port = 3389,
                    Rdp = new RdpOptions
                    {
                        DisplayConnectionBar = false,
                        EnableCompression = false,
                        KeyboardHookMode = RdpKeyboardHookMode.Remote,
                        RedirectComPorts = true,
                        RedirectPosDevices = true,
                        RedirectCameras = true
                    }
                }
            ]
        };

        WorkspaceTransferService service = new();
        await service.ExportCsvAsync(document, csvPath, includeSecrets: false);
        ImportResult trusted = await service.ImportCsvAsync(csvPath, trustLaunchConfiguration: true);
        ConnectionProfile rustDesk = FindConnection(trusted, "RustDesk extended");
        Assert(rustDesk.Options["server"] == "relay.example:21118" && rustDesk.Options["serverKey"] == "abc+/=" && rustDesk.Options["forceRelay"] == "true",
            "CSV round-trip lost RustDesk options. / CSV 往返丢失了 RustDesk 选项。");
        RdpOptions rdp = FindConnection(trusted, "RDP extended").Rdp;
        Assert(!rdp.DisplayConnectionBar && !rdp.EnableCompression && rdp.KeyboardHookMode == RdpKeyboardHookMode.Remote && rdp.RedirectComPorts && rdp.RedirectPosDevices && rdp.RedirectCameras,
            "CSV round-trip lost extended RDP options. / CSV 往返丢失了扩展 RDP 选项。");

        ImportResult untrusted = await service.ImportCsvAsync(csvPath);
        ConnectionProfile sanitizedRustDesk = FindConnection(untrusted, "RustDesk extended");
        RdpOptions sanitizedRdp = FindConnection(untrusted, "RDP extended").Rdp;
        Assert(sanitizedRustDesk.Options.Count == 0 &&
               !sanitizedRdp.DisplayConnectionBar && !sanitizedRdp.EnableCompression && sanitizedRdp.KeyboardHookMode == RdpKeyboardHookMode.Remote && !sanitizedRdp.RedirectComPorts && !sanitizedRdp.RedirectPosDevices && !sanitizedRdp.RedirectCameras,
            "CSV import did not preserve harmless RDP preferences or sanitize new device redirections. / CSV 导入未保留无害 RDP 偏好或未清理新增设备重定向。");

        string versionOnePath = System.IO.Path.Combine(scope.Path, "version-one.csv");
        IReadOnlyList<IReadOnlyList<string?>> versionOneRows =
        [
            ["Name", "Type", "Host", "RemoteHubStudioCsvVersion", "OptionsJson", "RdpOptionsJson"],
            ["v1 ignores extensions", "RustDesk", "123456789", "1", "{\"server\":\"hidden.example\"}", "{\"redirectDrives\":true}"]
        ];
        await File.WriteAllTextAsync(versionOnePath, CsvCodec.Encode(versionOneRows));
        ImportResult versionOne = await service.ImportCsvAsync(versionOnePath, trustLaunchConfiguration: true);
        Assert(versionOne.Connections.Single().Options.Count == 0 && !versionOne.Connections.Single().Rdp.RedirectDrives,
            "CSV version 1 restored version 2 extension columns. / CSV 版本 1 恢复了仅属于版本 2 的扩展列。");

        string versionlessPath = System.IO.Path.Combine(scope.Path, "versionless-extensions.csv");
        IReadOnlyList<IReadOnlyList<string?>> versionlessRows =
        [
            ["Name", "Type", "Host", "OptionsJson", "RdpOptionsJson"],
            ["Legacy ignores extensions", "RustDesk", "123456789", "{\"server\":\"hidden.example\"}", "{\"redirectDrives\":true}"]
        ];
        await File.WriteAllTextAsync(versionlessPath, CsvCodec.Encode(versionlessRows));
        ImportResult versionless = await service.ImportCsvAsync(versionlessPath, trustLaunchConfiguration: true);
        Assert(versionless.Connections.Single().Options.Count == 0 && !versionless.Connections.Single().Rdp.RedirectDrives,
            "Versionless CSV restored native extension columns. / 无版本 CSV 恢复了原生扩展列。");
    }

    /// <summary>
    /// Gets one imported connection by its test name. / 按测试名称获取一个导入连接。
    /// </summary>
    /// <param name="result">CSV import result. / CSV 导入结果。</param>
    /// <param name="name">Connection name. / 连接名称。</param>
    /// <returns>The matching connection. / 匹配的连接。</returns>
    private static ConnectionProfile FindConnection(ImportResult result, string name)
    {
        return result.Connections.Single(connection => connection.Name == name);
    }

    /// <summary>
    /// Writes invalid JSON and verifies portable import rejects it. / 写入无效 JSON 并验证便携导入会拒绝它。
    /// </summary>
    /// <param name="service">Transfer service. / 传输服务。</param>
    /// <param name="directory">Test directory. / 测试目录。</param>
    /// <param name="fileName">Test file name. / 测试文件名。</param>
    /// <param name="json">Invalid JSON content. / 无效 JSON 内容。</param>
    /// <param name="message">Failure message. / 失败消息。</param>
    private static async Task AssertInvalidImportAsync(WorkspaceTransferService service, string directory, string fileName, string json, string message)
    {
        string path = System.IO.Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(path, json);
        await AssertThrowsAsync<InvalidDataException>(() => service.ImportJsonAsync(path), message);
    }

    /// <summary>
    /// Verifies an asynchronous action throws the requested exception type. / 验证异步操作抛出指定异常类型。
    /// </summary>
    /// <typeparam name="TException">Expected exception type. / 预期异常类型。</typeparam>
    /// <param name="action">Asynchronous action. / 异步操作。</param>
    /// <param name="message">Failure message. / 失败消息。</param>
    private static async Task AssertThrowsAsync<TException>(Func<Task> action, string message)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Verifies expiration boundaries around today and the warning threshold. / 验证今天及预警阈值附近的到期边界。
    /// </summary>
    private static void TestExpirationRules()
    {
        ExpirationService service = new();
        DateTime today = new(2026, 9, 2);
        Assert(service.Classify(new ConnectionProfile(), today, 30) == ExpirationState.NotSet, "Unset expiration failed. / 未设置到期日的分类失败。");
        Assert(service.Classify(new ConnectionProfile { ExpiresOn = today.AddDays(-1) }, today, 30) == ExpirationState.Expired, "Expired state failed. / 已到期状态失败。");
        Assert(service.Classify(new ConnectionProfile { ExpiresOn = today }, today, 30) == ExpirationState.Today, "Today state failed. / 今日到期状态失败。");
        Assert(service.Classify(new ConnectionProfile { ExpiresOn = today.AddDays(30) }, today, 30) == ExpirationState.ExpiringSoon, "Warning boundary failed. / 预警边界失败。");
        Assert(service.Classify(new ConnectionProfile { ExpiresOn = today.AddDays(31) }, today, 30) == ExpirationState.Healthy, "Healthy state failed. / 正常状态失败。");
    }

    /// <summary>
    /// Verifies launch plans use argument tokens, pass passwords by default, and honor an explicit block. / 验证启动计划使用独立参数、默认传递密码并尊重明确禁用。
    /// </summary>
    private static void TestLaunchPlans()
    {
        using TemporaryDirectoryScope scope = new();
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Test executable path is unavailable. / 测试程序路径不可用。");
        ConnectionProfile profile = new()
        {
            Name = "SSH test",
            Type = ConnectionType.Putty,
            Protocol = "ssh",
            Host = "server.example",
            Port = 22,
            Username = "user name",
            Password = "top-secret",
            ExecutableOverride = executable
        };
        ConnectionLaunchService service = new(scope.Path);
        AppSettings defaultSettings = new();
        LaunchPlan defaultPlan = service.CreatePlan(profile, defaultSettings);
        Assert(defaultPlan.Arguments.Contains("top-secret") && defaultPlan.ContainsSensitiveData, "Default plan did not pass or mark the saved password. / 默认启动计划未传递或标记已保存密码。");
        Assert(defaultPlan.Arguments.Contains("user name"), "Username was not preserved as one argument. / 用户名未作为单个参数保留。");

        ConnectionProfile secureCrtTelnet = new()
        {
            Name = "SecureCRT Telnet",
            Type = ConnectionType.SecureCrt,
            Protocol = "telnet",
            Host = "telnet.example",
            Port = 23,
            Username = "must-not-be-used",
            Password = "must-not-be-used-secret",
            ExecutableOverride = executable
        };
        LaunchPlan secureCrtTelnetPlan = service.CreatePlan(secureCrtTelnet, defaultSettings);
        Assert(!secureCrtTelnetPlan.Arguments.Any(argument =>
                   argument.Contains("must-not-be-used", StringComparison.Ordinal)),
            "SecureCRT Telnet leaked SSH-only username or password arguments. / SecureCRT Telnet 泄漏了仅供 SSH 使用的用户名或密码参数。");

        AppSettings passwordBlockedSettings = new() { AllowPasswordInCommandLine = false };
        LaunchPlan blockedPlan = service.CreatePlan(profile, passwordBlockedSettings);
        Assert(!blockedPlan.Arguments.Contains("top-secret") && !blockedPlan.ContainsSensitiveData, "Explicitly disabled password passing still exposed a password. / 明确关闭密码传递后仍暴露了密码。");

        ConnectionProfile custom = new()
        {
            Name = "Custom",
            Type = ConnectionType.Custom,
            Host = "host & whoami",
            Port = 443,
            ExecutableOverride = executable,
            CustomArguments = "--host \"{host}\" --port {port}"
        };
        LaunchPlan customPlan = service.CreatePlan(custom, defaultSettings);
        int hostIndex = customPlan.Arguments.ToList().IndexOf("--host");
        Assert(hostIndex >= 0 && customPlan.Arguments[hostIndex + 1] == "host & whoami", "Custom host token was split or executed as shell text. / 自定义主机参数被拆分或作为外壳文本执行。");
        Assert(!customPlan.CreateStartInfo().UseShellExecute, "Launch plan enabled shell execution. / 启动计划启用了外壳执行。");

        string keyPath = System.IO.Path.Combine(scope.Path, "test key.ppk");
        File.WriteAllText(keyPath, "test-key");
        custom.PrivateKeyPath = keyPath;
        custom.CustomArguments = "--key {key}";
        LaunchPlan customKeyPlan = service.CreatePlan(custom, defaultSettings);
        Assert(customKeyPlan.Arguments.Count == 2 && customKeyPlan.Arguments[1] == keyPath, "Custom {key} placeholder did not resolve the private-key path as one token. / 自定义 {key} 占位符未将私钥路径解析为单个参数。");

        ConnectionProfile vnc = new()
        {
            Name = "VNC",
            Type = ConnectionType.Vnc,
            Protocol = "tightvnc",
            Host = "server.example",
            Port = 5900,
            ExecutableOverride = executable
        };
        LaunchPlan tightVncPlan = service.CreatePlan(vnc, defaultSettings);
        Assert(tightVncPlan.Arguments[0] == "server.example::5900", "TightVNC treated a TCP port as a display number. / TightVNC 将 TCP 端口当作了显示器编号。");
        vnc.Protocol = "ultravnc";
        LaunchPlan ultraVncPlan = service.CreatePlan(vnc, defaultSettings);
        Assert(ultraVncPlan.Arguments.Count >= 2 && ultraVncPlan.Arguments[0] == "-connect" && ultraVncPlan.Arguments[1] == "server.example::5900", "UltraVNC explicit-port arguments are invalid. / UltraVNC 显式端口参数无效。");
        vnc.Protocol = "realvnc";
        LaunchPlan realVncPlan = service.CreatePlan(vnc, defaultSettings);
        Assert(realVncPlan.Arguments[0] == "server.example::5900", "RealVNC treated a TCP port as a display number. / RealVNC 将 TCP 端口当作了显示器编号。");
        vnc.Protocol = "rdp";
        LaunchPlan defaultVncPlan = service.CreatePlan(vnc, defaultSettings);
        Assert(defaultVncPlan.Arguments.Count == 1 && defaultVncPlan.Arguments[0] == "server.example::5900", "VNC legacy/default protocol did not consistently select TightVNC. / VNC 旧版或缺省协议未一致选择 TightVNC。");

        string tightViewer = System.IO.Path.Combine(scope.Path, "tight-viewer.exe");
        string realViewer = System.IO.Path.Combine(scope.Path, "real-viewer.exe");
        string ultraViewer = System.IO.Path.Combine(scope.Path, "ultra-viewer.exe");
        File.WriteAllText(tightViewer, string.Empty);
        File.WriteAllText(realViewer, string.Empty);
        File.WriteAllText(ultraViewer, string.Empty);
        vnc.ExecutableOverride = string.Empty;
        AppSettings splitVncSettings = new()
        {
            ToolPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["vnc-tightvnc"] = tightViewer,
                ["vnc-realvnc"] = realViewer,
                ["vnc-ultravnc"] = ultraViewer
            }
        };
        vnc.Protocol = "tightvnc";
        Assert(service.CreatePlan(vnc, splitVncSettings).ExecutablePath == tightViewer, "TightVNC did not use its dedicated executable. / TightVNC 未使用其专用程序。");
        vnc.Protocol = "realvnc";
        Assert(service.CreatePlan(vnc, splitVncSettings).ExecutablePath == realViewer, "RealVNC did not use its dedicated executable. / RealVNC 未使用其专用程序。");
        vnc.Protocol = "ultravnc";
        Assert(service.CreatePlan(vnc, splitVncSettings).ExecutablePath == ultraViewer, "UltraVNC did not use its dedicated executable. / UltraVNC 未使用其专用程序。");

        ConnectionProfile legacyWebDav = new()
        {
            Name = "Legacy WebDAV",
            Type = ConnectionType.WinScp,
            Protocol = "https",
            Host = "placeholder.example",
            Port = 22,
            Username = "user name",
            Password = "webdav-secret",
            ExecutableOverride = executable,
            Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["dav_address"] = "https://dav.example:8443/web dav/"
            }
        };
        LaunchPlan blockedWebDavPlan = service.CreatePlan(legacyWebDav, passwordBlockedSettings);
        Assert(blockedWebDavPlan.Arguments.Single() == "davs://user%20name@dav.example:8443/web%20dav", "Explicitly blocked WinSCP WebDAV password passing changed the legacy address incorrectly. / 明确关闭 WinSCP WebDAV 密码传递后旧版地址转换错误。");
        LaunchPlan defaultWebDavPlan = service.CreatePlan(legacyWebDav, defaultSettings);
        Assert(defaultWebDavPlan.Arguments.Single() == "davs://user%20name:webdav-secret@dav.example:8443/web%20dav" && defaultWebDavPlan.ContainsSensitiveData, "Default WinSCP WebDAV credential handling is invalid. / 默认 WinSCP WebDAV 凭据处理无效。");

        ConnectionProfile radmin = new()
        {
            Name = "Radmin",
            Type = ConnectionType.Radmin,
            Protocol = "file",
            Host = "radmin.example",
            Port = 4899,
            ExecutableOverride = executable,
            Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["encrypt"] = "true",
                ["fullscreen"] = "true",
                ["noFullKeyboardControl"] = "true",
                ["colorDepth"] = "invalid",
                ["updates"] = "999"
            }
        };
        LaunchPlan radminFilePlan = service.CreatePlan(radmin, defaultSettings);
        Assert(radminFilePlan.Arguments.Contains("/file") && radminFilePlan.Arguments.Contains("/encrypt") &&
               !radminFilePlan.Arguments.Contains("/fullscreen") &&
               !radminFilePlan.Arguments.Contains("/nofullkbcontrol") &&
               !radminFilePlan.Arguments.Any(argument => argument.StartsWith("/updates:", StringComparison.Ordinal)) &&
               !radminFilePlan.Arguments.Any(argument => argument is "/24bpp" or "/16bpp" or "/8bpp" or "/4bpp" or "/2bpp" or "/1bpp"),
            "Radmin file mode received display-only arguments or lost encryption. / Radmin 文件模式收到了显示专属参数或丢失了加密参数。");

        radmin.Protocol = "view";
        radmin.Options["colorDepth"] = "16bpp";
        radmin.Options["updates"] = "45";
        LaunchPlan radminViewPlan = service.CreatePlan(radmin, defaultSettings);
        Assert(radminViewPlan.Arguments.Contains("/noinput") &&
               radminViewPlan.Arguments.Contains("/fullscreen") &&
               radminViewPlan.Arguments.Contains("/nofullkbcontrol") &&
               radminViewPlan.Arguments.Contains("/16bpp") &&
               radminViewPlan.Arguments.Contains("/updates:45"),
            "Radmin view mode omitted its display arguments. / Radmin 查看模式遗漏了显示参数。");

        ConnectionProfile rdp = new()
        {
            Name = "RDP",
            Type = ConnectionType.RemoteDesktop,
            Protocol = "rdp",
            Host = "server.example",
            Port = 3389,
            Username = "administrator",
            Password = "rdp-secret",
            ExecutableOverride = executable
        };
        LaunchPlan standardRdpPlan = service.CreatePlan(rdp, defaultSettings);
        Assert(!standardRdpPlan.Arguments.Contains("/console") && !standardRdpPlan.Arguments.Contains("/admin"), "Standard RDP launch used an administrative switch. / 标准 RDP 启动使用了管理会话开关。");
        Assert(!standardRdpPlan.Arguments.Contains("/prompt") && standardRdpPlan.ContainsSensitiveData, "Default RDP launch prompted for credentials or failed to mark password data. / 默认 RDP 启动仍提示凭据或未标记密码数据。");
        string standardRdpFile = File.ReadAllText(standardRdpPlan.TemporaryFiles.Single());
        Assert(standardRdpFile.Contains("prompt for credentials:i:0", StringComparison.Ordinal), "Default RDP file did not disable the credential prompt. / 默认 RDP 文件未关闭凭据提示。");
        Assert(!standardRdpFile.Contains("rdp-secret", StringComparison.Ordinal), "RDP file exposed the password in plaintext. / RDP 文件以明文暴露了密码。");
        string[] passwordLines = standardRdpFile
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("password 51:b:", StringComparison.Ordinal))
            .ToArray();
        Assert(passwordLines.Length == 1, "Default RDP file did not contain exactly one password 51 value. / 默认 RDP 文件未包含且仅包含一个 password 51 值。");
        byte[] protectedPassword = Convert.FromHexString(passwordLines[0]["password 51:b:".Length..]);
        byte[] unprotectedPassword = [];
        try
        {
            unprotectedPassword = ProtectedData.Unprotect(protectedPassword, null, DataProtectionScope.CurrentUser);
            Assert(Encoding.Unicode.GetString(unprotectedPassword) == "rdp-secret", "RDP password 51 did not round-trip through CurrentUser DPAPI. / RDP password 51 未能通过 CurrentUser DPAPI 正确还原。");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedPassword);
            CryptographicOperations.ZeroMemory(unprotectedPassword);
        }

        LaunchPlan blockedRdpPlan = service.CreatePlan(rdp, passwordBlockedSettings);
        string blockedRdpFile = File.ReadAllText(blockedRdpPlan.TemporaryFiles.Single());
        Assert(blockedRdpPlan.Arguments.Contains("/prompt") && !blockedRdpPlan.ContainsSensitiveData, "Explicitly blocked RDP password passing did not force a safe prompt. / 明确关闭 RDP 密码传递后未强制安全提示。");
        Assert(blockedRdpFile.Contains("prompt for credentials:i:1", StringComparison.Ordinal) &&
               !blockedRdpFile.Contains("password 51:b:", StringComparison.Ordinal),
            "Explicitly blocked RDP file still carried password data or omitted the prompt. / 明确关闭后的 RDP 文件仍携带密码数据或缺少提示。");

        rdp.Rdp.PromptForCredentials = true;
        LaunchPlan promptedRdpPlan = service.CreatePlan(rdp, defaultSettings);
        string promptedRdpFile = File.ReadAllText(promptedRdpPlan.TemporaryFiles.Single());
        Assert(promptedRdpPlan.Arguments.Contains("/prompt") &&
               promptedRdpFile.Contains("prompt for credentials:i:1", StringComparison.Ordinal),
            "Connection-level RDP credential prompting did not override the enabled default. / 连接级 RDP 凭据提示未覆盖已开启的默认传密设置。");
        rdp.Rdp.PromptForCredentials = false;

        rdp.Rdp.AdministrativeSession = true;
        LaunchPlan administrativeRdpPlan = service.CreatePlan(rdp, defaultSettings);
        Assert(administrativeRdpPlan.Arguments.Contains("/admin"), "Administrative RDP launch omitted /admin. / 管理 RDP 启动缺少 /admin。");

        rdp.Rdp.AdministrativeSession = false;
        rdp.Rdp.DisplayConnectionBar = false;
        rdp.Rdp.EnableCompression = false;
        rdp.Rdp.KeyboardHookMode = RdpKeyboardHookMode.Remote;
        rdp.Rdp.RedirectComPorts = true;
        rdp.Rdp.RedirectPosDevices = true;
        rdp.Rdp.RedirectCameras = true;
        LaunchPlan extendedRdpPlan = service.CreatePlan(rdp, defaultSettings);
        string rdpFile = File.ReadAllText(extendedRdpPlan.TemporaryFiles.Single());
        Assert(rdpFile.Contains("displayconnectionbar:i:0", StringComparison.Ordinal) &&
               rdpFile.Contains("compression:i:0", StringComparison.Ordinal) &&
               rdpFile.Contains("keyboardhook:i:1", StringComparison.Ordinal) &&
               rdpFile.Contains("redirectcomports:i:1", StringComparison.Ordinal) &&
               rdpFile.Contains("redirectposdevices:i:1", StringComparison.Ordinal) &&
               rdpFile.Contains("camerastoredirect:s:*", StringComparison.Ordinal),
            "Extended RDP options were not written to the generated file. / 扩展 RDP 选项未写入生成文件。");

        ConnectionProfile toDesk = new()
        {
            Name = "ToDesk",
            Type = ConnectionType.ToDesk,
            Protocol = "device",
            Host = "123 456 789",
            ExecutableOverride = executable
        };
        LaunchPlan toDeskPlan = service.CreatePlan(toDesk, defaultSettings);
        Assert(toDeskPlan.Arguments.Contains("123456789"), "Legacy ToDesk protocol or device ID did not normalize. / 旧 ToDesk 协议或设备 ID 未正常规范化。");

        ConnectionProfile rustDesk = new()
        {
            Name = "RustDesk",
            Type = ConnectionType.RustDesk,
            Protocol = "connect",
            Host = "123 456 789",
            Port = 0,
            Password = "rust-secret",
            ExecutableOverride = executable
        };
        LaunchPlan blockedRustDeskPlan = service.CreatePlan(rustDesk, passwordBlockedSettings);
        Assert(blockedRustDeskPlan.Arguments.SequenceEqual(["--connect", "123456789"]), "RustDesk blocked-password launch shape is invalid or failed to normalize a displayed ID. / RustDesk 禁止传密后的启动参数无效或未规范化显示格式 ID。");
        LaunchPlan defaultRustDeskPlan = service.CreatePlan(rustDesk, defaultSettings);
        Assert(defaultRustDeskPlan.Arguments.SequenceEqual(["--connect", "123456789", "--password", "rust-secret"]) && defaultRustDeskPlan.ContainsSensitiveData, "RustDesk default password launch shape is invalid. / RustDesk 默认密码启动参数无效。");
        rustDesk.Protocol = "file";
        LaunchPlan legacyRustDeskModePlan = service.CreatePlan(rustDesk, defaultSettings);
        Assert(legacyRustDeskModePlan.Arguments[0] == "--file-transfer",
            "RustDesk launcher did not normalize a legacy mode alias. / RustDesk 启动器未规范化旧模式别名。");
        foreach (string mode in ConnectionType.RustDesk.GetProtocols())
        {
            rustDesk.Protocol = mode;
            LaunchPlan modePlan = service.CreatePlan(rustDesk, defaultSettings);
            Assert(modePlan.Arguments[0] == $"--{mode}", $"RustDesk mode '{mode}' was not preserved. / RustDesk 模式“{mode}”未保留。");
        }

        rustDesk.Protocol = "connect";
        rustDesk.Options["server"] = "relay.example:21118";
        rustDesk.Options["server_key"] = "abc+/=";
        rustDesk.Options["relay"] = "true";
        LaunchPlan keyedRustDeskPlan = service.CreatePlan(rustDesk, defaultSettings);
        Assert(keyedRustDeskPlan.Arguments.SequenceEqual(["--connect", "123456789@relay.example:21118?key=abc%2B%2F%3D", "--relay"]) && !keyedRustDeskPlan.ContainsSensitiveData, "RustDesk self-hosted target, relay, or key/password compatibility handling is invalid. / RustDesk 自建目标、中继或公钥/密码兼容处理无效。");
        rustDesk.Options["server"] = "unsafe?server";
        AssertThrows<LaunchValidationException>(() => service.CreatePlan(rustDesk, defaultSettings), "RustDesk unsafe server syntax was accepted. / RustDesk 接受了不安全的服务器语法。");
        rustDesk.Options.Remove("server");
        AssertThrows<LaunchValidationException>(() => service.CreatePlan(rustDesk, defaultSettings), "RustDesk accepted a server key without a server address. / RustDesk 接受了没有服务器地址的公钥。");

        ConnectionProfile mobaIpv6 = new()
        {
            Name = "Moba IPv6",
            Type = ConnectionType.MobaXterm,
            Protocol = "telnet",
            Host = "2001:db8::25",
            Port = 23,
            ExecutableOverride = executable
        };
        LaunchPlan mobaIpv6Plan = service.CreatePlan(mobaIpv6, defaultSettings);
        Assert(mobaIpv6Plan.Arguments.Count >= 2 && !mobaIpv6Plan.Arguments[1].Contains(" -4 ", StringComparison.Ordinal) && mobaIpv6Plan.Arguments[1].Contains("2001:db8::25", StringComparison.Ordinal), "MobaXterm Telnet forced IPv4 for an IPv6 target. / MobaXterm Telnet 对 IPv6 目标强制使用了 IPv4。");

        ConnectionProfile remoteExecutable = new()
        {
            Name = "Remote executable",
            Type = ConnectionType.Custom,
            Host = "host",
            ExecutableOverride = @"\\server\share\viewer.exe"
        };
        AssertThrows<LaunchValidationException>(() => service.CreatePlan(remoteExecutable, defaultSettings), "UNC executable override was accepted. / UNC 可执行程序覆盖被接受。");

        string batchPath = System.IO.Path.Combine(scope.Path, "viewer.cmd");
        File.WriteAllText(batchPath, "@exit /b 0");
        remoteExecutable.ExecutableOverride = batchPath;
        AssertThrows<LaunchValidationException>(() => service.CreatePlan(remoteExecutable, defaultSettings), "Batch-script executable override was accepted. / 批处理脚本程序覆盖被接受。");
    }

    /// <summary>
    /// Verifies one lifetime owner deletes short-process artifacts and safely sweeps only stale owned RDP files. / 验证单一生命周期所有者会删除短进程文件，并仅安全清理陈旧自有 RDP 文件。
    /// </summary>
    private static async Task TestProcessCleanupAsync()
    {
        using TemporaryDirectoryScope scope = new();
        string rdpDirectory = System.IO.Path.Combine(scope.Path, "rdp");
        Directory.CreateDirectory(rdpDirectory);
        string staleFile = System.IO.Path.Combine(rdpDirectory, $"stale-{Guid.NewGuid():N}.rdp");
        string recentFile = System.IO.Path.Combine(rdpDirectory, $"recent-{Guid.NewGuid():N}.rdp");
        string unrelatedFile = System.IO.Path.Combine(rdpDirectory, "manual.rdp");
        string siblingFile = System.IO.Path.Combine(scope.Path, $"sibling-{Guid.NewGuid():N}.rdp");
        File.WriteAllText(staleFile, "stale");
        File.WriteAllText(recentFile, "recent");
        File.WriteAllText(unrelatedFile, "manual");
        File.WriteAllText(siblingFile, "sibling");
        File.SetLastWriteTimeUtc(staleFile, DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(unrelatedFile, DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(siblingFile, DateTime.UtcNow.AddDays(-2));

        ConnectionLaunchService service = new(rdpDirectory);
        Assert(!File.Exists(staleFile), "Stale owned RDP file was not swept. / 陈旧自有 RDP 文件未被清理。");
        Assert(File.Exists(recentFile) && File.Exists(unrelatedFile) && File.Exists(siblingFile), "RDP stale cleanup escaped its age, name, or directory boundary. / RDP 陈旧清理越过了时间、命名或目录边界。");

        string processArtifact = System.IO.Path.Combine(scope.Path, "short-process-artifact.rdp");
        File.WriteAllText(processArtifact, "temporary");
        string quickExecutable = System.IO.Path.Combine(Environment.SystemDirectory, "where.exe");
        LaunchPlan plan = new(quickExecutable, ["/Q", "where.exe"], [processArtifact]);
        service.Start(plan);
        await WaitForFileDeletionAsync(processArtifact, TimeSpan.FromSeconds(5));
        Assert(!File.Exists(processArtifact), "Short-lived process artifact was not deleted. / 短生命周期进程文件未被删除。");
    }

    /// <summary>
    /// Verifies device-ID and command profiles are reported as not applicable to ICMP checks. / 验证设备 ID 与命令配置会被标记为不适用 ICMP 检测。
    /// </summary>
    private static async Task TestStatusRulesAsync()
    {
        ConnectionStatusService service = new();
        ConnectionStatus toDesk = await service.CheckAsync(
            new ConnectionProfile { Type = ConnectionType.ToDesk, Host = "123 456 789" },
            100);
        ConnectionStatus custom = await service.CheckAsync(
            new ConnectionProfile { Type = ConnectionType.Custom, Host = "arbitrary-token" },
            100);
        ConnectionStatus rustDesk = await service.CheckAsync(
            new ConnectionProfile { Type = ConnectionType.RustDesk, Host = "123456789" },
            100);
        Assert(toDesk.State == ReachabilityState.NotApplicable && custom.State == ReachabilityState.NotApplicable && rustDesk.State == ReachabilityState.NotApplicable, "Non-network profiles were treated as invalid or unreachable. / 非网络地址配置被当作无效或不可达。");
    }

    /// <summary>
    /// Verifies atomic storage, CRUD, plaintext default, and DPAPI opt-in round-trips. / 验证原子存储、CRUD、默认明文及 DPAPI 主动启用后的往返。
    /// </summary>
    private static async Task TestPersistenceAndEncryptionAsync()
    {
        using TemporaryDirectoryScope scope = new();
        AppDataPaths paths = new(scope.Path);
        JsonWorkspaceRepository repository = new(paths, new DpapiCurrentUserProtector(Encoding.UTF8.GetBytes("RemoteHubStudio.Tests")));
        WorkspaceService workspace = new(repository);
        await workspace.InitializeAsync();
        Assert(workspace.GetSettings().AllowPasswordInCommandLine, "A new workspace did not enable automatic password passing by default. / 新工作区未默认开启密码自动传递。");

        await workspace.AddConnectionAsync(new ConnectionProfile
        {
            Name = "Production RDP",
            Type = ConnectionType.RemoteDesktop,
            Host = "10.0.0.8",
            Port = 3389,
            Username = "administrator",
            Password = "storage-secret",
            Rdp = new RdpOptions
            {
                DisplayConnectionBar = false,
                EnableCompression = false,
                KeyboardHookMode = RdpKeyboardHookMode.Remote,
                RedirectComPorts = true,
                RedirectPosDevices = true,
                RedirectCameras = true
            }
        });
        RdpOptions storedRdp = workspace.GetConnections().Single().Rdp;
        Assert(!storedRdp.DisplayConnectionBar && !storedRdp.EnableCompression && storedRdp.KeyboardHookMode == RdpKeyboardHookMode.Remote && storedRdp.RedirectComPorts && storedRdp.RedirectPosDevices && storedRdp.RedirectCameras,
            "Workspace cloning lost extended RDP options. / 工作区复制丢失了扩展 RDP 选项。");

        string plaintextFile = await File.ReadAllTextAsync(paths.WorkspaceFilePath);
        Assert(plaintextFile.Contains("storage-secret", StringComparison.Ordinal), "Plaintext default was not represented honestly. / 默认明文模式未如实保存。");

        AppSettings protectedSettings = workspace.GetSettings();
        protectedSettings.EncryptionEnabled = true;
        await workspace.UpdateSettingsAsync(protectedSettings);
        string encryptedFile = await File.ReadAllTextAsync(paths.WorkspaceFilePath);
        Assert(!encryptedFile.Contains("storage-secret", StringComparison.Ordinal), "Encrypted file contains a plaintext password. / 加密文件仍包含明文密码。");
        Assert(encryptedFile.Contains("dpapi-current-user", StringComparison.Ordinal), "Encrypted envelope does not identify DPAPI. / 加密信封未标识 DPAPI。");
        string protectedBackup = await File.ReadAllTextAsync(paths.BackupFilePath);
        Assert(!protectedBackup.Contains("storage-secret", StringComparison.Ordinal), "Encryption opt-in left a plaintext backup. / 启用加密后仍留有明文备份。");
        Assert(protectedBackup.Contains("dpapi-current-user", StringComparison.Ordinal), "Encryption opt-in did not protect the backup. / 启用加密后未保护备份。");

        WorkspaceService reopened = new(new JsonWorkspaceRepository(paths, new DpapiCurrentUserProtector(Encoding.UTF8.GetBytes("RemoteHubStudio.Tests"))));
        await reopened.InitializeAsync();
        ConnectionProfile reopenedConnection = reopened.GetConnections().Single();
        Assert(reopenedConnection.Username == "administrator" && reopenedConnection.Password == "storage-secret",
            "DPAPI reload lost inline authentication values. / DPAPI 重新加载后丢失了内联认证信息。");
        Assert(reopened.GetSettings().AllowPasswordInCommandLine, "The default password-passing preference was not preserved after reload. / 默认密码传递设置在重新加载后未被保留。");

        AppSettings blockedSettings = reopened.GetSettings();
        blockedSettings.AllowPasswordInCommandLine = false;
        await reopened.UpdateSettingsAsync(blockedSettings);
        WorkspaceService reopenedWithBlock = new(new JsonWorkspaceRepository(paths, new DpapiCurrentUserProtector(Encoding.UTF8.GetBytes("RemoteHubStudio.Tests"))));
        await reopenedWithBlock.InitializeAsync();
        Assert(!reopenedWithBlock.GetSettings().AllowPasswordInCommandLine, "An explicitly disabled password-passing preference was not preserved after reload. / 明确关闭的密码传递设置在重新加载后未被保留。");
    }

    /// <summary>
    /// Verifies duplicate identifiers invalidate a primary workspace and trigger validated backup recovery. / 验证重复标识会使主工作区无效，并触发经验证的备份恢复。
    /// </summary>
    private static async Task TestWorkspaceGraphRecoveryAsync()
    {
        using TemporaryDirectoryScope scope = new();
        AppDataPaths paths = new(scope.Path);
        JsonWorkspaceRepository repository = new(paths, new DpapiCurrentUserProtector(Encoding.UTF8.GetBytes("RemoteHubStudio.Graph.Tests")));
        WorkspaceService workspace = new(repository);
        await workspace.InitializeAsync();
        ConnectionProfile committed = await workspace.AddConnectionAsync(new ConnectionProfile
        {
            Name = "Valid",
            Type = ConnectionType.RemoteDesktop,
            Protocol = "rdp",
            Host = "valid.example",
            Port = 3389
        });
        committed.Notes = "second revision";
        await workspace.UpdateConnectionAsync(committed);
        Assert(File.Exists(paths.BackupFilePath), "A valid backup was not created before graph recovery testing. / 图恢复测试前未创建有效备份。");

        Guid duplicateId = Guid.NewGuid();
        string invalidPrimary = $$"""
            {"schemaVersion":1,"settings":{},"groups":[],"connections":[
              {"id":"{{duplicateId}}","name":"One","type":"remoteDesktop","protocol":"rdp","host":"one.example","port":3389},
              {"id":"{{duplicateId}}","name":"Two","type":"remoteDesktop","protocol":"rdp","host":"two.example","port":3389}
            ]}
            """;
        await File.WriteAllTextAsync(paths.WorkspaceFilePath, invalidPrimary);

        WorkspaceLoadResult recovered = await new JsonWorkspaceRepository(
            paths,
            new DpapiCurrentUserProtector(Encoding.UTF8.GetBytes("RemoteHubStudio.Graph.Tests"))).LoadAsync();
        Assert(recovered.RecoveredFromBackup && recovered.Document.Connections.Count == 1, "Invalid workspace graph did not recover from the valid backup. / 无效工作区图未从有效备份恢复。");
        Assert(Directory.EnumerateFiles(scope.Path, "workspace.corrupt.*.json", SearchOption.TopDirectoryOnly).Any(), "Damaged primary workspace was not preserved for diagnosis. / 损坏的主工作区未保留供诊断。");
    }

    /// <summary>
    /// Verifies batch deletion uses one durable candidate, one save, one event, and publishes nothing after save failure. / 验证批量删除仅使用一个持久候选、一次保存和一次事件，且保存失败后不发布任何状态。
    /// </summary>
    private static async Task TestAtomicConnectionDeletionAsync()
    {
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        Guid remainingId = Guid.NewGuid();
        ControllableWorkspaceRepository repository = new(new AppDataDocument
        {
            Connections =
            [
                CreateTestConnection(firstId, "First", "first.example"),
                CreateTestConnection(secondId, "Second", "second.example"),
                CreateTestConnection(remainingId, "Remaining", "remaining.example")
            ]
        });
        WorkspaceService workspace = new(repository);
        await workspace.InitializeAsync();
        List<WorkspaceChangedEventArgs> changes = [];
        // Captures each published workspace mutation for atomic-event assertions. / 捕获每次发布的工作区变更，用于原子事件断言。
        workspace.Changed += (_, change) => changes.Add(change);

        int deletedCount = await workspace.DeleteConnectionsAsync([firstId, secondId, firstId, Guid.NewGuid()]);
        Assert(deletedCount == 2, "Atomic deletion returned the wrong count. / 原子删除返回了错误数量。");
        Assert(repository.SaveCount == 1, "Atomic deletion performed more than one save. / 原子删除执行了多于一次保存。");
        Assert(changes.Count == 1 && changes[0].Kind == WorkspaceChangeKind.ConnectionDeleted && changes[0].EntityId is null, "Atomic deletion did not emit exactly one batch event. / 原子删除未精确发出一次批量事件。");
        Assert(workspace.GetConnections().Select(connection => connection.Id).SequenceEqual([remainingId]), "Atomic deletion published an incorrect workspace. / 原子删除发布了错误的工作区。");
        Assert(repository.LastSaved?.Connections.Select(connection => connection.Id).SequenceEqual([remainingId]) == true, "Atomic deletion saved an incorrect candidate. / 原子删除保存了错误候选。");

        repository.FailNextSave = true;
        await AssertThrowsAsync<IOException>(
            () => workspace.DeleteConnectionsAsync([remainingId]),
            "A failed atomic save did not propagate. / 原子保存失败未传播。");
        Assert(repository.SaveCount == 2 && workspace.GetConnections().Single().Id == remainingId && changes.Count == 1, "A failed atomic save changed state or emitted an event. / 原子保存失败更改了状态或发出了事件。");

        bool deletedSingle = await workspace.DeleteConnectionAsync(remainingId);
        Assert(deletedSingle && repository.SaveCount == 3 && workspace.GetConnections().Count == 0, "Single deletion did not delegate to the atomic API. / 单条删除未委托给原子 API。");
        Assert(changes.Count == 2 && changes[1].EntityId == remainingId, "Single deletion lost its entity-specific event. / 单条删除丢失了实体特定事件。");
    }

    /// <summary>
    /// Verifies maximum-size connection merge preparation uses constant-time reference lookups instead of quadratic scans. / 验证最大规模连接合并准备使用常数时间引用查找，而不是平方级扫描。
    /// </summary>
    private static async Task TestMaximumConnectionMergeAsync()
    {
        ControllableWorkspaceRepository repository = new(new AppDataDocument());
        WorkspaceService workspace = new(repository);
        await workspace.InitializeAsync();
        List<ConnectionProfile> connections = new(WorkspaceLimits.MaximumConnectionCount);
        for (int index = 0; index < WorkspaceLimits.MaximumConnectionCount; index++)
        {
            connections.Add(CreateTestConnection(Guid.NewGuid(), $"Merge {index}", "h"));
        }

        await workspace.MergeAsync(new AppDataDocument { Connections = connections })
            .WaitAsync(TimeSpan.FromSeconds(15));
        Assert(workspace.GetConnections().Count == WorkspaceLimits.MaximumConnectionCount, "Maximum-size merge lost connections. / 最大规模合并丢失了连接。");
    }

    /// <summary>
    /// Verifies concurrent settings and bounds saves preserve both security choices and the newest bounds in either ordering. / 验证并发设置与边界保存在任一顺序下都会保留安全选项和最新边界。
    /// </summary>
    private static async Task TestConcurrentWindowBoundsPatchAsync()
    {
        ControllableWorkspaceRepository repository = new(new AppDataDocument());
        WorkspaceService workspace = new(repository);
        await workspace.InitializeAsync();

        Rectangle firstBounds = new(120, 90, 1100, 700);
        AppSettings secureSettings = workspace.GetSettings();
        secureSettings.EncryptionEnabled = true;
        secureSettings.AllowPasswordInCommandLine = true;
        repository.BlockNextSave();
        Task settingsFirst = workspace.UpdateSettingsAsync(secureSettings);
        await repository.WaitForBlockedSaveAsync();
        Task boundsSecond = workspace.UpdateWindowBoundsAsync(firstBounds);
        Assert(!boundsSecond.IsCompleted, "Bounds patch bypassed the workspace mutation gate. / 边界修补绕过了工作区变更锁。");
        repository.ReleaseBlockedSave();
        await Task.WhenAll(settingsFirst, boundsSecond);

        AppSettings settingsAfterFirstRace = workspace.GetSettings();
        Assert(settingsAfterFirstRace.EncryptionEnabled && settingsAfterFirstRace.AllowPasswordInCommandLine && settingsAfterFirstRace.WindowBounds == firstBounds, "Settings-first concurrency lost security choices or bounds. / 设置先行的并发丢失了安全选项或边界。");

        Rectangle secondBounds = new(80, 70, 980, 640);
        AppSettings staleBoundsSettings = workspace.GetSettings();
        staleBoundsSettings.EncryptionEnabled = false;
        staleBoundsSettings.AllowPasswordInCommandLine = false;
        staleBoundsSettings.IncludeSecretsInExports = true;
        repository.BlockNextSave();
        Task boundsFirst = workspace.UpdateWindowBoundsAsync(secondBounds);
        await repository.WaitForBlockedSaveAsync();
        Task settingsSecond = workspace.UpdateSettingsAsync(staleBoundsSettings);
        Assert(!settingsSecond.IsCompleted, "Settings replacement bypassed the workspace mutation gate. / 设置替换绕过了工作区变更锁。");
        repository.ReleaseBlockedSave();
        await Task.WhenAll(boundsFirst, settingsSecond);

        AppSettings settingsAfterReverseRace = workspace.GetSettings();
        Assert(!settingsAfterReverseRace.EncryptionEnabled && !settingsAfterReverseRace.AllowPasswordInCommandLine && settingsAfterReverseRace.IncludeSecretsInExports, "Bounds-first concurrency lost the later security choices. / 边界先行的并发丢失了后续安全选项。");
        Assert(settingsAfterReverseRace.WindowBounds == secondBounds, "A stale settings snapshot overwrote the newer window bounds. / 陈旧设置快照覆盖了较新窗口边界。");
    }

    /// <summary>
    /// Creates one valid connection for workspace-service tests. / 为工作区服务测试创建一条有效连接。
    /// </summary>
    /// <param name="id">Stable connection identifier. / 稳定连接标识。</param>
    /// <param name="name">Connection name. / 连接名称。</param>
    /// <param name="host">Connection host. / 连接主机。</param>
    /// <returns>A valid PuTTY connection. / 一条有效的 PuTTY 连接。</returns>
    private static ConnectionProfile CreateTestConnection(Guid id, string name, string host)
    {
        return new ConnectionProfile
        {
            Id = id,
            Name = name,
            Type = ConnectionType.Putty,
            Protocol = "ssh",
            Host = host,
            Port = 22
        };
    }

    /// <summary>
    /// Verifies stale native selections are removed, maximum-size index mapping remains linear, and destructive summaries stay bounded. / 验证会移除陈旧原生选择、最大规模索引映射保持线性，且破坏性操作摘要保持有界。
    /// </summary>
    private static void TestConnectionSelectionLogic()
    {
        Guid firstId = Guid.NewGuid();
        Guid hiddenId = Guid.NewGuid();
        Guid thirdId = Guid.NewGuid();
        Guid deletedId = Guid.NewGuid();
        IReadOnlyList<Guid> reconciled = ConnectionSelectionLogic.ReconcileVisibleSelection(
            [firstId, hiddenId, firstId, thirdId, Guid.Empty, deletedId],
            [firstId, thirdId, deletedId],
            [firstId, hiddenId, thirdId]);
        Assert(reconciled.SequenceEqual([firstId, thirdId]), "Visible native selection reconciliation retained stale identifiers. / 可见原生选择对齐保留了陈旧标识。");

        IReadOnlyList<Guid> resolvedIndices = ConnectionSelectionLogic.ResolveOneBasedSelection(
            [firstId, Guid.Empty, thirdId],
            [1, 3, 0, 4, 3, -1]);
        Assert(resolvedIndices.SequenceEqual([firstId, thirdId]), "One-based native table selection did not resolve to visible connection identifiers. / 表格原生从一开始的选择索引未能解析为可见连接标识。");

        string summary = ConnectionSelectionLogic.BuildDeletionNameSummary(
            ["One", "Two\r\nInjected", new string('x', 80), "Four", "Five", "Six"]);
        Assert(summary.Contains("One", StringComparison.Ordinal) && summary.Contains("and 1 more", StringComparison.Ordinal), "Deletion summary omitted selected names or overflow count. / 删除摘要遗漏了选中名称或溢出数量。");
        Assert(!summary.Contains('\r') && !summary.Contains('\n') && summary.Length < 260, "Deletion summary was not bounded to one line. / 删除摘要未限制为有界单行。");

        Guid[] maximumVisibleIds = Enumerable.Range(0, WorkspaceLimits.MaximumConnectionCount)
            .Select(_ => Guid.NewGuid())
            .ToArray();
        int[] maximumIndices = ConnectionSelectionLogic.BuildOneBasedVisibleIndices(
            maximumVisibleIds,
            maximumVisibleIds.Reverse().Append(Guid.Empty).Append(maximumVisibleIds[0]));
        Assert(maximumIndices.Length == WorkspaceLimits.MaximumConnectionCount &&
               maximumIndices[0] == 1 &&
               maximumIndices[^1] == WorkspaceLimits.MaximumConnectionCount,
            "Maximum-size native selection mapping lost rows or one-based ordering. / 最大规模原生选择映射丢失了行或从一开始的顺序。");
    }

    /// <summary>
    /// Verifies low-height and high-DPI breakpoints keep the command bar compact while preserving table space. / 验证低高度和高 DPI 断点保持命令栏紧凑并保留表格空间。
    /// </summary>
    private static void TestMainResponsiveLayoutLogic()
    {
        const float highDpiClientHeight = 350F;
        MainResponsiveLayoutPlan highDpiPlan = MainResponsiveLayoutLogic.CreatePlan(1366F / 2F - 72F - 32F, highDpiClientHeight);
        Assert(highDpiPlan.UseToolbarOverflow && highDpiClientHeight - 110 - highDpiPlan.ToolbarHeight - highDpiPlan.SecondaryToolbarHeight >= highDpiPlan.MinimumTableHeight, "The 1366x768 working area at 200% collapses the table. / 1366x768 工作区 @ 200% 压缩了表格。");
        int measuredOverflowHeight = MainResponsiveLayoutLogic.CalculateWrappedHeight(
            (int)(1366F / 2F - 72F - 32F),
            12,
            [
                new Size(206, 44),
                new Size(156, 44),
                new Size(72, 42),
                new Size(72, 42),
                new Size(72, 42),
                new Size(72, 42)
            ]);
        Assert(measuredOverflowHeight <= highDpiPlan.ToolbarHeight && highDpiClientHeight - 110 - measuredOverflowHeight - highDpiPlan.SecondaryToolbarHeight >= highDpiPlan.MinimumTableHeight, "Measured overflow controls still collapse the high-DPI table. / 实测溢出控件仍会压缩高 DPI 表格。");

        MainResponsiveLayoutPlan scaledPlan = MainResponsiveLayoutLogic.CreatePlan(1024F / 1.5F - 72F - 32F, 768F / 1.5F);
        Assert(scaledPlan.UseToolbarOverflow && 512 - 110 - scaledPlan.ToolbarHeight - scaledPlan.SecondaryToolbarHeight >= scaledPlan.MinimumTableHeight, "The 1024x768 at 150% layout collapses the table. / 1024x768 @ 150% 布局压缩了表格。");

        MainResponsiveLayoutPlan narrowPlan = MainResponsiveLayoutLogic.CreatePlan(480F - 72F - 32F, 800F);
        Assert(narrowPlan.UseToolbarOverflow && narrowPlan.ToolbarHeight >= 104 && narrowPlan.CompactToolbarText, "A narrow layout did not use the compact overflow command bar. / 窄布局未使用紧凑的溢出命令栏。");
        Assert(narrowPlan.SearchWidth <= 200 && narrowPlan.TypeFilterWidth <= 150, "A narrow layout retained overflowing filter widths. / 窄布局保留了溢出的筛选器宽度。");
        MainResponsiveLayoutPlan ultraNarrowPlan = MainResponsiveLayoutLogic.CreatePlan(360F, 600F);
        Assert(ultraNarrowPlan.ToolbarHeight >= 104 && ultraNarrowPlan.SearchWidth <= 160 && ultraNarrowPlan.TypeFilterWidth <= 136, "An ultra-narrow layout clipped its wrapped toolbar controls. / 超窄布局裁剪了换行工具栏控件。");
        MainResponsiveLayoutPlan desktopPlan = MainResponsiveLayoutLogic.CreatePlan(1280F - 232F - 32F, 800F);
        Assert(desktopPlan.UseToolbarOverflow && desktopPlan.CompactToolbarText && desktopPlan.ToolbarHeight == 56 && desktopPlan.SecondaryToolbarHeight == 48, "The standard desktop layout did not preserve two compact toolbar rows. / 标准桌面布局未保留两行紧凑工具栏。");
        MainResponsiveLayoutPlan widePlan = MainResponsiveLayoutLogic.CreatePlan(1600F, 800F);
        Assert(!widePlan.UseToolbarOverflow && !widePlan.CompactToolbarText && widePlan.ToolbarHeight == 56 && widePlan.SecondaryToolbarHeight == 48, "The wide layout unnecessarily hid commands, compacted labels, or lost the filter row. / 宽屏布局不必要地隐藏了命令、压缩了标签或丢失筛选行。");

        IReadOnlyList<Size> measuredToolbarItems =
        [
            new Size(206, 44),
            new Size(156, 44),
            new Size(72, 42),
            new Size(72, 42),
            new Size(72, 42),
            new Size(72, 42)
        ];
        int wrappedToolbarHeight = MainResponsiveLayoutLogic.CalculateWrappedHeight(579, 12, measuredToolbarItems);
        Assert(wrappedToolbarHeight > 56 && wrappedToolbarHeight <= 104, "Measured compact controls did not fit the planned two-row overflow height. / 实测紧凑控件未适配计划的两行溢出高度。");

        Rectangle workingArea = new(0, 0, 1366, 728);
        Rectangle clampedLarge = MainResponsiveLayoutLogic.ClampWindowBounds(
            new Rectangle(100, 60, 3000, 1800),
            workingArea,
            new Size(720, 480),
            new Size(1280, 800),
            margin: 8);
        Rectangle reachableArea = Rectangle.Inflate(workingArea, -8, -8);
        Assert(reachableArea.Contains(clampedLarge), "A restored 4K window remained unreachable on a smaller display. / 恢复的 4K 窗口在较小显示器上仍不可达。");

        Rectangle clampedFallback = MainResponsiveLayoutLogic.ClampWindowBounds(
            new Rectangle(900, 650, 200, 100),
            new Rectangle(0, 0, 1024, 728),
            new Size(720, 480),
            new Size(1280, 800),
            margin: 8);
        Assert(Rectangle.Inflate(new Rectangle(0, 0, 1024, 728), -8, -8).Contains(clampedFallback), "Fallback bounds exceeded the smaller working area. / 回退边界超出了较小工作区。");
    }

    /// <summary>
    /// Waits briefly for asynchronous process-lifetime cleanup to remove one file. / 短暂等待异步进程生命周期清理删除一个文件。
    /// </summary>
    /// <param name="filePath">File expected to disappear. / 预期消失的文件路径。</param>
    /// <param name="timeout">Maximum wait duration. / 最长等待时间。</param>
    private static async Task WaitForFileDeletionAsync(string filePath, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (File.Exists(filePath) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }

    /// <summary>
    /// Verifies a synchronous action throws the requested exception type. / 验证同步操作抛出指定的异常类型。
    /// </summary>
    /// <typeparam name="TException">Expected exception type. / 预期异常类型。</typeparam>
    /// <param name="action">Synchronous action. / 同步操作。</param>
    /// <param name="message">Failure message. / 失败消息。</param>
    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Throws when a regression assertion is false. / 当回归断言为 false 时抛出异常。
    /// </summary>
    /// <param name="condition">Assertion condition. / 断言条件。</param>
    /// <param name="message">Failure message. / 失败消息。</param>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Supplies the design-time license mode used by the isolated form-construction smoke test.
    /// / 为隔离的窗体构造烟雾测试提供设计时许可模式。
    /// </summary>
    private sealed class DesignerSmokeLicenseContext : LicenseContext
    {
        /// <summary>Gets the simulated designer usage mode. / 获取模拟的设计器使用模式。</summary>
        public override LicenseUsageMode UsageMode => LicenseUsageMode.Designtime;

        /// <summary>Returns no persisted design-time license key. / 不返回持久化设计时许可密钥。</summary>
        public override string? GetSavedLicenseKey(Type type, Assembly? resourceAssembly)
        {
            return null;
        }

        /// <summary>Ignores license-key persistence during the smoke test. / 在烟雾测试中忽略许可密钥持久化。</summary>
        public override void SetSavedLicenseKey(Type type, string key)
        {
        }
    }

    /// <summary>
    /// Provides observable saves, injected failures, and deterministic save blocking for workspace concurrency tests. / 为工作区并发测试提供可观测保存、注入失败与确定性保存阻塞。
    /// </summary>
    private sealed class ControllableWorkspaceRepository : IWorkspaceRepository
    {
        private readonly object _sync = new();
        private AppDataDocument _document;
        private int _saveCount;
        private bool _failNextSave;
        private bool _blockNextSave;
        private TaskCompletionSource<bool>? _blockedSaveEntered;
        private TaskCompletionSource<bool>? _blockedSaveRelease;
        private AppDataDocument? _lastSaved;

        /// <summary>
        /// Initializes the repository with one load result. / 使用一份加载结果初始化仓储。
        /// </summary>
        /// <param name="document">Initial workspace document. / 初始工作区文档。</param>
        public ControllableWorkspaceRepository(AppDataDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>Gets the number of attempted saves. / 获取尝试保存次数。</summary>
        public int SaveCount
        {
            get
            {
                lock (_sync)
                {
                    return _saveCount;
                }
            }
        }

        /// <summary>Gets the most recently completed save candidate. / 获取最近完成保存的候选文档。</summary>
        public AppDataDocument? LastSaved
        {
            get
            {
                lock (_sync)
                {
                    return _lastSaved;
                }
            }
        }

        /// <summary>Gets or sets whether the next save throws an I/O failure. / 获取或设置下一次保存是否抛出 I/O 失败。</summary>
        public bool FailNextSave
        {
            get
            {
                lock (_sync)
                {
                    return _failNextSave;
                }
            }
            set
            {
                lock (_sync)
                {
                    _failNextSave = value;
                }
            }
        }

        /// <summary>
        /// Loads the repository's current document. / 加载仓储的当前文档。
        /// </summary>
        /// <param name="cancellationToken">Cancellation token. / 取消令牌。</param>
        /// <returns>The current workspace load result. / 当前工作区加载结果。</returns>
        public Task<WorkspaceLoadResult> LoadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                return Task.FromResult(new WorkspaceLoadResult(_document));
            }
        }

        /// <summary>
        /// Saves one candidate while applying configured blocking or failure behavior. / 保存一个候选，同时应用已配置的阻塞或失败行为。
        /// </summary>
        /// <param name="document">Candidate workspace. / 候选工作区。</param>
        /// <param name="cancellationToken">Cancellation token. / 取消令牌。</param>
        /// <returns>A task that completes after the candidate is durably accepted. / 候选被持久接受后完成的任务。</returns>
        public async Task SaveAsync(AppDataDocument document, CancellationToken cancellationToken = default)
        {
            Task? releaseTask = null;
            bool fail;
            lock (_sync)
            {
                _saveCount++;
                fail = _failNextSave;
                _failNextSave = false;
                if (_blockNextSave)
                {
                    _blockNextSave = false;
                    _blockedSaveEntered!.TrySetResult(true);
                    releaseTask = _blockedSaveRelease!.Task;
                }
            }

            if (releaseTask is not null)
            {
                await releaseTask.WaitAsync(cancellationToken);
            }

            if (fail)
            {
                throw new IOException("Injected repository save failure. / 注入的仓储保存失败。");
            }

            lock (_sync)
            {
                _document = document;
                _lastSaved = document;
            }
        }

        /// <summary>
        /// Configures the next save to pause after entering the repository. / 配置下一次保存在进入仓储后暂停。
        /// </summary>
        public void BlockNextSave()
        {
            lock (_sync)
            {
                if (_blockNextSave)
                {
                    throw new InvalidOperationException("A blocked save is already configured. / 已配置一次阻塞保存。");
                }

                _blockedSaveEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _blockedSaveRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _blockNextSave = true;
            }
        }

        /// <summary>
        /// Waits until the configured save has entered its deterministic pause. / 等待已配置的保存进入确定性暂停。
        /// </summary>
        /// <returns>A task that times out if the save never enters. / 保存未进入时将超时的任务。</returns>
        public async Task WaitForBlockedSaveAsync()
        {
            Task enteredTask;
            lock (_sync)
            {
                enteredTask = _blockedSaveEntered?.Task
                    ?? throw new InvalidOperationException("No blocked save is configured. / 未配置阻塞保存。");
            }

            await enteredTask.WaitAsync(TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Releases the currently blocked save. / 释放当前被阻塞的保存。
        /// </summary>
        public void ReleaseBlockedSave()
        {
            lock (_sync)
            {
                (_blockedSaveRelease
                    ?? throw new InvalidOperationException("No blocked save is configured. / 未配置阻塞保存。"))
                    .TrySetResult(true);
            }
        }
    }

    /// <summary>
    /// Owns one uniquely named test directory and removes only that validated directory. / 管理一个唯一命名的测试目录，并仅删除经过验证的该目录。
    /// </summary>
    private sealed class TemporaryDirectoryScope : IDisposable
    {
        private readonly string _testRoot;

        /// <summary>
        /// Creates a unique directory beneath the operating-system temporary directory. / 在操作系统临时目录下创建唯一目录。
        /// </summary>
        public TemporaryDirectoryScope()
        {
            _testRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RemoteHubStudio.Tests"));
            Path = System.IO.Path.Combine(_testRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        /// <summary>Gets the unique test directory. / 获取唯一测试目录。</summary>
        public string Path { get; }

        /// <summary>
        /// Removes the unique directory after validating that it remains below the test root. / 验证唯一目录仍位于测试根目录下后将其删除。
        /// </summary>
        public void Dispose()
        {
            string fullPath = System.IO.Path.GetFullPath(Path);
            string requiredPrefix = _testRoot.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
            }
        }
    }
}
