namespace RemoteHubStudio.Application;

/// <summary>
/// Identifies the kind of committed workspace change. / 标识已提交的工作区变更类型。
/// </summary>
public enum WorkspaceChangeKind
{
    Loaded,
    ConnectionAdded,
    ConnectionUpdated,
    ConnectionDeleted,
    GroupAdded,
    GroupUpdated,
    GroupDeleted,
    SettingsUpdated,

    /// <summary>Indicates one atomic imported-workspace merge. / 表示一次原子导入工作区合并。</summary>
    WorkspaceImported
}

/// <summary>
/// Supplies details about a committed workspace change. / 提供已提交工作区变更的详细信息。
/// </summary>
public sealed class WorkspaceChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes workspace change event data. / 初始化工作区变更事件数据。
    /// </summary>
    /// <param name="kind">Committed change kind. / 已提交的变更类型。</param>
    /// <param name="entityId">Affected entity identifier, when applicable. / 受影响的实体标识（如适用）。</param>
    /// <param name="recoveredFromBackup">Whether loading recovered the workspace from backup. / 加载时是否从备份恢复了工作区。</param>
    public WorkspaceChangedEventArgs(
        WorkspaceChangeKind kind,
        Guid? entityId = null,
        bool recoveredFromBackup = false)
    {
        Kind = kind;
        EntityId = entityId;
        RecoveredFromBackup = recoveredFromBackup;
    }

    /// <summary>Gets the committed change kind. / 获取已提交的变更类型。</summary>
    public WorkspaceChangeKind Kind { get; }

    /// <summary>Gets the affected entity identifier, when applicable. / 获取受影响的实体标识（如适用）。</summary>
    public Guid? EntityId { get; }

    /// <summary>Gets whether loading recovered the workspace from backup. / 获取加载时是否从备份恢复了工作区。</summary>
    public bool RecoveredFromBackup { get; }
}
