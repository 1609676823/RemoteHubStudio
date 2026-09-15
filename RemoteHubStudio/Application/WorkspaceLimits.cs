using RemoteHubStudio.Domain;

namespace RemoteHubStudio.Application;

/// <summary>
/// Defines and validates central workspace entity-count limits across import, merge, mutation, and load boundaries. / 定义并验证导入、合并、变更与加载边界的中央工作区实体数量上限。
/// </summary>
public static class WorkspaceLimits
{
    /// <summary>Defines the maximum number of groups in one committed workspace. / 定义单个已提交工作区的最大分类数。</summary>
    public const int MaximumGroupCount = 5_000;

    /// <summary>Defines the maximum number of connections in one committed workspace. / 定义单个已提交工作区的最大连接数。</summary>
    public const int MaximumConnectionCount = 50_000;

    /// <summary>
    /// Validates the entity counts of one materialized workspace document. / 验证一份已实例化工作区文档的实体数量。
    /// </summary>
    /// <param name="document">Workspace document to validate. / 要验证的工作区文档。</param>
    public static void ValidateDocument(AppDataDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateCounts(
            document.Groups?.Count ?? 0,
            document.Connections?.Count ?? 0);
    }

    /// <summary>
    /// Validates the prospective aggregate counts before an imported document is merged. / 在合并导入文档前验证预期聚合数量。
    /// </summary>
    /// <param name="existing">Existing committed workspace. / 现有已提交工作区。</param>
    /// <param name="incoming">Incoming workspace to merge. / 要合并的导入工作区。</param>
    public static void ValidateCombined(AppDataDocument existing, AppDataDocument incoming)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(incoming);
        ValidateCounts(
            (long)(existing.Groups?.Count ?? 0) + (incoming.Groups?.Count ?? 0),
            (long)(existing.Connections?.Count ?? 0) + (incoming.Connections?.Count ?? 0));
    }

    /// <summary>
    /// Validates explicit group and connection counts without materializing entities. / 在不实例化实体的情况下验证显式的分类与连接数量。
    /// </summary>
    /// <param name="groupCount">Group count. / 分类数量。</param>
    /// <param name="connectionCount">Connection count. / 连接数量。</param>
    public static void ValidateCounts(long groupCount, long connectionCount)
    {
        if (groupCount < 0 || connectionCount < 0)
        {
            throw new InvalidDataException("Workspace entity counts cannot be negative. / 工作区实体数量不能为负数。");
        }

        if (groupCount > MaximumGroupCount)
        {
            throw new InvalidDataException($"The workspace exceeds {MaximumGroupCount} groups. / 工作区分类数超过 {MaximumGroupCount} 项。");
        }

        if (connectionCount > MaximumConnectionCount)
        {
            throw new InvalidDataException($"The workspace exceeds {MaximumConnectionCount} connections. / 工作区连接数超过 {MaximumConnectionCount} 项。");
        }
    }
}
