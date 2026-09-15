using System.Text;
using RemoteHubStudio.Application;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Infrastructure.ImportExport;
using RemoteHubStudio.Infrastructure.Persistence;
using RemoteHubStudio.Infrastructure.Security;

namespace RemoteHubStudio.Tests;

/// <summary>
/// Exercises bounded JSON, content, dictionary-key, and damaged-artifact compatibility contracts. / 验证受限 JSON、内容、字典键及损坏文件兼容性契约。
/// </summary>
internal static class ContentBoundaryRegression
{
    /// <summary>
    /// Runs every content-boundary regression. / 运行全部内容边界回归。
    /// </summary>
    public static async Task RunAsync()
    {
        await TestEscapedDictionaryKeyRoundTripsAsync();
        await TestPortableJsonPreflightLimitsAsync();
        await TestRepositoryEntityPreflightAsync();
        await TestContentBudgetsBeforeWritesAsync();
        await TestProtectedArtifactsAreNotRepeatedlyDecryptedAsync();
        await TestForgedProtectedArtifactsAreRemigratedAsync();
        await TestArtifactMigrationCountBoundaryAsync();
    }

    /// <summary>
    /// Verifies escaped, HTML-sensitive, and longer dictionary keys remain readable after repository and portable round trips. / 验证转义、HTML 敏感及较长字典键在仓储与便携往返后仍可读取。
    /// </summary>
    private static async Task TestEscapedDictionaryKeyRoundTripsAsync()
    {
        using TemporaryDirectoryScope scope = new();
        string toolKey = "工具&路径-" + new string('键', 300);
        string optionKey = "选项&名称-" + new string('值', 300);
        AppDataDocument source = CreateWorkspace();
        source.Settings.ToolPaths[toolKey] = @"C:\Tools\client.exe";
        source.Connections[0].Options[optionKey] = "保留&value";

        AppDataPaths paths = new(System.IO.Path.Combine(scope.Path, "repository"));
        JsonWorkspaceRepository repository = new(paths, new CountingPassthroughProtector());
        await repository.SaveAsync(source);
        AppDataDocument loaded = (await repository.LoadAsync()).Document;
        Assert(loaded.Settings.ToolPaths[toolKey] == @"C:\Tools\client.exe", "Repository load rejected or changed an escaped tool-path key. / 仓储加载拒绝或改变了转义的工具路径键。");
        Assert(loaded.Connections.Single().Options[optionKey] == "保留&value", "Repository load rejected or changed an escaped option key. / 仓储加载拒绝或改变了转义的选项键。");

        WorkspaceTransferService transfer = new();
        string portablePath = System.IO.Path.Combine(scope.Path, "escaped.rhs.json");
        await transfer.ExportJsonAsync(source, portablePath, includeSecrets: true);
        AppDataDocument imported = await transfer.ImportJsonAsync(portablePath, trustLaunchConfiguration: true);
        Assert(imported.Connections.Single().Options[optionKey] == "保留&value", "Portable JSON rejected or changed an escaped option key. / 便携 JSON 拒绝或改变了转义的选项键。");
    }

    /// <summary>
    /// Verifies portable JSON rejects oversized strings, excessive arrays, and excessive nesting before model creation. / 验证便携 JSON 在创建模型前拒绝超长字符串、过多数组及过深嵌套。
    /// </summary>
    private static async Task TestPortableJsonPreflightLimitsAsync()
    {
        using TemporaryDirectoryScope scope = new();
        WorkspaceTransferService transfer = new();

        string longStringPath = System.IO.Path.Combine(scope.Path, "long-string.json");
        await File.WriteAllTextAsync(
            longStringPath,
            "{\"schemaVersion\":1,\"connections\":[],\"ignored\":\"" + new string('x', 4 * 1024 * 1024 + 1) + "\"}");
        await AssertInvalidDataAsync(
            () => transfer.ImportJsonAsync(longStringPath),
            "Portable JSON accepted a string beyond the streaming preflight limit. / 便携 JSON 接受了超过流式预检上限的字符串。");

        string arrayFloodPath = System.IO.Path.Combine(scope.Path, "array-flood.json");
        StringBuilder arrayFlood = new("{\"schemaVersion\":1,\"connections\":[]");
        for (int index = 0; index < 100_000; index++)
        {
            arrayFlood.Append(",\"x\":[]");
        }

        arrayFlood.Append('}');
        await File.WriteAllTextAsync(arrayFloodPath, arrayFlood.ToString());
        await AssertInvalidDataAsync(
            () => transfer.ImportJsonAsync(arrayFloodPath),
            "Portable JSON accepted too many arrays. / 便携 JSON 接受了过多数组。");

        string deepPath = System.IO.Path.Combine(scope.Path, "deep.json");
        await File.WriteAllTextAsync(
            deepPath,
            "{\"schemaVersion\":1,\"connections\":[],\"ignored\":" + new string('[', 65) + "0" + new string(']', 65) + "}");
        await AssertInvalidDataAsync(
            () => transfer.ImportJsonAsync(deepPath),
            "Portable JSON accepted excessive nesting. / 便携 JSON 接受了过深嵌套。");
    }

    /// <summary>
    /// Verifies repository loading rejects excessive entity arrays before entity deserialization. / 验证仓储加载在实体反序列化前拒绝超限实体数组。
    /// </summary>
    private static async Task TestRepositoryEntityPreflightAsync()
    {
        using TemporaryDirectoryScope scope = new();
        AppDataPaths paths = new(scope.Path);
        paths.EnsureDirectoriesExist();
        string groups = string.Join(',', Enumerable.Repeat("{}", WorkspaceLimits.MaximumGroupCount + 1));
        await File.WriteAllTextAsync(
            paths.WorkspaceFilePath,
            "{\"schemaVersion\":1,\"groups\":[" + groups + "],\"connections\":[]}");
        JsonWorkspaceRepository repository = new(paths, new CountingPassthroughProtector());
        await AssertPersistenceFailureAsync(
            () => repository.LoadAsync(),
            "Repository load accepted too many groups. / 仓储加载接受了过多分类。");
    }

    /// <summary>
    /// Verifies individual and aggregate content budgets reject writes before replacing an existing destination. / 验证单字段及聚合内容预算在替换现有目标前拒绝写入。
    /// </summary>
    private static async Task TestContentBudgetsBeforeWritesAsync()
    {
        using TemporaryDirectoryScope scope = new();
        WorkspaceTransferService transfer = new();
        string destination = System.IO.Path.Combine(scope.Path, "existing.rhs.json");
        await File.WriteAllTextAsync(destination, "durable-baseline");
        AppDataDocument individualOverflow = CreateWorkspace();
        individualOverflow.Connections[0].Notes = new string('x', WorkspaceContentLimits.MaximumStringCharacterCount + 1);
        await AssertInvalidDataAsync(
            () => transfer.ExportJsonAsync(individualOverflow, destination, includeSecrets: true),
            "Portable export accepted an oversized field. / 便携导出接受了超长字段。");
        Assert(await File.ReadAllTextAsync(destination) == "durable-baseline", "Rejected portable export changed the existing destination. / 被拒绝的便携导出更改了现有目标。");

        AppDataDocument aggregateOverflow = new();
        for (int index = 0; index < 17; index++)
        {
            ConnectionProfile connection = CreateWorkspace().Connections.Single();
            connection.Id = Guid.NewGuid();
            connection.Name = "Aggregate " + index;
            connection.Notes = new string('a', WorkspaceContentLimits.MaximumStringCharacterCount);
            aggregateOverflow.Connections.Add(connection);
        }

        await AssertInvalidDataAsync(
            () => transfer.ExportCsvAsync(aggregateOverflow, System.IO.Path.Combine(scope.Path, "aggregate.csv"), includeSecrets: true),
            "CSV export accepted aggregate text beyond the workspace budget. / CSV 导出接受了超过工作区预算的聚合文本。");
    }

    /// <summary>
    /// Verifies an already protected damaged artifact is recognized structurally without another decryption. / 验证已受保护的损坏文件通过结构识别而不会再次解密。
    /// </summary>
    private static async Task TestProtectedArtifactsAreNotRepeatedlyDecryptedAsync()
    {
        using TemporaryDirectoryScope scope = new();
        AppDataPaths paths = new(scope.Path);
        CountingPassthroughProtector protector = new();
        JsonWorkspaceRepository repository = new(paths, protector);
        AppDataDocument document = CreateWorkspace();
        await repository.SaveAsync(document);
        string artifactPath = System.IO.Path.Combine(paths.DataDirectory, "workspace.corrupt.20260902123456789.json");
        await File.WriteAllTextAsync(artifactPath, "legacy damaged bytes");
        document.Settings.EncryptionEnabled = true;
        await repository.SaveAsync(document);

        protector.ResetUnprotectCount();
        await repository.LoadAsync();
        Assert(protector.UnprotectCount == 1, "Repository decrypted an already protected damaged artifact during ordinary load. / 仓储在普通加载期间解密了已受保护的损坏文件。");
    }

    /// <summary>
    /// Verifies invalid Base64, plaintext Base64, and length-mismatched protected-looking files are atomically protected again. / 验证无效 Base64、明文 Base64 及长度不匹配的伪受保护文件会再次被原子保护。
    /// </summary>
    private static async Task TestForgedProtectedArtifactsAreRemigratedAsync()
    {
        using TemporaryDirectoryScope scope = new();
        AppDataPaths paths = new(scope.Path);
        JsonWorkspaceRepository repository = new(paths, new CountingPassthroughProtector());
        AppDataDocument document = CreateWorkspace();
        document.Settings.EncryptionEnabled = true;
        await repository.SaveAsync(document);

        string[] forgedDocuments =
        [
            CreateForgedArtifactJson(originalLength: 8, payload: "not-base64", verificationPayload: "AAAA"),
            CreateForgedArtifactJson(originalLength: 9, payload: Convert.ToBase64String(Encoding.UTF8.GetBytes("plaintext")), verificationPayload: "AAAA"),
            CreateForgedArtifactJson(originalLength: 99, payload: Convert.ToBase64String(Encoding.UTF8.GetBytes("length")), verificationPayload: "AAAA")
        ];
        List<(string Path, byte[] Original)> fixtures = [];
        for (int index = 0; index < forgedDocuments.Length; index++)
        {
            string path = System.IO.Path.Combine(paths.DataDirectory, $"workspace.corrupt.2026090212345678{index}.json");
            byte[] original = Encoding.UTF8.GetBytes(forgedDocuments[index]);
            await File.WriteAllBytesAsync(path, original);
            fixtures.Add((path, original));
        }

        await repository.LoadAsync();
        foreach ((string path, byte[] original) in fixtures)
        {
            using System.Text.Json.JsonDocument json = System.Text.Json.JsonDocument.Parse(await File.ReadAllBytesAsync(path));
            byte[] migratedPayload = Convert.FromBase64String(json.RootElement.GetProperty("payload").GetString() ?? string.Empty);
            Assert(migratedPayload.SequenceEqual(original), "A forged protected-looking artifact was trusted instead of remigrated. / 伪造的受保护外观文件被信任而未重新迁移。");
        }
    }

    /// <summary>
    /// Creates an exact protected-artifact shape with caller-controlled untrusted payload fields. / 使用调用方控制的不可信载荷字段创建精确的受保护文件外形。
    /// </summary>
    /// <param name="originalLength">Declared original length. / 声明的原始长度。</param>
    /// <param name="payload">Candidate payload text. / 候选载荷文本。</param>
    /// <param name="verificationPayload">Candidate verification text. / 候选校验文本。</param>
    /// <returns>Forged JSON text. / 伪造的 JSON 文本。</returns>
    private static string CreateForgedArtifactJson(long originalLength, string payload, string verificationPayload)
    {
        return $$"""
            {"format":"remotehubstudio-corrupt-artifact","schemaVersion":2,"preservedAtUtc":"2026-09-02T00:00:00Z","protection":"test-counting","originalLength":{{originalLength}},"payload":"{{payload}}","verificationPayload":"{{verificationPayload}}"}
            """;
    }

    /// <summary>
    /// Verifies migration count overflow is diagnostic and cannot partially commit encryption opt-in. / 验证迁移数量超限可诊断且不会部分提交加密启用操作。
    /// </summary>
    private static async Task TestArtifactMigrationCountBoundaryAsync()
    {
        using TemporaryDirectoryScope scope = new();
        AppDataPaths paths = new(scope.Path);
        JsonWorkspaceRepository repository = new(paths, new CountingPassthroughProtector());
        AppDataDocument document = CreateWorkspace();
        await repository.SaveAsync(document);
        string baseline = await File.ReadAllTextAsync(paths.WorkspaceFilePath);
        for (int index = 0; index < 33; index++)
        {
            await File.WriteAllTextAsync(
                System.IO.Path.Combine(paths.DataDirectory, $"workspace.corrupt.{index:00000000000000000}.json"),
                "legacy plaintext " + index);
        }

        document.Settings.EncryptionEnabled = true;
        await AssertPersistenceFailureAsync(
            () => repository.SaveAsync(document),
            "Repository accepted more than 32 damaged artifacts for one migration. / 仓储在一次迁移中接受了超过 32 个损坏文件。");
        Assert(await File.ReadAllTextAsync(paths.WorkspaceFilePath) == baseline, "Failed artifact migration committed a new workspace revision. / 损坏文件迁移失败后仍提交了新的工作区修订版本。");
        Assert(Directory.EnumerateFiles(paths.DataDirectory, "workspace.corrupt.*.json").All(path => File.ReadAllText(path).StartsWith("legacy plaintext", StringComparison.Ordinal)), "Boundary failure partially migrated plaintext artifacts. / 边界失败部分迁移了明文保留文件。");
    }

    /// <summary>
    /// Creates a small valid workspace fixture. / 创建一个小型有效工作区测试数据。
    /// </summary>
    /// <returns>A valid document with one connection. / 包含一个连接的有效文档。</returns>
    private static AppDataDocument CreateWorkspace()
    {
        return new AppDataDocument
        {
            Connections =
            [
                new ConnectionProfile
                {
                    Name = "Content boundary fixture",
                    Type = ConnectionType.Putty,
                    Protocol = "ssh",
                    Host = "boundary.example",
                    Port = 22
                }
            ]
        };
    }

    /// <summary>
    /// Requires an asynchronous operation to fail with invalid input. / 要求异步操作因输入无效而失败。
    /// </summary>
    /// <param name="action">Operation to execute. / 要执行的操作。</param>
    /// <param name="message">Assertion message. / 断言消息。</param>
    private static async Task AssertInvalidDataAsync(Func<Task> action, string message)
    {
        try
        {
            await action();
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Requires an asynchronous repository operation to fail publicly. / 要求异步仓储操作以公开异常失败。
    /// </summary>
    /// <param name="action">Repository operation to execute. / 要执行的仓储操作。</param>
    /// <param name="message">Assertion message. / 断言消息。</param>
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
    /// Supplies deterministic reversible protection and counts decryptions. / 提供确定性可逆保护并统计解密次数。
    /// </summary>
    private sealed class CountingPassthroughProtector : IWorkspaceDataProtector
    {
        /// <summary>Gets the test protection scheme. / 获取测试保护方案。</summary>
        public string Scheme => "test-counting";

        /// <summary>Gets the number of unprotect operations since reset. / 获取重置后的解保护操作次数。</summary>
        public int UnprotectCount { get; private set; }

        /// <summary>Returns a detached protected representation. / 返回独立的受保护表示。</summary>
        /// <param name="plaintext">Plaintext bytes. / 明文字节。</param>
        /// <returns>Protected bytes. / 受保护字节。</returns>
        public byte[] Protect(byte[] plaintext) => (byte[])plaintext.Clone();

        /// <summary>Returns a detached plaintext representation and increments the counter. / 返回独立明文表示并递增计数器。</summary>
        /// <param name="protectedData">Protected bytes. / 受保护字节。</param>
        /// <returns>Plaintext bytes. / 明文字节。</returns>
        public byte[] Unprotect(byte[] protectedData)
        {
            UnprotectCount++;
            return (byte[])protectedData.Clone();
        }

        /// <summary>Resets the observed unprotect count. / 重置已观察到的解保护次数。</summary>
        public void ResetUnprotectCount()
        {
            UnprotectCount = 0;
        }
    }

    /// <summary>
    /// Owns and safely removes one uniquely named boundary-regression directory. / 管理并安全删除一个唯一命名的边界回归目录。
    /// </summary>
    private sealed class TemporaryDirectoryScope : IDisposable
    {
        private readonly string _testRoot;

        /// <summary>Creates a unique temporary regression directory. / 创建唯一的临时回归目录。</summary>
        public TemporaryDirectoryScope()
        {
            _testRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RemoteHubStudio.ContentBoundaryRegression"));
            Path = System.IO.Path.Combine(_testRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        /// <summary>Gets the unique absolute regression path. / 获取唯一的绝对回归路径。</summary>
        public string Path { get; }

        /// <summary>Deletes only the validated unique regression directory. / 仅删除经过验证的唯一回归目录。</summary>
        public void Dispose()
        {
            string fullPath = System.IO.Path.GetFullPath(Path);
            string prefix = _testRoot.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
            }
        }
    }
}
