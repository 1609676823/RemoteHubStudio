using RemoteHubStudio.Application;
using RemoteHubStudio.Domain;

namespace RemoteHubStudio.Infrastructure.ImportExport;

/// <summary>
/// Projects a complete workspace into a detached, self-contained connection subset for export.
/// / 将完整工作区投影为可供导出的、独立且自包含的连接子集。
/// </summary>
public static class WorkspaceExportProjector
{
    /// <summary>
    /// Creates a deep-copied export document containing the requested connections and their group ancestry.
    /// / 创建包含指定连接及其分类祖先的深拷贝导出文档。
    /// </summary>
    /// <param name="source">Complete source workspace. / 完整的源工作区。</param>
    /// <param name="connectionIds">Connection identifiers in the desired export order. / 按期望导出顺序排列的连接标识。</param>
    /// <returns>
    /// A detached document whose connections follow the requested order, without duplicate or missing identifiers.
    /// / 一份独立文档；其连接按请求顺序排列，并忽略重复或不存在的标识。
    /// </returns>
    /// <exception cref="InvalidDataException">Thrown when the source workspace is incomplete or contains invalid identifiers or references. / 源工作区不完整或包含无效标识、引用时抛出。</exception>
    public static AppDataDocument Create(AppDataDocument source, IEnumerable<Guid> connectionIds)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(connectionIds);

        ValidateRequiredCollections(source);
        WorkspaceLimits.ValidateDocument(source);
        WorkspaceContentLimits.ValidateDocument(source);
        GroupGraphValidator.Validate(source.Groups);

        Dictionary<Guid, ConnectionGroup> groupsById = BuildGroupIndex(source.Groups);
        Dictionary<Guid, ConnectionProfile> connectionsById = BuildConnectionIndex(
            source.Connections,
            groupsById);

        HashSet<Guid> seenConnectionIds = [];
        HashSet<Guid> requiredGroupIds = [];
        List<ConnectionProfile> projectedConnections = [];

        foreach (Guid connectionId in connectionIds)
        {
            if (!seenConnectionIds.Add(connectionId) ||
                !connectionsById.TryGetValue(connectionId, out ConnectionProfile? connection))
            {
                continue;
            }

            projectedConnections.Add(CloneConnection(connection));
            if (connection.GroupId is Guid groupId)
            {
                AddGroupAncestry(groupId, groupsById, requiredGroupIds);
            }
        }

        AppDataDocument projection = new()
        {
            SchemaVersion = source.SchemaVersion,
            Settings = CloneSettings(source.Settings),
            Groups = source.Groups
                .Where(group => requiredGroupIds.Contains(group.Id))
                .Select(CloneGroup)
                .ToList(),
            Connections = projectedConnections
        };

        ValidateRequiredCollections(projection);
        WorkspaceLimits.ValidateDocument(projection);
        WorkspaceContentLimits.ValidateDocument(projection);
        GroupGraphValidator.Validate(projection.Groups);
        BuildConnectionIndex(
            projection.Connections,
            BuildGroupIndex(projection.Groups));
        return projection;
    }

    /// <summary>
    /// Verifies that every required workspace collection is present. / 验证所有必需的工作区集合均已存在。
    /// </summary>
    /// <param name="document">Workspace document to inspect. / 要检查的工作区文档。</param>
    private static void ValidateRequiredCollections(AppDataDocument document)
    {
        if (document.Settings is null ||
            document.Groups is null ||
            document.Connections is null)
        {
            throw new InvalidDataException(
                "The workspace is missing required data collections. / 工作区缺少必需的数据集合。");
        }
    }

    /// <summary>
    /// Builds a group identifier index after the complete graph has been validated. / 在完整分类图通过验证后构建分类标识索引。
    /// </summary>
    /// <param name="groups">Validated groups. / 已验证的分类。</param>
    /// <returns>Group lookup keyed by stable identifier. / 按稳定标识索引的分类查找表。</returns>
    private static Dictionary<Guid, ConnectionGroup> BuildGroupIndex(IReadOnlyList<ConnectionGroup> groups)
    {
        return groups.ToDictionary(group => group.Id);
    }

    /// <summary>
    /// Builds and validates the connection identifier index and every exported dependency reference.
    /// / 构建并验证连接标识索引以及每个导出依赖引用。
    /// </summary>
    /// <param name="connections">Complete connection collection. / 完整的连接集合。</param>
    /// <param name="groupsById">Validated group lookup. / 已验证的分类查找表。</param>
    /// <returns>Connection lookup keyed by stable identifier. / 按稳定标识索引的连接查找表。</returns>
    private static Dictionary<Guid, ConnectionProfile> BuildConnectionIndex(
        IReadOnlyList<ConnectionProfile> connections,
        IReadOnlyDictionary<Guid, ConnectionGroup> groupsById)
    {
        Dictionary<Guid, ConnectionProfile> connectionsById = new(connections.Count);
        for (int index = 0; index < connections.Count; index++)
        {
            ConnectionProfile? connection = connections[index];
            if (connection is null)
            {
                throw new InvalidDataException($"Connection entry {index} is null. / 第 {index} 个连接条目为空。");
            }

            if (connection.Id == Guid.Empty)
            {
                throw new InvalidDataException("A connection identifier is empty. / 存在空的连接标识。");
            }

            if (!connectionsById.TryAdd(connection.Id, connection))
            {
                throw new InvalidDataException($"Duplicate connection identifier '{connection.Id}'. / 连接标识“{connection.Id}”重复。");
            }

            if (connection.GroupId is Guid groupId && !groupsById.ContainsKey(groupId))
            {
                throw new InvalidDataException($"Connection '{connection.Id}' references missing group '{groupId}'. / 连接“{connection.Id}”引用了不存在的分类“{groupId}”。");
            }

            if (connection.Rdp is null || connection.Options is null)
            {
                throw new InvalidDataException($"Connection '{connection.Id}' is incomplete. / 连接“{connection.Id}”的数据不完整。");
            }
        }

        return connectionsById;
    }

    /// <summary>
    /// Adds one referenced group and its complete ancestor chain to the required identifier set.
    /// / 将一个被引用的分类及其完整祖先链加入必需标识集合。
    /// </summary>
    /// <param name="groupId">Directly referenced group identifier. / 直接引用的分类标识。</param>
    /// <param name="groupsById">Validated complete group lookup. / 已验证的完整分类查找表。</param>
    /// <param name="requiredGroupIds">Required group identifiers accumulated so far. / 已累积的必需分类标识。</param>
    private static void AddGroupAncestry(
        Guid groupId,
        IReadOnlyDictionary<Guid, ConnectionGroup> groupsById,
        ISet<Guid> requiredGroupIds)
    {
        Guid? currentId = groupId;
        while (currentId is Guid id && requiredGroupIds.Add(id))
        {
            currentId = groupsById[id].ParentId;
        }
    }

    /// <summary>
    /// Creates a deep copy of application settings. / 创建应用设置的深拷贝。
    /// </summary>
    /// <param name="source">Source settings. / 源设置。</param>
    /// <returns>Detached settings. / 独立设置。</returns>
    private static AppSettings CloneSettings(AppSettings source)
    {
        return new AppSettings
        {
            Theme = source.Theme,
            EncryptionEnabled = source.EncryptionEnabled,
            AllowPasswordInCommandLine = source.AllowPasswordInCommandLine,
            IncludeSecretsInExports = source.IncludeSecretsInExports,
            MinimizeToTray = source.MinimizeToTray,
            ConfirmBeforeDelete = source.ConfirmBeforeDelete,
            ExpiryWarningDays = source.ExpiryWarningDays,
            PingTimeoutMilliseconds = source.PingTimeoutMilliseconds,
            ConcurrentStatusChecks = source.ConcurrentStatusChecks,
            SidebarCollapsed = source.SidebarCollapsed,
            WindowBounds = source.WindowBounds,
            ToolPaths = source.ToolPaths is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(source.ToolPaths, StringComparer.OrdinalIgnoreCase)
        };
    }

    /// <summary>
    /// Creates a detached group copy. / 创建分类的独立副本。
    /// </summary>
    /// <param name="source">Source group. / 源分类。</param>
    /// <returns>Detached group. / 独立分类。</returns>
    private static ConnectionGroup CloneGroup(ConnectionGroup source)
    {
        return new ConnectionGroup
        {
            Id = source.Id,
            Name = source.Name ?? string.Empty,
            ParentId = source.ParentId,
            Color = source.Color ?? string.Empty,
            SortOrder = source.SortOrder
        };
    }

    /// <summary>
    /// Creates a detached connection copy including mutable option state. / 创建包含可变选项状态的连接独立副本。
    /// </summary>
    /// <param name="source">Source connection. / 源连接。</param>
    /// <returns>Detached connection. / 独立连接。</returns>
    private static ConnectionProfile CloneConnection(ConnectionProfile source)
    {
        return new ConnectionProfile
        {
            Id = source.Id,
            Name = source.Name ?? string.Empty,
            GroupId = source.GroupId,
            Type = source.Type,
            Protocol = source.Protocol ?? string.Empty,
            Host = source.Host ?? string.Empty,
            Port = source.Port,
            Username = source.Username ?? string.Empty,
            Password = source.Password ?? string.Empty,
            PrivateKeyPath = source.PrivateKeyPath ?? string.Empty,
            ExpiresOn = source.ExpiresOn,
            Notes = source.Notes ?? string.Empty,
            IsFavorite = source.IsFavorite,
            ExecutableOverride = source.ExecutableOverride ?? string.Empty,
            CustomArguments = source.CustomArguments ?? string.Empty,
            Rdp = CloneRdpOptions(source.Rdp),
            Options = new Dictionary<string, string>(source.Options, StringComparer.OrdinalIgnoreCase),
            CreatedAtUtc = source.CreatedAtUtc,
            UpdatedAtUtc = source.UpdatedAtUtc
        };
    }

    /// <summary>
    /// Creates a detached Remote Desktop option copy. / 创建远程桌面选项的独立副本。
    /// </summary>
    /// <param name="source">Source Remote Desktop options. / 源远程桌面选项。</param>
    /// <returns>Detached Remote Desktop options. / 独立远程桌面选项。</returns>
    private static RdpOptions CloneRdpOptions(RdpOptions source)
    {
        return new RdpOptions
        {
            FullScreen = source.FullScreen,
            UseAllMonitors = source.UseAllMonitors,
            DesktopWidth = source.DesktopWidth,
            DesktopHeight = source.DesktopHeight,
            ColorDepth = source.ColorDepth,
            DisplayConnectionBar = source.DisplayConnectionBar,
            EnableCompression = source.EnableCompression,
            KeyboardHookMode = source.KeyboardHookMode,
            RedirectClipboard = source.RedirectClipboard,
            RedirectDrives = source.RedirectDrives,
            RedirectPrinters = source.RedirectPrinters,
            RedirectSmartCards = source.RedirectSmartCards,
            RedirectComPorts = source.RedirectComPorts,
            RedirectPosDevices = source.RedirectPosDevices,
            RedirectCameras = source.RedirectCameras,
            RedirectMicrophone = source.RedirectMicrophone,
            AudioMode = source.AudioMode,
            AdministrativeSession = source.AdministrativeSession,
            PromptForCredentials = source.PromptForCredentials,
            DisableWallpaper = source.DisableWallpaper,
            AutoReconnect = source.AutoReconnect
        };
    }
}
