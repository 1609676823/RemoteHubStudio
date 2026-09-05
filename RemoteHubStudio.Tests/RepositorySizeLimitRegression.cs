using RemoteHubStudio.Domain;
using RemoteHubStudio.Infrastructure.Persistence;
using RemoteHubStudio.Infrastructure.Security;

namespace RemoteHubStudio.Tests;

/// <summary>
/// Provides independently callable checks that repository writes obey their own read-size contract. / 提供可独立调用的仓储写入遵守自身读取大小契约检查。
/// </summary>
internal static class RepositorySizeLimitRegression
{
    private const int MaximumWorkspaceFileLength = 64 * 1024 * 1024;

    /// <summary>
    /// Verifies oversized plaintext and protected envelopes cannot replace a readable primary revision. / 验证明文和受保护超限信封均不能替换可读取的主修订版本。
    /// </summary>
    public static async Task RunAsync()
    {
        using TemporaryDirectoryScope scope = new();
        AppDataPaths paths = new(scope.Path);
        JsonWorkspaceRepository repository = new(paths, new PassthroughProtector());
        AppDataDocument document = CreateWorkspace();
        await repository.SaveAsync(document);
        string baseline = await File.ReadAllTextAsync(paths.WorkspaceFilePath);

        document.Connections[0].Notes = new string('p', MaximumWorkspaceFileLength);
        await AssertPersistenceFailureAsync(
            () => repository.SaveAsync(document),
            "An oversized plaintext workspace replaced the readable primary. / 超限明文工作区替换了可读取主文件。");
        Assert(await File.ReadAllTextAsync(paths.WorkspaceFilePath) == baseline, "A failed plaintext save changed the primary revision. / 失败的明文保存更改了主修订版本。");

        document.Connections[0].Notes = new string('e', 48 * 1024 * 1024);
        document.Settings.EncryptionEnabled = true;
        await AssertPersistenceFailureAsync(
            () => repository.SaveAsync(document),
            "An oversized protected workspace replaced the readable primary. / 超限受保护工作区替换了可读取主文件。");
        Assert(await File.ReadAllTextAsync(paths.WorkspaceFilePath) == baseline, "A failed protected save changed the primary revision. / 失败的受保护保存更改了主修订版本。");
        Assert(!File.Exists(paths.BackupFilePath), "A rejected oversized save created an unreadable backup. / 被拒绝的超限保存创建了不可读取备份。");
        Assert(!Directory.EnumerateFiles(paths.DataDirectory, "workspace.*.tmp", SearchOption.TopDirectoryOnly).Any(), "A rejected oversized save left a temporary file. / 被拒绝的超限保存留下了临时文件。");
    }

    /// <summary>
    /// Creates one small readable workspace used as the durable baseline. / 创建一个作为持久基线的小型可读取工作区。
    /// </summary>
    /// <returns>A valid one-connection workspace. / 有效的单连接工作区。</returns>
    private static AppDataDocument CreateWorkspace()
    {
        return new AppDataDocument
        {
            Connections =
            [
                new ConnectionProfile
                {
                    Name = "Repository size fixture",
                    Type = ConnectionType.Putty,
                    Protocol = "ssh",
                    Host = "size.example",
                    Port = 22,
                    Notes = "readable baseline"
                }
            ]
        };
    }

    /// <summary>
    /// Verifies an asynchronous repository operation fails with the public persistence exception. / 验证异步仓储操作以公开持久化异常失败。
    /// </summary>
    /// <param name="action">Repository operation to execute. / 要执行的仓储操作。</param>
    /// <param name="message">Failure message. / 失败消息。</param>
    private static async Task AssertPersistenceFailureAsync(Func<Task> action, string message)
    {
        try
        {
            await action();
        }
        catch (WorkspacePersistenceException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Throws when a regression assertion is false. / 当回归断言为 false 时抛出异常。
    /// </summary>
    /// <param name="condition">Assertion condition. / 断言条件。</param>
    /// <param name="message">Failure message. / 失败消息。</param>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Supplies deterministic reversible bytes so Base64 expansion can be tested without platform-specific cryptographic variance. / 提供确定性可逆字节，以便在不受平台加密差异影响的情况下测试 Base64 膨胀。
    /// </summary>
    private sealed class PassthroughProtector : IWorkspaceDataProtector
    {
        /// <summary>Gets the stable test protection scheme. / 获取稳定的测试保护方案。</summary>
        public string Scheme => "test-passthrough";

        /// <summary>
        /// Returns a detached copy of plaintext bytes. / 返回明文字节的独立副本。
        /// </summary>
        /// <param name="plaintext">Plaintext bytes. / 明文字节。</param>
        /// <returns>A detached protected representation. / 独立的受保护表示。</returns>
        public byte[] Protect(byte[] plaintext)
        {
            return (byte[])plaintext.Clone();
        }

        /// <summary>
        /// Returns a detached copy of protected bytes. / 返回受保护字节的独立副本。
        /// </summary>
        /// <param name="protectedData">Protected bytes. / 受保护字节。</param>
        /// <returns>A detached plaintext representation. / 独立的明文表示。</returns>
        public byte[] Unprotect(byte[] protectedData)
        {
            return (byte[])protectedData.Clone();
        }
    }

    /// <summary>
    /// Owns and safely removes one uniquely named repository regression directory. / 管理并安全删除一个唯一命名的仓储回归目录。
    /// </summary>
    private sealed class TemporaryDirectoryScope : IDisposable
    {
        private readonly string _testRoot;

        /// <summary>
        /// Creates one unique directory below the operating-system temporary directory. / 在操作系统临时目录下创建一个唯一目录。
        /// </summary>
        public TemporaryDirectoryScope()
        {
            _testRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RemoteHubStudio.RepositorySizeLimitRegression"));
            Path = System.IO.Path.Combine(_testRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        /// <summary>Gets the unique absolute regression directory. / 获取唯一的绝对回归目录。</summary>
        public string Path { get; }

        /// <summary>
        /// Deletes only the validated unique regression directory. / 仅删除经过验证的唯一回归目录。
        /// </summary>
        public void Dispose()
        {
            string fullPath = System.IO.Path.GetFullPath(Path);
            string requiredPrefix = _testRoot.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
            }
        }
    }
}
