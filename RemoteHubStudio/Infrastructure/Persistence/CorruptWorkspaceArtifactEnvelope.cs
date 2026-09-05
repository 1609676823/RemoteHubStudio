namespace RemoteHubStudio.Infrastructure.Persistence;

/// <summary>
/// Wraps a damaged workspace byte-for-byte under the current user's data protector. / 使用当前用户的数据保护器逐字节封装损坏的工作区。
/// </summary>
internal sealed class CorruptWorkspaceArtifactEnvelope
{
    /// <summary>Defines the stable damaged-artifact format identifier. / 定义稳定的损坏文件保留格式标识。</summary>
    public const string FormatIdentifier = "remotehubstudio-corrupt-artifact";

    /// <summary>Defines the newest damaged-artifact schema understood by this build. / 定义当前版本可识别的最新损坏文件架构。</summary>
    public const int CurrentSchemaVersion = 2;

    /// <summary>Gets or sets the damaged-artifact format identifier. / 获取或设置损坏文件保留格式标识。</summary>
    public string Format { get; set; } = FormatIdentifier;

    /// <summary>Gets or sets the damaged-artifact schema version. / 获取或设置损坏文件架构版本。</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Gets or sets the UTC preservation time. / 获取或设置 UTC 保留时间。</summary>
    public DateTime PreservedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the protection scheme applied to the raw bytes. / 获取或设置应用于原始字节的保护方案。</summary>
    public string Protection { get; set; } = string.Empty;

    /// <summary>Gets or sets the original damaged-file byte length. / 获取或设置原始损坏文件的字节长度。</summary>
    public long OriginalLength { get; set; }

    /// <summary>Gets or sets the Base64-encoded protected raw bytes. / 获取或设置 Base64 编码的受保护原始字节。</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Gets or sets a small protected marker binding payload hash and original length. / 获取或设置绑定载荷哈希与原始长度的小型受保护标记。</summary>
    public string VerificationPayload { get; set; } = string.Empty;
}
