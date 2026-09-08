using RemoteHubStudio.Localization;

namespace RemoteHubStudio.Domain;

/// <summary>
/// Defines the supported remote connection clients. / 定义支持的远程连接客户端。
/// </summary>
public enum ConnectionType
{
    RemoteDesktop = 0,
    Putty = 1,
    Xshell = 2,
    Xftp = 3,
    WinScp = 4,
    SecureCrt = 5,
    MobaXterm = 6,
    Vnc = 7,
    Radmin = 8,
    ToDesk = 9,
    Custom = 10,
    RustDesk = 11
}

/// <summary>
/// Supplies display and default values for connection types. / 为连接类型提供显示信息和默认值。
/// </summary>
public static class ConnectionTypeExtensions
{
    /// <summary>
    /// Gets a bilingual display name for a connection type. / 获取连接类型的双语显示名称。
    /// </summary>
    /// <param name="type">Connection type. / 连接类型。</param>
    /// <returns>Bilingual display name. / 双语显示名称。</returns>
    public static string ToDisplayName(this ConnectionType type) => type switch
    {
        ConnectionType.RemoteDesktop => L.Get("ConnectionType.RemoteDesktop"),
        ConnectionType.Putty => L.Get("ConnectionType.Putty"),
        ConnectionType.Xshell => L.Get("ConnectionType.Xshell"),
        ConnectionType.Xftp => L.Get("ConnectionType.Xftp"),
        ConnectionType.WinScp => L.Get("ConnectionType.WinScp"),
        ConnectionType.SecureCrt => L.Get("ConnectionType.SecureCrt"),
        ConnectionType.MobaXterm => L.Get("ConnectionType.MobaXterm"),
        ConnectionType.Vnc => L.Get("ConnectionType.Vnc"),
        ConnectionType.Radmin => L.Get("ConnectionType.Radmin"),
        ConnectionType.ToDesk => L.Get("ConnectionType.ToDesk"),
        ConnectionType.RustDesk => L.Get("ConnectionType.RustDesk"),
        ConnectionType.Custom => L.Get("ConnectionType.Custom"),
        _ => type.ToString()
    };

    /// <summary>
    /// Gets the conventional default port for a connection type. / 获取连接类型的常用默认端口。
    /// </summary>
    /// <param name="type">Connection type. / 连接类型。</param>
    /// <returns>Default port, or zero when no fixed port exists. / 默认端口；无固定端口时返回零。</returns>
    public static int GetDefaultPort(this ConnectionType type) => type switch
    {
        ConnectionType.RemoteDesktop => 3389,
        ConnectionType.Putty or ConnectionType.Xshell or ConnectionType.SecureCrt or ConnectionType.MobaXterm => 22,
        ConnectionType.Xftp or ConnectionType.WinScp => 22,
        ConnectionType.Vnc => 5900,
        ConnectionType.Radmin => 4899,
        _ => 0
    };

    /// <summary>
    /// Gets the conventional default port for a concrete client protocol or mode.
    /// / 获取具体客户端协议或模式的常用默认端口。
    /// </summary>
    /// <param name="type">Connection client. / 连接客户端。</param>
    /// <param name="protocol">Protocol or mode identifier. / 协议或模式标识。</param>
    /// <returns>Default port, or zero when the mode has no TCP port. / 默认端口；该模式没有 TCP 端口时返回零。</returns>
    public static int GetDefaultPort(this ConnectionType type, string? protocol)
    {
        string normalized = type.NormalizeProtocol(protocol);
        return type switch
        {
            ConnectionType.Putty or ConnectionType.Xshell or ConnectionType.SecureCrt or ConnectionType.MobaXterm
                when normalized == "telnet" => 23,
            ConnectionType.Xftp when normalized == "ftp" => 21,
            ConnectionType.WinScp when normalized == "ftp" => 21,
            ConnectionType.WinScp when normalized == "ftps" => 990,
            ConnectionType.WinScp when normalized == "webdav" => 80,
            ConnectionType.WinScp when normalized == "webdavs" => 443,
            _ => type.GetDefaultPort()
        };
    }

    /// <summary>
    /// Gets the settings key used to locate an external client executable. / 获取用于定位外部客户端程序的设置键。
    /// </summary>
    /// <param name="type">Connection type. / 连接类型。</param>
    /// <returns>Tool-path key, or an empty string for built-in/custom clients. / 工具路径键；内置或自定义客户端返回空字符串。</returns>
    public static string GetToolPathKey(this ConnectionType type) => type switch
    {
        ConnectionType.Putty => "putty",
        ConnectionType.Xshell => "xshell",
        ConnectionType.Xftp => "xftp",
        ConnectionType.WinScp => "winscp",
        ConnectionType.SecureCrt => "securecrt",
        ConnectionType.MobaXterm => "mobaxterm",
        ConnectionType.Vnc => "vnc-tightvnc",
        ConnectionType.Radmin => "radmin",
        ConnectionType.ToDesk => "todesk",
        ConnectionType.RustDesk => "rustdesk",
        _ => string.Empty
    };

    /// <summary>
    /// Gets the settings key for a client and its normalized protocol implementation. / 获取客户端及其规范化协议实现对应的设置键。
    /// </summary>
    /// <param name="type">Connection type. / 连接类型。</param>
    /// <param name="protocol">Normalized protocol identifier. / 规范化的协议标识。</param>
    /// <returns>Protocol-specific tool-path key, or the client default key when no split is required. / 协议专用的工具路径键；无需拆分时返回客户端默认键。</returns>
    public static string GetToolPathKey(this ConnectionType type, string? protocol)
    {
        if (type != ConnectionType.Vnc)
        {
            return type.GetToolPathKey();
        }

        return protocol?.Trim().ToLowerInvariant() switch
        {
            "realvnc" => "vnc-realvnc",
            "ultravnc" => "vnc-ultravnc",
            _ => "vnc-tightvnc"
        };
    }

    /// <summary>
    /// Gets the protocol choices supported by a connection type. / 获取连接类型支持的协议选项。
    /// </summary>
    /// <param name="type">Connection type. / 连接类型。</param>
    /// <returns>Supported protocol identifiers. / 支持的协议标识集合。</returns>
    public static IReadOnlyList<string> GetProtocols(this ConnectionType type) => type switch
    {
        ConnectionType.RemoteDesktop => ["rdp"],
        ConnectionType.Putty or ConnectionType.MobaXterm => ["ssh", "telnet"],
        ConnectionType.Xshell => ["ssh", "telnet", "sftp"],
        ConnectionType.SecureCrt => ["ssh2", "ssh1", "telnet"],
        ConnectionType.Xftp => ["sftp", "ftp"],
        ConnectionType.WinScp => ["sftp", "scp", "ftp", "ftps", "webdav", "webdavs"],
        ConnectionType.Vnc => ["tightvnc", "realvnc", "ultravnc"],
        ConnectionType.Radmin => ["control", "view", "telnet", "file", "shutdown", "chat", "voice", "message"],
        ConnectionType.ToDesk => ["todesk"],
        ConnectionType.RustDesk => ["connect", "file-transfer", "view-camera", "port-forward", "rdp", "terminal"],
        _ => []
    };

    /// <summary>Gets the default persisted protocol or mode for a client. / 获取客户端默认持久化协议或模式。</summary>
    /// <param name="type">Connection client. / 连接客户端。</param>
    /// <returns>Stable protocol identifier, or an empty string for a custom command. / 稳定协议标识；自定义命令返回空字符串。</returns>
    public static string GetDefaultProtocol(this ConnectionType type)
    {
        IReadOnlyList<string> protocols = type.GetProtocols();
        return protocols.Count == 0 ? string.Empty : protocols[0];
    }

    /// <summary>
    /// Normalizes legacy, localized, and missing protocol values to their stable client-specific identifiers.
    /// Unknown non-empty values are returned in lowercase so callers can apply their own validation policy.
    /// / 将旧版、本地化及缺失的协议值规范化为稳定的客户端专属标识。未知非空值以小写返回，供调用方自行验证。
    /// </summary>
    /// <param name="type">Connection client. / 连接客户端。</param>
    /// <param name="protocol">Persisted or imported protocol value. / 已持久化或导入的协议值。</param>
    /// <returns>The canonical protocol identifier, or an unknown lowercase value. / 规范协议标识，或未知的小写值。</returns>
    public static string NormalizeProtocol(this ConnectionType type, string? protocol)
    {
        string normalized = protocol?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length == 0 ||
            (normalized == "rdp" && type is not (ConnectionType.RemoteDesktop or ConnectionType.RustDesk)))
        {
            return type.GetDefaultProtocol();
        }

        return (type, normalized) switch
        {
            (ConnectionType.Custom, "custom") => string.Empty,
            (ConnectionType.SecureCrt, "ssh") => "ssh2",
            (ConnectionType.ToDesk, "device") => "todesk",
            (ConnectionType.WinScp, "http") => "webdav",
            (ConnectionType.WinScp, "https") => "webdavs",
            (ConnectionType.Radmin, "完全控制") => "control",
            (ConnectionType.Radmin, "仅限查看" or "仅查看") => "view",
            (ConnectionType.Radmin, "文件传送" or "文件传输") => "file",
            (ConnectionType.Radmin, "关机") => "shutdown",
            (ConnectionType.Radmin, "聊天" or "文字聊天") => "chat",
            (ConnectionType.Radmin, "语音聊天") => "voice",
            (ConnectionType.Radmin, "传送信息" or "传送讯息" or "发送消息") => "message",
            (ConnectionType.RustDesk, "control" or "remote-control" or "remotecontrol" or "远程控制") => "connect",
            (ConnectionType.RustDesk, "file" or "filetransfer" or "文件传输") => "file-transfer",
            (ConnectionType.RustDesk, "camera" or "viewcamera" or "查看摄像头") => "view-camera",
            (ConnectionType.RustDesk, "forward" or "portforward" or "端口转发") => "port-forward",
            (ConnectionType.RustDesk, "tunnel-rdp" or "rdp-tunnel" or "rdp隧道") => "rdp",
            (ConnectionType.RustDesk, "远程终端") => "terminal",
            _ => normalized
        };
    }

    /// <summary>Gets a bilingual label for a protocol or client mode. / 获取协议或客户端模式的双语标签。</summary>
    /// <param name="type">Connection client. / 连接客户端。</param>
    /// <param name="protocol">Stable protocol identifier. / 稳定协议标识。</param>
    /// <returns>User-facing bilingual text. / 面向用户的双语文本。</returns>
    public static string ToProtocolDisplayName(this ConnectionType type, string protocol)
    {
        string normalized = protocol.Trim().ToLowerInvariant();
        return type switch
        {
            ConnectionType.RustDesk => normalized switch
            {
                "connect" => L.Get("Protocol.RustDesk.Connect"),
                "file-transfer" => L.Get("Protocol.RustDesk.FileTransfer"),
                "view-camera" => L.Get("Protocol.RustDesk.ViewCamera"),
                "port-forward" => L.Get("Protocol.RustDesk.PortForward"),
                "rdp" => L.Get("Protocol.RustDesk.RdpTunnel"),
                "terminal" => L.Get("Protocol.RustDesk.Terminal"),
                _ => protocol
            },
            ConnectionType.Radmin => normalized switch
            {
                "control" => L.Get("Protocol.Radmin.Control"),
                "view" => L.Get("Protocol.Radmin.View"),
                "telnet" => "Telnet",
                "file" => L.Get("Protocol.Radmin.File"),
                "shutdown" => L.Get("Protocol.Radmin.Shutdown"),
                "chat" => L.Get("Protocol.Radmin.Chat"),
                "voice" => L.Get("Protocol.Radmin.Voice"),
                "message" => L.Get("Protocol.Radmin.Message"),
                _ => protocol
            },
            ConnectionType.Vnc => normalized switch
            {
                "tightvnc" => "TightVNC Viewer",
                "realvnc" => "RealVNC Viewer",
                "ultravnc" => "UltraVNC Viewer",
                _ => protocol
            },
            ConnectionType.WinScp => normalized switch
            {
                "webdav" => "WebDAV (HTTP)",
                "webdavs" => "WebDAVS (HTTPS)",
                _ => normalized.ToUpperInvariant()
            },
            _ => normalized.ToUpperInvariant()
        };
    }
}
