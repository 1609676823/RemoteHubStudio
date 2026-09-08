using RemoteHubStudio.Domain;

namespace RemoteHubStudio.Application;

/// <summary>
/// Bounds workspace text and dictionary content before serialization or UI projection. / 在序列化或界面投影前限制工作区文本与字典内容。
/// </summary>
public static class WorkspaceContentLimits
{
    /// <summary>Defines the maximum characters in one user-controlled string. / 定义单个用户可控字符串的最大字符数。</summary>
    public const int MaximumStringCharacterCount = 256 * 1024;

    /// <summary>Defines the maximum aggregate characters across one workspace. / 定义单个工作区的最大聚合字符数。</summary>
    public const long MaximumTotalStringCharacterCount = 4L * 1024L * 1024L;

    /// <summary>Defines the maximum configured external tool paths. / 定义配置的外部工具路径最大数量。</summary>
    public const int MaximumToolPathCount = 64;

    /// <summary>Defines the maximum client-specific options on one connection. / 定义单个连接的最大客户端专属选项数。</summary>
    public const int MaximumOptionsPerConnection = 256;

    /// <summary>Defines the maximum aggregate client-specific option entries. / 定义客户端专属选项条目的最大聚合数。</summary>
    public const int MaximumTotalOptionEntryCount = 100_000;

    /// <summary>
    /// Validates all persisted strings and dictionaries without allocating copies. / 在不分配副本的情况下验证所有持久化字符串与字典。
    /// </summary>
    /// <param name="document">Workspace document to validate. / 要验证的工作区文档。</param>
    public static void ValidateDocument(AppDataDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        long totalCharacters = 0;
        int totalOptionEntries = 0;

        IReadOnlyDictionary<string, string>? toolPaths = document.Settings?.ToolPaths;
        if ((toolPaths?.Count ?? 0) > MaximumToolPathCount)
        {
            throw new InvalidDataException($"The workspace exceeds {MaximumToolPathCount} external tool paths. / 工作区外部工具路径超过 {MaximumToolPathCount} 项。");
        }

        AddDictionaryCharacters(toolPaths, "tool path / 工具路径", ref totalCharacters);
        foreach (ConnectionGroup group in document.Groups ?? [])
        {
            AddStringCharacters(group?.Name, "group name / 分类名称", ref totalCharacters);
            AddStringCharacters(group?.Color, "group color / 分类颜色", ref totalCharacters);
        }

        foreach (ConnectionProfile connection in document.Connections ?? [])
        {
            AddStringCharacters(connection?.Name, "connection name / 连接名称", ref totalCharacters);
            AddStringCharacters(connection?.Protocol, "connection protocol / 连接协议", ref totalCharacters);
            AddStringCharacters(connection?.Host, "connection host / 连接主机", ref totalCharacters);
            AddStringCharacters(connection?.Username, "connection username / 连接用户名", ref totalCharacters);
            AddStringCharacters(connection?.Password, "connection password / 连接密码", ref totalCharacters);
            AddStringCharacters(connection?.PrivateKeyPath, "private-key path / 私钥路径", ref totalCharacters);
            AddStringCharacters(connection?.Notes, "connection notes / 连接备注", ref totalCharacters);
            AddStringCharacters(connection?.ExecutableOverride, "executable override / 程序覆盖", ref totalCharacters);
            AddStringCharacters(connection?.CustomArguments, "custom arguments / 自定义参数", ref totalCharacters);

            IReadOnlyDictionary<string, string>? options = connection?.Options;
            if ((options?.Count ?? 0) > MaximumOptionsPerConnection)
            {
                throw new InvalidDataException($"A connection exceeds {MaximumOptionsPerConnection} client options. / 单个连接的客户端选项超过 {MaximumOptionsPerConnection} 项。");
            }

            totalOptionEntries += options?.Count ?? 0;
            if (totalOptionEntries > MaximumTotalOptionEntryCount)
            {
                throw new InvalidDataException($"The workspace exceeds {MaximumTotalOptionEntryCount} client option entries. / 工作区客户端选项条目超过 {MaximumTotalOptionEntryCount} 项。");
            }

            AddDictionaryCharacters(options, "client option / 客户端选项", ref totalCharacters);
        }
    }

    /// <summary>
    /// Adds one string to the bounded aggregate after checking its individual length. / 检查单个字符串长度后将其加入受限聚合值。
    /// </summary>
    /// <param name="value">Candidate text. / 候选文本。</param>
    /// <param name="fieldName">Bilingual field description. / 双语字段描述。</param>
    /// <param name="totalCharacters">Running aggregate character count. / 正在累加的字符总数。</param>
    private static void AddStringCharacters(string? value, string fieldName, ref long totalCharacters)
    {
        int length = value?.Length ?? 0;
        if (length > MaximumStringCharacterCount)
        {
            throw new InvalidDataException($"A {fieldName} value exceeds {MaximumStringCharacterCount} characters. / {fieldName} 字段超过 {MaximumStringCharacterCount} 个字符。");
        }

        totalCharacters += length;
        if (totalCharacters > MaximumTotalStringCharacterCount)
        {
            throw new InvalidDataException($"The workspace exceeds {MaximumTotalStringCharacterCount} text characters. / 工作区文本总量超过 {MaximumTotalStringCharacterCount} 个字符。");
        }
    }

    /// <summary>
    /// Adds dictionary keys and values to the bounded text aggregate. / 将字典键与值加入受限文本聚合值。
    /// </summary>
    /// <param name="values">Dictionary values to inspect. / 要检查的字典值。</param>
    /// <param name="fieldName">Bilingual field description. / 双语字段描述。</param>
    /// <param name="totalCharacters">Running aggregate character count. / 正在累加的字符总数。</param>
    private static void AddDictionaryCharacters(
        IReadOnlyDictionary<string, string>? values,
        string fieldName,
        ref long totalCharacters)
    {
        if (values is null)
        {
            return;
        }

        foreach ((string key, string value) in values)
        {
            AddStringCharacters(key, fieldName + " key / 键", ref totalCharacters);
            AddStringCharacters(value, fieldName + " value / 值", ref totalCharacters);
        }
    }
}
