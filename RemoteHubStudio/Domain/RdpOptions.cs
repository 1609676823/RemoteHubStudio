namespace RemoteHubStudio.Domain;

/// <summary>
/// Stores Remote Desktop Protocol session options. / 保存远程桌面协议会话选项。
/// </summary>
public sealed class RdpOptions
{
    /// <summary>Gets or sets whether full-screen mode is used. / 获取或设置是否使用全屏模式。</summary>
    public bool FullScreen { get; set; } = true;

    /// <summary>Gets or sets whether all monitors are used. / 获取或设置是否使用全部显示器。</summary>
    public bool UseAllMonitors { get; set; }

    /// <summary>Gets or sets the desktop width in windowed mode. / 获取或设置窗口模式下的桌面宽度。</summary>
    public int DesktopWidth { get; set; } = 1440;

    /// <summary>Gets or sets the desktop height in windowed mode. / 获取或设置窗口模式下的桌面高度。</summary>
    public int DesktopHeight { get; set; } = 900;

    /// <summary>Gets or sets the color depth. / 获取或设置色彩深度。</summary>
    public int ColorDepth { get; set; } = 32;

    /// <summary>Gets or sets whether the full-screen connection bar is displayed. / 获取或设置是否显示全屏连接栏。</summary>
    public bool DisplayConnectionBar { get; set; } = true;

    /// <summary>Gets or sets whether RDP bitmap and protocol compression is enabled. / 获取或设置是否启用 RDP 位图及协议压缩。</summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>Gets or sets where Windows key combinations are applied. / 获取或设置 Windows 组合键的应用位置。</summary>
    public RdpKeyboardHookMode KeyboardHookMode { get; set; } = RdpKeyboardHookMode.FullScreenOnly;

    /// <summary>Gets or sets whether the clipboard is redirected. / 获取或设置是否重定向剪贴板。</summary>
    public bool RedirectClipboard { get; set; } = true;

    /// <summary>Gets or sets whether local drives are redirected. / 获取或设置是否重定向本地驱动器。</summary>
    public bool RedirectDrives { get; set; }

    /// <summary>Gets or sets whether local printers are redirected. / 获取或设置是否重定向本地打印机。</summary>
    public bool RedirectPrinters { get; set; }

    /// <summary>Gets or sets whether smart cards are redirected. / 获取或设置是否重定向智能卡。</summary>
    public bool RedirectSmartCards { get; set; }

    /// <summary>Gets or sets whether local serial ports are redirected. / 获取或设置是否重定向本地串行端口。</summary>
    public bool RedirectComPorts { get; set; }

    /// <summary>Gets or sets whether supported point-of-service devices are redirected. / 获取或设置是否重定向支持的服务点设备。</summary>
    public bool RedirectPosDevices { get; set; }

    /// <summary>Gets or sets whether local cameras are redirected. / 获取或设置是否重定向本地摄像头。</summary>
    public bool RedirectCameras { get; set; }

    /// <summary>Gets or sets whether microphone capture is redirected. / 获取或设置是否重定向麦克风录音。</summary>
    public bool RedirectMicrophone { get; set; }

    /// <summary>Gets or sets where remote audio is played. / 获取或设置远程音频播放位置。</summary>
    public RdpAudioMode AudioMode { get; set; } = RdpAudioMode.Local;

    /// <summary>Gets or sets whether the administrative session is requested. / 获取或设置是否请求管理会话。</summary>
    public bool AdministrativeSession { get; set; }

    /// <summary>Gets or sets whether Windows should prompt for credentials. / 获取或设置 Windows 是否提示输入凭据。</summary>
    public bool PromptForCredentials { get; set; }

    /// <summary>Gets or sets whether wallpaper is disabled for performance. / 获取或设置是否为性能禁用壁纸。</summary>
    public bool DisableWallpaper { get; set; }

    /// <summary>Gets or sets whether automatic reconnection is enabled. / 获取或设置是否启用自动重连。</summary>
    public bool AutoReconnect { get; set; } = true;
}

/// <summary>
/// Defines where Remote Desktop audio should play. / 定义远程桌面音频的播放位置。
/// </summary>
public enum RdpAudioMode
{
    Local = 0,
    Remote = 1,
    Disabled = 2
}

/// <summary>
/// Defines where Windows key combinations are handled during an RDP session. / 定义 RDP 会话期间 Windows 组合键的处理位置。
/// </summary>
public enum RdpKeyboardHookMode
{
    Local = 0,
    Remote = 1,
    FullScreenOnly = 2
}
