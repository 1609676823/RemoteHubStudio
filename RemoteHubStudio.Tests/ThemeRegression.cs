using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using RemoteHubStudio.Domain;
using RemoteHubStudio.UI.Theme;

namespace RemoteHubStudio.Tests;

/// <summary>
/// Runs deterministic, dependency-free regression checks for theme resolution, contrast, and native WinForms theming.
/// / 对主题解析、对比度与原生 WinForms 着色运行确定性、无外部依赖的回归检查。
/// </summary>
internal static class ThemeRegression
{
    private const double NormalTextContrast = 4.5D;
    private const double UiContrast = 3D;

    /// <summary>
    /// Runs all theme checks on an STA thread without displaying application windows. / 在 STA 线程上运行全部主题检查，不显示应用窗口。
    /// </summary>
    public static void Run()
    {
        Exception? threadFailure = null;
        Thread themeThread = new(() =>
        {
            AppTheme originalTheme = ThemeManager.CurrentTheme;
            try
            {
                AssertThemeResolution();
                AssertPaletteContrast();
                AssertDarkPaletteIsBlue();
                AssertAntdTokensRoundTrip();
                AssertConcreteFormsRoundTrip();
                AssertLateAddedControlTree();
            }
            catch (Exception exception)
            {
                threadFailure = new InvalidOperationException(
                    "Theme regression checks failed. / 主题回归检查失败。",
                    exception);
            }
            finally
            {
                ThemeManager.Apply(originalTheme);
            }
        })
        {
            IsBackground = true,
            Name = "RemoteHubStudio theme regression"
        };
        themeThread.SetApartmentState(ApartmentState.STA);
        themeThread.Start();

        if (!themeThread.Join(TimeSpan.FromSeconds(45)))
        {
            throw new TimeoutException("Theme regression checks timed out. / 主题回归检查超时。");
        }

        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    /// <summary>Verifies explicit themes ignore the system preference while System follows it deterministically. / 验证显式主题不受系统偏好干扰，而跟随系统可确定解析。</summary>
    private static void AssertThemeResolution()
    {
        Assert(
            ThemeManager.ResolveMode(AppTheme.System, systemDarkMode: false) == AntdUI.TMode.Light,
            "System theme did not resolve a light OS preference to light mode. / 跟随系统未将浅色系统偏好解析为浅色模式。");
        Assert(
            ThemeManager.ResolveMode(AppTheme.System, systemDarkMode: true) == AntdUI.TMode.Dark,
            "System theme did not resolve a dark OS preference to dark mode. / 跟随系统未将深色系统偏好解析为深色模式。");

        foreach (bool systemDarkMode in new[] { false, true })
        {
            Assert(
                ThemeManager.ResolveMode(AppTheme.Light, systemDarkMode) == AntdUI.TMode.Light,
                "Explicit light theme was overridden by the OS preference. / 显式浅色主题被系统偏好覆盖。");
            Assert(
                ThemeManager.ResolveMode(AppTheme.Dark, systemDarkMode) == AntdUI.TMode.Dark,
                "Explicit dark theme was overridden by the OS preference. / 显式深色主题被系统偏好覆盖。");
        }

        Assert(
            ThemeManager.ResolveWindowMode(AppTheme.System, AntdUI.TMode.Light) == AntdUI.TAMode.Light &&
            ThemeManager.ResolveWindowMode(AppTheme.System, AntdUI.TMode.Dark) == AntdUI.TAMode.Dark,
            "System window mode did not follow its already-resolved concrete mode. / 跟随系统的窗口模式未采用已解析的具体模式。");
        foreach (AntdUI.TMode resolvedSystemMode in Enum.GetValues<AntdUI.TMode>())
        {
            Assert(
                ThemeManager.ResolveWindowMode(AppTheme.Light, resolvedSystemMode) == AntdUI.TAMode.Light &&
                ThemeManager.ResolveWindowMode(AppTheme.Dark, resolvedSystemMode) == AntdUI.TAMode.Dark,
                "An explicit per-window theme was overridden by the resolved system mode. / 显式单窗口主题被已解析的系统模式覆盖。");
        }
    }

    /// <summary>Checks WCAG contrast for every palette surface that can carry readable text. / 检查每个可承载可读文字的色板表面是否满足 WCAG 对比度。</summary>
    private static void AssertPaletteContrast()
    {
        AssertPaletteContrast("Light", ThemeManager.GetPalette(AntdUI.TMode.Light));
        AssertPaletteContrast("Dark", ThemeManager.GetPalette(AntdUI.TMode.Dark));
    }

    /// <summary>Checks the readable foreground/background combinations in one palette. / 检查单个色板中的可读前景与背景组合。</summary>
    private static void AssertPaletteContrast(string paletteName, ThemePalette palette)
    {
        (string Name, Color Color)[] textSurfaces =
        [
            (nameof(palette.WindowBackground), palette.WindowBackground),
            (nameof(palette.LayoutBackground), palette.LayoutBackground),
            (nameof(palette.ContainerBackground), palette.ContainerBackground),
            (nameof(palette.ElevatedBackground), palette.ElevatedBackground),
            (nameof(palette.Fill), palette.Fill),
            (nameof(palette.FillSecondary), palette.FillSecondary),
            (nameof(palette.FillTertiary), palette.FillTertiary),
            (nameof(palette.FillQuaternary), palette.FillQuaternary),
            (nameof(palette.HoverBackground), palette.HoverBackground)
        ];
        foreach ((string surfaceName, Color surfaceColor) in textSurfaces)
        {
            AssertMinimumContrast(
                paletteName,
                nameof(palette.TextPrimary),
                palette.TextPrimary,
                surfaceName,
                surfaceColor,
                NormalTextContrast);
            AssertMinimumContrast(
                paletteName,
                nameof(palette.TextSecondary),
                palette.TextSecondary,
                surfaceName,
                surfaceColor,
                NormalTextContrast);
        }

        (string Name, Color Color)[] baseSurfaces =
        [
            (nameof(palette.WindowBackground), palette.WindowBackground),
            (nameof(palette.LayoutBackground), palette.LayoutBackground),
            (nameof(palette.ContainerBackground), palette.ContainerBackground),
            (nameof(palette.ElevatedBackground), palette.ElevatedBackground)
        ];
        foreach ((string surfaceName, Color surfaceColor) in baseSurfaces)
        {
            AssertMinimumContrast(
                paletteName,
                nameof(palette.TextTertiary),
                palette.TextTertiary,
                surfaceName,
                surfaceColor,
                NormalTextContrast);
        }

        AssertMinimumContrast(
            paletteName,
            nameof(palette.Border),
            palette.Border,
            nameof(palette.ContainerBackground),
            palette.ContainerBackground,
            UiContrast);

        (string Name, Color Color)[] statusColors =
        [
            (nameof(palette.Success), palette.Success),
            (nameof(palette.Warning), palette.Warning),
            (nameof(palette.Error), palette.Error),
            (nameof(palette.Info), palette.Info)
        ];
        foreach ((string statusName, Color statusColor) in statusColors)
        {
            foreach ((string surfaceName, Color surfaceColor) in baseSurfaces)
            {
                AssertMinimumContrast(
                    paletteName,
                    statusName,
                    statusColor,
                    surfaceName,
                    surfaceColor,
                    NormalTextContrast);
            }
        }

        AssertMinimumContrast(
            paletteName,
            nameof(palette.SpotlightText),
            palette.SpotlightText,
            nameof(palette.SpotlightBackground),
            palette.SpotlightBackground,
            NormalTextContrast);
        AssertMinimumContrast(
            paletteName,
            "PrimaryColor",
            Color.White,
            nameof(palette.Primary),
            palette.Primary,
            NormalTextContrast);
    }

    /// <summary>Locks the dark palette to cool, layered deep-blue surfaces instead of neutral charcoal. / 将深色色板固定为冷色、分层的深蓝表面，而非中性炭黑。</summary>
    private static void AssertDarkPaletteIsBlue()
    {
        ThemePalette palette = ThemeManager.GetPalette(AntdUI.TMode.Dark);
        (string Name, Color Color)[] darkSurfaces =
        [
            (nameof(palette.WindowBackground), palette.WindowBackground),
            (nameof(palette.LayoutBackground), palette.LayoutBackground),
            (nameof(palette.ContainerBackground), palette.ContainerBackground),
            (nameof(palette.ElevatedBackground), palette.ElevatedBackground),
            (nameof(palette.Fill), palette.Fill),
            (nameof(palette.FillSecondary), palette.FillSecondary),
            (nameof(palette.FillTertiary), palette.FillTertiary),
            (nameof(palette.FillQuaternary), palette.FillQuaternary),
            (nameof(palette.Border), palette.Border),
            (nameof(palette.BorderSubtle), palette.BorderSubtle),
            (nameof(palette.BorderDisabled), palette.BorderDisabled),
            (nameof(palette.HoverBackground), palette.HoverBackground),
            (nameof(palette.SpotlightBackground), palette.SpotlightBackground)
        ];

        foreach ((string surfaceName, Color surfaceColor) in darkSurfaces)
        {
            Assert(
                surfaceColor.B > surfaceColor.G &&
                surfaceColor.G > surfaceColor.R &&
                surfaceColor.B - surfaceColor.R >= 16,
                $"Dark {surfaceName} is neutral or insufficiently blue: {Describe(surfaceColor)}. / " +
                $"深色 {surfaceName} 偏中性或蓝色特征不足：{Describe(surfaceColor)}。");
            Assert(
                RelativeLuminance(surfaceColor) < 0.2D,
                $"Dark {surfaceName} is too bright for a deep-blue surface. / 深色 {surfaceName} 对深蓝表面而言过亮。");
        }

        double windowLuminance = RelativeLuminance(palette.WindowBackground);
        double layoutLuminance = RelativeLuminance(palette.LayoutBackground);
        double containerLuminance = RelativeLuminance(palette.ContainerBackground);
        double elevatedLuminance = RelativeLuminance(palette.ElevatedBackground);
        Assert(
            windowLuminance < layoutLuminance &&
            layoutLuminance < containerLuminance &&
            containerLuminance < elevatedLuminance,
            "Dark surfaces lost their window-to-elevated depth ordering. / 深色表面丢失了从窗口到浮层的明度层级。");
    }

    /// <summary>Verifies AntdUI receives every semantic token and a Light-Dark-Light cycle is lossless. / 验证 AntdUI 收到全部语义令牌，且浅-深-浅往返不会泄漏颜色。</summary>
    private static void AssertAntdTokensRoundTrip()
    {
        ThemeManager.Apply(AppTheme.Light);
        AssertAppliedMode(AppTheme.Light, AntdUI.TMode.Light);
        AssertAntdTokens(ThemeManager.GetPalette(AntdUI.TMode.Light));
        IReadOnlyDictionary<AntdUI.Colour, Color> firstLight = CaptureAntdTokens();

        ThemeManager.Apply(AppTheme.Dark);
        AssertAppliedMode(AppTheme.Dark, AntdUI.TMode.Dark);
        ThemePalette darkPalette = ThemeManager.GetPalette(AntdUI.TMode.Dark);
        AssertAntdTokens(darkPalette);
        IReadOnlyDictionary<AntdUI.Colour, Color> dark = CaptureAntdTokens();

        AntdUI.Colour[] modeSensitiveTokens =
        [
            AntdUI.Colour.Text,
            AntdUI.Colour.TextSecondary,
            AntdUI.Colour.BgBase,
            AntdUI.Colour.BgLayout,
            AntdUI.Colour.BgContainer,
            AntdUI.Colour.BgElevated,
            AntdUI.Colour.Fill,
            AntdUI.Colour.BorderColor,
            AntdUI.Colour.HoverBg,
            AntdUI.Colour.BgSpotlight
        ];
        foreach (AntdUI.Colour token in modeSensitiveTokens)
        {
            Assert(
                !SameColor(firstLight[token], dark[token]),
                $"AntdUI token '{token}' did not change between light and dark modes. / AntdUI 令牌“{token}”在浅色与深色模式间未变化。");
        }

        ThemeManager.Apply(AppTheme.Light);
        AssertAppliedMode(AppTheme.Light, AntdUI.TMode.Light);
        AssertAntdTokens(ThemeManager.GetPalette(AntdUI.TMode.Light));
        IReadOnlyDictionary<AntdUI.Colour, Color> secondLight = CaptureAntdTokens();
        foreach (AntdUI.Colour token in Enum.GetValues<AntdUI.Colour>())
        {
            AssertSameColor(
                firstLight[token],
                secondLight[token],
                $"AntdUI token '{token}' leaked across the Light-Dark-Light cycle. / AntdUI 令牌“{token}”在浅-深-浅循环中发生泄漏。");
        }
    }

    /// <summary>Verifies the global manager state after applying an explicit theme. / 验证应用显式主题后的全局管理器状态。</summary>
    private static void AssertAppliedMode(AppTheme expectedTheme, AntdUI.TMode expectedMode)
    {
        Assert(
            ThemeManager.CurrentTheme == expectedTheme &&
            ThemeManager.CurrentMode == expectedMode &&
            AntdUI.Config.Mode == expectedMode,
            $"Applied theme state is inconsistent for '{expectedTheme}'. / 已应用的“{expectedTheme}”主题状态不一致。");
    }

    /// <summary>Checks the AntdUI token values directly mapped from one semantic palette. / 检查从语义色板直接映射的 AntdUI 令牌值。</summary>
    private static void AssertAntdTokens(ThemePalette palette)
    {
        (AntdUI.Colour Token, Color Expected)[] expectedTokens =
        [
            (AntdUI.Colour.Primary, ResolveAntdSeed(palette.Primary)),
            (AntdUI.Colour.Success, ResolveAntdSeed(palette.Success)),
            (AntdUI.Colour.Warning, ResolveAntdSeed(palette.Warning)),
            (AntdUI.Colour.Error, ResolveAntdSeed(palette.Error)),
            (AntdUI.Colour.Info, ResolveAntdSeed(palette.Info)),
            (AntdUI.Colour.PrimaryColor, Color.White),
            (AntdUI.Colour.DefaultBg, palette.ContainerBackground),
            (AntdUI.Colour.DefaultColor, palette.TextPrimary),
            (AntdUI.Colour.DefaultBorder, palette.Border),
            (AntdUI.Colour.TagDefaultBg, palette.FillSecondary),
            (AntdUI.Colour.TagDefaultColor, palette.TextSecondary),
            (AntdUI.Colour.TextBase, palette.TextPrimary),
            (AntdUI.Colour.Text, palette.TextPrimary),
            (AntdUI.Colour.TextSecondary, palette.TextSecondary),
            (AntdUI.Colour.TextTertiary, palette.TextTertiary),
            (AntdUI.Colour.TextQuaternary, palette.TextDisabled),
            (AntdUI.Colour.BgBase, palette.WindowBackground),
            (AntdUI.Colour.BgLayout, palette.LayoutBackground),
            (AntdUI.Colour.BgContainer, palette.ContainerBackground),
            (AntdUI.Colour.BgElevated, palette.ElevatedBackground),
            (AntdUI.Colour.Fill, palette.Fill),
            (AntdUI.Colour.FillSecondary, palette.FillSecondary),
            (AntdUI.Colour.FillTertiary, palette.FillTertiary),
            (AntdUI.Colour.FillQuaternary, palette.FillQuaternary),
            (AntdUI.Colour.BorderColor, palette.Border),
            (AntdUI.Colour.BorderSecondary, palette.BorderSubtle),
            (AntdUI.Colour.BorderColorDisable, palette.BorderDisabled),
            (AntdUI.Colour.Split, palette.BorderSubtle),
            (AntdUI.Colour.HoverBg, palette.HoverBackground),
            (AntdUI.Colour.HoverColor, palette.TextPrimary),
            (AntdUI.Colour.SliderHandleColorDisabled, palette.BorderDisabled),
            (AntdUI.Colour.TextSpotlight, palette.SpotlightText),
            (AntdUI.Colour.BgSpotlight, palette.SpotlightBackground),
            (AntdUI.Colour.SwitchHandleBg, palette.SwitchHandleBackground)
        ];

        foreach ((AntdUI.Colour token, Color expected) in expectedTokens)
        {
            AssertSameColor(
                expected,
                AntdUI.Style.Get(token),
                $"AntdUI token '{token}' does not match the active semantic palette. / AntdUI 令牌“{token}”与当前语义色板不一致。");
        }
    }

    /// <summary>Resolves an AntdUI semantic seed to the mode-specific main swatch used by the library. / 将 AntdUI 语义种子色解析为库在当前模式下使用的主色阶。</summary>
    private static Color ResolveAntdSeed(Color seed)
    {
        IReadOnlyList<Color> swatches = ThemeManager.CurrentMode == AntdUI.TMode.Dark
            ? AntdUI.Style.GenerateDark(seed)
            : AntdUI.Style.Generate(seed);
        return swatches[5];
    }

    /// <summary>Captures all public AntdUI color tokens under the current concrete mode. / 捕获当前具体模式下的全部公开 AntdUI 颜色令牌。</summary>
    private static IReadOnlyDictionary<AntdUI.Colour, Color> CaptureAntdTokens()
    {
        return Enum.GetValues<AntdUI.Colour>()
            .ToDictionary(token => token, AntdUI.Style.Get);
    }

    /// <summary>Constructs and recolors every concrete application form through a Light-Dark-Light cycle. / 构建每个具体应用窗体，并完成浅-深-浅着色循环。</summary>
    private static void AssertConcreteFormsRoundTrip()
    {
        Assert(
            Thread.CurrentThread.GetApartmentState() == ApartmentState.STA,
            "Form theme checks must run on an STA thread. / 窗体主题检查必须在 STA 线程上运行。");

        Type[] formTypes = typeof(ThemeManager).Assembly.GetTypes()
            .Where(type =>
                !type.IsAbstract &&
                !type.ContainsGenericParameters &&
                typeof(Form).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        Assert(formTypes.Length > 0, "No concrete application forms were found for theme testing. / 未找到可用于主题测试的具体应用窗体。");

        foreach (Type formType in formTypes)
        {
            string formName = formType.FullName ?? formType.Name;
            ConstructorInfo? constructor = formType.GetConstructor(Type.EmptyTypes);
            Assert(
                constructor is not null,
                $"Form '{formName}' has no public parameterless constructor for non-visible theme testing. / " +
                $"窗体“{formName}”没有可供非可见主题测试使用的公共无参构造函数。");

            using Form form = LicenseManager.CreateWithContext(formType, new ThemeLicenseContext()) as Form
                ?? throw new InvalidOperationException(
                    $"Theme test construction returned an unexpected value for '{formName}'. / 主题测试构建“{formName}”时返回了意外值。");
            Assert(!form.Visible, $"Theme test construction displayed '{formName}'. / 主题测试构建意外显示了“{formName}”。");

            ToolStrip[] toolStrips = GetComponentToolStrips(form).ToArray();
            ApplyAndAssertForm(form, toolStrips, AppTheme.Light, formName);
            ApplyAndAssertForm(form, toolStrips, AppTheme.Dark, formName);
            ApplyAndAssertForm(form, toolStrips, AppTheme.Light, formName);
        }
    }

    /// <summary>Checks that controls inserted after initial theming inherit the active palette. / 检查初次着色后插入的控件是否继承当前色板。</summary>
    private static void AssertLateAddedControlTree()
    {
        ThemeManager.Apply(AppTheme.Dark);
        ThemePalette palette = ThemeManager.CurrentPalette;

        using AntdUI.Window window = new();
        AntdUI.Panel section = new();
        window.Controls.Add(section);
        ThemeManager.ApplyTo(window);

        UserControl latePage = new();
        TableLayoutPanel lateLayout = new();
        latePage.Controls.Add(lateLayout);
        section.Controls.Add(latePage);

        AssertSameColor(
            palette.ContainerBackground,
            latePage.BackColor,
            "A late-added page did not inherit the themed container surface. / 延迟加入的页面未继承主题容器表面色。");
        AssertSameColor(
            palette.TextPrimary,
            latePage.ForeColor,
            "A late-added page did not inherit the themed foreground. / 延迟加入的页面未继承主题前景色。");
        AssertSameColor(
            palette.ContainerBackground,
            lateLayout.BackColor,
            "A nested late-added layout did not inherit the themed container surface. / 延迟加入页面中的布局未继承主题容器表面色。");
        AssertSameColor(
            palette.TextPrimary,
            lateLayout.ForeColor,
            "A nested late-added layout did not inherit the themed foreground. / 延迟加入页面中的布局未继承主题前景色。");
    }

    /// <summary>Applies one explicit mode and checks every native host and component-owned menu. / 应用一个显式模式，并检查每个原生宿主与组件菜单。</summary>
    private static void ApplyAndAssertForm(Form form, ToolStrip[] toolStrips, AppTheme theme, string formName)
    {
        ThemeManager.Apply(theme);
        ThemeManager.ApplyTo(form, toolStrips);
        ThemePalette palette = ThemeManager.CurrentPalette;

        Assert(!form.Visible, $"Applying {theme} displayed '{formName}'. / 应用 {theme} 时意外显示了“{formName}”。");
        int nativeControlCount = AssertNativeControlTree(form, palette.WindowBackground, palette, formName);
        Assert(
            nativeControlCount > 0,
            $"Form '{formName}' exposed no native WinForms hosts for theme validation. / 窗体“{formName}”未公开可验证主题的原生 WinForms 宿主。");

        foreach (ToolStrip toolStrip in toolStrips)
        {
            AssertToolStrip(toolStrip, palette, $"{formName}.{toolStrip.Name}");
        }
    }

    /// <summary>Checks the recursively inherited semantic colors on native controls. / 检查原生控件递归继承的语义颜色。</summary>
    private static int AssertNativeControlTree(
        Control control,
        Color inheritedSurface,
        ThemePalette palette,
        string path)
    {
        int nativeControlCount = 0;
        Color childSurface = inheritedSurface;
        string controlName = string.IsNullOrWhiteSpace(control.Name) ? control.GetType().Name : control.Name;
        string controlPath = $"{path}/{controlName}";

        if (control is AntdUI.BaseForm window)
        {
            AssertSameColor(
                palette.WindowBackground,
                window.BackColor,
                $"Window background was not themed at '{controlPath}'. / 窗口背景未在“{controlPath}”处应用主题。");
            AssertSameColor(
                palette.TextPrimary,
                window.ForeColor,
                $"Window foreground was not themed at '{controlPath}'. / 窗口前景未在“{controlPath}”处应用主题。");
            Assert(
                window.Mode == ThemeManager.ResolveWindowMode(ThemeManager.CurrentTheme),
                $"Window mode was not updated at '{controlPath}'. / 窗口模式未在“{controlPath}”处更新。");
            childSurface = palette.LayoutBackground;
        }
        else if (control is AntdUI.Panel)
        {
            childSurface = palette.ContainerBackground;
        }
        else if (control is ToolStrip toolStrip)
        {
            nativeControlCount++;
            AssertToolStrip(toolStrip, palette, controlPath);
            childSurface = palette.ElevatedBackground;
        }
        else if (control is not AntdUI.IControl)
        {
            nativeControlCount++;
            Color expectedBackground = control is TextBoxBase or ListControl
                ? palette.ContainerBackground
                : inheritedSurface;
            AssertSameColor(
                expectedBackground,
                control.BackColor,
                $"Native background was not themed at '{controlPath}'. / 原生控件背景未在“{controlPath}”处应用主题。");
            AssertSameColor(
                palette.TextPrimary,
                control.ForeColor,
                $"Native foreground was not themed at '{controlPath}'. / 原生控件前景未在“{controlPath}”处应用主题。");
        }

        if (control.ContextMenuStrip is ContextMenuStrip contextMenu)
        {
            AssertToolStrip(contextMenu, palette, $"{controlPath}.ContextMenuStrip");
        }

        foreach (Control child in control.Controls)
        {
            nativeControlCount += AssertNativeControlTree(child, childSurface, palette, controlPath);
        }

        return nativeControlCount;
    }

    /// <summary>Checks a native menu and every nested drop-down item. / 检查原生菜单及其每个嵌套下拉项。</summary>
    private static void AssertToolStrip(ToolStrip toolStrip, ThemePalette palette, string path)
    {
        AssertSameColor(
            palette.ElevatedBackground,
            toolStrip.BackColor,
            $"ToolStrip background was not themed at '{path}'. / ToolStrip 背景未在“{path}”处应用主题。");
        AssertSameColor(
            palette.TextPrimary,
            toolStrip.ForeColor,
            $"ToolStrip foreground was not themed at '{path}'. / ToolStrip 前景未在“{path}”处应用主题。");

        foreach (ToolStripItem item in toolStrip.Items)
        {
            string itemName = string.IsNullOrWhiteSpace(item.Name) ? item.GetType().Name : item.Name;
            string itemPath = $"{path}/{itemName}";
            AssertSameColor(
                palette.ElevatedBackground,
                item.BackColor,
                $"ToolStrip item background was not themed at '{itemPath}'. / ToolStrip 项背景未在“{itemPath}”处应用主题。");
            AssertSameColor(
                item.Enabled ? palette.TextPrimary : palette.TextDisabled,
                item.ForeColor,
                $"ToolStrip item foreground was not themed at '{itemPath}'. / ToolStrip 项前景未在“{itemPath}”处应用主题。");

            if (item is ToolStripDropDownItem dropDownItem)
            {
                AssertToolStrip(dropDownItem.DropDown, palette, $"{itemPath}.DropDown");
            }
        }
    }

    /// <summary>Finds ToolStrip components held in private fields throughout a form's inheritance chain. / 在窗体继承链的私有字段中查找 ToolStrip 组件。</summary>
    private static IEnumerable<ToolStrip> GetComponentToolStrips(Form form)
    {
        HashSet<ToolStrip> result = new(ReferenceEqualityComparer.Instance);
        for (Type? type = form.GetType(); type is not null; type = type.BaseType)
        {
            foreach (FieldInfo field in type.GetFields(
                         BindingFlags.Instance |
                         BindingFlags.Public |
                         BindingFlags.NonPublic |
                         BindingFlags.DeclaredOnly))
            {
                if (typeof(ToolStrip).IsAssignableFrom(field.FieldType) &&
                    field.GetValue(form) is ToolStrip toolStrip)
                {
                    result.Add(toolStrip);
                }
            }
        }

        Queue<Control> pending = new();
        pending.Enqueue(form);
        while (pending.Count > 0)
        {
            Control control = pending.Dequeue();
            if (control is ToolStrip toolStrip)
            {
                result.Add(toolStrip);
            }

            if (control.ContextMenuStrip is ContextMenuStrip contextMenu)
            {
                result.Add(contextMenu);
            }

            foreach (Control child in control.Controls)
            {
                pending.Enqueue(child);
            }
        }

        return result;
    }

    /// <summary>Enforces one WCAG contrast threshold and reports the measured ratio. / 强制一个 WCAG 对比度阈值，并报告实测比率。</summary>
    private static void AssertMinimumContrast(
        string paletteName,
        string foregroundName,
        Color foreground,
        string backgroundName,
        Color background,
        double minimum)
    {
        double ratio = ContrastRatio(foreground, background);
        Assert(
            ratio + 0.0001D >= minimum,
            $"{paletteName} {foregroundName}/{backgroundName} contrast is {ratio:F2}:1; expected at least {minimum:F1}:1. / " +
            $"{paletteName} {foregroundName}/{backgroundName} 对比度为 {ratio:F2}:1，期望至少 {minimum:F1}:1。");
    }

    /// <summary>Returns the WCAG relative-luminance contrast ratio for two opaque colors. / 返回两个不透明颜色的 WCAG 相对亮度对比率。</summary>
    private static double ContrastRatio(Color first, Color second)
    {
        double firstLuminance = RelativeLuminance(first);
        double secondLuminance = RelativeLuminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05D) /
               (Math.Min(firstLuminance, secondLuminance) + 0.05D);
    }

    /// <summary>Calculates WCAG relative luminance in linear sRGB. / 在线性 sRGB 空间中计算 WCAG 相对亮度。</summary>
    private static double RelativeLuminance(Color color)
    {
        static double Linearize(byte component)
        {
            double value = component / 255D;
            return value <= 0.04045D
                ? value / 12.92D
                : Math.Pow((value + 0.055D) / 1.055D, 2.4D);
        }

        return 0.2126D * Linearize(color.R) +
               0.7152D * Linearize(color.G) +
               0.0722D * Linearize(color.B);
    }

    /// <summary>Compares colors by ARGB value, independent of known-color metadata. / 按 ARGB 值比较颜色，不受已知颜色元数据影响。</summary>
    private static bool SameColor(Color expected, Color actual)
    {
        return expected.ToArgb() == actual.ToArgb();
    }

    /// <summary>Throws a descriptive error when two colors differ. / 两个颜色不同时抛出描述性错误。</summary>
    private static void AssertSameColor(Color expected, Color actual, string message)
    {
        Assert(
            SameColor(expected, actual),
            $"{message} Expected {Describe(expected)}, actual {Describe(actual)}. / " +
            $"期望 {Describe(expected)}，实际 {Describe(actual)}。");
    }

    /// <summary>Formats one color as an eight-digit ARGB value. / 将颜色格式化为八位 ARGB 值。</summary>
    private static string Describe(Color color)
    {
        return $"#{unchecked((uint)color.ToArgb()):X8}";
    }

    /// <summary>Throws when a regression condition is false. / 回归条件为假时抛出异常。</summary>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>Supplies designer usage mode while concrete forms are built without being shown. / 在不显示具体窗体的构建期间提供设计器使用模式。</summary>
    private sealed class ThemeLicenseContext : LicenseContext
    {
        public override LicenseUsageMode UsageMode => LicenseUsageMode.Designtime;

        public override string? GetSavedLicenseKey(Type type, Assembly? resourceAssembly)
        {
            return null;
        }

        public override void SetSavedLicenseKey(Type type, string key)
        {
        }
    }
}
