namespace RemoteHubStudio.Domain;

/// <summary>
/// Represents the versioned application data document. / 表示带版本的应用数据文档。
/// </summary>
public sealed class AppDataDocument
{
    /// <summary>Gets or sets the current storage schema version. / 获取或设置当前存储架构版本。</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Gets or sets application settings. / 获取或设置应用设置。</summary>
    public AppSettings Settings { get; set; } = new();

    /// <summary>Gets or sets nested connection groups. / 获取或设置嵌套连接分类。</summary>
    public List<ConnectionGroup> Groups { get; set; } = [];

    /// <summary>Gets or sets saved connections. / 获取或设置已保存连接。</summary>
    public List<ConnectionProfile> Connections { get; set; } = [];

    /// <summary>Defines the newest storage schema understood by this build. / 定义当前版本可识别的最新存储架构。</summary>
    public const int CurrentSchemaVersion = 1;
}
