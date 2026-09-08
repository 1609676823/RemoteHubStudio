using System.Text.Json;
using RemoteHubStudio.Application;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Infrastructure.ImportExport;

namespace RemoteHubStudio.Tests;

/// <summary>
/// Verifies deterministic, name-based workspace import upserts. / 验证确定性的按名称工作区导入更新或创建行为。
/// </summary>
internal static class NameBasedImportRegression
{
    /// <summary>
    /// Runs name matching, relationship, idempotence, ambiguity, secret-replacement, and capacity regressions.
    /// / 运行名称匹配、关联、幂等、歧义、秘密替换与容量回归检查。
    /// </summary>
    public static async Task RunAsync()
    {
        await TestNameBasedUpsertAndIdempotenceAsync();
        await TestBlankImportedSecretsReplaceExistingSecretsAsync();
        await TestDuplicateImportedNamesAreRejectedAtomicallyAsync();
        await TestAmbiguousLocalNamesAreRejectedAtomicallyAsync();
        await TestFullCapacityNameUpdatesAsync();
    }

    /// <summary>
    /// Verifies full name-based replacements do not silently retain secrets omitted from an imported document.
    /// / 验证按名称全量替换不会静默保留导入文档中缺失的秘密。
    /// </summary>
    private static async Task TestBlankImportedSecretsReplaceExistingSecretsAsync()
    {
        RecordingWorkspaceRepository repository = new(new AppDataDocument
        {
            Connections =
            [
                new ConnectionProfile
                {
                    Name = "Inline",
                    Type = ConnectionType.Putty,
                    Protocol = "ssh",
                    Host = "old-inline.example",
                    Port = 22,
                    Username = "local-inline-user",
                    Password = "local-inline-secret"
                }
            ]
        });
        WorkspaceService workspace = new(repository);
        await workspace.InitializeAsync();

        AppDataDocument exportSource = new()
        {
            Connections =
            [
                new ConnectionProfile
                {
                    Name = "inline",
                    Type = ConnectionType.Putty,
                    Protocol = "ssh",
                    Host = "new-inline.example",
                    Port = 22,
                    Username = "imported-inline-user",
                    Password = "exported-inline-secret"
                }
            ]
        };
        string exportDirectory = Path.Combine(
            Path.GetTempPath(),
            "RemoteHubStudio.NameBasedImportRegression",
            Guid.NewGuid().ToString("N"));
        string exportPath = Path.Combine(exportDirectory, "redacted.rhs.json");
        Directory.CreateDirectory(exportDirectory);
        try
        {
            WorkspaceTransferService transferService = new();
            await transferService.ExportJsonAsync(exportSource, exportPath, includeSecrets: false);
            AppDataDocument redactedImport = await transferService.ImportJsonAsync(
                exportPath,
                trustLaunchConfiguration: true);
            await workspace.MergeAsync(redactedImport);
        }
        finally
        {
            if (Directory.Exists(exportDirectory))
            {
                Directory.Delete(exportDirectory, recursive: true);
            }
        }

        AppDataDocument snapshot = workspace.GetSnapshot();
        ConnectionProfile imported = FindConnection(snapshot, "inline");
        Assert(imported.Username == "imported-inline-user" && imported.Password.Length == 0,
            "A redacted inline import retained the existing password or lost the imported username. / 脱敏后的内联导入保留了现有密码或丢失了导入用户名。");
    }

    /// <summary>
    /// Verifies groups and connections use trimmed, ordinal-ignore-case names while preserving local identities and references.
    /// / 验证分类与连接均使用裁剪后且不区分大小写的名称，同时保留本地标识与引用。
    /// </summary>
    private static async Task TestNameBasedUpsertAndIdempotenceAsync()
    {
        Guid localProductionGroupId = Guid.NewGuid();
        Guid localPrimaryConnectionId = Guid.NewGuid();
        Guid observerConnectionId = Guid.NewGuid();
        DateTime primaryCreatedAtUtc = new(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        DateTime primaryPreviousUpdateUtc = new(2025, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        DateTime observerUpdatedAtUtc = new(2025, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        DateTime firstImportUtc = new(2026, 9, 4, 1, 2, 3, DateTimeKind.Utc);
        MutableTimeProvider timeProvider = new(firstImportUtc);
        RecordingWorkspaceRepository repository = new(new AppDataDocument
        {
            Groups =
            [
                new ConnectionGroup
                {
                    Id = localProductionGroupId,
                    Name = "Production",
                    Color = "#111111",
                    SortOrder = 1
                }
            ],
            Connections =
            [
                CreateConnection(
                    localPrimaryConnectionId,
                    "  PRIMARY  ",
                    "old-primary.example",
                    groupId: localProductionGroupId,
                    username: "old-primary-user",
                    password: "old-primary-password",
                    createdAtUtc: primaryCreatedAtUtc,
                    updatedAtUtc: primaryPreviousUpdateUtc),
                CreateConnection(
                    observerConnectionId,
                    "Observer",
                    "observer.example",
                    groupId: localProductionGroupId,
                    username: "observer-user",
                    password: "observer-password",
                    createdAtUtc: primaryCreatedAtUtc.AddDays(1),
                    updatedAtUtc: observerUpdatedAtUtc)
            ]
        });
        WorkspaceService workspace = new(repository, timeProvider);
        await workspace.InitializeAsync();
        int importEventCount = 0;
        workspace.Changed += (_, change) =>
        {
            if (change.Kind == WorkspaceChangeKind.WorkspaceImported)
            {
                importEventCount++;
            }
        };

        Guid sourceProductionGroupId = Guid.NewGuid();
        Guid sourceChildGroupId = Guid.NewGuid();
        AppDataDocument imported = new()
        {
            // Put the child first to ensure parent remapping does not depend on source order.
            // 将子分组放在前面，以确保父级重映射不依赖源顺序。
            Groups =
            [
                new ConnectionGroup
                {
                    Id = sourceChildGroupId,
                    Name = "  Child  ",
                    ParentId = sourceProductionGroupId,
                    Color = "#333333",
                    SortOrder = 3
                },
                new ConnectionGroup
                {
                    Id = sourceProductionGroupId,
                    Name = "  production  ",
                    Color = "#222222",
                    SortOrder = 2
                }
            ],
            Connections =
            [
                new ConnectionProfile
                {
                    Id = Guid.NewGuid(),
                    Name = "primary",
                    GroupId = sourceProductionGroupId,
                    Type = ConnectionType.Putty,
                    Protocol = "ssh",
                    Host = "updated-primary.example",
                    Port = 2222,
                    Username = "updated-primary-user",
                    Password = "updated-primary-password",
                    Notes = "updated connection",
                    CreatedAtUtc = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAtUtc = new DateTime(2002, 2, 2, 0, 0, 0, DateTimeKind.Utc)
                },
                new ConnectionProfile
                {
                    Id = Guid.NewGuid(),
                    Name = "  New Connection  ",
                    GroupId = sourceChildGroupId,
                    Type = ConnectionType.Putty,
                    Protocol = "ssh",
                    Host = "new-connection.example",
                    Port = 22,
                    Username = "new-user",
                    Password = "new-password"
                }
            ]
        };

        WorkspaceImportSummary firstSummary = await workspace.MergeAsync(imported);
        AppDataDocument firstSnapshot = workspace.GetSnapshot();
        Assert(firstSummary.CreatedConnectionCount == 1 && firstSummary.UpdatedConnectionCount == 1,
            "The first import reported incorrect connection create/update counts. / 首次导入报告的连接新增/更新数量不正确。");
        Assert(firstSnapshot.Groups.Count == 2, "A name-matched group was appended instead of updated. / 按名称匹配的分组被追加而非更新。");
        Assert(firstSnapshot.Connections.Count == 3, "A name-matched connection was appended instead of updated. / 按名称匹配的连接被追加而非更新。");

        ConnectionGroup production = FindGroup(firstSnapshot, "production");
        ConnectionGroup child = FindGroup(firstSnapshot, "child");
        ConnectionProfile primary = FindConnection(firstSnapshot, "primary");
        ConnectionProfile newConnection = FindConnection(firstSnapshot, "new connection");
        ConnectionProfile observer = FindConnection(firstSnapshot, "observer");

        Assert(production.Id == localProductionGroupId, "A group update replaced its local identifier. / 分组更新替换了其本地标识。");
        Assert(production.Name == "production" && production.Color == "#222222" && production.SortOrder == 2,
            "A group name was not trimmed or its configuration was not replaced. / 分组名称未裁剪或其配置未被替换。");
        Assert(child.ParentId == localProductionGroupId, "An imported child group was not remapped to the matched local parent. / 导入子分组未重映射到匹配的本地父分组。");

        Assert(primary.Id == localPrimaryConnectionId, "A connection update replaced its local identifier. / 连接更新替换了其本地标识。");
        Assert(primary.Name == "primary" && primary.Host == "updated-primary.example" && primary.Port == 2222,
            "A connection name was not trimmed or its configuration was not replaced. / 连接名称未裁剪或其配置未被替换。");
        Assert(primary.GroupId == localProductionGroupId,
            "A matched connection did not remap its imported group reference. / 匹配连接未重映射导入分组引用。");
        Assert(primary.Username == "updated-primary-user" && primary.Password == "updated-primary-password",
            "A matched connection did not replace its inline authentication values. / 匹配连接未替换其内联认证信息。");
        Assert(primary.CreatedAtUtc == primaryCreatedAtUtc && primary.UpdatedAtUtc == firstImportUtc,
            "A connection update did not preserve CreatedAtUtc or stamp UpdatedAtUtc from the workspace clock. / 连接更新未保留 CreatedAtUtc 或未使用工作区时钟写入 UpdatedAtUtc。");

        Assert(observer.GroupId == localProductionGroupId &&
               observer.Username == "observer-user" &&
               observer.Password == "observer-password" &&
               observer.UpdatedAtUtc == observerUpdatedAtUtc,
            "Updating a matched group broke or modified an unrelated local connection. / 更新匹配分组时破坏或修改了无关本地连接。");
        Assert(newConnection.GroupId == child.Id &&
               newConnection.Username == "new-user" &&
               newConnection.Password == "new-password",
            "A newly created connection did not retain its group or inline authentication values. / 新建连接未保留其分组或内联认证信息。");
        Assert(child.Name == "Child" && newConnection.Name == "New Connection",
            "New entity names were not trimmed before creation. / 新实体名称在创建前未裁剪。");
        Assert(repository.SaveCount == 1 && importEventCount == 1,
            "The first import did not produce exactly one save and one import event. / 首次导入未精确产生一次保存和一次导入事件。");

        Dictionary<string, Guid> firstGroupIds = CollectGroupIds(firstSnapshot);
        Dictionary<string, Guid> firstConnectionIds = CollectConnectionIds(firstSnapshot);
        DateTime newConnectionCreatedAtUtc = newConnection.CreatedAtUtc;
        DateTime secondImportUtc = firstImportUtc.AddHours(1);
        timeProvider.UtcNow = secondImportUtc;

        WorkspaceImportSummary secondSummary = await workspace.MergeAsync(imported);
        AppDataDocument secondSnapshot = workspace.GetSnapshot();
        Assert(secondSummary.CreatedConnectionCount == 0 && secondSummary.UpdatedConnectionCount == 2,
            "The repeated import reported incorrect connection create/update counts. / 重复导入报告的连接新增/更新数量不正确。");
        Assert(secondSnapshot.Groups.Count == firstSnapshot.Groups.Count &&
               secondSnapshot.Connections.Count == firstSnapshot.Connections.Count,
            "Importing the same document twice changed entity counts. / 同一文档导入两次后实体数量发生变化。");
        AssertNameIdsEqual(firstGroupIds, CollectGroupIds(secondSnapshot), "group / 分组");
        AssertNameIdsEqual(firstConnectionIds, CollectConnectionIds(secondSnapshot), "connection / 连接");
        ConnectionProfile repeatedPrimary = FindConnection(secondSnapshot, "primary");
        ConnectionProfile repeatedNewConnection = FindConnection(secondSnapshot, "new connection");
        Assert(repeatedPrimary.CreatedAtUtc == primaryCreatedAtUtc && repeatedPrimary.UpdatedAtUtc == secondImportUtc,
            "A repeated connection update lost creation time or did not refresh update time. / 重复连接更新丢失创建时间或未刷新更新时间。");
        Assert(repeatedNewConnection.CreatedAtUtc == newConnectionCreatedAtUtc && repeatedNewConnection.UpdatedAtUtc == secondImportUtc,
            "A connection created by the first import was not updated in place by the second import. / 首次导入创建的连接未在第二次导入时原位更新。");
        Assert(repository.SaveCount == 2 && importEventCount == 2,
            "A repeated import did not remain a single atomic commit. / 重复导入未保持为单次原子提交。");
    }

    /// <summary>
    /// Verifies duplicate normalized names in one source are rejected before any state is published.
    /// / 验证同一来源中重复的规范化名称会在发布任何状态前被拒绝。
    /// </summary>
    private static async Task TestDuplicateImportedNamesAreRejectedAtomicallyAsync()
    {
        await AssertImportRejectedAtomicallyAsync(
            new AppDataDocument(),
            new AppDataDocument
            {
                Groups =
                [
                    new ConnectionGroup { Name = " Duplicate Group " },
                    new ConnectionGroup { Name = "duplicate group" }
                ]
            },
            "duplicate imported group names / 重复导入分组名称");

        await AssertImportRejectedAtomicallyAsync(
            new AppDataDocument(),
            new AppDataDocument
            {
                Connections =
                [
                    CreateConnection(Guid.NewGuid(), " Duplicate Connection ", "first.example"),
                    CreateConnection(Guid.NewGuid(), "duplicate connection", "second.example")
                ]
            },
            "duplicate imported connection names / 重复导入连接名称");
    }

    /// <summary>
    /// Verifies an import cannot arbitrarily select one of several existing entities with the same normalized name.
    /// / 验证导入无法在多个规范化名称相同的现有实体中任意选择一个。
    /// </summary>
    private static async Task TestAmbiguousLocalNamesAreRejectedAtomicallyAsync()
    {
        await AssertImportRejectedAtomicallyAsync(
            new AppDataDocument
            {
                Groups =
                [
                    new ConnectionGroup { Name = " Local Group " },
                    new ConnectionGroup { Name = "local group" }
                ]
            },
            new AppDataDocument
            {
                Groups = [new ConnectionGroup { Name = "LOCAL GROUP" }]
            },
            "ambiguous local group name / 有歧义的本地分组名称");

        await AssertImportRejectedAtomicallyAsync(
            new AppDataDocument
            {
                Connections =
                [
                    CreateConnection(Guid.NewGuid(), " Local Connection ", "first.example"),
                    CreateConnection(Guid.NewGuid(), "local connection", "second.example")
                ]
            },
            new AppDataDocument
            {
                Connections = [CreateConnection(Guid.NewGuid(), "LOCAL CONNECTION", "imported.example")]
            },
            "ambiguous local connection name / 有歧义的本地连接名称");
    }

    /// <summary>
    /// Verifies a workspace already at the connection limit can atomically update every connection by name.
    /// / 验证已达到连接上限的工作区仍可按名称原子更新每条连接。
    /// </summary>
    private static async Task TestFullCapacityNameUpdatesAsync()
    {
        int count = WorkspaceLimits.MaximumConnectionCount;
        List<ConnectionProfile> existing = new(count);
        List<ConnectionProfile> imported = new(count);
        Guid firstExistingId = Guid.Empty;
        Guid lastExistingId = Guid.Empty;
        DateTime originalCreatedAtUtc = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int index = 0; index < count; index++)
        {
            Guid existingId = Guid.NewGuid();
            if (index == 0)
            {
                firstExistingId = existingId;
            }

            if (index == count - 1)
            {
                lastExistingId = existingId;
            }

            string suffix = index.ToString("D5", System.Globalization.CultureInfo.InvariantCulture);
            existing.Add(CreateConnection(
                existingId,
                $"Capacity-{suffix}",
                "old.example",
                createdAtUtc: originalCreatedAtUtc,
                updatedAtUtc: originalCreatedAtUtc));
            imported.Add(CreateConnection(
                Guid.NewGuid(),
                $"  capacity-{suffix}  ",
                $"updated-{suffix}.example",
                createdAtUtc: new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                updatedAtUtc: new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        }

        DateTime importUtc = new(2026, 9, 4, 8, 0, 0, DateTimeKind.Utc);
        RecordingWorkspaceRepository repository = new(new AppDataDocument { Connections = existing });
        WorkspaceService workspace = new(repository, new MutableTimeProvider(importUtc));
        await workspace.InitializeAsync();

        await workspace.MergeAsync(new AppDataDocument { Connections = imported })
            .WaitAsync(TimeSpan.FromSeconds(20));

        AppDataDocument saved = repository.LastSaved
                                ?? throw new InvalidOperationException("The full-capacity import did not save a candidate. / 满容量导入未保存候选工作区。");
        Assert(saved.Connections.Count == count && repository.SaveCount == 1,
            "Pure name updates at the connection limit were counted as new entities. / 达到连接上限时，纯名称更新被计为新实体。");
        ConnectionProfile first = saved.Connections[0];
        ConnectionProfile last = saved.Connections[^1];
        Assert(first.Id == firstExistingId && last.Id == lastExistingId,
            "Full-capacity updates replaced stable connection identifiers. / 满容量更新替换了稳定连接标识。");
        Assert(first.Name == "capacity-00000" && last.Name == $"capacity-{count - 1:D5}",
            "Full-capacity updates did not trim imported names. / 满容量更新未裁剪导入名称。");
        Assert(first.Host == "updated-00000.example" && last.Host == $"updated-{count - 1:D5}.example",
            "Full-capacity updates did not replace connection values. / 满容量更新未替换连接值。");
        Assert(first.CreatedAtUtc == originalCreatedAtUtc && last.CreatedAtUtc == originalCreatedAtUtc &&
               first.UpdatedAtUtc == importUtc && last.UpdatedAtUtc == importUtc,
            "Full-capacity updates did not preserve creation timestamps or stamp update timestamps. / 满容量更新未保留创建时间或写入更新时间。");
    }

    /// <summary>
    /// Runs one rejected import and verifies neither persistence nor in-memory publication occurred.
    /// / 运行一次应被拒绝的导入，并验证持久化与内存发布均未发生。
    /// </summary>
    private static async Task AssertImportRejectedAtomicallyAsync(
        AppDataDocument existing,
        AppDataDocument imported,
        string scenario)
    {
        RecordingWorkspaceRepository repository = new(existing);
        WorkspaceService workspace = new(repository, new MutableTimeProvider(
            new DateTime(2026, 9, 4, 9, 0, 0, DateTimeKind.Utc)));
        await workspace.InitializeAsync();
        string before = JsonSerializer.Serialize(workspace.GetSnapshot());
        int changeCount = 0;
        workspace.Changed += (_, _) => changeCount++;

        await AssertImportRejectedAsync(() => workspace.MergeAsync(imported), scenario);

        string after = JsonSerializer.Serialize(workspace.GetSnapshot());
        Assert(repository.SaveCount == 0, $"Rejected import attempted a save for {scenario}. / 被拒绝的导入仍尝试保存：{scenario}。");
        Assert(changeCount == 0, $"Rejected import raised a change event for {scenario}. / 被拒绝的导入仍发出变更事件：{scenario}。");
        Assert(string.Equals(before, after, StringComparison.Ordinal),
            $"Rejected import changed the in-memory workspace for {scenario}. / 被拒绝的导入更改了内存工作区：{scenario}。");
    }

    /// <summary>
    /// Verifies an import is rejected with a domain/data validation exception.
    /// / 验证导入以领域或数据验证异常被拒绝。
    /// </summary>
    private static async Task AssertImportRejectedAsync(Func<Task> action, string scenario)
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException($"Import unexpectedly succeeded for {scenario}. / 导入意外成功：{scenario}。");
    }

    /// <summary>Creates a valid connection used by import tests. / 创建导入测试使用的有效连接。</summary>
    private static ConnectionProfile CreateConnection(
        Guid id,
        string name,
        string host,
        Guid? groupId = null,
        string username = "",
        string password = "",
        DateTime? createdAtUtc = null,
        DateTime? updatedAtUtc = null)
    {
        return new ConnectionProfile
        {
            Id = id,
            Name = name,
            GroupId = groupId,
            Type = ConnectionType.Putty,
            Protocol = "ssh",
            Host = host,
            Port = 22,
            Username = username,
            Password = password,
            CreatedAtUtc = createdAtUtc ?? new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = updatedAtUtc ?? new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    /// <summary>Finds one group by its normalized name. / 按规范化名称查找一个分组。</summary>
    private static ConnectionGroup FindGroup(AppDataDocument document, string name)
    {
        return document.Groups.Single(group => NamesEqual(group.Name, name));
    }

    /// <summary>Finds one connection by its normalized name. / 按规范化名称查找一个连接。</summary>
    private static ConnectionProfile FindConnection(AppDataDocument document, string name)
    {
        return document.Connections.Single(connection => NamesEqual(connection.Name, name));
    }

    /// <summary>Compares names using the import natural-key rule. / 使用导入自然键规则比较名称。</summary>
    private static bool NamesEqual(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Collects normalized group names and their stable identifiers. / 收集规范化分组名称及其稳定标识。</summary>
    private static Dictionary<string, Guid> CollectGroupIds(AppDataDocument document)
    {
        return document.Groups.ToDictionary(group => group.Name.Trim(), group => group.Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Collects normalized connection names and their stable identifiers. / 收集规范化连接名称及其稳定标识。</summary>
    private static Dictionary<string, Guid> CollectConnectionIds(AppDataDocument document)
    {
        return document.Connections.ToDictionary(connection => connection.Name.Trim(), connection => connection.Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Asserts two normalized-name identifier maps are identical. / 断言两个规范化名称标识映射完全相同。</summary>
    private static void AssertNameIdsEqual(
        IReadOnlyDictionary<string, Guid> expected,
        IReadOnlyDictionary<string, Guid> actual,
        string entityName)
    {
        Assert(expected.Count == actual.Count && expected.All(pair => actual.TryGetValue(pair.Key, out Guid id) && id == pair.Value),
            $"Repeated import changed a {entityName} identifier. / 重复导入更改了 {entityName} 标识。");
    }

    /// <summary>Throws when a regression assertion is false. / 当回归断言为假时抛出异常。</summary>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>Provides a mutable deterministic UTC clock. / 提供可变且确定性的 UTC 时钟。</summary>
    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public MutableTimeProvider(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow
        {
            get => _utcNow.UtcDateTime;
            set => _utcNow = new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }

    /// <summary>Records workspace saves without adding validation outside the service under test. / 记录工作区保存，不在被测服务之外增加验证。</summary>
    private sealed class RecordingWorkspaceRepository : IWorkspaceRepository
    {
        private readonly AppDataDocument _loadedDocument;

        public RecordingWorkspaceRepository(AppDataDocument loadedDocument)
        {
            _loadedDocument = loadedDocument ?? throw new ArgumentNullException(nameof(loadedDocument));
        }

        public int SaveCount { get; private set; }

        public AppDataDocument? LastSaved { get; private set; }

        public Task<WorkspaceLoadResult> LoadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new WorkspaceLoadResult(_loadedDocument));
        }

        public Task SaveAsync(AppDataDocument document, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCount++;
            LastSaved = document;
            return Task.CompletedTask;
        }
    }
}
