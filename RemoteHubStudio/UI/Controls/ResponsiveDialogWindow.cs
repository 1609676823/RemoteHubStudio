using System.ComponentModel;
using System.Windows.Forms;
using RemoteHubStudio.UI.Theme;
using RemoteHubStudio.UI.Branding;

namespace RemoteHubStudio.UI.Controls;

/// <summary>
/// Supplies reusable AntdUI dialog chrome, scrolling content, sections, and a responsive footer. / 提供可复用的 AntdUI 对话框外观、滚动内容、分区与响应式页脚。
/// </summary>
[DesignerCategory("Form")]
public partial class ResponsiveDialogWindow : AntdUI.Window
{
    private readonly Dictionary<Control, AntdUI.Panel> _sectionByContent = [];

    /// <summary>
    /// Initializes the designer-compatible dialog chrome. / 初始化兼容设计器的对话框外观。
    /// </summary>
    public ResponsiveDialogWindow()
    {
        InitializeComponent();
        Icon = AppIcons.Application;
    }

    /// <summary>
    /// Initializes standard dialog chrome and responsive content hosts. / 初始化标准对话框外观与响应式内容容器。
    /// </summary>
    /// <param name="title">Window title. / 窗口标题。</param>
    /// <param name="initialSize">Initial window size. / 初始窗口尺寸。</param>
    /// <param name="minimumSize">Minimum supported window size. / 支持的最小窗口尺寸。</param>
    protected ResponsiveDialogWindow(string title, Size initialSize, Size minimumSize)
        : this()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Text = title;
        Header.Text = title;
        Size = initialSize;
        MinimumSize = minimumSize;
    }

    /// <summary>
    /// Gets the standard AntdUI page header. / 获取标准 AntdUI 页头。
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    protected AntdUI.PageHeader Header => _header;

    /// <summary>
    /// Gets the inherited flow host into which derived designer files add their sections. / 获取派生设计器文件用于加入分区的继承流式容器。
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    protected FlowLayoutPanel ContentHost => _contentFlow;

    /// <summary>
    /// Gets the inherited right-aligned host into which derived designer files add buttons. / 获取派生设计器文件用于加入按钮的继承右对齐容器。
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    protected FlowLayoutPanel FooterHost => _footerFlow;

    /// <summary>
    /// Applies the current explicit window mode and native-container colors before the dialog becomes visible. / 在对话框显示前应用当前显式窗口模式与原生容器颜色。
    /// </summary>
    /// <param name="e">Load event data. / 加载事件数据。</param>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ThemeManager.ApplyTo(this);
    }

    /// <summary>
    /// Clamps the dialog to the active monitor before first display so small screens can reach every action. / 首次显示前将对话框限制在当前显示器内，使小屏幕也能访问所有操作。
    /// </summary>
    /// <param name="e">Shown event data. / 显示事件数据。</param>
    protected override void OnShown(EventArgs e)
    {
        // VS displays an instance of this base class as the derived form's design surface.
        // Moving that instance to screen coordinates pushes it outside the designer viewport.
        // / VS 使用本基类作为派生窗体的设计画布；按显示器居中会将画布移出可见区域。
        if (DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime)
        {
            base.OnShown(e);
            return;
        }

        Rectangle workingArea = Screen.FromControl(this).WorkingArea;
        int availableWidth = Math.Max(1, workingArea.Width - ScaleLogical(32));
        int availableHeight = Math.Max(1, workingArea.Height - ScaleLogical(32));

        MinimumSize = new Size(
            Math.Min(MinimumSize.Width, availableWidth),
            Math.Min(MinimumSize.Height, availableHeight));
        Size = new Size(
            Math.Min(Width, availableWidth),
            Math.Min(Height, availableHeight));
        Location = new Point(
            Math.Clamp(workingArea.Left + (workingArea.Width - Width) / 2, workingArea.Left, workingArea.Right - Width),
            Math.Clamp(workingArea.Top + (workingArea.Height - Height) / 2, workingArea.Top, workingArea.Bottom - Height));

        base.OnShown(e);
    }

    /// <summary>
    /// Reflows dialog sections after a per-monitor DPI transition has scaled their child controls. / 每显示器 DPI 切换缩放子控件后重新排列对话框分区。
    /// </summary>
    /// <param name="e">Per-monitor DPI change data. / 每显示器 DPI 变化数据。</param>
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        HandleScrollHostSizeChanged(this, EventArgs.Empty);
        foreach (KeyValuePair<Control, AntdUI.Panel> entry in _sectionByContent)
        {
            UpdateSectionHeight(entry.Key, entry.Value);
        }
    }

    /// <summary>
    /// Adds a responsive visual section around the supplied content. / 在指定内容外添加响应式可视分区。
    /// </summary>
    /// <param name="title">Section title. / 分区标题。</param>
    /// <param name="content">Section content. / 分区内容。</param>
    /// <returns>The section panel, useful for conditional visibility. / 分区面板，可用于条件显示。</returns>
    protected AntdUI.Panel AddSection(string title, Control content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(content);

        AntdUI.Label titleLabel = new()
        {
            Dock = DockStyle.Top,
            Height = 36,
            Text = title,
            Font = new Font(Font.FontFamily, 11F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 8, 0)
        };

        AntdUI.Panel section = new()
        {
            Radius = 10,
            BorderWidth = 1F,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(8),
            Width = Math.Max(ScaleLogical(100), ContentHost.ClientSize.Width - ContentHost.Padding.Horizontal)
        };
        section.Controls.Add(content);
        section.Controls.Add(titleLabel);
        ContentHost.Controls.Add(section);
        RegisterSection(content, section);
        return section;
    }

    /// <summary>
    /// Registers a section created by a derived designer file for responsive width and height updates. / 注册由派生设计器文件创建的分区，以响应式更新宽度和高度。
    /// </summary>
    /// <param name="content">The section's content control. / 分区内容控件。</param>
    /// <param name="section">The panel containing the title and content. / 包含标题和内容的面板。</param>
    protected void RegisterSection(Control content, AntdUI.Panel section)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(section);

        if (_sectionByContent.TryGetValue(content, out AntdUI.Panel? existingSection))
        {
            if (!ReferenceEquals(existingSection, section))
            {
                throw new InvalidOperationException("The content control is already registered with another section.");
            }

            UpdateSectionHeight(content, section);
            return;
        }

        content.Dock = DockStyle.Top;
        content.SizeChanged += HandleSectionContentSizeChanged;
        _sectionByContent.Add(content, section);
        section.Width = Math.Max(ScaleLogical(100), ContentHost.ClientSize.Width - ContentHost.Padding.Horizontal);
        UpdateSectionHeight(content, section);
    }

    /// <summary>
    /// Adds an action button to the right-aligned footer. / 向右对齐页脚添加操作按钮。
    /// </summary>
    /// <param name="button">Button to add. / 要添加的按钮。</param>
    protected void AddFooterButton(AntdUI.Button button)
    {
        ArgumentNullException.ThrowIfNull(button);
        button.Height = 38;
        button.Margin = new Padding(8, 0, 0, 0);
        FooterHost.Controls.Add(button);
    }

    /// <summary>
    /// Applies a result and closes the dialog. / 应用结果并关闭对话框。
    /// </summary>
    /// <param name="result">Dialog result. / 对话框结果。</param>
    protected void CompleteDialog(DialogResult result)
    {
        DialogResult = result;
        Close();
    }

    /// <summary>
    /// Updates content width when the scrolling viewport changes. / 滚动视口变化时更新内容宽度。
    /// </summary>
    /// <param name="sender">Event sender. / 事件发送者。</param>
    /// <param name="e">Event data. / 事件数据。</param>
    private void HandleScrollHostSizeChanged(object? sender, EventArgs e)
    {
        int width = Math.Max(ScaleLogical(100), _scrollHost.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
        ContentHost.Width = width;

        foreach (Control child in ContentHost.Controls)
        {
            child.Width = Math.Max(ScaleLogical(100), width - ContentHost.Padding.Horizontal);
        }
    }

    /// <summary>
    /// Keeps a section tall enough for responsive content after reflow. / 响应式内容重排后保持分区高度足够。
    /// </summary>
    /// <param name="sender">Event sender. / 事件发送者。</param>
    /// <param name="e">Event data. / 事件数据。</param>
    private void HandleSectionContentSizeChanged(object? sender, EventArgs e)
    {
        if (sender is Control content && _sectionByContent.TryGetValue(content, out AntdUI.Panel? section))
        {
            UpdateSectionHeight(content, section);
        }
    }

    /// <summary>
    /// Calculates the containing section height from its header and content. / 根据标题与内容计算分区容器高度。
    /// </summary>
    /// <param name="content">Section content. / 分区内容。</param>
    /// <param name="section">Containing section. / 分区容器。</param>
    private void UpdateSectionHeight(Control content, Control section)
    {
        section.Height = Math.Max(
            ScaleLogical(80),
            ScaleLogical(36) + content.Height + section.Padding.Vertical);
    }

    /// <summary>
    /// Converts a logical 96-DPI measurement to physical pixels for the dialog's current monitor. / 将 96 DPI 逻辑尺寸转换为对话框当前显示器的物理像素。
    /// </summary>
    /// <param name="logicalPixels">Logical pixel measurement at 96 DPI. / 96 DPI 下的逻辑像素尺寸。</param>
    /// <returns>Rounded physical pixel measurement. / 四舍五入后的物理像素尺寸。</returns>
    private int ScaleLogical(int logicalPixels)
    {
        float scale = DeviceDpi <= 0 ? 1F : DeviceDpi / 96F;
        return Math.Max(0, (int)Math.Round(logicalPixels * scale, MidpointRounding.AwayFromZero));
    }
}
