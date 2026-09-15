using System.Text.Json;
using System.Text.Json.Serialization;

namespace RemoteHubStudio.Infrastructure.Persistence;

/// <summary>
/// Defines the versioned outer JSON envelope used on disk. / 定义磁盘上使用的带版本外层 JSON 信封。
/// </summary>
internal sealed class WorkspaceEnvelope
{
    /// <summary>Defines the stable workspace file format identifier. / 定义稳定的工作区文件格式标识。</summary>
    public const string FormatIdentifier = "remotehubstudio-workspace";

    /// <summary>Defines the newest envelope schema understood by this build. / 定义当前版本可识别的最新信封架构。</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Defines the unencrypted payload scheme identifier. / 定义未加密载荷方案标识。</summary>
    public const string NoProtectionScheme = "none";

    /// <summary>Gets or sets the stable file format identifier. / 获取或设置稳定的文件格式标识。</summary>
    public string Format { get; set; } = FormatIdentifier;

    /// <summary>Gets or sets the envelope schema version. / 获取或设置信封架构版本。</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Gets or sets the UTC save time. / 获取或设置 UTC 保存时间。</summary>
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the payload protection scheme. / 获取或设置载荷保护方案。</summary>
    public string Protection { get; set; } = NoProtectionScheme;

    /// <summary>Gets or sets the readable JSON document when protection is disabled. / 获取或设置关闭保护时可读的 JSON 文档。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Data { get; set; }

    /// <summary>Gets or sets the Base64 protected payload when protection is enabled. / 获取或设置启用保护时的 Base64 受保护载荷。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Payload { get; set; }
}
