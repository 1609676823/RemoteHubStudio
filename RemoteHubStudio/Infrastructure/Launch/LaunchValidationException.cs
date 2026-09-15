namespace RemoteHubStudio.Infrastructure.Launch;

/// <summary>
/// Represents a user-correctable launch configuration error. / 表示可由用户修正的连接启动配置错误。
/// </summary>
public sealed class LaunchValidationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a launch validation exception with a bilingual message. / 使用双语消息初始化启动验证异常。
    /// </summary>
    /// <param name="message">Error message shown to the caller. / 向调用方显示的错误消息。</param>
    public LaunchValidationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a launch validation exception with a bilingual message and inner error. / 使用双语消息和内部错误初始化启动验证异常。
    /// </summary>
    /// <param name="message">Error message shown to the caller. / 向调用方显示的错误消息。</param>
    /// <param name="innerException">Underlying error. / 底层错误。</param>
    public LaunchValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
