using RemoteHubStudio.Domain;

namespace RemoteHubStudio.Infrastructure.ImportExport;

/// <summary>
/// Describes profiles and categories parsed from an import source. / 描述从导入源解析出的连接配置与分类。
/// </summary>
public sealed class ImportResult
{
    /// <summary>Gets parsed categories. / 获取解析出的分类。</summary>
    public List<ConnectionGroup> Groups { get; } = [];

    /// <summary>Gets parsed connections. / 获取解析出的连接。</summary>
    public List<ConnectionProfile> Connections { get; } = [];

    /// <summary>Gets non-fatal parsing warnings. / 获取非致命解析警告。</summary>
    public List<string> Warnings { get; } = [];

    /// <summary>Gets the number of source rows rejected as invalid. / 获取因无效而被拒绝的源数据行数。</summary>
    public int SkippedRowCount { get; internal set; }

    /// <summary>Gets the number of imported rows whose active launch configuration was disabled. / 获取已导入但主动启动配置被禁用的行数。</summary>
    public int ModifiedRowCount { get; internal set; }
}
