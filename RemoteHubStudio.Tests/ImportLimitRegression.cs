using RemoteHubStudio.Application;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Infrastructure.ImportExport;

namespace RemoteHubStudio.Tests;

/// <summary>
/// Provides independently callable regression checks for untrusted import resource limits. / 提供可独立调用的不可信导入资源上限回归检查。
/// </summary>
internal static class ImportLimitRegression
{
    private const int MaximumFieldCharacterCount = 256 * 1024;

    /// <summary>
    /// Runs CSV and JSON resource-limit regression checks. / 运行 CSV 和 JSON 资源上限回归检查。
    /// </summary>
    public static async Task RunAsync()
    {
        TestCsvCommaStorm();
        TestCsvRecordLimit();
        TestCsvFieldLimit();
        await TestJsonEntityLimitsAsync();
        await TestMaximumConnectionCsvRoundTripAsync();
        await TestImportFileLimitAsync();
        await TestOversizedExportsRemainAtomicAsync();
    }

    /// <summary>
    /// Verifies a comma storm cannot allocate an unbounded number of fields. / 验证逗号风暴无法分配无界数量的字段。
    /// </summary>
    private static void TestCsvCommaStorm()
    {
        AssertInvalidData(
            () => CsvCodec.Decode(new string(',', 64)),
            "CSV comma storm was accepted. / CSV 逗号风暴被接受。");
    }

    /// <summary>
    /// Verifies CSV encoding and decoding reject more than the shared bounded record count. / 验证 CSV 编码与解码都会拒绝超过共享上限的记录数。
    /// </summary>
    private static void TestCsvRecordLimit()
    {
        string records = new('\n', CsvCodec.MaximumRecordCount + 1);
        AssertInvalidData(
            () => CsvCodec.Decode(records),
            "Oversized CSV record count was accepted. / 超限的 CSV 记录数被接受。");
        IEnumerable<IReadOnlyList<string?>> oversizedRows = Enumerable.Repeat<IReadOnlyList<string?>>(
            new string?[] { "x" },
            CsvCodec.MaximumRecordCount + 1);
        AssertInvalidData(
            () => CsvCodec.Encode(oversizedRows),
            "Oversized CSV encode record count was accepted. / CSV 编码接受了超限记录数。");
    }

    /// <summary>
    /// Verifies CSV decoding bounds a field before its buffer grows beyond the limit. / 验证 CSV 解码会在字段缓冲区超限前限制其大小。
    /// </summary>
    private static void TestCsvFieldLimit()
    {
        string field = new('x', MaximumFieldCharacterCount + 1);
        AssertInvalidData(
            () => CsvCodec.Decode(field),
            "Oversized CSV field was accepted. / 超限的 CSV 字段被接受。");
    }

    /// <summary>
    /// Verifies per-collection JSON entity limits before model allocation. / 验证模型分配前的 JSON 单集合实体上限。
    /// </summary>
    private static async Task TestJsonEntityLimitsAsync()
    {
        string testDirectory = CreateTestDirectory();
        try
        {
            WorkspaceTransferService service = new();
            string perCollectionPath = System.IO.Path.Combine(testDirectory, "too-many-groups.json");
            string tooManyGroups = BuildEmptyObjectArray(WorkspaceLimits.MaximumGroupCount + 1);
            await File.WriteAllTextAsync(perCollectionPath, $"{{\"schemaVersion\":1,\"groups\":[{tooManyGroups}]}}");
            await AssertInvalidDataAsync(
                () => service.ImportJsonAsync(perCollectionPath),
                "Oversized JSON entity collection was accepted. / 超限的 JSON 实体集合被接受。");

            string connectionPath = System.IO.Path.Combine(testDirectory, "too-many-connections.json");
            string tooManyConnections = BuildEmptyObjectArray(WorkspaceLimits.MaximumConnectionCount + 1);
            await File.WriteAllTextAsync(
                connectionPath,
                $"{{\"schemaVersion\":1,\"connections\":[{tooManyConnections}]}}");
            await AssertInvalidDataAsync(
                () => service.ImportJsonAsync(connectionPath),
                "Oversized JSON connection collection was accepted. / 超限的 JSON 连接集合被接受。");
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    /// <summary>
    /// Verifies import rejects a file larger than the desktop-oriented 16 MiB limit. / 验证导入会拒绝超过桌面管理器定位的 16 MiB 文件。
    /// </summary>
    private static async Task TestImportFileLimitAsync()
    {
        string testDirectory = CreateTestDirectory();
        try
        {
            string filePath = System.IO.Path.Combine(testDirectory, "oversized.json");
            await using (FileStream stream = new(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.SetLength(WorkspaceTransferService.MaximumTransferFileLength + 1L);
            }

            WorkspaceTransferService service = new();
            await AssertInvalidDataAsync(
                () => service.ImportJsonAsync(filePath),
                "Oversized import file was accepted. / 超限的导入文件被接受。");
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    /// <summary>
    /// Verifies the maximum supported connection count exports with one header and imports without an off-by-one rejection. / 验证最大支持连接数可连同一个表头导出，并可在无差一错误的情况下重新导入。
    /// </summary>
    private static async Task TestMaximumConnectionCsvRoundTripAsync()
    {
        string testDirectory = CreateTestDirectory();
        try
        {
            List<ConnectionProfile> connections = new(WorkspaceLimits.MaximumConnectionCount);
            for (int index = 0; index < WorkspaceLimits.MaximumConnectionCount; index++)
            {
                connections.Add(new ConnectionProfile
                {
                    Name = $"C{index}",
                    Type = ConnectionType.Putty,
                    Protocol = "ssh",
                    Host = "h",
                    Port = 22
                });
            }

            WorkspaceTransferService service = new();
            string csvPath = System.IO.Path.Combine(testDirectory, "maximum-connections.csv");
            await service.ExportCsvAsync(
                new AppDataDocument { Connections = connections },
                csvPath,
                includeSecrets: false);
            ImportResult imported = await service.ImportCsvAsync(csvPath);

            Assert(imported.Connections.Count == WorkspaceLimits.MaximumConnectionCount, "Maximum-size CSV connection round-trip lost rows. / 最大规模 CSV 连接往返丢失了行。");
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    /// <summary>
    /// Verifies oversized JSON and CSV exports fail before replacing an existing destination. / 验证超限 JSON 与 CSV 导出会在替换现有目标前失败。
    /// </summary>
    private static async Task TestOversizedExportsRemainAtomicAsync()
    {
        string testDirectory = CreateTestDirectory();
        try
        {
            WorkspaceTransferService service = new();
            AppDataDocument document = new()
            {
                Connections =
                [
                    new ConnectionProfile
                    {
                        Name = "Oversized export",
                        Type = ConnectionType.Putty,
                        Protocol = "ssh",
                        Host = "oversized.example",
                        Port = 22,
                        Notes = new string('x', checked((int)WorkspaceTransferService.MaximumTransferFileLength + 1024))
                    }
                ]
            };
            string jsonPath = System.IO.Path.Combine(testDirectory, "existing.rhs.json");
            string csvPath = System.IO.Path.Combine(testDirectory, "existing.csv");
            const string existingContent = "existing-destination-content";
            await File.WriteAllTextAsync(jsonPath, existingContent);
            await File.WriteAllTextAsync(csvPath, existingContent);

            await AssertInvalidDataAsync(
                () => service.ExportJsonAsync(document, jsonPath, includeSecrets: false),
                "An oversized JSON export was accepted. / 超限 JSON 导出被接受。");
            await AssertInvalidDataAsync(
                () => service.ExportCsvAsync(document, csvPath, includeSecrets: false),
                "An oversized CSV export was accepted. / 超限 CSV 导出被接受。");

            Assert(await File.ReadAllTextAsync(jsonPath) == existingContent, "Failed JSON export replaced the existing destination. / 失败的 JSON 导出替换了现有目标。");
            Assert(await File.ReadAllTextAsync(csvPath) == existingContent, "Failed CSV export replaced the existing destination. / 失败的 CSV 导出替换了现有目标。");
            Assert(!Directory.EnumerateFiles(testDirectory, ".*.tmp", SearchOption.TopDirectoryOnly).Any(), "Failed export left a temporary file behind. / 失败的导出留下了临时文件。");
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    /// <summary>
    /// Builds compact JSON array contents containing empty objects. / 构建由空对象组成的紧凑 JSON 数组内容。
    /// </summary>
    /// <param name="count">Object count. / 对象数量。</param>
    /// <returns>Comma-separated JSON objects without surrounding brackets. / 不含外层方括号的逗号分隔 JSON 对象。</returns>
    private static string BuildEmptyObjectArray(int count)
    {
        return string.Join(',', Enumerable.Repeat("{}", count));
    }

    /// <summary>
    /// Creates a unique regression directory beneath the operating-system temporary directory. / 在操作系统临时目录下创建唯一回归测试目录。
    /// </summary>
    /// <returns>The created absolute directory path. / 已创建的绝对目录路径。</returns>
    private static string CreateTestDirectory()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RemoteHubStudio.ImportLimitRegression", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return System.IO.Path.GetFullPath(path);
    }

    /// <summary>
    /// Removes only a validated unique regression directory. / 仅删除已验证的唯一回归测试目录。
    /// </summary>
    /// <param name="path">Regression directory path. / 回归测试目录路径。</param>
    private static void DeleteTestDirectory(string path)
    {
        string testRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RemoteHubStudio.ImportLimitRegression"));
        string fullPath = System.IO.Path.GetFullPath(path);
        string requiredPrefix = testRoot.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
        if (fullPath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a synchronous action throws InvalidDataException. / 验证同步操作抛出 InvalidDataException。
    /// </summary>
    /// <param name="action">Action to execute. / 要执行的操作。</param>
    /// <param name="message">Failure message. / 失败消息。</param>
    private static void AssertInvalidData(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Verifies an asynchronous action throws InvalidDataException. / 验证异步操作抛出 InvalidDataException。
    /// </summary>
    /// <param name="action">Asynchronous action to execute. / 要执行的异步操作。</param>
    /// <param name="message">Failure message. / 失败消息。</param>
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
}
