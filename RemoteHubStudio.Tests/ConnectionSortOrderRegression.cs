using System.Text.Json.Nodes;
using RemoteHubStudio.Application;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Infrastructure.ImportExport;
using RemoteHubStudio.Infrastructure.Persistence;
using RemoteHubStudio.Infrastructure.Security;

namespace RemoteHubStudio.Tests;

/// <summary>Verifies persisted connection ordering and compatibility with older files. / 验证连接排序持久化及旧文件兼容性。</summary>
internal static class ConnectionSortOrderRegression
{
    /// <summary>Runs real repository, service, projection, and transfer regressions in temporary directories. / 在临时目录运行真实仓储、服务、投影和传输回归。</summary>
    public static async Task RunAsync()
    {
        using TemporaryDirectoryScope scope = new();
        await TestLegacyJsonAsync(scope.Path);
        await TestLegacyCsvAsync(scope.Path);
        await TestPersistenceAndTransfersAsync(scope.Path);
    }

    /// <summary>Loads old raw documents and envelopes without ordering while retaining their other values. / 加载没有排序字段的旧文档与信封，并保留其他数据。</summary>
    private static async Task TestLegacyJsonAsync(string directory)
    {
        const string legacyJson = """
            {
              "schemaVersion": 1,
              "settings": { "encryptionEnabled": false, "toolPaths": { "putty": "C:\\Tools\\putty.exe" } },
              "groups": [{ "id": "11111111-1111-1111-1111-111111111111", "name": "Legacy group", "sortOrder": 6 }],
              "connections": [{
                "id": "22222222-2222-2222-2222-222222222222",
                "name": "Legacy connection",
                "groupId": "11111111-1111-1111-1111-111111111111",
                "type": "putty", "protocol": "ssh", "host": "legacy.example", "port": 2222,
                "username": "legacy-user", "password": "  legacy-secret  ",
                "notes": "旧备注, preserved", "isFavorite": true,
                "rdp": { "desktopWidth": 1280 }, "options": { "terminalType": "xterm" },
                "createdAtUtc": "2025-01-02T03:04:05Z", "updatedAtUtc": "2025-02-03T04:05:06Z"
              }]
            }
            """;
        AppDataPaths paths = new(System.IO.Path.Combine(directory, "legacy-json"));
        paths.EnsureDirectoriesExist();
        await File.WriteAllTextAsync(paths.WorkspaceFilePath, legacyJson);
        JsonWorkspaceRepository repository = CreateRepository(paths);
        WorkspaceLoadResult loaded = await repository.LoadAsync();
        Assert(!loaded.RecoveredFromBackup, "Legacy JSON required recovery. / 旧 JSON 意外需要恢复。");
        AssertLegacyValues(loaded.Document);
        WorkspaceService workspace = new(repository);
        await workspace.InitializeAsync();
        AssertLegacyValues(workspace.GetSnapshot());

        // Saving upgrades the raw document to a local envelope; missing fields in either format must default to zero.
        // 保存会将裸文档升级为本地信封；两种格式中的缺失字段都应默认为零。
        await repository.SaveAsync(loaded.Document);
        AssertLegacyValues((await CreateRepository(paths).LoadAsync()).Document);
        await RemoveSortOrderFromEnvelopeAsync(paths.WorkspaceFilePath);
        loaded = await CreateRepository(paths).LoadAsync();
        Assert(!loaded.RecoveredFromBackup, "An old local envelope required recovery. / 旧本地信封意外需要恢复。");
        AssertLegacyValues(loaded.Document);

        WorkspaceTransferService transfer = new();
        string portablePath = System.IO.Path.Combine(directory, "legacy-portable.json");
        await File.WriteAllTextAsync(portablePath, legacyJson);
        AssertLegacyValues(await transfer.ImportJsonAsync(portablePath, trustLaunchConfiguration: true));
        await transfer.ExportJsonAsync(loaded.Document, portablePath, includeSecrets: true);
        await RemoveSortOrderFromEnvelopeAsync(portablePath);
        AssertLegacyValues(await transfer.ImportJsonAsync(portablePath, trustLaunchConfiguration: true), includeSettings: false);
    }

    /// <summary>Checks missing/empty CSV ordering, retained legacy fields, and existing invalid-row handling. / 检查 CSV 缺失及空排序、旧字段保留和既有无效行处理。</summary>
    private static async Task TestLegacyCsvAsync(string directory)
    {
        WorkspaceTransferService transfer = new();
        string csvPath = System.IO.Path.Combine(directory, "legacy.csv");
        await File.WriteAllTextAsync(csvPath, CsvCodec.Encode([
            ["Name", "Type", "Protocol", "Host", "Port", "Group", "Username", "Password", "Notes", "Favorite"],
            ["Legacy CSV", "Putty", "ssh", "legacy.example", "2222", "Old group", "old-user", "  old-secret  ", "旧备注, retained", "true"]
        ]));
        ImportResult result = await transfer.ImportCsvAsync(csvPath);
        ConnectionProfile legacy = result.Connections.Single();
        Assert(legacy.SortOrder == 0 && result.SkippedRowCount == 0 && result.ModifiedRowCount == 0,
            "CSV without SortOrder did not import normally with zero. / 缺少 SortOrder 的 CSV 未以零排序正常导入。");
        Assert(legacy.Name == "Legacy CSV" && legacy.Type == ConnectionType.Putty && legacy.Protocol == "ssh" &&
               legacy.Host == "legacy.example" && legacy.Port == 2222 && legacy.Username == "old-user" &&
               legacy.Password == "  old-secret  " && legacy.Notes == "旧备注, retained" && legacy.IsFavorite &&
               result.Groups.Single().Name == "Old group" && legacy.GroupId == result.Groups.Single().Id,
            "Legacy CSV values were changed or lost. / 旧 CSV 字段被更改或丢失。");

        await File.WriteAllTextAsync(csvPath, CsvCodec.Encode([
            ["Name", "Type", "Host", "SortOrder"],
            ["Blank", "Putty", "blank.example", "  "],
            ["Missing trailing value", "Putty", "missing.example"],
            ["Explicit zero", "Putty", "zero.example", "0"],
            ["Negative", "Putty", "negative.example", "-12"],
            ["Positive", "Putty", "positive.example", "34"],
            ["Invalid", "Putty", "invalid.example", "not-a-number"],
            ["Overflow", "Putty", "overflow.example", "2147483648"]
        ]));
        result = await transfer.ImportCsvAsync(csvPath);
        Assert(result.Connections.Select(connection => connection.SortOrder).SequenceEqual([0, 0, 0, -12, 34]) &&
               result.SkippedRowCount == 2 && result.Warnings.Count == 2,
            "CSV optional ordering or invalid-row handling changed. / CSV 可选排序或无效行处理不正确。");
    }

    /// <summary>Preserves signed orders through service mutations, durable reopening, projection, and both transfer formats. / 通过服务修改、持久重开、投影及两种传输格式保留有符号排序。</summary>
    private static async Task TestPersistenceAndTransfersAsync(string directory)
    {
        int[] orders = [12, -7, 0, int.MinValue, int.MaxValue];
        AppDataPaths paths = new(System.IO.Path.Combine(directory, "round-trip"));
        WorkspaceService workspace = new(CreateRepository(paths));
        await workspace.InitializeAsync();
        ConnectionGroup group = await workspace.AddGroupAsync(new ConnectionGroup { Name = "Sort group", SortOrder = 3 });
        for (int index = 0; index < orders.Length; index++)
        {
            ConnectionProfile draft = new()
            {
                Name = $"Sort {index}", GroupId = group.Id, Type = ConnectionType.Putty,
                Protocol = "ssh", Host = $"sort-{index}.example", Port = 22,
                Username = "sort-user", Password = "sort-secret", Notes = "排序测试, notes",
                IsFavorite = index % 2 == 0, SortOrder = orders[index],
                Rdp = new RdpOptions { DesktopWidth = 1280 },
                Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["terminalType"] = "xterm" }
            };
            ConnectionProfile added = await workspace.AddConnectionAsync(draft);
            Assert(added.SortOrder == orders[index], "Add lost the requested order. / 新增丢失请求的排序。");
            added.SortOrder = 99;
            draft.SortOrder = 98;
            Assert(workspace.GetConnection(added.Id)!.SortOrder == orders[index],
                "Add retained a shared connection reference. / 新增保留了共享连接引用。");
            Assert(await workspace.UpdateConnectionAsync(added), "Connection update failed. / 连接更新失败。");
            Assert(workspace.GetConnection(added.Id)!.SortOrder == 99, "Update lost the new order. / 更新丢失新排序。");
            added.SortOrder = orders[index];
            Assert(await workspace.UpdateConnectionAsync(added), "Restoring the signed order failed. / 恢复有符号排序失败。");
        }

        AppDataDocument expected = workspace.GetSnapshot();
        Assert(expected.Connections.Select(connection => connection.SortOrder).SequenceEqual(orders),
            "Snapshot lost signed or zero orders. / 快照丢失有符号或零排序。");
        expected.Connections[0].SortOrder = 88;
        IReadOnlyList<ConnectionProfile> detached = workspace.GetConnections();
        detached[0].SortOrder = 77;
        Assert(workspace.GetConnection(expected.Connections[0].Id)!.SortOrder == orders[0],
            "Snapshot or connection list shared mutable ordering state. / 快照或连接列表共享了可变排序状态。");
        expected = workspace.GetSnapshot();
        WorkspaceService reopened = new(CreateRepository(paths));
        await reopened.InitializeAsync();
        AssertConnectionValues(expected, reopened.GetSnapshot());

        AppDataDocument projected = WorkspaceExportProjector.Create(expected, expected.Connections.Select(connection => connection.Id).Reverse());
        AssertConnectionValues(expected, projected);
        Assert(projected.Connections.Select(connection => connection.SortOrder).SequenceEqual(orders.Reverse()),
            "Projection changed requested order or sort values. / 投影更改了请求顺序或排序值。");
        projected.Connections[0].SortOrder = 55;
        Assert(expected.Connections[^1].SortOrder == orders[^1], "Projection shared sort state. / 投影共享了排序状态。");

        // Exercise the encrypted repository path with a deterministic test-only protector.
        // 使用仅用于测试的确定性保护器，覆盖加密仓储路径。
        expected.Settings.EncryptionEnabled = true;
        await CreateRepository(paths).SaveAsync(expected);
        AssertConnectionValues(expected, (await CreateRepository(paths).LoadAsync()).Document);

        WorkspaceTransferService transfer = new();
        string jsonPath = System.IO.Path.Combine(directory, "sorted.json");
        string csvPath = System.IO.Path.Combine(directory, "sorted.csv");
        await transfer.ExportJsonAsync(expected, jsonPath, includeSecrets: true);
        AppDataDocument importedJson = await transfer.ImportJsonAsync(jsonPath, trustLaunchConfiguration: true);
        AssertConnectionValues(expected, importedJson);
        await transfer.ExportCsvAsync(expected, csvPath, includeSecrets: true);
        ImportResult importedCsv = await transfer.ImportCsvAsync(csvPath, trustLaunchConfiguration: true);
        Assert(importedCsv.SkippedRowCount == 0 && importedCsv.ModifiedRowCount == 0,
            "Native CSV did not preserve every signed order. / 原生 CSV 未保留全部有符号排序。");
        AppDataDocument csvDocument = new() { Groups = importedCsv.Groups, Connections = importedCsv.Connections };
        AssertConnectionValues(expected, csvDocument);

        // Both formats must replace an existing nonzero value, including an explicitly imported zero.
        // 两种格式都必须替换已有非零排序，包括明确导入的零。
        foreach (AppDataDocument imported in new[] { importedJson, csvDocument })
        {
            foreach (ConnectionProfile connection in reopened.GetConnections())
            {
                connection.SortOrder = 42;
                await reopened.UpdateConnectionAsync(connection);
            }

            WorkspaceImportSummary summary = await reopened.MergeAsync(imported);
            Assert(summary.CreatedConnectionCount == 0 && summary.UpdatedConnectionCount == orders.Length,
                "Sort import did not update existing connections by name. / 排序导入未按名称更新现有连接。");
            AssertConnectionValues(expected, reopened.GetSnapshot());
            AssertConnectionValues(expected, (await CreateRepository(paths).LoadAsync()).Document);
        }
    }

    /// <summary>Removes only connection ordering from a generated local or portable envelope. / 仅移除生成的本地或便携信封中的连接排序。</summary>
    private static async Task RemoveSortOrderFromEnvelopeAsync(string path)
    {
        JsonNode envelope = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        foreach (JsonNode? connection in envelope["data"]!["connections"]!.AsArray())
        {
            Assert(connection!.AsObject().Remove("sortOrder"), "Export omitted SortOrder. / 导出遗漏 SortOrder。");
        }

        await File.WriteAllTextAsync(path, envelope.ToJsonString());
    }

    /// <summary>Checks old connection values and the default zero without relying on current serialization defaults. / 检查旧连接字段与默认零，不依赖当前序列化默认值。</summary>
    private static void AssertLegacyValues(AppDataDocument document, bool includeSettings = true)
    {
        ConnectionProfile connection = document.Connections.Single();
        Assert(connection.SortOrder == 0 && connection.Id == Guid.Parse("22222222-2222-2222-2222-222222222222") &&
               connection.Name == "Legacy connection" && connection.Host == "legacy.example" && connection.Port == 2222 &&
               connection.Type == ConnectionType.Putty && connection.Protocol == "ssh" && connection.Username == "legacy-user" &&
               connection.Password == "  legacy-secret  " && connection.Notes == "旧备注, preserved" && connection.IsFavorite &&
               connection.Rdp.DesktopWidth == 1280 && connection.Options["terminalType"] == "xterm" &&
               connection.CreatedAtUtc == new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc) &&
               connection.UpdatedAtUtc == new DateTime(2025, 2, 3, 4, 5, 6, DateTimeKind.Utc) &&
               connection.GroupId == document.Groups.Single().Id && document.Groups.Single().Name == "Legacy group" &&
               document.Groups.Single().SortOrder == 6,
            "Old JSON did not default ordering to zero while preserving existing fields. / 旧 JSON 未在保留已有字段时将排序默认为零。");
        if (includeSettings)
        {
            Assert(document.Settings.ToolPaths["putty"] == @"C:\Tools\putty.exe", "Legacy settings were lost. / 旧设置丢失。");
        }
    }

    /// <summary>Compares transferable connection fields by name, allowing CSV to regenerate identifiers. / 按名称比较可传输字段，允许 CSV 重新生成标识。</summary>
    private static void AssertConnectionValues(AppDataDocument expected, AppDataDocument actual)
    {
        Assert(expected.Connections.Count == actual.Connections.Count, "Connection count changed. / 连接数量发生变化。");
        foreach (ConnectionProfile source in expected.Connections)
        {
            ConnectionProfile target = actual.Connections.Single(connection => connection.Name == source.Name);
            Assert(target.SortOrder == source.SortOrder && target.Type == source.Type && target.Protocol == source.Protocol &&
                   target.Host == source.Host && target.Port == source.Port && target.Username == source.Username &&
                   target.Password == source.Password && target.Notes == source.Notes && target.IsFavorite == source.IsFavorite &&
                   target.Rdp.DesktopWidth == source.Rdp.DesktopWidth && target.Options["terminalType"] == source.Options["terminalType"] &&
                   actual.Groups.Single(group => group.Id == target.GroupId).Name == expected.Groups.Single(group => group.Id == source.GroupId).Name,
                $"Ordering or existing fields changed for {source.Name}. / {source.Name} 的排序或已有字段发生变化。");
        }
    }

    /// <summary>Creates a repository rooted in the caller's isolated test directory. / 在调用方的隔离测试目录创建仓储。</summary>
    private static JsonWorkspaceRepository CreateRepository(AppDataPaths paths) => new(paths, new PassthroughProtector());

    /// <summary>Throws when a regression assertion fails. / 回归断言失败时抛出异常。</summary>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>Supplies reversible test bytes without accessing user cryptographic keys. / 提供可逆测试字节，不访问用户加密密钥。</summary>
    private sealed class PassthroughProtector : IWorkspaceDataProtector
    {
        public string Scheme => "test-sort-order";
        public byte[] Protect(byte[] plaintext) => (byte[])plaintext.Clone();
        public byte[] Unprotect(byte[] protectedData) => (byte[])protectedData.Clone();
    }

    /// <summary>Owns a uniquely named temporary directory and validates its boundary before cleanup. / 管理唯一临时目录，并在清理前验证边界。</summary>
    private sealed class TemporaryDirectoryScope : IDisposable
    {
        private readonly string _root = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RemoteHubStudio.ConnectionSortOrderRegression"));

        public TemporaryDirectoryScope()
        {
            Path = System.IO.Path.Combine(_root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            string fullPath = System.IO.Path.GetFullPath(Path);
            string prefix = _root.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
            }
        }
    }
}
