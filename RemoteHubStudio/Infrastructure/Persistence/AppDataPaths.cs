namespace RemoteHubStudio.Infrastructure.Persistence;

/// <summary>
/// Provides stable local paths for workspace data, backups, logs, and temporary files. / 为工作区数据、备份、日志和临时文件提供稳定的本地路径。
/// </summary>
public sealed class AppDataPaths
{
    /// <summary>
    /// Gets the default data directory relative to the application executable. / 获取相对于应用程序可执行文件的默认数据目录。
    /// </summary>
    public const string DefaultRelativeDataDirectory = "data";

    /// <summary>Directory name used for user-maintained language packs. / 用户自行维护的语言包目录名。</summary>
    public const string LanguagesDirectoryName = "Languages";

    /// <summary>File name used for the independently persisted UI language preference. / 独立持久化 UI 语言偏好所用的文件名。</summary>
    public const string LanguagePreferenceFileName = "language-preference.json";

    /// <summary>
    /// Gets the normalized absolute path represented by <see cref="DefaultRelativeDataDirectory"/>. / 获取 <see cref="DefaultRelativeDataDirectory"/> 所表示的规范化绝对路径。
    /// </summary>
    public static string DefaultDataDirectory => ResolveDataDirectory(DefaultRelativeDataDirectory);

    /// <summary>
    /// Initializes paths below the application's portable data directory. / 在应用程序的便携数据目录下初始化路径。
    /// </summary>
    public AppDataPaths()
        : this(DefaultRelativeDataDirectory)
    {
    }

    /// <summary>
    /// Initializes paths below an explicit data directory, primarily for tests and portable hosts. / 在显式数据目录下初始化路径，主要供测试和便携宿主使用。
    /// </summary>
    /// <param name="dataDirectory">Absolute or relative root data directory. / 绝对或相对的根数据目录。</param>
    public AppDataPaths(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        DataDirectory = ResolveDataDirectory(dataDirectory);
        WorkspaceFilePath = Path.Combine(DataDirectory, "workspace.json");
        BackupFilePath = Path.Combine(DataDirectory, "workspace.json.bak");
        TemporaryDirectory = Path.Combine(DataDirectory, "temp");
        LogsDirectory = Path.Combine(DataDirectory, "logs");
        LanguagesDirectory = Path.Combine(DataDirectory, LanguagesDirectoryName);
        LanguagePreferenceFilePath = Path.Combine(DataDirectory, LanguagePreferenceFileName);
    }

    /// <summary>Gets the root application data directory. / 获取应用数据根目录。</summary>
    public string DataDirectory { get; }

    /// <summary>Gets the primary workspace file path. / 获取主工作区文件路径。</summary>
    public string WorkspaceFilePath { get; }

    /// <summary>Gets the previous valid workspace backup path. / 获取上一个有效工作区备份路径。</summary>
    public string BackupFilePath { get; }

    /// <summary>Gets the application temporary directory. / 获取应用临时目录。</summary>
    public string TemporaryDirectory { get; }

    /// <summary>Gets the application log directory. / 获取应用日志目录。</summary>
    public string LogsDirectory { get; }

    /// <summary>Gets the directory containing user-provided language packs. / 获取包含用户语言包的目录。</summary>
    public string LanguagesDirectory { get; }

    /// <summary>Gets the independently persisted UI language preference path. / 获取独立持久化的 UI 语言偏好路径。</summary>
    public string LanguagePreferenceFilePath { get; }

    /// <summary>
    /// Creates all directories owned by the application. / 创建应用拥有的全部目录。
    /// </summary>
    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(TemporaryDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(LanguagesDirectory);
    }

    /// <summary>
    /// Creates a unique same-volume path used for an atomic workspace write. / 创建用于原子写入工作区的同卷唯一临时路径。
    /// </summary>
    /// <returns>A unique temporary workspace path. / 唯一的工作区临时路径。</returns>
    public string CreateAtomicTemporaryFilePath()
    {
        return Path.Combine(DataDirectory, $"workspace.{Guid.NewGuid():N}.tmp");
    }

    /// <summary>
    /// Creates a unique same-volume path used for an atomic language-preference write. / 创建用于原子写入语言偏好的同卷唯一临时路径。
    /// </summary>
    /// <returns>A unique temporary preference path. / 唯一的临时偏好文件路径。</returns>
    public string CreateAtomicLanguagePreferenceFilePath()
    {
        return Path.Combine(
            DataDirectory,
            $".{LanguagePreferenceFileName}.{Guid.NewGuid():N}.tmp");
    }

    /// <summary>
    /// Creates a timestamped path used to preserve a damaged primary file during recovery. / 创建带时间戳的路径，用于恢复时保留损坏的主文件。
    /// </summary>
    /// <returns>A unique damaged-file preservation path. / 唯一的损坏文件保留路径。</returns>
    public string CreateCorruptFilePath()
    {
        return Path.Combine(
            DataDirectory,
            $"workspace.corrupt.{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json");
    }

    /// <summary>
    /// Resolves a data directory, anchoring relative paths to the executable directory rather than the process working directory. / 解析数据目录，并将相对路径锚定到可执行文件目录而不是进程工作目录。
    /// </summary>
    /// <param name="dataDirectory">Absolute or application-relative data directory. / 绝对或相对于应用程序的数据目录。</param>
    /// <returns>The normalized absolute data directory. / 规范化后的绝对数据目录。</returns>
    private static string ResolveDataDirectory(string dataDirectory)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataDirectory, AppContext.BaseDirectory));
    }
}
