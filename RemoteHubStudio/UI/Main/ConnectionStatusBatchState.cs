using RemoteHubStudio.Infrastructure.Monitoring;

namespace RemoteHubStudio.UI.Main;

/// <summary>
/// Owns generation-scoped connection statuses so superseded checks cannot leave stale pending state. / 管理按代数隔离的连接状态，避免已替换检测留下陈旧的等待状态。
/// </summary>
public sealed class ConnectionStatusBatchState
{
    private readonly Dictionary<Guid, ConnectionStatus> _statuses = [];
    private readonly HashSet<Guid> _pendingConnectionIds = [];

    /// <summary>
    /// Gets the generation of the most recently started batch. / 获取最近启动批次的代数。
    /// </summary>
    public long CurrentGeneration { get; private set; }

    /// <summary>
    /// Starts a new batch, clears unresolved state from the replaced batch, and marks the supplied connections as pending. / 启动新批次、清除被替换批次的未完成状态，并将给定连接标记为等待中。
    /// </summary>
    /// <param name="connectionIds">Connections owned by the new batch. / 新批次拥有的连接。</param>
    /// <returns>The generation assigned to the new batch. / 分配给新批次的代数。</returns>
    public long BeginBatch(IEnumerable<Guid> connectionIds)
    {
        ArgumentNullException.ThrowIfNull(connectionIds);

        ClearPendingStatuses();
        CurrentGeneration++;
        foreach (Guid connectionId in connectionIds.Where(id => id != Guid.Empty).Distinct())
        {
            _pendingConnectionIds.Add(connectionId);
            _statuses[connectionId] = new ConnectionStatus
            {
                ConnectionId = connectionId,
                State = ReachabilityState.Checking
            };
        }

        return CurrentGeneration;
    }

    /// <summary>
    /// Applies a completed result set only when it belongs to the current batch. / 仅当完整结果集属于当前批次时才应用它。
    /// </summary>
    /// <param name="generation">Generation captured by the completing batch. / 完成批次捕获的代数。</param>
    /// <param name="results">Completed connection statuses. / 已完成的连接状态。</param>
    /// <returns>True when the results belonged to and were applied to the current batch. / 当结果属于当前批次且已应用时返回 true。</returns>
    public bool TryApplyResults(long generation, IEnumerable<ConnectionStatus> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        if (generation != CurrentGeneration)
        {
            return false;
        }

        foreach (ConnectionStatus status in results)
        {
            if (status is null || !_pendingConnectionIds.Remove(status.ConnectionId))
            {
                continue;
            }

            _statuses[status.ConnectionId] = status;
        }

        return true;
    }

    /// <summary>
    /// Finishes the current batch and removes any unresolved checking markers without touching a replacement batch. / 结束当前批次并移除仍未完成的检测标记，同时不影响替代批次。
    /// </summary>
    /// <param name="generation">Generation captured by the finishing batch. / 结束批次捕获的代数。</param>
    /// <returns>True when the requested batch was still current. / 当请求的批次仍为当前批次时返回 true。</returns>
    public bool TryFinishBatch(long generation)
    {
        if (generation != CurrentGeneration)
        {
            return false;
        }

        ClearPendingStatuses();
        return true;
    }

    /// <summary>
    /// Tries to obtain the latest visible status for one connection. / 尝试获取一条连接最近可见的状态。
    /// </summary>
    /// <param name="connectionId">Connection identifier. / 连接标识。</param>
    /// <param name="status">Resolved status when present. / 存在时解析出的状态。</param>
    /// <returns>True when a status is available. / 当状态可用时返回 true。</returns>
    public bool TryGetStatus(Guid connectionId, out ConnectionStatus? status)
    {
        return _statuses.TryGetValue(connectionId, out status);
    }

    /// <summary>
    /// Gets the latest state for one connection or Unknown when no result exists. / 获取一条连接的最近状态；无结果时返回 Unknown。
    /// </summary>
    /// <param name="connectionId">Connection identifier. / 连接标识。</param>
    /// <returns>The latest reachability state. / 最近的可达性状态。</returns>
    public ReachabilityState GetState(Guid connectionId)
    {
        return _statuses.GetValueOrDefault(connectionId)?.State ?? ReachabilityState.Unknown;
    }

    /// <summary>
    /// Removes checking entries still owned by the active pending set. / 移除仍由活动等待集合拥有的检测中条目。
    /// </summary>
    private void ClearPendingStatuses()
    {
        foreach (Guid connectionId in _pendingConnectionIds)
        {
            if (_statuses.GetValueOrDefault(connectionId)?.State == ReachabilityState.Checking)
            {
                _statuses.Remove(connectionId);
            }
        }

        _pendingConnectionIds.Clear();
    }
}
