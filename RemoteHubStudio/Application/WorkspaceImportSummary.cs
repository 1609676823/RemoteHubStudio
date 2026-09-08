namespace RemoteHubStudio.Application;

/// <summary>
/// Describes the connection changes committed by one workspace import. / 描述一次工作区导入提交的连接变更。
/// </summary>
public readonly record struct WorkspaceImportSummary(
    int CreatedConnectionCount,
    int UpdatedConnectionCount)
{
    /// <summary>Gets the number of connection rows handled by the import. / 获取本次导入处理的连接行数。</summary>
    public int TotalConnectionCount => CreatedConnectionCount + UpdatedConnectionCount;
}
