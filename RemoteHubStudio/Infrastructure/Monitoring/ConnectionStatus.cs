namespace RemoteHubStudio.Infrastructure.Monitoring;

/// <summary>
/// Describes the latest reachability probe for one connection. / 描述一条连接最近一次的可达性检测结果。
/// </summary>
public sealed class ConnectionStatus
{
    /// <summary>Gets or sets the connection identifier. / 获取或设置连接标识。</summary>
    public Guid ConnectionId { get; set; }

    /// <summary>Gets or sets the reachability state. / 获取或设置可达性状态。</summary>
    public ReachabilityState State { get; set; } = ReachabilityState.Unknown;

    /// <summary>Gets or sets round-trip latency in milliseconds. / 获取或设置往返延迟毫秒数。</summary>
    public long? LatencyMilliseconds { get; set; }

    /// <summary>Gets or sets the UTC probe time. / 获取或设置 UTC 检测时间。</summary>
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets a non-sensitive diagnostic message. / 获取或设置不含敏感信息的诊断消息。</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Defines conservative network reachability states. / 定义保守的网络可达性状态。
/// </summary>
public enum ReachabilityState
{
    Unknown,
    Checking,
    NotApplicable,
    Reachable,
    NoIcmpResponse,
    InvalidAddress,
    Error
}
