namespace RemoteHubStudio.Application;

/// <summary>
/// Provides pure reconciliation and presentation helpers for connection selection. / 为连接选择提供纯逻辑对齐与展示辅助功能。
/// </summary>
public static class ConnectionSelectionLogic
{
    /// <summary>
    /// Resolves AntdUI's one-based real row indices to stable visible row identifiers. / 将 AntdUI 从一开始的真实行索引解析为稳定的可见行标识。
    /// </summary>
    /// <param name="visibleIds">Identifiers in the original table data-source order. / 按表格原始数据源顺序排列的标识。</param>
    /// <param name="oneBasedIndices">One-based real row indices reported by the table. / 表格报告的从一开始的真实行索引。</param>
    /// <returns>Valid nonempty identifiers in native selection order, without duplicates. / 按原生选择顺序返回有效、非空且不重复的标识。</returns>
    public static IReadOnlyList<Guid> ResolveOneBasedSelection(
        IReadOnlyList<Guid> visibleIds,
        IEnumerable<int> oneBasedIndices)
    {
        ArgumentNullException.ThrowIfNull(visibleIds);
        ArgumentNullException.ThrowIfNull(oneBasedIndices);

        HashSet<Guid> added = [];
        List<Guid> result = [];
        foreach (int oneBasedIndex in oneBasedIndices)
        {
            int zeroBasedIndex = oneBasedIndex - 1;
            if ((uint)zeroBasedIndex >= (uint)visibleIds.Count)
            {
                continue;
            }

            Guid id = visibleIds[zeroBasedIndex];
            if (id != Guid.Empty && added.Add(id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    /// <summary>
    /// Keeps selected identifiers only when they remain visible, existing, nonempty, and unique. / 仅保留仍可见、仍存在、非空且不重复的已选标识。
    /// </summary>
    /// <param name="selectedIds">Identifiers reported by the native table selection. / 原生表格选择报告的标识。</param>
    /// <param name="visibleIds">Identifiers in the current filtered view. / 当前筛选视图中的标识。</param>
    /// <param name="existingIds">Identifiers that still exist in the workspace. / 工作区中仍存在的标识。</param>
    /// <returns>Stable selected identifiers in their original order. / 按原始顺序返回的稳定已选标识。</returns>
    public static IReadOnlyList<Guid> ReconcileVisibleSelection(
        IEnumerable<Guid> selectedIds,
        IEnumerable<Guid> visibleIds,
        IEnumerable<Guid> existingIds)
    {
        ArgumentNullException.ThrowIfNull(selectedIds);
        ArgumentNullException.ThrowIfNull(visibleIds);
        ArgumentNullException.ThrowIfNull(existingIds);

        HashSet<Guid> visible = visibleIds.Where(id => id != Guid.Empty).ToHashSet();
        HashSet<Guid> existing = existingIds.Where(id => id != Guid.Empty).ToHashSet();
        HashSet<Guid> added = [];
        List<Guid> result = [];
        foreach (Guid id in selectedIds)
        {
            if (id != Guid.Empty && visible.Contains(id) && existing.Contains(id) && added.Add(id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    /// <summary>
    /// Maps selected identifiers to AntdUI's one-based native row indices in linear time. / 以线性时间将已选标识映射为 AntdUI 从一开始的原生行索引。
    /// </summary>
    /// <param name="visibleIds">Identifiers in current row order. / 按当前行顺序排列的标识。</param>
    /// <param name="selectedIds">Identifiers requested for selection. / 请求选择的标识。</param>
    /// <returns>Distinct one-based indices in visible row order. / 按可见行顺序排列的不重复从一开始索引。</returns>
    public static int[] BuildOneBasedVisibleIndices(
        IReadOnlyList<Guid> visibleIds,
        IEnumerable<Guid> selectedIds)
    {
        ArgumentNullException.ThrowIfNull(visibleIds);
        ArgumentNullException.ThrowIfNull(selectedIds);
        HashSet<Guid> requested = selectedIds.Where(id => id != Guid.Empty).ToHashSet();
        if (requested.Count == 0)
        {
            return [];
        }

        List<int> indices = new(Math.Min(visibleIds.Count, requested.Count));
        for (int index = 0; index < visibleIds.Count; index++)
        {
            if (requested.Contains(visibleIds[index]))
            {
                indices.Add(index + 1);
            }
        }

        return indices.ToArray();
    }

    /// <summary>
    /// Builds a bounded one-line name summary for destructive-action confirmation. / 为破坏性操作确认构建有界的单行名称摘要。
    /// </summary>
    /// <param name="names">Selected connection names. / 已选连接名称。</param>
    /// <param name="maximumNames">Maximum number of names to display. / 最多显示的名称数量。</param>
    /// <param name="maximumNameLength">Maximum displayed characters per name. / 每个名称最多显示的字符数。</param>
    /// <param name="separator">Localized separator placed between names. / 名称之间使用的本地化分隔符。</param>
    /// <param name="unnamed">Localized fallback for an empty name. / 空名称的本地化回退文本。</param>
    /// <param name="formatRemaining">Optional localized overflow formatter. / 可选的本地化溢出数量格式化器。</param>
    /// <returns>Localized-safe bounded summary text. / 适用于本地化界面的有界摘要文本。</returns>
    public static string BuildDeletionNameSummary(
        IEnumerable<string?> names,
        int maximumNames = 5,
        int maximumNameLength = 48,
        string separator = ", ",
        string unnamed = "(Unnamed)",
        Func<int, string>? formatRemaining = null)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(separator);
        ArgumentException.ThrowIfNullOrWhiteSpace(unnamed);
        if (maximumNames < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumNames));
        }

        if (maximumNameLength < 4)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumNameLength));
        }

        string[] normalizedNames = names
            .Select(name => NormalizeDisplayName(name, maximumNameLength, unnamed))
            .ToArray();
        string summary = string.Join(separator, normalizedNames.Take(maximumNames));
        int remainingCount = normalizedNames.Length - maximumNames;
        if (remainingCount <= 0)
        {
            return summary;
        }

        string remainingText = formatRemaining?.Invoke(remainingCount) ?? $"… and {remainingCount} more";
        return $"{summary} {remainingText}";
    }

    /// <summary>
    /// Converts an arbitrary connection name into bounded single-line display text. / 将任意连接名称转换为有界的单行展示文本。
    /// </summary>
    /// <param name="name">Untrusted connection name. / 不可信的连接名称。</param>
    /// <param name="maximumLength">Maximum output character count. / 输出的最大字符数。</param>
    /// <returns>Single-line bounded name. / 有界的单行名称。</returns>
    private static string NormalizeDisplayName(string? name, int maximumLength, string unnamed)
    {
        string singleLine = string.Concat((name ?? string.Empty).Select(character => char.IsControl(character) ? ' ' : character)).Trim();
        if (singleLine.Length == 0)
        {
            singleLine = unnamed;
        }

        return singleLine.Length <= maximumLength
            ? singleLine
            : $"{singleLine[..(maximumLength - 1)]}…";
    }
}
