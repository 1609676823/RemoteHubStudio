using System.Text;
using System.Text.Json;
using RemoteHubStudio.Application;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Infrastructure.Persistence;
using RemoteHubStudio.Infrastructure.Security;

namespace RemoteHubStudio.Tests;

/// <summary>
/// Provides independently callable regression checks for protected damaged-workspace artifacts. / 提供可独立调用的损坏工作区受保护文件回归检查。
/// </summary>
internal static class ProtectedArtifactRegression
{
    private static readonly byte[] TestEntropy = Encoding.UTF8.GetBytes("RemoteHubStudio.ProtectedArtifact.Tests");

    /// <summary>
    /// Runs migration, recovery, and orphan-backup protection checks. / 运行迁移、恢复与孤立备份保护检查。
    /// </summary>
    public static async Task RunAsync()
    {
        await TestLegacyArtifactMigrationWithTrailingSeparatorAsync();
        await TestRecoveryArtifactProtectionAsync();
        await TestOrphanBackupEncryptionTransitionAsync();
    }

    /// <summary>
    /// Verifies encryption opt-in migrates an exactly named legacy plaintext artifact even when the data path has a trailing separator. / 验证启用加密时，即使数据路径带尾分隔符，也会迁移名称精确匹配的旧版明文保留文件。
    /// </summary>
    private static async Task TestLegacyArtifactMigrationWithTrailingSeparatorAsync()
    {
        using TemporaryDirectoryScope scope = new();
        string pathWithTrailingSeparator = scope.Path + System.IO.Path.DirectorySeparatorChar;
        AppDataPaths paths = new(pathWithTrailingSeparator);
        DpapiCurrentUserProtector protector = CreateProtector();
        JsonWorkspaceRepository repository = new(paths, protector);
        AppDataDocument document = CreateWorkspace("workspace-migration-secret");
        await repository.SaveAsync(document);

        string artifactPath = System.IO.Path.Combine(
            paths.DataDirectory,
            "workspace.corrupt.20260902123456789.json");
        byte[] legacyBytes = Encoding.UTF8.GetBytes("legacy-artifact-secret");
        await File.WriteAllBytesAsync(artifactPath, legacyBytes);

        document.Settings.EncryptionEnabled = true;
        await repository.SaveAsync(document);

        string artifactJson = await File.ReadAllTextAsync(artifactPath);
        Assert(!artifactJson.Contains("legacy-artifact-secret", StringComparison.Ordinal), "Encryption opt-in left a plaintext legacy artifact. / 启用加密后仍留下明文旧版保留文件。");
        byte[] recoveredBytes = await ReadProtectedArtifactAsync(artifactPath, protector);
        Assert(recoveredBytes.SequenceEqual(legacyBytes), "Legacy artifact migration changed the preserved bytes. / 旧版保留文件迁移改变了所保留的字节。");
    }

    /// <summary>
    /// Verifies backup recovery always preserves damaged primary bytes inside a protected artifact. / 验证备份恢复始终将损坏主文件字节保存在受保护信封中。
    /// </summary>
    private static async Task TestRecoveryArtifactProtectionAsync()
    {
        using TemporaryDirectoryScope scope = new();
        AppDataPaths paths = new(scope.Path);
        DpapiCurrentUserProtector protector = CreateProtector();
        JsonWorkspaceRepository repository = new(paths, protector);
        AppDataDocument document = CreateWorkspace("recovery-workspace-secret");
        await repository.SaveAsync(document);
        document.Connections[0].Notes = "create a valid backup";
        await repository.SaveAsync(document);

        byte[] damagedBytes = Encoding.UTF8.GetBytes("{\"damaged\":\"recovery-artifact-secret\"");
        await File.WriteAllBytesAsync(paths.WorkspaceFilePath, damagedBytes);
        WorkspaceLoadResult recovered = await repository.LoadAsync();

        Assert(recovered.RecoveredFromBackup, "Damaged primary workspace was not recovered from backup. / 损坏的主工作区未从备份恢复。");
        string artifactPath = Directory.EnumerateFiles(
                paths.DataDirectory,
                "workspace.corrupt.*.json",
                SearchOption.TopDirectoryOnly)
            .Single();
        string artifactJson = await File.ReadAllTextAsync(artifactPath);
        Assert(!artifactJson.Contains("recovery-artifact-secret", StringComparison.Ordinal), "Recovery artifact exposed the damaged plaintext. / 恢复保留文件暴露了损坏文件明文。");
        byte[] recoveredBytes = await ReadProtectedArtifactAsync(artifactPath, protector);
        Assert(recoveredBytes.SequenceEqual(damagedBytes), "Recovery artifact did not preserve the damaged primary byte-for-byte. / 恢复保留文件未逐字节保存损坏的主文件。");
    }

    /// <summary>
    /// Verifies encryption opt-in protects both files when only a plaintext backup remains. / 验证仅剩明文备份时，启用加密会同时保护主文件与备份。
    /// </summary>
    private static async Task TestOrphanBackupEncryptionTransitionAsync()
    {
        using TemporaryDirectoryScope scope = new();
        AppDataPaths paths = new(scope.Path);
        DpapiCurrentUserProtector protector = CreateProtector();
        JsonWorkspaceRepository repository = new(paths, protector);
        AppDataDocument document = CreateWorkspace("orphan-backup-secret");
        await repository.SaveAsync(document);
        document.Connections[0].Notes = "rotate plaintext backup";
        await repository.SaveAsync(document);
        Assert(File.Exists(paths.BackupFilePath), "The plaintext backup fixture was not created. / 未创建明文备份测试数据。");

        File.Delete(paths.WorkspaceFilePath);
        document.Settings.EncryptionEnabled = true;
        await repository.SaveAsync(document);

        string primaryJson = await File.ReadAllTextAsync(paths.WorkspaceFilePath);
        string backupJson = await File.ReadAllTextAsync(paths.BackupFilePath);
        Assert(!primaryJson.Contains("orphan-backup-secret", StringComparison.Ordinal), "Encrypted primary contains the orphan-backup secret. / 加密主文件包含孤立备份秘密。");
        Assert(!backupJson.Contains("orphan-backup-secret", StringComparison.Ordinal), "Encryption opt-in left an orphan plaintext backup. / 启用加密后留下了孤立明文备份。");
        Assert(primaryJson.Contains("dpapi-current-user", StringComparison.Ordinal) && backupJson.Contains("dpapi-current-user", StringComparison.Ordinal), "The orphan-backup transition did not protect both revisions. / 孤立备份切换未保护两个修订版本。");

        AppDataDocument loaded = (await repository.LoadAsync()).Document;
        Assert(loaded.Connections.Single().Password == "orphan-backup-secret", "The protected orphan-backup transition lost workspace data. / 受保护的孤立备份切换丢失了工作区数据。");
    }

    /// <summary>
    /// Creates a representative workspace containing a unique secret marker. / 创建包含唯一秘密标记的代表性工作区。
    /// </summary>
    /// <param name="secret">Secret marker used to detect plaintext remnants. / 用于检测明文残留的秘密标记。</param>
    /// <returns>A valid one-connection workspace. / 有效的单连接工作区。</returns>
    private static AppDataDocument CreateWorkspace(string secret)
    {
        return new AppDataDocument
        {
            Connections =
            [
                new ConnectionProfile
                {
                    Name = "Protected artifact fixture",
                    Type = ConnectionType.Putty,
                    Protocol = "ssh",
                    Host = "fixture.example",
                    Port = 22,
                    Username = "fixture-user",
                    Password = secret
                }
            ]
        };
    }

    /// <summary>
    /// Creates the deterministic current-user protector shared by this regression suite. / 创建此回归套件共享的确定性当前用户保护器。
    /// </summary>
    /// <returns>A DPAPI protector with test-specific entropy. / 使用测试专用熵的 DPAPI 保护器。</returns>
    private static DpapiCurrentUserProtector CreateProtector()
    {
        return new DpapiCurrentUserProtector(TestEntropy);
    }

    /// <summary>
    /// Reads and decrypts one strict protected-artifact envelope. / 读取并解密一个严格的受保护文件信封。
    /// </summary>
    /// <param name="artifactPath">Protected artifact path. / 受保护文件路径。</param>
    /// <param name="protector">Protector expected by the envelope. / 信封预期使用的保护器。</param>
    /// <returns>The original damaged bytes. / 原始损坏字节。</returns>
    private static async Task<byte[]> ReadProtectedArtifactAsync(
        string artifactPath,
        IWorkspaceDataProtector protector)
    {
        using JsonDocument json = JsonDocument.Parse(await File.ReadAllTextAsync(artifactPath));
        JsonElement root = json.RootElement;
        Assert(root.GetProperty("format").GetString() == "remotehubstudio-corrupt-artifact", "Protected artifact format is invalid. / 受保护文件格式无效。");
        Assert(root.GetProperty("protection").GetString() == protector.Scheme, "Protected artifact scheme is invalid. / 受保护文件方案无效。");
        byte[] protectedBytes = Convert.FromBase64String(root.GetProperty("payload").GetString() ?? string.Empty);
        byte[] plaintext = protector.Unprotect(protectedBytes);
        Assert(root.GetProperty("originalLength").GetInt64() == plaintext.LongLength, "Protected artifact length metadata is invalid. / 受保护文件长度元数据无效。");
        return plaintext;
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
    /// Owns and safely removes one uniquely named regression directory. / 管理并安全删除一个唯一命名的回归目录。
    /// </summary>
    private sealed class TemporaryDirectoryScope : IDisposable
    {
        private readonly string _testRoot;

        /// <summary>
        /// Creates one unique directory below the operating-system temporary directory. / 在操作系统临时目录下创建一个唯一目录。
        /// </summary>
        public TemporaryDirectoryScope()
        {
            _testRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RemoteHubStudio.ProtectedArtifactRegression"));
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
