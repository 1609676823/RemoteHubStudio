namespace RemoteHubStudio.Infrastructure.Persistence;

/// <summary>
/// Reports a workspace persistence failure with its original cause. / 报告工作区持久化失败及其原始原因。
/// </summary>
public sealed class WorkspacePersistenceException : Exception
{
    /// <summary>
    /// Initializes a workspace persistence exception. / 初始化工作区持久化异常。
    /// </summary>
    /// <param name="message">Bilingual failure description. / 双语失败说明。</param>
    /// <param name="innerException">Original persistence failure. / 原始持久化错误。</param>
    public WorkspacePersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Reports a workspace format that cannot safely be read by this build. / 报告当前版本无法安全读取的工作区格式。
/// </summary>
public sealed class WorkspaceCompatibilityException : Exception
{
    /// <summary>
    /// Initializes a workspace compatibility exception. / 初始化工作区兼容性异常。
    /// </summary>
    /// <param name="message">Bilingual compatibility description. / 双语兼容性说明。</param>
    public WorkspaceCompatibilityException(string message)
        : base(message)
    {
    }
}
