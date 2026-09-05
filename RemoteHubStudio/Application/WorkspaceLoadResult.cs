using RemoteHubStudio.Domain;

namespace RemoteHubStudio.Application;

/// <summary>
/// Describes a workspace load and whether a backup was used. / 描述工作区加载结果以及是否使用了备份。
/// </summary>
public sealed class WorkspaceLoadResult
{
    /// <summary>
    /// Initializes a workspace load result. / 初始化工作区加载结果。
    /// </summary>
    /// <param name="document">Loaded workspace document. / 已加载的工作区文档。</param>
    /// <param name="recoveredFromBackup">Whether the backup supplied the document. / 文档是否来自备份恢复。</param>
    /// <param name="primaryFailure">Failure encountered while reading the primary file, when present. / 读取主文件时遇到的错误（如有）。</param>
    public WorkspaceLoadResult(
        AppDataDocument document,
        bool recoveredFromBackup = false,
        Exception? primaryFailure = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        RecoveredFromBackup = recoveredFromBackup;
        PrimaryFailure = primaryFailure;
    }

    /// <summary>Gets the loaded workspace document. / 获取已加载的工作区文档。</summary>
    public AppDataDocument Document { get; }

    /// <summary>Gets whether the backup supplied the document. / 获取文档是否来自备份恢复。</summary>
    public bool RecoveredFromBackup { get; }

    /// <summary>Gets the primary-file failure that caused recovery, when present. / 获取触发恢复的主文件错误（如有）。</summary>
    public Exception? PrimaryFailure { get; }
}
