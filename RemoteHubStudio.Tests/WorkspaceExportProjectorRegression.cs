using RemoteHubStudio.Domain;
using RemoteHubStudio.Infrastructure.ImportExport;

namespace RemoteHubStudio.Tests;

/// <summary>
/// Covers ordered, self-contained, and detached current-data export projection. / 覆盖有序、自包含且彻底分离的当前数据导出投影。
/// </summary>
internal static class WorkspaceExportProjectorRegression
{
    /// <summary>
    /// Runs current-data selection, dependency-closure, and deep-copy checks. / 运行当前数据选择、依赖闭包与深拷贝检查。
    /// </summary>
    public static void Run()
    {
        ConnectionGroup root = new() { Name = "Root" };
        ConnectionGroup parent = new() { Name = "Parent", ParentId = root.Id };
        ConnectionGroup selectedGroup = new() { Name = "Selected", ParentId = parent.Id };
        ConnectionGroup unrelatedGroup = new() { Name = "Unrelated" };
        ConnectionProfile first = CreateConnection("First", "first.example", root.Id, "first-user", "first-secret");
        ConnectionProfile second = CreateConnection("Second", "second.example", selectedGroup.Id, "selected-user", "selected-secret");
        ConnectionProfile unrelated = CreateConnection("Unrelated", "unrelated.example", unrelatedGroup.Id, "unrelated-user", "unrelated-secret");
        AppDataDocument source = new()
        {
            Settings = new AppSettings
            {
                ToolPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["putty"] = @"C:\Tools\putty.exe"
                }
            },
            Groups = [root, parent, selectedGroup, unrelatedGroup],
            Connections = [first, second, unrelated]
        };

        AppDataDocument projection = WorkspaceExportProjector.Create(
            source,
            [second.Id, second.Id, Guid.NewGuid(), first.Id]);

        Assert(
            projection.Connections.Select(connection => connection.Id).SequenceEqual([second.Id, first.Id]),
            "Projected connections did not preserve requested distinct existing-ID order. / 投影连接未保留请求中存在且去重后的标识顺序。");
        Assert(
            projection.Groups.Select(group => group.Id).ToHashSet().SetEquals([root.Id, parent.Id, selectedGroup.Id]),
            "Projection did not contain exactly the direct groups and complete ancestor chain. / 投影未准确包含直接分类及完整祖先链。");
        AssertDeepCopy(source, projection, root.Id, second.Id);
    }

    /// <summary>
    /// Creates one valid connection with mutable nested option state. / 创建一条包含可变嵌套选项状态的有效连接。
    /// </summary>
    /// <param name="name">Connection name. / 连接名称。</param>
    /// <param name="host">Connection host. / 连接主机。</param>
    /// <param name="groupId">Referenced group identifier. / 引用的分类标识。</param>
    /// <param name="username">Inline username. / 内联用户名。</param>
    /// <param name="password">Inline password. / 内联密码。</param>
    /// <returns>Created connection. / 已创建的连接。</returns>
    private static ConnectionProfile CreateConnection(
        string name,
        string host,
        Guid groupId,
        string username,
        string password)
    {
        return new ConnectionProfile
        {
            Name = name,
            Type = ConnectionType.Putty,
            Protocol = "ssh",
            Host = host,
            Port = 22,
            GroupId = groupId,
            Username = username,
            Password = password,
            Rdp = new RdpOptions { DesktopWidth = 1280 },
            Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["terminalType"] = "xterm"
            }
        };
    }

    /// <summary>
    /// Verifies every projected mutable object is detached and mutations cannot reach the source.
    /// / 验证每个投影可变对象均已分离，且修改无法影响源文档。
    /// </summary>
    /// <param name="source">Source workspace. / 源工作区。</param>
    /// <param name="projection">Projected workspace. / 投影工作区。</param>
    /// <param name="groupId">Group identifier to inspect. / 要检查的分类标识。</param>
    /// <param name="connectionId">Connection identifier to inspect. / 要检查的连接标识。</param>
    private static void AssertDeepCopy(
        AppDataDocument source,
        AppDataDocument projection,
        Guid groupId,
        Guid connectionId)
    {
        ConnectionGroup sourceGroup = source.Groups.Single(group => group.Id == groupId);
        ConnectionGroup projectedGroup = projection.Groups.Single(group => group.Id == groupId);
        ConnectionProfile sourceConnection = source.Connections.Single(connection => connection.Id == connectionId);
        ConnectionProfile projectedConnection = projection.Connections.Single(connection => connection.Id == connectionId);

        Assert(!ReferenceEquals(source.Settings, projection.Settings) &&
               !ReferenceEquals(source.Settings.ToolPaths, projection.Settings.ToolPaths) &&
               !ReferenceEquals(sourceGroup, projectedGroup) &&
               !ReferenceEquals(sourceConnection, projectedConnection) &&
               !ReferenceEquals(sourceConnection.Rdp, projectedConnection.Rdp) &&
               !ReferenceEquals(sourceConnection.Options, projectedConnection.Options),
            "Projection retained shared mutable objects. / 投影保留了共享的可变对象。");

        projection.Settings.ToolPaths["putty"] = "changed";
        projectedGroup.Name = "Changed";
        projectedConnection.Username = "changed-user";
        projectedConnection.Password = "changed-secret";
        projectedConnection.Rdp.DesktopWidth = 640;
        projectedConnection.Options["terminalType"] = "changed";
        Assert(source.Settings.ToolPaths["putty"] == @"C:\Tools\putty.exe" &&
               sourceGroup.Name == "Root" &&
               sourceConnection.Username == "selected-user" &&
               sourceConnection.Password == "selected-secret" &&
               sourceConnection.Rdp.DesktopWidth == 1280 &&
               sourceConnection.Options["terminalType"] == "xterm",
            "Mutating the projection changed the source workspace. / 修改投影时改变了源工作区。");
    }

    /// <summary>
    /// Throws when an export-projection regression assertion fails. / 导出投影回归断言失败时抛出异常。
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
