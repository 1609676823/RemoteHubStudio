using RemoteHubStudio.Domain;

namespace RemoteHubStudio.Application;

/// <summary>
/// Validates connection-group ancestry in linear time without recursive traversal. / 以线性时间且不使用递归遍历验证连接分类父系图。
/// </summary>
public static class GroupGraphValidator
{
    /// <summary>Defines the maximum supported nesting depth, counting a root group as depth one. / 定义支持的最大嵌套深度，根分类计为第一层。</summary>
    public const int MaximumDepth = 64;

    /// <summary>
    /// Validates identifiers, parent references, cycles, and nesting depth for a complete group graph. / 验证完整分类图的标识、父引用、循环与嵌套深度。
    /// </summary>
    /// <param name="groups">Complete group collection. / 完整的分类集合。</param>
    /// <exception cref="InvalidDataException">Thrown when the graph is malformed or exceeds the depth limit. / 图结构损坏或超过深度上限时抛出。</exception>
    public static void Validate(IReadOnlyList<ConnectionGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        Dictionary<Guid, ConnectionGroup> groupsById = BuildGroupIndex(groups);
        ValidateParentReferences(groups, groupsById);
        ValidateAncestryDepths(groups, groupsById);
    }

    /// <summary>
    /// Builds a constant-time identifier index while rejecting null, empty, and duplicate identifiers. / 构建常数时间标识索引，同时拒绝空对象、空标识与重复标识。
    /// </summary>
    /// <param name="groups">Complete group collection. / 完整的分类集合。</param>
    /// <returns>Group lookup keyed by stable identifier. / 按稳定标识索引的分类查找表。</returns>
    private static Dictionary<Guid, ConnectionGroup> BuildGroupIndex(IReadOnlyList<ConnectionGroup> groups)
    {
        Dictionary<Guid, ConnectionGroup> groupsById = new(groups.Count);
        for (int index = 0; index < groups.Count; index++)
        {
            ConnectionGroup? group = groups[index];
            if (group is null)
            {
                throw new InvalidDataException($"Group entry {index} is null. / 第 {index} 个分类条目为空。");
            }

            if (group.Id == Guid.Empty)
            {
                throw new InvalidDataException("A group identifier is empty. / 分类标识为空。");
            }

            if (!groupsById.TryAdd(group.Id, group))
            {
                throw new InvalidDataException($"Duplicate group identifier '{group.Id}'. / 分类标识“{group.Id}”重复。");
            }
        }

        return groupsById;
    }

    /// <summary>
    /// Verifies every non-null parent identifier resolves through the prebuilt index. / 验证每个非空父标识均能通过预建索引解析。
    /// </summary>
    /// <param name="groups">Complete group collection. / 完整的分类集合。</param>
    /// <param name="groupsById">Group identifier index. / 分类标识索引。</param>
    private static void ValidateParentReferences(
        IReadOnlyList<ConnectionGroup> groups,
        IReadOnlyDictionary<Guid, ConnectionGroup> groupsById)
    {
        foreach (ConnectionGroup group in groups)
        {
            if (group.ParentId is Guid parentId && !groupsById.ContainsKey(parentId))
            {
                throw new InvalidDataException($"Group '{group.Id}' references missing parent '{parentId}'. / 分类“{group.Id}”引用了不存在的父分类“{parentId}”。");
            }
        }
    }

    /// <summary>
    /// Resolves and memoizes every ancestry depth using an explicit path stack. / 使用显式路径栈解析并记忆每个父系深度。
    /// </summary>
    /// <param name="groups">Complete group collection. / 完整的分类集合。</param>
    /// <param name="groupsById">Group identifier index. / 分类标识索引。</param>
    private static void ValidateAncestryDepths(
        IReadOnlyList<ConnectionGroup> groups,
        IReadOnlyDictionary<Guid, ConnectionGroup> groupsById)
    {
        Dictionary<Guid, int> resolvedDepths = new(groups.Count);
        HashSet<Guid> activePath = new();
        List<Guid> path = [];

        foreach (ConnectionGroup startingGroup in groups)
        {
            if (resolvedDepths.ContainsKey(startingGroup.Id))
            {
                continue;
            }

            path.Clear();
            Guid currentId = startingGroup.Id;
            int resolvedParentDepth;
            while (!resolvedDepths.TryGetValue(currentId, out resolvedParentDepth))
            {
                if (!activePath.Add(currentId))
                {
                    throw new InvalidDataException($"Group ancestry for '{startingGroup.Id}' contains a cycle. / 分类“{startingGroup.Id}”的父系包含循环。");
                }

                path.Add(currentId);
                Guid? parentId = groupsById[currentId].ParentId;
                if (parentId is null)
                {
                    resolvedParentDepth = 0;
                    break;
                }

                currentId = parentId.Value;
            }

            for (int index = path.Count - 1; index >= 0; index--)
            {
                Guid groupId = path[index];
                activePath.Remove(groupId);
                resolvedParentDepth++;
                if (resolvedParentDepth > MaximumDepth)
                {
                    throw new InvalidDataException($"Group '{groupId}' exceeds the maximum nesting depth of {MaximumDepth}. / 分类“{groupId}”超过最大嵌套深度 {MaximumDepth}。");
                }

                resolvedDepths.Add(groupId, resolvedParentDepth);
            }
        }
    }
}
