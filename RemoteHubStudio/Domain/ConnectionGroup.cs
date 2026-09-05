namespace RemoteHubStudio.Domain;

/// <summary>
/// Represents a nested connection category. / 表示可嵌套的连接分类。
/// </summary>
public sealed class ConnectionGroup
{
    /// <summary>Gets or sets the stable identifier. / 获取或设置稳定标识。</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the category name. / 获取或设置分类名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional parent category. / 获取或设置可选的父分类。</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Gets or sets the accent color in HTML format. / 获取或设置 HTML 格式的强调色。</summary>
    public string Color { get; set; } = "#1677FF";

    /// <summary>Gets or sets the user-defined display order. / 获取或设置用户定义的显示顺序。</summary>
    public int SortOrder { get; set; }
}
