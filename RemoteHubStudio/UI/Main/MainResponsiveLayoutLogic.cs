namespace RemoteHubStudio.UI.Main;

/// <summary>
/// Describes DPI-independent toolbar layout choices for the main workspace. / 描述主工作区与 DPI 无关的工具栏布局选择。
/// </summary>
public sealed class MainResponsiveLayoutPlan
{
    /// <summary>Gets the logical toolbar height. / 获取逻辑工具栏高度。</summary>
    public int ToolbarHeight { get; init; }

    /// <summary>Gets the logical height of the quick-filter and secondary-command row. / 获取快捷筛选与次要命令行的逻辑高度。</summary>
    public int SecondaryToolbarHeight { get; init; }

    /// <summary>Gets the logical search-box width. / 获取逻辑搜索框宽度。</summary>
    public int SearchWidth { get; init; }

    /// <summary>Gets the logical type-filter width. / 获取逻辑类型筛选器宽度。</summary>
    public int TypeFilterWidth { get; init; }

    /// <summary>Gets whether toolbar labels use compact Chinese text alongside their icons. / 获取工具栏标签是否配合图标使用紧凑中文文本。</summary>
    public bool CompactToolbarText { get; init; }

    /// <summary>Gets whether secondary commands move into a More menu to preserve table height. / 获取次要命令是否移入“更多”菜单以保留表格高度。</summary>
    public bool UseToolbarOverflow { get; init; }

    /// <summary>Gets the logical table height reserved by the breakpoint calculation. / 获取断点计算保留的逻辑表格高度。</summary>
    public int MinimumTableHeight { get; init; }
}

/// <summary>
/// Computes main-window breakpoints without depending on WinForms state. / 在不依赖 WinForms 状态的情况下计算主窗口断点。
/// </summary>
public static class MainResponsiveLayoutLogic
{
    private const int FixedChromeHeight = 110;
    private const int ReservedTableHeight = 80;
    private const int SecondaryToolbarHeight = 48;

    /// <summary>
    /// Builds a logical layout plan that moves secondary commands into overflow before the toolbar becomes crowded. / 构建逻辑布局计划，在工具栏拥挤前将次要命令移入溢出菜单。
    /// </summary>
    /// <param name="logicalToolbarWidth">DPI-independent toolbar content width after sidebar and padding. / 扣除侧栏与内边距后的 DPI 无关工具栏内容宽度。</param>
    /// <param name="logicalHeight">DPI-independent client height. / 与 DPI 无关的客户区高度。</param>
    /// <returns>A deterministic responsive layout plan. / 确定性响应式布局计划。</returns>
    public static MainResponsiveLayoutPlan CreatePlan(float logicalToolbarWidth, float logicalHeight)
    {
        if (!float.IsFinite(logicalToolbarWidth) || logicalToolbarWidth < 0F)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalToolbarWidth));
        }

        if (!float.IsFinite(logicalHeight) || logicalHeight < 0F)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalHeight));
        }

        int toolbarHeight = logicalToolbarWidth >= 700F
            ? 56
            : logicalToolbarWidth >= 380F
                ? 104
                : 152;
        bool useToolbarOverflow = logicalToolbarWidth < 1180F ||
                                  logicalHeight < FixedChromeHeight + toolbarHeight + SecondaryToolbarHeight + ReservedTableHeight;

        int searchWidth = logicalToolbarWidth >= 1150F ? 300 : logicalToolbarWidth >= 700F ? 250 : logicalToolbarWidth >= 480F ? 200 : 160;
        int typeFilterWidth = logicalToolbarWidth >= 700F ? 174 : logicalToolbarWidth >= 480F ? 150 : 136;
        return new MainResponsiveLayoutPlan
        {
            ToolbarHeight = toolbarHeight,
            SecondaryToolbarHeight = SecondaryToolbarHeight,
            SearchWidth = searchWidth,
            TypeFilterWidth = typeFilterWidth,
            CompactToolbarText = logicalToolbarWidth < 1180F,
            UseToolbarOverflow = useToolbarOverflow,
            MinimumTableHeight = ReservedTableHeight
        };
    }

    /// <summary>
    /// Calculates exact wrapped content height from measured outer item sizes. / 根据测得的控件外部尺寸精确计算换行内容高度。
    /// </summary>
    /// <param name="availableWidth">Available content width excluding horizontal padding. / 不含水平内边距的可用内容宽度。</param>
    /// <param name="verticalPadding">Combined top and bottom padding. / 上下内边距总和。</param>
    /// <param name="itemOuterSizes">Visible item sizes including margins. / 包含外边距的可见控件尺寸。</param>
    /// <returns>Total wrapped height including vertical padding. / 包含垂直内边距的换行总高度。</returns>
    public static int CalculateWrappedHeight(
        int availableWidth,
        int verticalPadding,
        IReadOnlyList<Size> itemOuterSizes)
    {
        if (availableWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableWidth));
        }

        if (verticalPadding < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(verticalPadding));
        }

        ArgumentNullException.ThrowIfNull(itemOuterSizes);
        int totalHeight = verticalPadding;
        int rowWidth = 0;
        int rowHeight = 0;
        foreach (Size itemSize in itemOuterSizes)
        {
            if (itemSize.Width <= 0 || itemSize.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(itemOuterSizes));
            }

            if (rowWidth > 0 && rowWidth + itemSize.Width > availableWidth)
            {
                totalHeight += rowHeight;
                rowWidth = 0;
                rowHeight = 0;
            }

            rowWidth += itemSize.Width;
            rowHeight = Math.Max(rowHeight, itemSize.Height);
        }

        return totalHeight + rowHeight;
    }

    /// <summary>
    /// Clamps restored window size and location completely inside one monitor working area. / 将恢复的窗口大小与位置完整限制在一个显示器工作区内。
    /// </summary>
    /// <param name="requestedBounds">Persisted or current window bounds. / 持久化或当前窗口边界。</param>
    /// <param name="workingArea">Target monitor working area. / 目标显示器工作区。</param>
    /// <param name="minimumSize">Preferred minimum window size. / 首选窗口最小尺寸。</param>
    /// <param name="fallbackSize">Fallback size for invalid persisted dimensions. / 持久化尺寸无效时的回退尺寸。</param>
    /// <param name="margin">Physical margin retained inside the working area. / 工作区内保留的物理边距。</param>
    /// <returns>Fully reachable physical window bounds. / 完全可达的物理窗口边界。</returns>
    public static Rectangle ClampWindowBounds(
        Rectangle requestedBounds,
        Rectangle workingArea,
        Size minimumSize,
        Size fallbackSize,
        int margin)
    {
        if (workingArea.Width <= 0 || workingArea.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workingArea));
        }

        if (margin < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(margin));
        }

        int horizontalMargin = Math.Min(margin, Math.Max(0, (workingArea.Width - 1) / 2));
        int verticalMargin = Math.Min(margin, Math.Max(0, (workingArea.Height - 1) / 2));
        Rectangle reachableArea = new(
            workingArea.Left + horizontalMargin,
            workingArea.Top + verticalMargin,
            Math.Max(1, workingArea.Width - horizontalMargin * 2),
            Math.Max(1, workingArea.Height - verticalMargin * 2));
        bool hasValidPersistedSize = requestedBounds.Width >= Math.Max(1, minimumSize.Width) &&
                                     requestedBounds.Height >= Math.Max(1, minimumSize.Height);
        Size sourceSize = hasValidPersistedSize ? requestedBounds.Size : fallbackSize;
        int width = Math.Clamp(Math.Max(sourceSize.Width, Math.Min(minimumSize.Width, reachableArea.Width)), 1, reachableArea.Width);
        int height = Math.Clamp(Math.Max(sourceSize.Height, Math.Min(minimumSize.Height, reachableArea.Height)), 1, reachableArea.Height);
        int centeredX = reachableArea.Left + (reachableArea.Width - width) / 2;
        int centeredY = reachableArea.Top + (reachableArea.Height - height) / 2;
        int sourceX = hasValidPersistedSize ? requestedBounds.X : centeredX;
        int sourceY = hasValidPersistedSize ? requestedBounds.Y : centeredY;
        int x = Math.Clamp(sourceX, reachableArea.Left, reachableArea.Right - width);
        int y = Math.Clamp(sourceY, reachableArea.Top, reachableArea.Bottom - height);
        return new Rectangle(x, y, width, height);
    }
}
