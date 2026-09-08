using RemoteHubStudio.Domain;

namespace RemoteHubStudio.Application;

/// <summary>
/// Coordinates thread-safe workspace queries, mutations, persistence, and change notifications. / 协调线程安全的工作区查询、变更、持久化与变更通知。
/// </summary>
public sealed class WorkspaceService
{
    private readonly IWorkspaceRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly object _stateLock = new();
    private AppDataDocument _document = new();
    private bool _isInitialized;
    private bool _recoveredFromBackup;

    /// <summary>
    /// Initializes a workspace service using the system clock. / 使用系统时钟初始化工作区服务。
    /// </summary>
    /// <param name="repository">Durable workspace repository. / 持久化工作区仓储。</param>
    public WorkspaceService(IWorkspaceRepository repository)
        : this(repository, TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a workspace service with an injectable clock for deterministic tests. / 使用可注入时钟初始化工作区服务，以支持确定性测试。
    /// </summary>
    /// <param name="repository">Durable workspace repository. / 持久化工作区仓储。</param>
    /// <param name="timeProvider">Clock used for connection timestamps. / 用于连接时间戳的时钟。</param>
    public WorkspaceService(IWorkspaceRepository repository, TimeProvider timeProvider)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>Occurs after a workspace revision has been loaded or durably committed. / 在工作区版本完成加载或持久提交后发生。</summary>
    public event EventHandler<WorkspaceChangedEventArgs>? Changed;

    /// <summary>Gets whether the repository has been loaded into memory. / 获取仓储是否已加载到内存。</summary>
    public bool IsInitialized
    {
        get
        {
            lock (_stateLock)
            {
                return _isInitialized;
            }
        }
    }

    /// <summary>Gets whether the most recent initialization recovered from backup. / 获取最近一次初始化是否从备份恢复。</summary>
    public bool RecoveredFromBackup
    {
        get
        {
            lock (_stateLock)
            {
                return _recoveredFromBackup;
            }
        }
    }

    /// <summary>
    /// Loads the durable workspace and replaces the current in-memory snapshot. / 加载持久化工作区并替换当前内存快照。
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>A task that represents initialization. / 表示初始化的任务。</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        WorkspaceLoadResult result;
        try
        {
            result = await _repository.LoadAsync(cancellationToken);
            WorkspaceLimits.ValidateDocument(result.Document);
            GroupGraphValidator.Validate(result.Document.Groups);
            AppDataDocument detachedDocument = CloneDocument(result.Document);
            lock (_stateLock)
            {
                _document = detachedDocument;
                _isInitialized = true;
                _recoveredFromBackup = result.RecoveredFromBackup;
            }
        }
        finally
        {
            _operationGate.Release();
        }

        RaiseChanged(new WorkspaceChangedEventArgs(
            WorkspaceChangeKind.Loaded,
            recoveredFromBackup: result.RecoveredFromBackup));
    }

    /// <summary>
    /// Returns a detached snapshot suitable for export or read-only presentation. / 返回适合导出或只读展示的独立快照。
    /// </summary>
    /// <returns>A deep copy of the current workspace. / 当前工作区的深层副本。</returns>
    public AppDataDocument GetSnapshot()
    {
        lock (_stateLock)
        {
            EnsureInitializedLocked();
            return CloneDocument(_document);
        }
    }

    /// <summary>
    /// Returns detached copies of all connections. / 返回全部连接的独立副本。
    /// </summary>
    /// <returns>Current connections in storage order. / 按存储顺序返回当前连接。</returns>
    public IReadOnlyList<ConnectionProfile> GetConnections()
    {
        lock (_stateLock)
        {
            EnsureInitializedLocked();
            List<ConnectionProfile> connections = new(_document.Connections.Count);
            for (int index = 0; index < _document.Connections.Count; index++)
            {
                connections.Add(CloneConnection(_document.Connections[index]));
            }

            return connections;
        }
    }

    /// <summary>
    /// Finds one connection and returns a detached copy. / 查找一条连接并返回独立副本。
    /// </summary>
    /// <param name="id">Connection identifier. / 连接标识。</param>
    /// <returns>The connection, or null when it does not exist. / 连接；不存在时返回 null。</returns>
    public ConnectionProfile? GetConnection(Guid id)
    {
        lock (_stateLock)
        {
            EnsureInitializedLocked();
            int index = FindConnectionIndex(_document.Connections, id);
            return index < 0 ? null : CloneConnection(_document.Connections[index]);
        }
    }

    /// <summary>
    /// Adds and durably saves a connection. / 添加并持久保存一条连接。
    /// </summary>
    /// <param name="connection">Connection to add. / 要添加的连接。</param>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>A detached copy of the committed connection. / 已提交连接的独立副本。</returns>
    public async Task<ConnectionProfile> AddConnectionAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await _operationGate.WaitAsync(cancellationToken);
        ConnectionProfile committed;
        try
        {
            AppDataDocument candidate = CreateCandidate();
            committed = CloneConnection(connection);
            if (committed.Id == Guid.Empty)
            {
                committed.Id = Guid.NewGuid();
            }

            if (FindConnectionIndex(candidate.Connections, committed.Id) >= 0)
            {
                throw new InvalidOperationException("A connection with the same identifier already exists. / 已存在相同标识的连接。");
            }

            ValidateConnectionReferences(candidate, committed);
            DateTime now = GetUtcNow();
            committed.CreatedAtUtc = now;
            committed.UpdatedAtUtc = now;
            candidate.Connections.Add(committed);
            await CommitCandidateAsync(candidate, cancellationToken);
        }
        finally
        {
            _operationGate.Release();
        }

        RaiseChanged(new WorkspaceChangedEventArgs(WorkspaceChangeKind.ConnectionAdded, committed.Id));
        return CloneConnection(committed);
    }

    /// <summary>
    /// Updates and durably saves an existing connection. / 更新并持久保存一条现有连接。
    /// </summary>
    /// <param name="connection">Replacement connection values. / 替换用的连接值。</param>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>True when the connection existed and was updated. / 连接存在且已更新时返回 true。</returns>
    public async Task<bool> UpdateConnectionAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await _operationGate.WaitAsync(cancellationToken);
        bool updated = false;
        try
        {
            AppDataDocument candidate = CreateCandidate();
            int index = FindConnectionIndex(candidate.Connections, connection.Id);
            if (index < 0)
            {
                return false;
            }

            ConnectionProfile replacement = CloneConnection(connection);
            ValidateConnectionReferences(candidate, replacement);
            replacement.CreatedAtUtc = candidate.Connections[index].CreatedAtUtc;
            replacement.UpdatedAtUtc = GetUtcNow();
            candidate.Connections[index] = replacement;
            await CommitCandidateAsync(candidate, cancellationToken);
            updated = true;
        }
        finally
        {
            _operationGate.Release();
        }

        if (updated)
        {
            RaiseChanged(new WorkspaceChangedEventArgs(WorkspaceChangeKind.ConnectionUpdated, connection.Id));
        }

        return updated;
    }

    /// <summary>
    /// Deletes a connection and durably saves the resulting workspace. / 删除一条连接并持久保存结果工作区。
    /// </summary>
    /// <param name="id">Connection identifier. / 连接标识。</param>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>True when a connection was deleted. / 删除了连接时返回 true。</returns>
    public async Task<bool> DeleteConnectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DeleteConnectionsAsync([id], cancellationToken) > 0;
    }

    /// <summary>
    /// Deletes multiple connections as one candidate, one durable save, and one change notification. / 使用一个候选文档、一次持久保存和一次变更通知删除多条连接。
    /// </summary>
    /// <param name="ids">Connection identifiers to delete; duplicates are ignored. / 要删除的连接标识；重复项会被忽略。</param>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>The number of connections atomically deleted. / 以原子方式删除的连接数量。</returns>
    public async Task<int> DeleteConnectionsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        HashSet<Guid> requestedIds = ids.Where(id => id != Guid.Empty).ToHashSet();
        if (requestedIds.Count == 0)
        {
            return 0;
        }

        await _operationGate.WaitAsync(cancellationToken);
        int deletedCount = 0;
        try
        {
            AppDataDocument candidate = CreateCandidate();
            deletedCount = candidate.Connections.RemoveAll(connection => requestedIds.Contains(connection.Id));
            if (deletedCount == 0)
            {
                return 0;
            }

            await CommitCandidateAsync(candidate, cancellationToken);
        }
        finally
        {
            _operationGate.Release();
        }

        Guid? singleEntityId = requestedIds.Count == 1 ? requestedIds.Single() : null;
        RaiseChanged(new WorkspaceChangedEventArgs(WorkspaceChangeKind.ConnectionDeleted, singleEntityId));

        return deletedCount;
    }

    /// <summary>
    /// Returns detached copies of all nested groups. / 返回全部嵌套分类的独立副本。
    /// </summary>
    /// <returns>Current groups in storage order. / 按存储顺序返回当前分类。</returns>
    public IReadOnlyList<ConnectionGroup> GetGroups()
    {
        lock (_stateLock)
        {
            EnsureInitializedLocked();
            List<ConnectionGroup> groups = new(_document.Groups.Count);
            for (int index = 0; index < _document.Groups.Count; index++)
            {
                groups.Add(CloneGroup(_document.Groups[index]));
            }

            return groups;
        }
    }

    /// <summary>
    /// Finds one group and returns a detached copy. / 查找一个分类并返回独立副本。
    /// </summary>
    /// <param name="id">Group identifier. / 分类标识。</param>
    /// <returns>The group, or null when it does not exist. / 分类；不存在时返回 null。</returns>
    public ConnectionGroup? GetGroup(Guid id)
    {
        lock (_stateLock)
        {
            EnsureInitializedLocked();
            int index = FindGroupIndex(_document.Groups, id);
            return index < 0 ? null : CloneGroup(_document.Groups[index]);
        }
    }

    /// <summary>
    /// Adds and durably saves a connection group. / 添加并持久保存一个连接分类。
    /// </summary>
    /// <param name="group">Group to add. / 要添加的分类。</param>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>A detached copy of the committed group. / 已提交分类的独立副本。</returns>
    public async Task<ConnectionGroup> AddGroupAsync(
        ConnectionGroup group,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        await _operationGate.WaitAsync(cancellationToken);
        ConnectionGroup committed;
        try
        {
            AppDataDocument candidate = CreateCandidate();
            committed = CloneGroup(group);
            if (committed.Id == Guid.Empty)
            {
                committed.Id = Guid.NewGuid();
            }

            if (FindGroupIndex(candidate.Groups, committed.Id) >= 0)
            {
                throw new InvalidOperationException("A group with the same identifier already exists. / 已存在相同标识的分类。");
            }

            candidate.Groups.Add(committed);
            await CommitCandidateAsync(candidate, cancellationToken);
        }
        finally
        {
            _operationGate.Release();
        }

        RaiseChanged(new WorkspaceChangedEventArgs(WorkspaceChangeKind.GroupAdded, committed.Id));
        return CloneGroup(committed);
    }

    /// <summary>
    /// Updates and durably saves an existing connection group. / 更新并持久保存一个现有连接分类。
    /// </summary>
    /// <param name="group">Replacement group values. / 替换用的分类值。</param>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>True when the group existed and was updated. / 分类存在且已更新时返回 true。</returns>
    public async Task<bool> UpdateGroupAsync(
        ConnectionGroup group,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        await _operationGate.WaitAsync(cancellationToken);
        bool updated = false;
        try
        {
            AppDataDocument candidate = CreateCandidate();
            int index = FindGroupIndex(candidate.Groups, group.Id);
            if (index < 0)
            {
                return false;
            }

            ConnectionGroup replacement = CloneGroup(group);
            candidate.Groups[index] = replacement;
            await CommitCandidateAsync(candidate, cancellationToken);
            updated = true;
        }
        finally
        {
            _operationGate.Release();
        }

        if (updated)
        {
            RaiseChanged(new WorkspaceChangedEventArgs(WorkspaceChangeKind.GroupUpdated, group.Id));
        }

        return updated;
    }

    /// <summary>
    /// Deletes a group, reparents its children and connections, and durably saves the workspace. / 删除分类、重新挂接其子分类与连接，并持久保存工作区。
    /// </summary>
    /// <param name="id">Group identifier. / 分类标识。</param>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>True when a group was deleted. / 删除了分类时返回 true。</returns>
    public async Task<bool> DeleteGroupAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        bool deleted = false;
        try
        {
            AppDataDocument candidate = CreateCandidate();
            int index = FindGroupIndex(candidate.Groups, id);
            if (index < 0)
            {
                return false;
            }

            Guid? parentId = candidate.Groups[index].ParentId;
            candidate.Groups.RemoveAt(index);
            for (int groupIndex = 0; groupIndex < candidate.Groups.Count; groupIndex++)
            {
                if (candidate.Groups[groupIndex].ParentId == id)
                {
                    candidate.Groups[groupIndex].ParentId = parentId;
                }
            }

            for (int connectionIndex = 0; connectionIndex < candidate.Connections.Count; connectionIndex++)
            {
                if (candidate.Connections[connectionIndex].GroupId == id)
                {
                    candidate.Connections[connectionIndex].GroupId = parentId;
                    candidate.Connections[connectionIndex].UpdatedAtUtc = GetUtcNow();
                }
            }

            await CommitCandidateAsync(candidate, cancellationToken);
            deleted = true;
        }
        finally
        {
            _operationGate.Release();
        }

        if (deleted)
        {
            RaiseChanged(new WorkspaceChangedEventArgs(WorkspaceChangeKind.GroupDeleted, id));
        }

        return deleted;
    }

    /// <summary>
    /// Returns a detached copy of application settings. / 返回应用设置的独立副本。
    /// </summary>
    /// <returns>Current application settings. / 当前应用设置。</returns>
    public AppSettings GetSettings()
    {
        lock (_stateLock)
        {
            EnsureInitializedLocked();
            return CloneSettings(_document.Settings);
        }
    }

    /// <summary>
    /// Updates settings and rewrites the workspace using the selected encryption mode. / 更新设置，并使用所选加密模式重写工作区。
    /// </summary>
    /// <param name="settings">Replacement settings. / 替换用的设置。</param>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>A task that represents the durable update. / 表示持久更新的任务。</returns>
    public async Task UpdateSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            AppDataDocument candidate = CreateCandidate();
            Rectangle currentWindowBounds = candidate.Settings.WindowBounds;
            candidate.Settings = CloneSettings(settings);
            candidate.Settings.WindowBounds = currentWindowBounds;
            await CommitCandidateAsync(candidate, cancellationToken);
        }
        finally
        {
            _operationGate.Release();
        }

        RaiseChanged(new WorkspaceChangedEventArgs(WorkspaceChangeKind.SettingsUpdated));
    }

    /// <summary>
    /// Atomically patches only the normal window bounds while preserving every concurrently committed setting. / 仅以原子方式修补正常窗口边界，同时保留每一项并发已提交设置。
    /// </summary>
    /// <param name="windowBounds">Latest normal window bounds. / 最新的正常窗口边界。</param>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>A task that represents the durable bounds patch. / 表示持久边界修补的任务。</returns>
    public async Task UpdateWindowBoundsAsync(
        Rectangle windowBounds,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            AppDataDocument candidate = CreateCandidate();
            candidate.Settings.WindowBounds = windowBounds;
            await CommitCandidateAsync(candidate, cancellationToken);
        }
        finally
        {
            _operationGate.Release();
        }

        RaiseChanged(new WorkspaceChangedEventArgs(WorkspaceChangeKind.SettingsUpdated));
    }

    /// <summary>
    /// Resets settings to defaults, including disabled encryption, and durably saves the workspace. / 将设置重置为默认值（包括关闭加密），并持久保存工作区。
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>A task that represents the durable reset. / 表示持久重置的任务。</returns>
    public async Task ResetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await UpdateSettingsAsync(new AppSettings(), cancellationToken);
    }

    /// <summary>
    /// Atomically imports groups and connections by their normalized names. / 按规范化名称原子导入分类与连接。
    /// </summary>
    /// <param name="imported">Imported workspace document; its machine-local settings are intentionally ignored. / 导入的工作区文档；其中的机器本地设置会被有意忽略。</param>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>Counts of connections created and updated by the single durable commit. / 单次持久提交所创建及更新的连接数量。</returns>
    public async Task<WorkspaceImportSummary> MergeAsync(
        AppDataDocument imported,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imported);
        ValidateImportedDocument(imported);
        AppDataDocument detachedImport = CloneDocument(imported);

        await _operationGate.WaitAsync(cancellationToken);
        WorkspaceImportSummary summary;
        try
        {
            AppDataDocument candidate = CreateCandidate();
            Dictionary<string, int> groupNames = BuildExistingNameIndex(
                candidate.Groups,
                static group => group.Name,
                "group / 分类");
            Dictionary<string, int> connectionNames = BuildExistingNameIndex(
                candidate.Connections,
                static connection => connection.Name,
                "connection / 连接");
            int addedGroupCount = ValidateIncomingNamesAndCountAdditions(
                detachedImport.Groups,
                static group => group.Name,
                groupNames,
                "group / 分类");
            int addedConnectionCount = ValidateIncomingNamesAndCountAdditions(
                detachedImport.Connections,
                static connection => connection.Name,
                connectionNames,
                "connection / 连接");
            WorkspaceLimits.ValidateCounts(
                (long)candidate.Groups.Count + addedGroupCount,
                (long)candidate.Connections.Count + addedConnectionCount);

            HashSet<Guid> existingGroupIds = CollectGroupIds(candidate.Groups);
            Dictionary<Guid, Guid> groupIdMap = MergeImportedGroups(
                candidate,
                detachedImport.Groups,
                existingGroupIds,
                groupNames);
            int updatedConnectionCount = MergeImportedConnections(
                candidate,
                detachedImport.Connections,
                groupIdMap,
                connectionNames,
                GetUtcNow());
            await CommitCandidateAsync(candidate, cancellationToken);
            summary = new WorkspaceImportSummary(addedConnectionCount, updatedConnectionCount);
        }
        finally
        {
            _operationGate.Release();
        }

        RaiseChanged(new WorkspaceChangedEventArgs(WorkspaceChangeKind.WorkspaceImported));
        return summary;
    }

    /// <summary>
    /// Creates a detached mutation candidate while holding the state lock. / 在持有状态锁时创建独立的变更候选文档。
    /// </summary>
    /// <returns>A deep copy of the current workspace. / 当前工作区的深层副本。</returns>
    private AppDataDocument CreateCandidate()
    {
        lock (_stateLock)
        {
            EnsureInitializedLocked();
            return CloneDocument(_document);
        }
    }

    /// <summary>
    /// Saves a candidate and publishes it as the current state only after persistence succeeds. / 保存候选文档，并仅在持久化成功后将其发布为当前状态。
    /// </summary>
    /// <param name="candidate">Complete candidate workspace. / 完整候选工作区。</param>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>A task that represents the commit. / 表示提交的任务。</returns>
    private async Task CommitCandidateAsync(AppDataDocument candidate, CancellationToken cancellationToken)
    {
        WorkspaceLimits.ValidateDocument(candidate);
        GroupGraphValidator.Validate(candidate.Groups);
        await _repository.SaveAsync(candidate, cancellationToken);
        lock (_stateLock)
        {
            _document = candidate;
            _recoveredFromBackup = false;
        }
    }

    /// <summary>
    /// Validates the imported document version and required collection shape before cloning. / 在复制前验证导入文档版本及必需集合结构。
    /// </summary>
    /// <param name="imported">Imported workspace document. / 导入的工作区文档。</param>
    private static void ValidateImportedDocument(AppDataDocument imported)
    {
        if (imported.SchemaVersion > AppDataDocument.CurrentSchemaVersion)
        {
            throw new NotSupportedException(
                $"Workspace data version {imported.SchemaVersion} is newer than supported version {AppDataDocument.CurrentSchemaVersion}. / 工作区数据版本 {imported.SchemaVersion} 高于当前支持的版本 {AppDataDocument.CurrentSchemaVersion}。");
        }

        if (imported.Settings is null ||
            imported.Groups is null ||
            imported.Connections is null)
        {
            throw new ArgumentException(
                "The imported workspace is missing required data collections. / 导入的工作区缺少必需的数据集合。",
                nameof(imported));
        }

        WorkspaceLimits.ValidateDocument(imported);
        GroupGraphValidator.Validate(imported.Groups);

        for (int index = 0; index < imported.Connections.Count; index++)
        {
            if (imported.Connections[index] is null || imported.Connections[index].Rdp is null)
            {
                throw new ArgumentException(
                    "The imported workspace contains an incomplete connection. / 导入的工作区包含不完整的连接。",
                    nameof(imported));
            }
        }
    }

    /// <summary>
    /// Builds a case-insensitive existing-name index while retaining an ambiguity marker for legacy duplicates. / 构建不区分大小写的现有名称索引，并为旧版重复项保留歧义标记。
    /// </summary>
    /// <typeparam name="T">Entity type. / 实体类型。</typeparam>
    /// <param name="entities">Existing entities. / 现有实体。</param>
    /// <param name="nameSelector">Entity name selector. / 实体名称选择器。</param>
    /// <param name="entityName">Bilingual entity name used in errors. / 错误中使用的双语实体名称。</param>
    /// <returns>Normalized names mapped to indices; -1 denotes more than one existing match. / 规范化名称到索引的映射；-1 表示存在多个匹配项。</returns>
    private static Dictionary<string, int> BuildExistingNameIndex<T>(
        IReadOnlyList<T> entities,
        Func<T, string> nameSelector,
        string entityName)
        where T : class
    {
        Dictionary<string, int> names = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < entities.Count; index++)
        {
            T entity = entities[index] ?? throw new InvalidDataException(
                $"The existing workspace contains a null {entityName}. / 现有工作区包含空的 {entityName}。");
            string name = (nameSelector(entity) ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                continue;
            }

            if (!names.TryAdd(name, index))
            {
                names[name] = -1;
            }
        }

        return names;
    }

    /// <summary>
    /// Validates imported natural keys and counts names that do not exist locally. / 验证导入自然键，并统计本地不存在的名称。
    /// </summary>
    /// <typeparam name="T">Entity type. / 实体类型。</typeparam>
    /// <param name="entities">Imported entities. / 导入实体。</param>
    /// <param name="nameSelector">Entity name selector. / 实体名称选择器。</param>
    /// <param name="existingNames">Existing normalized-name index. / 现有规范化名称索引。</param>
    /// <param name="entityName">Bilingual entity name used in errors. / 错误中使用的双语实体名称。</param>
    /// <returns>The number of entities that will be created. / 将创建的实体数量。</returns>
    private static int ValidateIncomingNamesAndCountAdditions<T>(
        IReadOnlyList<T> entities,
        Func<T, string> nameSelector,
        IReadOnlyDictionary<string, int> existingNames,
        string entityName)
        where T : class
    {
        HashSet<string> importedNames = new(StringComparer.OrdinalIgnoreCase);
        int additionCount = 0;
        for (int index = 0; index < entities.Count; index++)
        {
            T entity = entities[index] ?? throw new ArgumentException(
                $"The imported workspace contains a null {entityName}. / 导入的工作区包含空的 {entityName}。");
            string name = NormalizeImportName(nameSelector(entity), entityName);
            if (!importedNames.Add(name))
            {
                throw new ArgumentException(
                    $"The imported workspace contains duplicate {entityName} names after trimming and case-insensitive comparison. / 导入的工作区在裁剪并忽略大小写后包含重复的 {entityName} 名称。");
            }

            if (existingNames.TryGetValue(name, out int existingIndex))
            {
                if (existingIndex < 0)
                {
                    throw new InvalidOperationException(
                        $"More than one existing {entityName} has the name '{name}', so the import target is ambiguous. / 多个现有 {entityName} 使用名称“{name}”，无法确定导入更新目标。");
                }

                continue;
            }

            additionCount++;
        }

        return additionCount;
    }

    /// <summary>
    /// Normalizes one imported natural key used for case-insensitive matching. / 规范化一个用于不区分大小写匹配的导入自然键。
    /// </summary>
    /// <param name="name">Raw imported name. / 原始导入名称。</param>
    /// <param name="entityName">Bilingual entity name used in errors. / 错误中使用的双语实体名称。</param>
    /// <returns>The trimmed non-empty name. / 裁剪后的非空名称。</returns>
    private static string NormalizeImportName(string? name, string entityName)
    {
        string normalized = (name ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            throw new ArgumentException(
                $"The imported {entityName} name cannot be empty. / 导入的 {entityName} 名称不能为空。");
        }

        return normalized;
    }

    /// <summary>
    /// Upserts imported groups by name, preserving local identifiers and remapping parent references. / 按名称更新或新增导入分类，并保留本地标识及重新映射父引用。
    /// </summary>
    /// <param name="candidate">Candidate workspace receiving imported groups. / 接收导入分类的候选工作区。</param>
    /// <param name="importedGroups">Detached imported groups. / 独立的导入分类。</param>
    /// <param name="existingGroupIds">Group identifiers that existed before the merge. / 合并前已存在的分类标识。</param>
    /// <param name="existingNames">Existing group indices keyed by normalized name. / 按规范化名称索引的现有分类。</param>
    /// <returns>A source-to-committed group identifier map. / 源分类标识到已提交分类标识的映射。</returns>
    private static Dictionary<Guid, Guid> MergeImportedGroups(
        AppDataDocument candidate,
        IReadOnlyList<ConnectionGroup> importedGroups,
        HashSet<Guid> existingGroupIds,
        IReadOnlyDictionary<string, int> existingNames)
    {
        Dictionary<Guid, Guid> idMap = new();
        HashSet<Guid> occupiedIds = new(existingGroupIds);
        HashSet<Guid> sourceIds = [];
        List<(ConnectionGroup Source, ConnectionGroup Committed)> mergedGroups = new(importedGroups.Count);

        for (int index = 0; index < importedGroups.Count; index++)
        {
            ConnectionGroup importedGroup = importedGroups[index];
            Guid sourceId = importedGroup.Id;
            EnsureUniqueImportedIdentifier(sourceId, sourceIds, "group / 分类");

            string name = NormalizeImportName(importedGroup.Name, "group / 分类");
            bool updatesExisting = existingNames.TryGetValue(name, out int existingIndex);
            Guid committedId = updatesExisting
                ? candidate.Groups[existingIndex].Id
                : sourceId == Guid.Empty || occupiedIds.Contains(sourceId)
                    ? CreateUniqueIdentifier(occupiedIds)
                    : sourceId;
            occupiedIds.Add(committedId);
            if (sourceId != Guid.Empty)
            {
                idMap.Add(sourceId, committedId);
            }

            ConnectionGroup committedGroup = CloneGroup(importedGroup);
            committedGroup.Id = committedId;
            committedGroup.Name = name;
            committedGroup.ParentId = null;
            if (updatesExisting)
            {
                candidate.Groups[existingIndex] = committedGroup;
            }
            else
            {
                candidate.Groups.Add(committedGroup);
            }

            mergedGroups.Add((importedGroup, committedGroup));
        }

        for (int index = 0; index < mergedGroups.Count; index++)
        {
            (ConnectionGroup source, ConnectionGroup committed) = mergedGroups[index];
            committed.ParentId = RemapImportedReference(
                source.ParentId,
                idMap,
                "parent group / 父分类");
        }

        return idMap;
    }

    /// <summary>
    /// Upserts imported connections by name while remapping internal references. / 按名称更新或新增导入连接，同时重新映射内部引用。
    /// </summary>
    /// <param name="candidate">Candidate workspace receiving imported connections. / 接收导入连接的候选工作区。</param>
    /// <param name="importedConnections">Detached imported connections. / 独立的导入连接。</param>
    /// <param name="groupIdMap">Imported group identifier map. / 导入分类标识映射。</param>
    /// <param name="existingNames">Existing connection indices keyed by normalized name. / 按规范化名称索引的现有连接。</param>
    /// <param name="updatedAtUtc">Timestamp assigned to imported creations and updates. / 分配给导入创建及更新的时间戳。</param>
    /// <returns>The number of existing connections updated. / 更新的现有连接数量。</returns>
    private static int MergeImportedConnections(
        AppDataDocument candidate,
        IReadOnlyList<ConnectionProfile> importedConnections,
        IReadOnlyDictionary<Guid, Guid> groupIdMap,
        IReadOnlyDictionary<string, int> existingNames,
        DateTime updatedAtUtc)
    {
        HashSet<Guid> occupiedIds = CollectConnectionIds(candidate.Connections);
        HashSet<Guid> validGroupIds = CollectGroupIds(candidate.Groups);
        HashSet<Guid> sourceIds = [];
        int updatedCount = 0;

        for (int index = 0; index < importedConnections.Count; index++)
        {
            ConnectionProfile importedConnection = importedConnections[index];
            Guid sourceId = importedConnection.Id;
            EnsureUniqueImportedIdentifier(sourceId, sourceIds, "connection / 连接");

            string name = NormalizeImportName(importedConnection.Name, "connection / 连接");
            bool updatesExisting = existingNames.TryGetValue(name, out int existingIndex);
            Guid committedId = updatesExisting
                ? candidate.Connections[existingIndex].Id
                : sourceId == Guid.Empty || occupiedIds.Contains(sourceId)
                    ? CreateUniqueIdentifier(occupiedIds)
                    : sourceId;
            occupiedIds.Add(committedId);

            ConnectionProfile committedConnection = CloneConnection(importedConnection);
            committedConnection.Id = committedId;
            committedConnection.Name = name;
            committedConnection.GroupId = RemapImportedReference(
                importedConnection.GroupId,
                groupIdMap,
                "connection group / 连接分类");
            ValidateConnectionReferences(validGroupIds, committedConnection);
            committedConnection.UpdatedAtUtc = updatedAtUtc;
            if (updatesExisting)
            {
                committedConnection.CreatedAtUtc = candidate.Connections[existingIndex].CreatedAtUtc;
                candidate.Connections[existingIndex] = committedConnection;
                updatedCount++;
            }
            else
            {
                committedConnection.CreatedAtUtc = updatedAtUtc;
                candidate.Connections.Add(committedConnection);
            }
        }

        return updatedCount;
    }

    /// <summary>
    /// Rejects duplicate non-empty identifiers inside one imported entity collection. / 拒绝同一导入实体集合中的重复非空标识。
    /// </summary>
    /// <param name="sourceId">Imported source identifier. / 导入的源标识。</param>
    /// <param name="sourceIds">Previously observed source identifiers. / 先前已观察到的源标识。</param>
    /// <param name="entityName">Bilingual entity name used in errors. / 错误中使用的双语实体名称。</param>
    private static void EnsureUniqueImportedIdentifier(
        Guid sourceId,
        ISet<Guid> sourceIds,
        string entityName)
    {
        if (sourceId != Guid.Empty && !sourceIds.Add(sourceId))
        {
            throw new ArgumentException(
                $"The imported workspace contains duplicate {entityName} identifiers. / 导入的工作区包含重复的 {entityName} 标识。");
        }
    }

    /// <summary>
    /// Generates an identifier that is not present in the occupied set. / 生成一个不在已占用集合中的标识。
    /// </summary>
    /// <param name="occupiedIds">Identifiers that cannot be reused. / 不可复用的标识。</param>
    /// <returns>A new unique identifier. / 新的唯一标识。</returns>
    private static Guid CreateUniqueIdentifier(IReadOnlySet<Guid> occupiedIds)
    {
        Guid identifier;
        do
        {
            identifier = Guid.NewGuid();
        }
        while (occupiedIds.Contains(identifier));

        return identifier;
    }

    /// <summary>
    /// Remaps a self-contained imported reference and rejects links into the existing local workspace. / 重新映射自包含的导入引用，并拒绝指向现有本地工作区的链接。
    /// </summary>
    /// <param name="sourceReference">Imported reference. / 导入的引用。</param>
    /// <param name="idMap">Imported source-to-committed identifier map. / 导入源标识到已提交标识的映射。</param>
    /// <param name="referenceName">Bilingual reference kind used in validation errors. / 验证错误中使用的双语引用类型。</param>
    /// <returns>The valid committed reference, or null when the source reference is empty. / 有效的已提交引用；源引用为空时返回 null。</returns>
    private static Guid? RemapImportedReference(
        Guid? sourceReference,
        IReadOnlyDictionary<Guid, Guid> idMap,
        string referenceName)
    {
        if (sourceReference is not Guid sourceId || sourceId == Guid.Empty)
        {
            return null;
        }

        if (idMap.TryGetValue(sourceId, out Guid mappedId))
        {
            return mappedId;
        }

        throw new ArgumentException(
            $"The imported {referenceName} reference '{sourceId}' is not included in the import document. / 导入的 {referenceName} 引用“{sourceId}”未包含在导入文档中。");
    }

    /// <summary>
    /// Collects all existing group identifiers. / 收集全部现有分类标识。
    /// </summary>
    /// <param name="groups">Groups to inspect. / 要检查的分类。</param>
    /// <returns>A set of occupied group identifiers. / 已占用分类标识集合。</returns>
    private static HashSet<Guid> CollectGroupIds(IReadOnlyList<ConnectionGroup> groups)
    {
        HashSet<Guid> identifiers = [];
        for (int index = 0; index < groups.Count; index++)
        {
            identifiers.Add(groups[index].Id);
        }

        return identifiers;
    }

    /// <summary>
    /// Collects all existing connection identifiers. / 收集全部现有连接标识。
    /// </summary>
    /// <param name="connections">Connections to inspect. / 要检查的连接。</param>
    /// <returns>A set of occupied connection identifiers. / 已占用连接标识集合。</returns>
    private static HashSet<Guid> CollectConnectionIds(IReadOnlyList<ConnectionProfile> connections)
    {
        HashSet<Guid> identifiers = [];
        for (int index = 0; index < connections.Count; index++)
        {
            identifiers.Add(connections[index].Id);
        }

        return identifiers;
    }

    /// <summary>
    /// Validates the group reference before a connection is committed. / 在提交连接前验证分类引用。
    /// </summary>
    /// <param name="document">Candidate workspace. / 候选工作区。</param>
    /// <param name="connection">Connection being validated. / 正在验证的连接。</param>
    private static void ValidateConnectionReferences(AppDataDocument document, ConnectionProfile connection)
    {
        ValidateConnectionValues(connection);

        if (connection.GroupId is Guid groupId && FindGroupIndex(document.Groups, groupId) < 0)
        {
            throw new ArgumentException("The selected connection group does not exist. / 所选连接分类不存在。", nameof(connection));
        }

    }

    /// <summary>
    /// Validates one connection against a prebuilt constant-time group identifier set. / 使用预建的常数时间分类标识集合验证一条连接。
    /// </summary>
    /// <param name="validGroupIds">Valid committed group identifiers. / 有效的已提交分类标识。</param>
    /// <param name="connection">Connection being validated. / 正在验证的连接。</param>
    private static void ValidateConnectionReferences(
        IReadOnlySet<Guid> validGroupIds,
        ConnectionProfile connection)
    {
        ValidateConnectionValues(connection);
        if (connection.GroupId is Guid groupId && !validGroupIds.Contains(groupId))
        {
            throw new ArgumentException("The selected connection group does not exist. / 所选连接分类不存在。", nameof(connection));
        }

    }

    /// <summary>
    /// Validates intrinsic connection values. / 验证连接固有值。
    /// </summary>
    /// <param name="connection">Connection being validated. / 正在验证的连接。</param>
    private static void ValidateConnectionValues(ConnectionProfile connection)
    {
        if (connection.Id == Guid.Empty)
        {
            throw new ArgumentException("The connection identifier cannot be empty. / 连接标识不能为空。", nameof(connection));
        }

        if (string.IsNullOrWhiteSpace(connection.Name))
        {
            throw new ArgumentException("The connection name cannot be empty. / 连接名称不能为空。", nameof(connection));
        }

        if (string.IsNullOrWhiteSpace(connection.Host))
        {
            throw new ArgumentException("The host or device identifier cannot be empty. / 主机或设备标识不能为空。", nameof(connection));
        }

        if (!Enum.IsDefined(connection.Type))
        {
            throw new ArgumentException("The connection type is invalid. / 连接类型无效。", nameof(connection));
        }

        if (connection.Port is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(connection), "The connection port must be between 0 and 65535. / 连接端口必须介于 0 与 65535 之间。");
        }

        if (connection.Type.GetDefaultPort() > 0 && connection.Port == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(connection), "This connection type requires a port between 1 and 65535. / 此连接类型需要 1 到 65535 之间的端口。");
        }
    }

    /// <summary>
    /// Finds a connection index without exposing mutable collection entries. / 在不暴露可变集合项的情况下查找连接索引。
    /// </summary>
    /// <param name="connections">Connection collection. / 连接集合。</param>
    /// <param name="id">Connection identifier. / 连接标识。</param>
    /// <returns>The zero-based index, or -1 when absent. / 从零开始的索引；不存在时返回 -1。</returns>
    private static int FindConnectionIndex(IReadOnlyList<ConnectionProfile> connections, Guid id)
    {
        for (int index = 0; index < connections.Count; index++)
        {
            if (connections[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Finds a group index without exposing mutable collection entries. / 在不暴露可变集合项的情况下查找分类索引。
    /// </summary>
    /// <param name="groups">Group collection. / 分类集合。</param>
    /// <param name="id">Group identifier. / 分类标识。</param>
    /// <returns>The zero-based index, or -1 when absent. / 从零开始的索引；不存在时返回 -1。</returns>
    private static int FindGroupIndex(IReadOnlyList<ConnectionGroup> groups, Guid id)
    {
        for (int index = 0; index < groups.Count; index++)
        {
            if (groups[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Ensures callers loaded the repository before querying or mutating it. / 确保调用方在查询或变更前已加载仓储。
    /// </summary>
    private void EnsureInitializedLocked()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("WorkspaceService.InitializeAsync must be called first. / 必须先调用 WorkspaceService.InitializeAsync。");
        }
    }

    /// <summary>
    /// Gets a UTC timestamp from the injected clock. / 从注入时钟获取 UTC 时间戳。
    /// </summary>
    /// <returns>The current UTC date and time. / 当前 UTC 日期与时间。</returns>
    private DateTime GetUtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Raises the public change event after locks and persistence gates have been released. / 在释放锁和持久化门后触发公开变更事件。
    /// </summary>
    /// <param name="eventArgs">Committed change details. / 已提交变更的详细信息。</param>
    private void RaiseChanged(WorkspaceChangedEventArgs eventArgs)
    {
        Changed?.Invoke(this, eventArgs);
    }

    /// <summary>
    /// Creates a deep copy of a workspace document. / 创建工作区文档的深层副本。
    /// </summary>
    /// <param name="source">Source workspace. / 源工作区。</param>
    /// <returns>Detached workspace copy. / 独立工作区副本。</returns>
    private static AppDataDocument CloneDocument(AppDataDocument source)
    {
        AppDataDocument clone = new()
        {
            SchemaVersion = source.SchemaVersion,
            Settings = CloneSettings(source.Settings),
            Groups = new List<ConnectionGroup>(source.Groups.Count),
            Connections = new List<ConnectionProfile>(source.Connections.Count)
        };

        for (int index = 0; index < source.Groups.Count; index++)
        {
            clone.Groups.Add(CloneGroup(source.Groups[index]));
        }

        for (int index = 0; index < source.Connections.Count; index++)
        {
            clone.Connections.Add(CloneConnection(source.Connections[index]));
        }

        return clone;
    }

    /// <summary>
    /// Creates a deep copy of application settings. / 创建应用设置的深层副本。
    /// </summary>
    /// <param name="source">Source settings. / 源设置。</param>
    /// <returns>Detached settings copy. / 独立设置副本。</returns>
    private static AppSettings CloneSettings(AppSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
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
    /// Creates a detached copy of one connection group. / 创建一个连接分类的独立副本。
    /// </summary>
    /// <param name="source">Source group. / 源分类。</param>
    /// <returns>Detached group copy. / 独立分类副本。</returns>
    private static ConnectionGroup CloneGroup(ConnectionGroup source)
    {
        ArgumentNullException.ThrowIfNull(source);
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
    /// Creates a deep copy of one connection profile. / 创建一条连接配置的深层副本。
    /// </summary>
    /// <param name="source">Source connection. / 源连接。</param>
    /// <returns>Detached connection copy. / 独立连接副本。</returns>
    private static ConnectionProfile CloneConnection(ConnectionProfile source)
    {
        ArgumentNullException.ThrowIfNull(source);
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
            Options = source.Options is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(source.Options, StringComparer.OrdinalIgnoreCase),
            CreatedAtUtc = source.CreatedAtUtc,
            UpdatedAtUtc = source.UpdatedAtUtc
        };
    }

    /// <summary>
    /// Creates a detached copy of Remote Desktop options. / 创建远程桌面选项的独立副本。
    /// </summary>
    /// <param name="source">Source Remote Desktop options. / 源远程桌面选项。</param>
    /// <returns>Detached Remote Desktop options. / 独立远程桌面选项。</returns>
    private static RdpOptions CloneRdpOptions(RdpOptions source)
    {
        ArgumentNullException.ThrowIfNull(source);
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
