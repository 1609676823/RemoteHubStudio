namespace RemoteHubStudio.Domain;

/// <summary>
/// Represents one saved remote connection. / 表示一条已保存的远程连接。
/// </summary>
public sealed class ConnectionProfile
{
    /// <summary>Gets or sets the stable identifier. / 获取或设置稳定标识。</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the connection name. / 获取或设置连接名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional category identifier. / 获取或设置可选的分类标识。</summary>
    public Guid? GroupId { get; set; }

    /// <summary>Gets or sets the client type. / 获取或设置客户端类型。</summary>
    public ConnectionType Type { get; set; } = ConnectionType.RemoteDesktop;

    /// <summary>Gets or sets the secondary protocol or mode. / 获取或设置次级协议或模式。</summary>
    public string Protocol { get; set; } = string.Empty;

    /// <summary>Gets or sets the host name, IP address, or device identifier. / 获取或设置主机名、IP 地址或设备标识。</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>Gets or sets the connection port. / 获取或设置连接端口。</summary>
    public int Port { get; set; } = 3389;

    /// <summary>Gets or sets the inline username. / 获取或设置内联用户名。</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the inline password. / 获取或设置内联密码。</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional private-key path. / 获取或设置可选的私钥路径。</summary>
    public string PrivateKeyPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional expiration date. / 获取或设置可选的到期日期。</summary>
    public DateTime? ExpiresOn { get; set; }

    /// <summary>Gets or sets user notes. / 获取或设置用户备注。</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the connection is a favorite. / 获取或设置连接是否为收藏。</summary>
    public bool IsFavorite { get; set; }

    /// <summary>Gets or sets a profile-specific executable override. / 获取或设置配置专属的可执行文件覆盖路径。</summary>
    public string ExecutableOverride { get; set; } = string.Empty;

    /// <summary>Gets or sets a custom argument template. / 获取或设置自定义参数模板。</summary>
    public string CustomArguments { get; set; } = string.Empty;

    /// <summary>Gets or sets Remote Desktop options. / 获取或设置远程桌面选项。</summary>
    public RdpOptions Rdp { get; set; } = new();

    /// <summary>Gets or sets client-specific option values. / 获取或设置客户端专属选项值。</summary>
    public Dictionary<string, string> Options { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the UTC creation time. / 获取或设置 UTC 创建时间。</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the UTC update time. / 获取或设置 UTC 更新时间。</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
