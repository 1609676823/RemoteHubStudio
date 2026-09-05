namespace RemoteHubStudio.Domain;

/// <summary>
/// Stores user-configurable application behavior. / 保存用户可配置的应用行为。
/// </summary>
public sealed class AppSettings
{
    /// <summary>Gets or sets the visual theme. / 获取或设置视觉主题。</summary>
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>Gets or sets whether the local data file is encrypted. Disabled by default. / 获取或设置是否加密本地数据文件；默认关闭。</summary>
    public bool EncryptionEnabled { get; set; }

    /// <summary>Gets or sets whether compatible launchers may pass saved passwords automatically. Enabled by default. / 获取或设置兼容启动器是否可自动传递已保存密码；默认开启。</summary>
    public bool AllowPasswordInCommandLine { get; set; } = true;

    /// <summary>Gets or sets whether exports include secrets by default. Disabled by default. / 获取或设置导出是否默认包含秘密；默认关闭。</summary>
    public bool IncludeSecretsInExports { get; set; }

    /// <summary>Gets or sets whether closing the window minimizes it to the tray. / 获取或设置关闭窗口时是否最小化到托盘。</summary>
    public bool MinimizeToTray { get; set; }

    /// <summary>Gets or sets whether deletion requires confirmation. / 获取或设置删除操作是否需要确认。</summary>
    public bool ConfirmBeforeDelete { get; set; } = true;

    /// <summary>Gets or sets the number of days used for expiration warnings. / 获取或设置到期预警天数。</summary>
    public int ExpiryWarningDays { get; set; } = 30;

    /// <summary>Gets or sets the ping timeout in milliseconds. / 获取或设置 Ping 超时毫秒数。</summary>
    public int PingTimeoutMilliseconds { get; set; } = 1500;

    /// <summary>Gets or sets the maximum number of concurrent status checks. / 获取或设置最大并发状态检测数。</summary>
    public int ConcurrentStatusChecks { get; set; } = 8;

    /// <summary>Gets or sets whether the navigation sidebar starts collapsed. / 获取或设置导航侧栏是否初始折叠。</summary>
    public bool SidebarCollapsed { get; set; }

    /// <summary>Gets or sets the last normal window bounds. / 获取或设置最近一次正常窗口边界。</summary>
    public Rectangle WindowBounds { get; set; } = new(100, 80, 1280, 800);

    /// <summary>Gets or sets configured external executable paths by tool key. / 获取或设置按工具键保存的外部程序路径。</summary>
    public Dictionary<string, string> ToolPaths { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Defines the available visual themes. / 定义可用的视觉主题。
/// </summary>
public enum AppTheme
{
    System,
    Light,
    Dark
}
