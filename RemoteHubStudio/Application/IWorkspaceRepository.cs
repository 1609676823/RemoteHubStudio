using RemoteHubStudio.Domain;

namespace RemoteHubStudio.Application;

/// <summary>
/// Defines durable loading and saving for the application workspace. / 定义应用工作区的持久化加载与保存操作。
/// </summary>
public interface IWorkspaceRepository
{
    /// <summary>
    /// Loads the newest usable workspace, recovering from the backup when necessary. / 加载最新可用工作区，并在必要时从备份恢复。
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>The loaded workspace and recovery information. / 已加载的工作区及恢复信息。</returns>
    Task<WorkspaceLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a complete workspace as one atomic revision. / 将完整工作区作为一个原子版本保存。
    /// </summary>
    /// <param name="document">Workspace document to save. / 要保存的工作区文档。</param>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>A task that represents the save operation. / 表示保存操作的任务。</returns>
    Task SaveAsync(AppDataDocument document, CancellationToken cancellationToken = default);
}
