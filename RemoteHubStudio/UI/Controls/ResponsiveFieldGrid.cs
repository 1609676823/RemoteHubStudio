using System.ComponentModel;
using System.Windows.Forms;

namespace RemoteHubStudio.UI.Controls;

/// <summary>
/// Arranges labeled editors in two columns on wide windows and one column on narrow windows. / 在宽窗口中以两列、在窄窗口中以单列排列带标签的编辑器。
/// </summary>
public sealed class ResponsiveFieldGrid : TableLayoutPanel
{
    private const int DefaultBreakpoint = 760;
    private const int LabelWidth = 150;
    private const int RowHeight = 48;
    private const int EditorVerticalMargin = 10;
    private const int SwitchWidth = 60;
    private const int SwitchHeight = 32;
    private readonly List<FieldEntry> _fields = [];
    private bool _wideLayout;

    /// <summary>
    /// Initializes a responsive, auto-sized field grid. / 初始化可响应且自动调整大小的字段网格。
    /// </summary>
    public ResponsiveFieldGrid()
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Dock = DockStyle.Top;
        GrowStyle = TableLayoutPanelGrowStyle.AddRows;
        Margin = Padding.Empty;
        Padding = new Padding(4, 2, 4, 4);
        SizeChanged += HandleSizeChanged;
    }

    /// <summary>
    /// Gets or sets the width at which the grid changes to two field columns. / 获取或设置网格切换为两个字段列的宽度。
    /// </summary>
    [DefaultValue(DefaultBreakpoint)]
    public int WideLayoutBreakpoint { get; set; } = DefaultBreakpoint;

    /// <summary>
    /// Adds one labeled editor to the responsive grid. / 向响应式网格添加一个带标签的编辑器。
    /// </summary>
    /// <param name="labelText">Bilingual field label. / 双语字段标签。</param>
    /// <param name="editor">Editor control. / 编辑器控件。</param>
    public void AddField(string labelText, Control editor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(labelText);
        ArgumentNullException.ThrowIfNull(editor);

        AntdUI.Label label = CreateLabel(labelText);
        FieldEntry field = new(label, editor, editor.Height, editor.DeviceDpi);
        ConfigureEditorLayout(label, editor);
        _fields.Add(field);
        ApplyLayout(force: true);
    }

    /// <summary>Shows or hides a complete label/editor field and compacts the remaining rows. / 显示或隐藏完整的标签/编辑器字段，并压紧剩余行。</summary>
    /// <param name="editor">Registered editor control. / 已注册的编辑控件。</param>
    /// <param name="visible">Whether the field should participate in layout. / 该字段是否参与布局。</param>
    public void SetFieldVisible(Control editor, bool visible)
    {
        ArgumentNullException.ThrowIfNull(editor);
        FieldEntry field = _fields.FirstOrDefault(item => ReferenceEquals(item.Editor, editor))
            ?? throw new ArgumentException("The editor is not registered in this field grid. / 编辑控件尚未注册到此字段网格。", nameof(editor));
        if (field.Visible == visible)
        {
            return;
        }

        field.Visible = visible;
        ApplyLayout(force: true);
    }

    /// <summary>Changes the label associated with a registered editor. / 更改已注册编辑控件对应的标签。</summary>
    /// <param name="editor">Registered editor control. / 已注册的编辑控件。</param>
    /// <param name="labelText">New bilingual label. / 新的双语标签。</param>
    public void SetFieldLabel(Control editor, string labelText)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelText);
        FieldEntry field = _fields.FirstOrDefault(item => ReferenceEquals(item.Editor, editor))
            ?? throw new ArgumentException("The editor is not registered in this field grid. / 编辑控件尚未注册到此字段网格。", nameof(editor));
        field.Label.Text = labelText;
        editor.AccessibleName = labelText;
    }

    /// <summary>
    /// Registers a label and editor that were created by a form's designer code. / 注册由窗体设计器代码创建的标签与编辑器。
    /// </summary>
    /// <param name="label">Designer-created field label. / 设计器创建的字段标签。</param>
    /// <param name="editor">Designer-created field editor. / 设计器创建的字段编辑器。</param>
    internal void RegisterField(Control label, Control editor)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(editor);
        if (_fields.Any(field => ReferenceEquals(field.Label, label) || ReferenceEquals(field.Editor, editor)))
        {
            return;
        }

        FieldEntry field = new(label, editor, editor.Height, editor.DeviceDpi);
        ConfigureEditorLayout(label, editor);
        _fields.Add(field);
        ApplyLayout(force: true);
    }

    /// <summary>
    /// Keeps compact editors at their intended size while stretchable inputs continue to fill their cell. / 让紧凑编辑器保持预期尺寸，同时让可拉伸输入框继续填满单元格。
    /// </summary>
    /// <param name="label">The editor's visible field label. / 编辑器对应的可见字段标签。</param>
    /// <param name="editor">Editor whose layout is being configured. / 要配置布局的编辑器。</param>
    private void ConfigureEditorLayout(Control label, Control editor)
    {
        editor.Margin = new Padding(4, 5, 10, 5);
        if (string.IsNullOrWhiteSpace(editor.AccessibleName))
        {
            editor.AccessibleName = label.Text;
        }

        if (editor is AntdUI.Switch switchEditor)
        {
            switchEditor.Dock = DockStyle.None;
            switchEditor.Anchor = AnchorStyles.Left;
            UpdateSwitchSize(switchEditor);
            return;
        }

        editor.Dock = DockStyle.Fill;
    }

    /// <summary>
    /// Applies the standard AntdUI switch proportions at the grid's current monitor DPI. / 按网格当前显示器 DPI 应用标准 AntdUI 开关比例。
    /// </summary>
    /// <param name="switchEditor">Switch to resize. / 要调整尺寸的开关。</param>
    private void UpdateSwitchSize(AntdUI.Switch switchEditor)
    {
        switchEditor.Size = new Size(ScaleLogical(SwitchWidth), ScaleLogical(SwitchHeight));
    }

    /// <summary>
    /// Creates a consistently styled label for a field. / 为字段创建样式一致的标签。
    /// </summary>
    /// <param name="text">Label text. / 标签文本。</param>
    /// <returns>The configured AntdUI label. / 配置后的 AntdUI 标签。</returns>
    private static AntdUI.Label CreateLabel(string text)
    {
        return new AntdUI.Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(8, 5, 4, 5)
        };
    }

    /// <summary>
    /// Rebuilds rows and columns for the current responsive state. / 为当前响应状态重建行与列。
    /// </summary>
    /// <param name="force">Whether to rebuild even when the breakpoint state is unchanged. / 断点状态未变时是否仍强制重建。</param>
    private void ApplyLayout(bool force)
    {
        // Source designers replay InitializeComponent without RegisterField. DPI notifications
        // must not clear that unregistered tree, including before Site has been assigned.
        // / 源设计器不会调用 RegisterField；设置 Site 前后的 DPI 通知都不能清空序列化控件。
        if (_fields.Count == 0 || DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime)
        {
            return;
        }

        float scale = DeviceDpi <= 0 ? 1F : DeviceDpi / 96F;
        float logicalWidth = ClientSize.Width / scale;
        bool useWideLayout = logicalWidth >= WideLayoutBreakpoint;
        if (!force && useWideLayout == _wideLayout)
        {
            return;
        }

        _wideLayout = useWideLayout;
        SuspendLayout();
        Controls.Clear();
        ColumnStyles.Clear();
        RowStyles.Clear();

        foreach (FieldEntry field in _fields.Where(field => field.Visible))
        {
            if (field.Editor is AntdUI.Switch switchEditor)
            {
                UpdateSwitchSize(switchEditor);
            }
        }

        if (_wideLayout)
        {
            ConfigureWideColumns();
        }
        else
        {
            ConfigureNarrowColumns();
        }

        AddFieldsToRows();
        ResumeLayout(performLayout: true);
    }

    /// <summary>
    /// Configures label/editor pairs for a two-field-wide row. / 配置每行两个字段的标签与编辑器列。
    /// </summary>
    private void ConfigureWideColumns()
    {
        ColumnCount = 4;
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleLogical(LabelWidth)));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleLogical(LabelWidth)));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
    }

    /// <summary>
    /// Configures one label/editor pair per row. / 配置每行一个标签与编辑器。
    /// </summary>
    private void ConfigureNarrowColumns()
    {
        ColumnCount = 2;
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleLogical(LabelWidth)));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
    }

    /// <summary>
    /// Places all registered fields into the configured grid. / 将所有已注册字段放入配置后的网格。
    /// </summary>
    private void AddFieldsToRows()
    {
        FieldEntry[] visibleFields = _fields.Where(field => field.Visible).ToArray();
        int fieldsPerRow = _wideLayout ? 2 : 1;
        RowCount = (visibleFields.Length + fieldsPerRow - 1) / fieldsPerRow;

        for (int row = 0; row < RowCount; row++)
        {
            int firstIndex = row * fieldsPerRow;
            int preferredHeight = GetPreferredRowHeight(visibleFields[firstIndex]);
            if (fieldsPerRow == 2 && firstIndex + 1 < visibleFields.Length)
            {
                preferredHeight = Math.Max(preferredHeight, GetPreferredRowHeight(visibleFields[firstIndex + 1]));
            }

            RowStyles.Add(new RowStyle(SizeType.Absolute, preferredHeight));
        }

        for (int index = 0; index < visibleFields.Length; index++)
        {
            FieldEntry field = visibleFields[index];
            int row = index / fieldsPerRow;
            int column = _wideLayout ? (index % fieldsPerRow) * 2 : 0;
            Controls.Add(field.Label, column, row);
            Controls.Add(field.Editor, column + 1, row);
        }
    }

    /// <summary>
    /// Re-evaluates the layout when the available width changes. / 可用宽度变化时重新评估布局。
    /// </summary>
    /// <param name="sender">Event sender. / 事件发送者。</param>
    /// <param name="e">Event data. / 事件数据。</param>
    private void HandleSizeChanged(object? sender, EventArgs e)
    {
        // InitializeComponent may assign the serialized controls before the owning form
        // registers their label/editor pairs. Do not erase that design-time tree while
        // the grid is still in this unregistered initialization state.
        if (_fields.Count == 0)
        {
            return;
        }

        ApplyLayout(force: false);
    }

    /// <summary>
    /// Rebuilds absolute row and column measurements after the parent applies a new monitor DPI. / 父容器应用新显示器 DPI 后重建绝对行列尺寸。
    /// </summary>
    /// <param name="e">DPI-change event data. / DPI 变化事件数据。</param>
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyLayout(force: true);
    }

    /// <summary>
    /// Releases registered fields that may currently be detached from the WinForms control tree.
    /// Hidden fields are removed by <see cref="ApplyLayout(bool)"/>, so the base container cannot
    /// discover and dispose them on its own. / 释放可能已从 WinForms 控件树脱离的已注册字段；
    /// 隐藏字段会被 <see cref="ApplyLayout(bool)"/> 移出，基类容器无法自行发现并释放它们。
    /// </summary>
    /// <param name="disposing">Whether managed resources should be released. / 是否应释放托管资源。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SizeChanged -= HandleSizeChanged;
            foreach (FieldEntry field in _fields)
            {
                if (!Controls.Contains(field.Label))
                {
                    field.Label.Dispose();
                }

                if (!Controls.Contains(field.Editor))
                {
                    field.Editor.Dispose();
                }
            }

            _fields.Clear();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Calculates a physical row height from the logical baseline and the editor's registered design height.
    /// The live editor height cannot be used because a fill-docked editor reflects the previous table layout.
    /// / 根据逻辑基线和编辑器注册时的设计高度计算物理行高；填充停靠控件的实时高度来自上一次表格布局，不能用于反向测量。
    /// </summary>
    /// <param name="field">Field whose editor determines the preferred row height. / 其编辑器决定首选行高的字段。</param>
    /// <returns>Preferred physical row height. / 首选物理行高。</returns>
    private int GetPreferredRowHeight(FieldEntry field)
    {
        int preferredEditorHeight = ScaleFromDpi(field.PreferredEditorHeight, field.PreferredEditorDpi);
        return Math.Max(ScaleLogical(RowHeight), preferredEditorHeight + ScaleLogical(EditorVerticalMargin));
    }

    /// <summary>
    /// Scales a captured physical measurement from its registration DPI to the grid's current DPI.
    /// / 将注册时捕获的物理尺寸从当时的 DPI 缩放到网格当前 DPI。
    /// </summary>
    /// <param name="physicalPixels">Physical pixels captured at registration. / 注册时捕获的物理像素。</param>
    /// <param name="sourceDpi">DPI used by the editor at registration. / 编辑器注册时使用的 DPI。</param>
    /// <returns>The equivalent physical measurement at the current DPI. / 当前 DPI 下的等效物理尺寸。</returns>
    private int ScaleFromDpi(int physicalPixels, int sourceDpi)
    {
        int normalizedSourceDpi = sourceDpi <= 0 ? 96 : sourceDpi;
        int targetDpi = DeviceDpi <= 0 ? 96 : DeviceDpi;
        return Math.Max(
            0,
            (int)Math.Round(
                physicalPixels * targetDpi / (double)normalizedSourceDpi,
                MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Converts a logical 96-DPI measurement to physical pixels inherited from the parent window. / 将 96 DPI 逻辑尺寸转换为从父窗口继承的物理像素。
    /// </summary>
    /// <param name="logicalPixels">Logical pixel measurement at 96 DPI. / 96 DPI 下的逻辑像素尺寸。</param>
    /// <returns>Rounded physical pixel measurement. / 四舍五入后的物理像素尺寸。</returns>
    private int ScaleLogical(int logicalPixels)
    {
        float scale = DeviceDpi <= 0 ? 1F : DeviceDpi / 96F;
        return Math.Max(0, (int)Math.Round(logicalPixels * scale, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Stores one label/editor pair without coupling it to a row. / 存储一组与行无关的标签与编辑器。
    /// </summary>
    private sealed class FieldEntry
    {
        /// <summary>
        /// Initializes one field entry. / 初始化一个字段项。
        /// </summary>
        /// <param name="label">Field label. / 字段标签。</param>
        /// <param name="editor">Field editor. / 字段编辑器。</param>
        /// <param name="preferredEditorHeight">Editor height before any grid layout. / 网格布局前的编辑器高度。</param>
        /// <param name="preferredEditorDpi">Editor DPI when its height was captured. / 捕获编辑器高度时的 DPI。</param>
        public FieldEntry(Control label, Control editor, int preferredEditorHeight, int preferredEditorDpi)
        {
            Label = label;
            Editor = editor;
            PreferredEditorHeight = Math.Max(0, preferredEditorHeight);
            PreferredEditorDpi = preferredEditorDpi <= 0 ? 96 : preferredEditorDpi;
        }

        public Control Label { get; }

        public Control Editor { get; }

        public int PreferredEditorHeight { get; }

        public int PreferredEditorDpi { get; }

        public bool Visible { get; set; } = true;
    }
}
