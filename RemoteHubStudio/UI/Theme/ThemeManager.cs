using Microsoft.Win32;
using RemoteHubStudio.Domain;
using System.Windows.Forms;

namespace RemoteHubStudio.UI.Theme;

/// <summary>
/// Applies RemoteHubStudio theme choices to the global AntdUI palette. / 将 RemoteHubStudio 主题选项应用到全局 AntdUI 调色板。
/// </summary>
public static class ThemeManager
{
    private static readonly ThemePalette LightPalette = new()
    {
        Primary = Color.FromArgb(37, 99, 235),
        Success = Color.FromArgb(21, 128, 61),
        Warning = Color.FromArgb(180, 83, 9),
        Error = Color.FromArgb(217, 31, 38),
        Info = Color.FromArgb(3, 105, 161),
        TextPrimary = Color.FromArgb(16, 32, 58),
        TextSecondary = Color.FromArgb(71, 85, 105),
        TextTertiary = Color.FromArgb(95, 111, 133),
        TextDisabled = Color.FromArgb(148, 163, 184),
        WindowBackground = Color.FromArgb(246, 248, 252),
        LayoutBackground = Color.FromArgb(243, 246, 250),
        ContainerBackground = Color.White,
        ElevatedBackground = Color.White,
        Fill = Color.FromArgb(226, 232, 240),
        FillSecondary = Color.FromArgb(233, 239, 246),
        FillTertiary = Color.FromArgb(238, 243, 248),
        FillQuaternary = Color.FromArgb(244, 247, 251),
        Border = Color.FromArgb(133, 149, 168),
        BorderSubtle = Color.FromArgb(223, 231, 241),
        BorderDisabled = Color.FromArgb(226, 232, 240),
        HoverBackground = Color.FromArgb(235, 242, 252),
        SpotlightBackground = Color.FromArgb(15, 23, 42),
        SpotlightText = Color.White,
        SwitchHandleBackground = Color.White
    };

    private static readonly ThemePalette DarkPalette = new()
    {
        Primary = Color.FromArgb(37, 99, 235),
        Success = Color.FromArgb(52, 211, 153),
        Warning = Color.FromArgb(251, 191, 36),
        Error = Color.FromArgb(251, 113, 133),
        Info = Color.FromArgb(56, 189, 248),
        TextPrimary = Color.FromArgb(234, 242, 255),
        TextSecondary = Color.FromArgb(169, 190, 214),
        TextTertiary = Color.FromArgb(124, 152, 184),
        TextDisabled = Color.FromArgb(88, 113, 140),
        WindowBackground = Color.FromArgb(7, 17, 31),
        LayoutBackground = Color.FromArgb(9, 21, 37),
        ContainerBackground = Color.FromArgb(15, 32, 56),
        ElevatedBackground = Color.FromArgb(20, 42, 70),
        Fill = Color.FromArgb(40, 73, 108),
        FillSecondary = Color.FromArgb(27, 54, 83),
        FillTertiary = Color.FromArgb(21, 46, 73),
        FillQuaternary = Color.FromArgb(16, 38, 64),
        Border = Color.FromArgb(75, 112, 153),
        BorderSubtle = Color.FromArgb(28, 53, 81),
        BorderDisabled = Color.FromArgb(32, 56, 82),
        HoverBackground = Color.FromArgb(23, 54, 83),
        SpotlightBackground = Color.FromArgb(3, 10, 22),
        SpotlightText = Color.FromArgb(247, 250, 255),
        SwitchHandleBackground = Color.FromArgb(234, 242, 255)
    };

    /// <summary>Gets the persisted user-facing theme choice last applied by the application. / 获取应用最近应用的用户主题选项。</summary>
    public static AppTheme CurrentTheme { get; private set; } = AppTheme.System;

    /// <summary>Gets the concrete light or dark mode currently used by the global palette. / 获取全局色板当前使用的具体浅色或深色模式。</summary>
    public static AntdUI.TMode CurrentMode { get; private set; } = AntdUI.TMode.Light;

    /// <summary>Gets the semantic palette currently applied to the application. / 获取应用当前使用的语义色板。</summary>
    public static ThemePalette CurrentPalette { get; private set; } = LightPalette;

    /// <summary>
    /// Applies stable global AntdUI defaults and the selected application theme. / 应用稳定的 AntdUI 全局默认值与所选应用主题。
    /// </summary>
    /// <param name="theme">Application theme choice. / 应用主题选项。</param>
    public static void Apply(AppTheme theme)
    {
        AppTheme normalizedTheme = Enum.IsDefined(theme) ? theme : AppTheme.System;
        AntdUI.TMode mode = ResolveMode(normalizedTheme);

        AntdUI.Config.ShowInWindow = true;
        AntdUI.Config.TextRenderingHighQuality = true;
        // AntdUI custom tokens are global rather than keyed by mode. Always clear the
        // previous resolved palette first so a dark token cannot leak back into light mode.
        AntdUI.Style.Clear();
        AntdUI.Config.Mode = mode;
        ThemePalette palette = GetPalette(mode);
        ApplyAntdPalette(palette);
        ToolStripManager.Renderer = new ThemeToolStripRenderer(palette);

        CurrentTheme = normalizedTheme;
        CurrentMode = mode;
        CurrentPalette = palette;

        // A theme can change while modal dialogs are open (notably when following
        // Windows). Refresh every live window so native hosts never lag behind AntdUI.
        Form[] openForms = System.Windows.Forms.Application.OpenForms.Cast<Form>().ToArray();
        foreach (Form openForm in openForms)
        {
            ApplyTo(openForm);
        }
    }

    /// <summary>
    /// Converts an application theme into the concrete AntdUI light or dark mode. / 将应用主题转换为具体的 AntdUI 浅色或深色模式。
    /// </summary>
    /// <param name="theme">Application theme choice. / 应用主题选项。</param>
    /// <returns>The concrete AntdUI mode. / 具体的 AntdUI 模式。</returns>
    public static AntdUI.TMode ResolveMode(AppTheme theme)
    {
        return ResolveMode(theme, IsSystemDarkMode());
    }

    /// <summary>
    /// Converts a theme into a concrete mode using an explicit system preference. / 使用显式系统偏好将主题转换为具体模式。
    /// </summary>
    /// <param name="theme">Application theme choice. / 应用主题选项。</param>
    /// <param name="systemDarkMode">Whether the operating system requests dark application colors. / 操作系统是否请求深色应用颜色。</param>
    /// <returns>The concrete AntdUI mode. / 具体的 AntdUI 模式。</returns>
    public static AntdUI.TMode ResolveMode(AppTheme theme, bool systemDarkMode)
    {
        return theme switch
        {
            AppTheme.Dark => AntdUI.TMode.Dark,
            AppTheme.Light => AntdUI.TMode.Light,
            _ => systemDarkMode ? AntdUI.TMode.Dark : AntdUI.TMode.Light
        };
    }

    /// <summary>
    /// Converts an application theme into an explicit per-window AntdUI mode. / 将应用主题转换为 AntdUI 单窗口的显式模式。
    /// </summary>
    /// <param name="theme">Application theme choice. / 应用主题选项。</param>
    /// <returns>The AntdUI window mode. / AntdUI 窗口模式。</returns>
    public static AntdUI.TAMode ResolveWindowMode(AppTheme theme)
    {
        return ResolveWindowMode(theme, ResolveMode(theme));
    }

    /// <summary>
    /// Converts a theme into an explicit window mode using a previously resolved system mode. / 使用已解析的系统模式将主题转换为显式窗口模式。
    /// </summary>
    /// <param name="theme">Application theme choice. / 应用主题选项。</param>
    /// <param name="resolvedSystemMode">Concrete system mode to use for System. / System 选项使用的具体系统模式。</param>
    /// <returns>The explicit AntdUI window mode. / 显式 AntdUI 窗口模式。</returns>
    public static AntdUI.TAMode ResolveWindowMode(AppTheme theme, AntdUI.TMode resolvedSystemMode)
    {
        return theme switch
        {
            AppTheme.Dark => AntdUI.TAMode.Dark,
            AppTheme.Light => AntdUI.TAMode.Light,
            _ => resolvedSystemMode == AntdUI.TMode.Dark ? AntdUI.TAMode.Dark : AntdUI.TAMode.Light
        };
    }

    /// <summary>
    /// Returns the semantic palette for a concrete light or dark mode. / 返回具体浅色或深色模式对应的语义色板。
    /// </summary>
    /// <param name="mode">Concrete AntdUI mode. / 具体 AntdUI 模式。</param>
    /// <returns>The matching immutable palette. / 对应的不可变色板。</returns>
    public static ThemePalette GetPalette(AntdUI.TMode mode)
    {
        return mode == AntdUI.TMode.Dark ? DarkPalette : LightPalette;
    }

    /// <summary>
    /// Applies the current palette to one window and every native WinForms host beneath it. / 将当前色板应用到窗口及其所有原生 WinForms 容器。
    /// </summary>
    /// <param name="root">Root window or control. / 根窗口或控件。</param>
    /// <param name="toolStrips">Optional native menus that are component-owned rather than in the control tree. / 可选的组件级原生菜单。</param>
    public static void ApplyTo(Control root, params ToolStrip[] toolStrips)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root is AntdUI.BaseForm window)
        {
            window.Theme()
                .Light(LightPalette.WindowBackground, LightPalette.TextPrimary)
                .Dark(DarkPalette.WindowBackground, DarkPalette.TextPrimary)
                .Header(LightPalette.ContainerBackground, DarkPalette.ContainerBackground)
                .FormBorderColor(LightPalette.BorderSubtle, DarkPalette.Border);
            // AntdUI 2.4.8 can retain its previous Dark flag when switching an existing
            // window to Auto. System mode is therefore kept explicit and refreshed by
            // the Windows preference event handled by MainForm.
            window.Mode = ResolveWindowMode(CurrentTheme, CurrentMode);
        }

        ApplyControlTree(root, CurrentPalette.WindowBackground);
        foreach (ToolStrip toolStrip in toolStrips)
        {
            if (toolStrip is not null)
            {
                ApplyToolStrip(toolStrip, CurrentPalette);
            }
        }

        root.Invalidate(true);
    }

    /// <summary>
    /// Applies the current palette to a native ToolStrip or context menu. / 将当前色板应用到原生 ToolStrip 或上下文菜单。
    /// </summary>
    /// <param name="toolStrip">ToolStrip to update. / 要更新的 ToolStrip。</param>
    public static void ApplyTo(ToolStrip toolStrip)
    {
        ArgumentNullException.ThrowIfNull(toolStrip);
        ApplyToolStrip(toolStrip, CurrentPalette);
    }

    /// <summary>
    /// Loads the resolved palette into AntdUI after Config.Mode has already been updated. / 在 Config.Mode 更新后将解析后的色板载入 AntdUI。
    /// </summary>
    private static void ApplyAntdPalette(ThemePalette palette)
    {
        AntdUI.Style.SetPrimary(palette.Primary);
        AntdUI.Style.SetSuccess(palette.Success);
        AntdUI.Style.SetWarning(palette.Warning);
        AntdUI.Style.SetError(palette.Error);
        AntdUI.Style.SetInfo(palette.Info);

        AntdUI.Style.Set(AntdUI.Colour.PrimaryColor, Color.White);
        AntdUI.Style.Set(AntdUI.Colour.DefaultBg, palette.ContainerBackground);
        AntdUI.Style.Set(AntdUI.Colour.DefaultColor, palette.TextPrimary);
        AntdUI.Style.Set(AntdUI.Colour.DefaultBorder, palette.Border);
        AntdUI.Style.Set(AntdUI.Colour.TagDefaultBg, palette.FillSecondary);
        AntdUI.Style.Set(AntdUI.Colour.TagDefaultColor, palette.TextSecondary);
        AntdUI.Style.Set(AntdUI.Colour.TextBase, palette.TextPrimary);
        AntdUI.Style.Set(AntdUI.Colour.Text, palette.TextPrimary);
        AntdUI.Style.Set(AntdUI.Colour.TextSecondary, palette.TextSecondary);
        AntdUI.Style.Set(AntdUI.Colour.TextTertiary, palette.TextTertiary);
        AntdUI.Style.Set(AntdUI.Colour.TextQuaternary, palette.TextDisabled);
        AntdUI.Style.Set(AntdUI.Colour.BgBase, palette.WindowBackground);
        AntdUI.Style.Set(AntdUI.Colour.BgLayout, palette.LayoutBackground);
        AntdUI.Style.Set(AntdUI.Colour.BgContainer, palette.ContainerBackground);
        AntdUI.Style.Set(AntdUI.Colour.BgElevated, palette.ElevatedBackground);
        AntdUI.Style.Set(AntdUI.Colour.Fill, palette.Fill);
        AntdUI.Style.Set(AntdUI.Colour.FillSecondary, palette.FillSecondary);
        AntdUI.Style.Set(AntdUI.Colour.FillTertiary, palette.FillTertiary);
        AntdUI.Style.Set(AntdUI.Colour.FillQuaternary, palette.FillQuaternary);
        AntdUI.Style.Set(AntdUI.Colour.BorderColor, palette.Border);
        AntdUI.Style.Set(AntdUI.Colour.BorderSecondary, palette.BorderSubtle);
        AntdUI.Style.Set(AntdUI.Colour.BorderColorDisable, palette.BorderDisabled);
        AntdUI.Style.Set(AntdUI.Colour.Split, palette.BorderSubtle);
        AntdUI.Style.Set(AntdUI.Colour.HoverBg, palette.HoverBackground);
        AntdUI.Style.Set(AntdUI.Colour.HoverColor, palette.TextPrimary);
        AntdUI.Style.Set(AntdUI.Colour.SliderHandleColorDisabled, palette.BorderDisabled);
        AntdUI.Style.Set(AntdUI.Colour.TextSpotlight, palette.SpotlightText);
        AntdUI.Style.Set(AntdUI.Colour.BgSpotlight, palette.SpotlightBackground);
        AntdUI.Style.Set(AntdUI.Colour.SwitchHandleBg, palette.SwitchHandleBackground);
    }

    /// <summary>
    /// Walks one mixed AntdUI/WinForms tree while preserving semantic surface depth. / 遍历混合 AntdUI/WinForms 控件树并保留语义表面层级。
    /// </summary>
    private static void ApplyControlTree(Control control, Color inheritedSurface)
    {
        Color childSurface = inheritedSurface;
        if (control is AntdUI.BaseForm)
        {
            control.BackColor = CurrentPalette.WindowBackground;
            control.ForeColor = CurrentPalette.TextPrimary;
            childSurface = CurrentPalette.LayoutBackground;
        }
        else if (control is AntdUI.Panel)
        {
            childSurface = CurrentPalette.ContainerBackground;
        }
        else if (control is not AntdUI.IControl)
        {
            control.BackColor = inheritedSurface;
            control.ForeColor = CurrentPalette.TextPrimary;

            if (control is TextBoxBase or ListControl)
            {
                control.BackColor = CurrentPalette.ContainerBackground;
            }
        }

        if (control.ContextMenuStrip is ContextMenuStrip contextMenu)
        {
            ApplyToolStrip(contextMenu, CurrentPalette);
        }

        if (control is AntdUI.BaseForm or AntdUI.Panel or AntdUI.PageHeader or System.Windows.Forms.Panel or UserControl)
        {
            // Layouts such as the connection-type page are created lazily after a
            // dialog is already visible. Theme future children at the insertion point.
            control.ControlAdded -= HandleThemedControlAdded;
            control.ControlAdded += HandleThemedControlAdded;
        }

        foreach (Control child in control.Controls)
        {
            ApplyControlTree(child, childSurface);
        }
    }

    /// <summary>Applies the active palette to a lazily inserted control subtree. / 将当前色板应用到延迟插入的控件子树。</summary>
    private static void HandleThemedControlAdded(object? sender, ControlEventArgs e)
    {
        if (sender is not Control parent || e.Control is not Control addedControl)
        {
            return;
        }

        Color inheritedSurface = parent switch
        {
            AntdUI.BaseForm => CurrentPalette.LayoutBackground,
            AntdUI.Panel => CurrentPalette.ContainerBackground,
            AntdUI.PageHeader => CurrentPalette.ContainerBackground,
            _ => parent.BackColor
        };
        ApplyControlTree(addedControl, inheritedSurface);
        addedControl.Invalidate(true);
    }

    /// <summary>Colors a native menu and every nested drop-down. / 为原生菜单及全部嵌套下拉菜单着色。</summary>
    private static void ApplyToolStrip(ToolStrip toolStrip, ThemePalette palette)
    {
        toolStrip.Renderer = new ThemeToolStripRenderer(palette);
        toolStrip.BackColor = palette.ElevatedBackground;
        toolStrip.ForeColor = palette.TextPrimary;

        foreach (ToolStripItem item in toolStrip.Items)
        {
            ApplyToolStripItem(item, palette);
        }

        toolStrip.Invalidate();
    }

    /// <summary>Colors one menu item and its optional nested drop-down. / 为单个菜单项及其可选嵌套下拉菜单着色。</summary>
    private static void ApplyToolStripItem(ToolStripItem item, ThemePalette palette)
    {
        item.BackColor = palette.ElevatedBackground;
        item.ForeColor = item.Enabled ? palette.TextPrimary : palette.TextDisabled;
        if (item is ToolStripDropDownItem dropDownItem)
        {
            ApplyToolStrip(dropDownItem.DropDown, palette);
        }
    }

    /// <summary>
    /// Reads the current Windows application color preference, falling back to light mode. / 读取当前 Windows 应用颜色偏好，不可用时回退到浅色模式。
    /// </summary>
    /// <returns><see langword="true"/> when Windows requests dark application colors. / Windows 请求深色应用颜色时返回 <see langword="true"/>。</returns>
    private static bool IsSystemDarkMode()
    {
        const string personalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(personalizeKey, writable: false);
            object? value = key?.GetValue("AppsUseLightTheme");
            return value is int numericValue && numericValue == 0;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            return false;
        }
    }

    /// <summary>Supplies semantic colors for native context menus. / 为原生上下文菜单提供语义颜色。</summary>
    private sealed class ThemeToolStripColorTable(ThemePalette palette) : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => palette.ElevatedBackground;

        public override Color ImageMarginGradientBegin => palette.ElevatedBackground;

        public override Color ImageMarginGradientMiddle => palette.ElevatedBackground;

        public override Color ImageMarginGradientEnd => palette.ElevatedBackground;

        public override Color MenuBorder => palette.Border;

        public override Color MenuItemBorder => palette.Border;

        public override Color MenuItemSelected => palette.HoverBackground;

        public override Color MenuItemSelectedGradientBegin => palette.HoverBackground;

        public override Color MenuItemSelectedGradientEnd => palette.HoverBackground;

        public override Color MenuItemPressedGradientBegin => palette.FillSecondary;

        public override Color MenuItemPressedGradientMiddle => palette.FillSecondary;

        public override Color MenuItemPressedGradientEnd => palette.FillSecondary;

        public override Color SeparatorDark => palette.BorderSubtle;

        public override Color SeparatorLight => palette.BorderSubtle;

        public override Color ToolStripBorder => palette.Border;

        public override Color ToolStripGradientBegin => palette.ElevatedBackground;

        public override Color ToolStripGradientMiddle => palette.ElevatedBackground;

        public override Color ToolStripGradientEnd => palette.ElevatedBackground;

        public override Color ButtonSelectedBorder => palette.Border;

        public override Color ButtonSelectedGradientBegin => palette.HoverBackground;

        public override Color ButtonSelectedGradientMiddle => palette.HoverBackground;

        public override Color ButtonSelectedGradientEnd => palette.HoverBackground;

        public override Color ButtonPressedBorder => palette.Border;

        public override Color ButtonPressedGradientBegin => palette.FillSecondary;

        public override Color ButtonPressedGradientMiddle => palette.FillSecondary;

        public override Color ButtonPressedGradientEnd => palette.FillSecondary;

        public override Color CheckBackground => palette.Fill;

        public override Color CheckSelectedBackground => palette.HoverBackground;

        public override Color CheckPressedBackground => palette.FillSecondary;
    }

    /// <summary>Draws native menu text and arrows with theme-aware contrast. / 使用主题感知的对比色绘制原生菜单文字与箭头。</summary>
    private sealed class ThemeToolStripRenderer(ThemePalette palette)
        : ToolStripProfessionalRenderer(new ThemeToolStripColorTable(palette))
    {
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? palette.TextPrimary : palette.TextDisabled;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item?.Enabled != false ? palette.TextSecondary : palette.TextDisabled;
            base.OnRenderArrow(e);
        }
    }
}
