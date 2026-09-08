using RemoteHubStudio.Infrastructure.Persistence;

namespace RemoteHubStudio.Tests;

/// <summary>
/// Covers executable-relative portable application data paths. / 覆盖相对于可执行文件的便携应用数据路径。
/// </summary>
internal static class AppDataPathsRegression
{
    /// <summary>
    /// Verifies both the default and an explicit relative data directory are anchored to the application directory. / 验证默认目录和显式相对数据目录均锚定到应用程序目录。
    /// </summary>
    public static void Run()
    {
        string originalWorkingDirectory = Environment.CurrentDirectory;
        string alternateWorkingDirectory = Path.Combine(
            Path.GetTempPath(),
            $"RemoteHubStudio.Tests.cwd.{Guid.NewGuid():N}");
        Directory.CreateDirectory(alternateWorkingDirectory);

        try
        {
            Environment.CurrentDirectory = alternateWorkingDirectory;

            string expectedDefault = Normalize(Path.Combine(AppContext.BaseDirectory, AppDataPaths.DefaultRelativeDataDirectory));
            AppDataPaths defaultPaths = new();

            Assert(AppDataPaths.DefaultDataDirectory == expectedDefault && defaultPaths.DataDirectory == expectedDefault,
                "The default data directory is not application-relative. / 默认数据目录未相对于应用程序定位。");
            Assert(defaultPaths.WorkspaceFilePath == Path.Combine(expectedDefault, "workspace.json"),
                "The default workspace path escaped the portable data directory. / 默认工作区路径脱离了便携数据目录。");

            const string explicitRelativeDirectory = "portable-test-data";
            AppDataPaths explicitRelativePaths = new(explicitRelativeDirectory);
            string expectedExplicit = Normalize(Path.Combine(AppContext.BaseDirectory, explicitRelativeDirectory));

            Assert(explicitRelativePaths.DataDirectory == expectedExplicit,
                "An explicit relative data directory was resolved against the process working directory. / 显式相对数据目录错误地按进程工作目录解析。");
        }
        finally
        {
            Environment.CurrentDirectory = originalWorkingDirectory;
            Directory.Delete(alternateWorkingDirectory);
        }
    }

    /// <summary>
    /// Normalizes a directory path like the production path model. / 按生产路径模型规范化目录路径。
    /// </summary>
    /// <param name="path">Directory path to normalize. / 要规范化的目录路径。</param>
    /// <returns>The normalized absolute directory path. / 规范化后的绝对目录路径。</returns>
    private static string Normalize(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    /// <summary>
    /// Throws when a path regression assertion fails. / 路径回归断言失败时抛出异常。
    /// </summary>
    /// <param name="condition">Condition expected to be true. / 预期为真的条件。</param>
    /// <param name="message">Bilingual failure message. / 双语失败消息。</param>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
