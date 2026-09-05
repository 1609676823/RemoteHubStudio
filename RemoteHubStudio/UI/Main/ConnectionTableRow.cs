namespace RemoteHubStudio.UI.Main;

/// <summary>
/// Provides a read-only table projection for one connection profile. / 为一条连接配置提供只读表格投影。
/// </summary>
public sealed class ConnectionTableRow
{
    /// <summary>Gets or initializes the connection identifier. / 获取或初始化连接标识。</summary>
    public Guid Id { get; init; }

    /// <summary>Gets or initializes the one-click favorite control. / 获取或初始化一键收藏控件。</summary>
    public AntdUI.CellButton? Favorite { get; init; }

    /// <summary>Gets or initializes the display name. / 获取或初始化显示名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets or initializes the client display name. / 获取或初始化客户端显示名称。</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Gets or initializes the formatted endpoint. / 获取或初始化格式化端点。</summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>Gets or initializes the category path. / 获取或初始化分类路径。</summary>
    public string Group { get; init; } = string.Empty;

    /// <summary>Gets or initializes the effective username. / 获取或初始化有效用户名。</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Gets or initializes the latest reachability text. / 获取或初始化最近可达性文本。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Gets or initializes the expiration text. / 获取或初始化到期文本。</summary>
    public string Expiration { get; init; } = string.Empty;

    /// <summary>Gets or initializes optional notes. / 获取或初始化可选备注。</summary>
    public string Notes { get; init; } = string.Empty;
}
