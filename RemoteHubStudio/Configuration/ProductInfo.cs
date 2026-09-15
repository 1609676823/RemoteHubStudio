using System.Reflection;

namespace RemoteHubStudio.Configuration;

/// <summary>
/// Provides product-wide metadata generated from Directory.Build.props. / 提供由 Directory.Build.props 生成的全局产品元数据。
/// </summary>
public static class ProductInfo
{
    // Use the product assembly even when a test runner or designer hosts the application types.
    // / 即使应用类型由测试程序或设计器承载，也始终读取产品程序集。
    private static readonly Assembly ProductAssembly = typeof(ProductInfo).Assembly;
    private static readonly IReadOnlyDictionary<string, string?> Metadata = ProductAssembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .ToDictionary(attribute => attribute.Key, attribute => attribute.Value, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the public product name. / 获取公开产品名称。
    /// </summary>
    public static string Name { get; } =
        ProductAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
        ?? ProductAssembly.GetName().Name
        ?? string.Empty;

    /// <summary>
    /// Gets the display version, retaining prerelease labels but omitting build metadata. / 获取显示版本，保留预发布标识并省略构建元数据。
    /// </summary>
    public static string Version => InformationalVersion.Split('+', 2)[0];

    /// <summary>
    /// Gets the complete informational version, including the SDK's source revision when available. / 获取完整信息版本，包含 SDK 提供的源代码提交号（如有）。
    /// </summary>
    public static string InformationalVersion { get; } =
        ProductAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? ProductAssembly.GetName().Version?.ToString()
        ?? string.Empty;

    /// <summary>Gets the configured product authors. / 获取配置的产品作者。</summary>
    public static string Authors { get; } = ReadMetadata("Authors");

    /// <summary>Gets the configured product description. / 获取配置的产品描述。</summary>
    public static string Description { get; } =
        ProductAssembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty;

    /// <summary>
    /// Gets the configured publisher or company name. / 获取配置的发布者或公司名称。
    /// </summary>
    public static string Publisher { get; } =
        ProductAssembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;

    /// <summary>
    /// Gets the configured copyright statement. / 获取配置的版权声明。
    /// </summary>
    public static string Copyright { get; } =
        ProductAssembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;

    /// <summary>
    /// Gets the public source repository URL. / 获取公开源代码仓库地址。
    /// </summary>
    public static string RepositoryUrl { get; } = ReadMetadata("RepositoryUrl");

    /// <summary>
    /// Gets the public project home URL. / 获取公开项目主页地址。
    /// </summary>
    public static string ProjectUrl { get; } = ReadMetadata("ProjectUrl");

    /// <summary>
    /// Gets the public issue tracker URL. / 获取公开问题跟踪地址。
    /// </summary>
    public static string IssuesUrl { get; } = ReadMetadata("IssuesUrl");

    /// <summary>
    /// Gets the public releases and updates URL. / 获取公开版本发布与更新地址。
    /// </summary>
    public static string ReleasesUrl { get; } = ReadMetadata("ReleasesUrl");

    /// <summary>
    /// Gets the configured open-source license identifier. / 获取配置的开源许可证标识。
    /// </summary>
    public static string License { get; } = ReadMetadata("License");

    /// <summary>
    /// Gets the public license document URL. / 获取公开许可证文档地址。
    /// </summary>
    public static string LicenseUrl { get; } = ReadMetadata("LicenseUrl");

    /// <summary>
    /// Gets the stable application data directory name. / 获取稳定的应用数据目录名称。
    /// </summary>
    public const string DataDirectoryName = "RemoteHubStudio";

    /// <summary>
    /// Gets the stable workspace format identifier. / 获取稳定的工作区格式标识。
    /// </summary>
    public const string WorkspaceFormatId = "remotehubstudio-portable-workspace";

    /// <summary>
    /// Gets the application-wide single-instance mutex name. / 获取应用级单实例互斥体名称。
    /// </summary>
    public const string SingleInstanceMutexName = "Local\\RemoteHubStudio.Application";

    /// <summary>
    /// Gets the named event used to activate the first application instance. / 获取用于激活首个应用实例的命名事件。
    /// </summary>
    public const string SingleInstanceActivationEventName = "Local\\RemoteHubStudio.Activate";

    /// <summary>
    /// Gets the portable workspace file extension. / 获取可移植工作区文件扩展名。
    /// </summary>
    public const string WorkspaceExportExtension = ".rhs.json";

    /// <summary>
    /// Reads one generated assembly metadata value. / 读取一项由构建生成的程序集元数据。
    /// </summary>
    /// <param name="key">Metadata key. / 元数据键。</param>
    /// <returns>The metadata value, or an empty string when absent. / 返回元数据值，缺失时返回空字符串。</returns>
    private static string ReadMetadata(string key)
    {
        return Metadata.GetValueOrDefault(key) ?? string.Empty;
    }
}
