using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using RemoteHubStudio.Configuration;
using RemoteHubStudio.Domain;

namespace RemoteHubStudio.Infrastructure.Launch;

/// <summary>
/// Builds and starts secure launch plans for all RemoteHubStudio connection clients. / 为 RemoteHubStudio 的全部连接客户端构建并启动安全计划。
/// </summary>
public sealed class ConnectionLaunchService
{
    private const string PasswordPlaceholder = "{password}";
    private static readonly Regex OwnedRdpFileNamePattern = new(
        @"^.+-[0-9a-f]{32}\.rdp$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly TimeSpan StaleRdpFileAge = TimeSpan.FromHours(24);
    private readonly string _rdpDirectory;

    /// <summary>
    /// Initializes the service with an application-specific temporary RDP directory. / 使用应用专属的 RDP 临时目录初始化服务。
    /// </summary>
    public ConnectionLaunchService()
        : this(Path.Combine(Path.GetTempPath(), ProductInfo.DataDirectoryName, "rdp"))
    {
    }

    /// <summary>
    /// Initializes the service with a caller-selected temporary RDP directory. / 使用调用方指定的 RDP 临时目录初始化服务。
    /// </summary>
    /// <param name="rdpDirectory">Dedicated directory for generated RDP files. / 生成 RDP 文件的专用目录。</param>
    public ConnectionLaunchService(string rdpDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rdpDirectory);
        _rdpDirectory = Path.GetFullPath(rdpDirectory);
        CleanupStaleOwnedRdpFiles(_rdpDirectory);
    }

    /// <summary>
    /// Creates a validated, shell-free launch plan without starting a process. / 创建经过验证且不调用外壳的启动计划，但不启动进程。
    /// </summary>
    /// <param name="profile">Connection profile to launch. / 要启动的连接配置。</param>
    /// <param name="settings">Current application settings. / 当前应用设置。</param>
    /// <returns>Immutable launch plan. / 不可变启动计划。</returns>
    public LaunchPlan CreatePlan(
        ConnectionProfile profile,
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(settings);
        ValidateProfile(profile);

        AuthenticationValues authentication = new(
            profile.Username ?? string.Empty,
            profile.Password ?? string.Empty);
        ValidateAuthenticationValues(authentication);
        string protocol = NormalizeProtocol(profile);
        string executablePath = ToolPathResolver.Resolve(profile, settings, protocol);

        if (profile.Type == ConnectionType.Custom || !string.IsNullOrWhiteSpace(profile.CustomArguments))
        {
            return BuildCustomPlan(executablePath, profile, authentication, settings.AllowPasswordInCommandLine);
        }

        return profile.Type switch
        {
            ConnectionType.RemoteDesktop => BuildRemoteDesktopPlan(executablePath, profile, authentication, settings.AllowPasswordInCommandLine),
            ConnectionType.Putty => BuildPuttyPlan(executablePath, profile, authentication, protocol, settings.AllowPasswordInCommandLine),
            ConnectionType.Xshell => BuildXshellPlan(executablePath, profile, authentication, protocol, settings.AllowPasswordInCommandLine),
            ConnectionType.Xftp => BuildXftpPlan(executablePath, profile, authentication, protocol, settings.AllowPasswordInCommandLine),
            ConnectionType.WinScp => BuildWinScpPlan(executablePath, profile, authentication, protocol, settings.AllowPasswordInCommandLine),
            ConnectionType.SecureCrt => BuildSecureCrtPlan(executablePath, profile, authentication, protocol, settings.AllowPasswordInCommandLine),
            ConnectionType.MobaXterm => BuildMobaXtermPlan(executablePath, profile, authentication, protocol, settings.AllowPasswordInCommandLine),
            ConnectionType.Vnc => BuildVncPlan(executablePath, profile, authentication, protocol, settings.AllowPasswordInCommandLine),
            ConnectionType.Radmin => BuildRadminPlan(executablePath, profile, protocol),
            ConnectionType.ToDesk => BuildToDeskPlan(executablePath, profile, authentication, settings.AllowPasswordInCommandLine),
            ConnectionType.RustDesk => BuildRustDeskPlan(executablePath, profile, authentication, protocol, settings.AllowPasswordInCommandLine),
            _ => throw new LaunchValidationException($"Unsupported connection type: {profile.Type}. / 不支持的连接类型：{profile.Type}。")
        };
    }

    /// <summary>
    /// Creates and immediately starts a connection launch plan. / 创建并立即启动连接计划。
    /// </summary>
    /// <param name="profile">Connection profile to launch. / 要启动的连接配置。</param>
    /// <param name="settings">Current application settings. / 当前应用设置。</param>
    /// <returns>Started process whose handle is released automatically after exit. / 已启动进程；其句柄会在退出后自动释放。</returns>
    public Process Launch(
        ConnectionProfile profile,
        AppSettings settings)
    {
        LaunchPlan plan = CreatePlan(profile, settings);
        return Start(plan);
    }

    /// <summary>
    /// Starts an existing launch plan with ProcessStartInfo.ArgumentList and registers exit cleanup. / 使用 ProcessStartInfo.ArgumentList 启动现有计划并注册退出清理。
    /// </summary>
    /// <param name="plan">Validated launch plan. / 已验证的启动计划。</param>
    /// <returns>Started process whose lifetime and temporary files are managed together. / 已启动进程；其生命周期与临时文件由同一组件管理。</returns>
    public Process Start(LaunchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Process process = new()
        {
            StartInfo = plan.CreateStartInfo()
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The client process did not start. / 客户端进程未能启动。");
            }

            ProcessCleanupRegistration cleanup = new(process, plan.TemporaryFiles);
            cleanup.CleanupIfAlreadyExited();

            return process;
        }
        catch (Exception exception)
        {
            process.Dispose();
            DeleteTemporaryFiles(plan.TemporaryFiles);
            throw new InvalidOperationException(
                $"Unable to start '{plan.ExecutablePath}'. / 无法启动“{plan.ExecutablePath}”。",
                exception);
        }
    }

    /// <summary>
    /// Builds a custom argument plan after tokenization and safe placeholder replacement. / 在拆分参数并安全替换占位符后构建自定义启动计划。
    /// </summary>
    /// <param name="executablePath">Resolved executable. / 已解析的可执行文件。</param>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="authentication">Connection authentication values. / 连接认证值。</param>
    /// <param name="allowPassword">Whether current settings permit password substitution. / 当前设置是否允许替换密码。</param>
    /// <returns>Custom launch plan. / 自定义启动计划。</returns>
    private static LaunchPlan BuildCustomPlan(
        string executablePath,
        ConnectionProfile profile,
        AuthenticationValues authentication,
        bool allowPassword)
    {
        string template = profile.CustomArguments ?? string.Empty;
        bool requestsPassword = template.Contains(PasswordPlaceholder, StringComparison.OrdinalIgnoreCase);
        if (requestsPassword && !allowPassword)
        {
            throw new LaunchValidationException(
                "The custom argument template uses {password}, but password arguments are disabled. / 自定义参数模板使用了 {password}，但密码参数当前已禁用。");
        }

        bool requestsPrivateKey = template.Contains("{key}", StringComparison.OrdinalIgnoreCase);
        string privateKeyPath = string.Empty;
        if (requestsPrivateKey)
        {
            privateKeyPath = ResolvePrivateKeyPath(profile.PrivateKeyPath)
                ?? throw new LaunchValidationException("The custom argument template uses {key}, but no private-key file is configured. / 自定义参数模板使用了 {key}，但未配置私钥文件。");
        }

        IReadOnlyList<string> tokens = CustomArgumentTokenizer.Tokenize(template);
        string[] arguments = tokens.Select(token => ReplaceCustomPlaceholders(token, profile, authentication, privateKeyPath, allowPassword)).ToArray();
        bool containsSensitiveData = requestsPassword && allowPassword && !string.IsNullOrEmpty(authentication.Password);
        return new LaunchPlan(executablePath, arguments, containsSensitiveData: containsSensitiveData);
    }

    /// <summary>
    /// Builds an RDP file plan and includes password 51 when current settings allow it. / 构建 RDP 文件计划，并在当前设置允许时包含 password 51。
    /// </summary>
    /// <param name="executablePath">Resolved MSTSC executable. / 已解析的 MSTSC 程序。</param>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="authentication">Connection authentication values. / 连接认证值。</param>
    /// <param name="allowPassword">Whether current settings permit password material. / 当前设置是否允许密码材料。</param>
    /// <returns>Remote Desktop launch plan. / 远程桌面启动计划。</returns>
    private LaunchPlan BuildRemoteDesktopPlan(
        string executablePath,
        ConnectionProfile profile,
        AuthenticationValues authentication,
        bool allowPassword)
    {
        RdpOptions options = profile.Rdp ?? new RdpOptions();
        string rdpPath = RdpFileBuilder.CreateFile(_rdpDirectory, profile, authentication.Username, authentication.Password, allowPassword);
        bool promptForCredentials = options.PromptForCredentials || (!allowPassword && !string.IsNullOrEmpty(authentication.Password));
        List<string> arguments = [rdpPath];

        if (options.AdministrativeSession)
        {
            arguments.Add("/admin");
        }

        if (promptForCredentials)
        {
            arguments.Add("/prompt");
        }

        bool containsSensitiveData = allowPassword && !string.IsNullOrEmpty(authentication.Password);
        return new LaunchPlan(executablePath, arguments, [rdpPath], containsSensitiveData);
    }

    /// <summary>
    /// Builds PuTTY SSH or Telnet arguments. / 构建 PuTTY SSH 或 Telnet 参数。
    /// </summary>
    /// <param name="executablePath">Resolved PuTTY executable. / 已解析的 PuTTY 程序。</param>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="authentication">Connection authentication values. / 连接认证值。</param>
    /// <param name="protocol">Normalized protocol. / 规范化协议。</param>
    /// <param name="allowPassword">Whether current settings permit password arguments. / 当前设置是否允许密码参数。</param>
    /// <returns>PuTTY launch plan. / PuTTY 启动计划。</returns>
    private static LaunchPlan BuildPuttyPlan(
        string executablePath,
        ConnectionProfile profile,
        AuthenticationValues authentication,
        string protocol,
        bool allowPassword)
    {
        List<string> arguments = [];
        bool containsSensitiveData = false;

        if (protocol == "telnet")
        {
            arguments.Add("-telnet");
        }
        else
        {
            arguments.Add("-ssh");
            if (!string.IsNullOrWhiteSpace(authentication.Username))
            {
                arguments.Add("-l");
                arguments.Add(authentication.Username);
            }

            string? keyPath = ResolvePrivateKeyPath(profile.PrivateKeyPath);
            if (keyPath is not null)
            {
                arguments.Add("-i");
                arguments.Add(keyPath);
            }
            else if (allowPassword && !string.IsNullOrEmpty(authentication.Password))
            {
                arguments.Add("-pw");
                arguments.Add(authentication.Password);
                containsSensitiveData = true;
            }
        }

        arguments.Add("-P");
        arguments.Add(profile.Port.ToString(CultureInfo.InvariantCulture));
        arguments.Add(profile.Host.Trim());
        return new LaunchPlan(executablePath, arguments, containsSensitiveData: containsSensitiveData);
    }

    /// <summary>
    /// Builds Xshell URL arguments with percent-encoded authentication values. / 使用百分号编码认证值构建 Xshell URL 参数。
    /// </summary>
    /// <param name="executablePath">Resolved Xshell executable. / 已解析的 Xshell 程序。</param>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="authentication">Connection authentication values. / 连接认证值。</param>
    /// <param name="protocol">Normalized protocol. / 规范化协议。</param>
    /// <param name="allowPassword">Whether current settings permit password arguments. / 当前设置是否允许密码参数。</param>
    /// <returns>Xshell launch plan. / Xshell 启动计划。</returns>
    private static LaunchPlan BuildXshellPlan(
        string executablePath,
        ConnectionProfile profile,
        AuthenticationValues authentication,
        string protocol,
        bool allowPassword)
    {
        bool containsSensitiveData = CanEmbedPassword(authentication, allowPassword);
        string uri = BuildSessionUri(protocol, profile, authentication, containsSensitiveData);
        return new LaunchPlan(executablePath, ["-url", uri], containsSensitiveData: containsSensitiveData);
    }

    /// <summary>
    /// Builds Xftp SFTP or FTP URL arguments with percent-encoded authentication values. / 使用百分号编码认证值构建 Xftp SFTP 或 FTP URL 参数。
    /// </summary>
    /// <param name="executablePath">Resolved Xftp executable. / 已解析的 Xftp 程序。</param>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="authentication">Connection authentication values. / 连接认证值。</param>
    /// <param name="protocol">Normalized protocol. / 规范化协议。</param>
    /// <param name="allowPassword">Whether current settings permit password arguments. / 当前设置是否允许密码参数。</param>
    /// <returns>Xftp launch plan. / Xftp 启动计划。</returns>
    private static LaunchPlan BuildXftpPlan(
        string executablePath,
        ConnectionProfile profile,
        AuthenticationValues authentication,
        string protocol,
        bool allowPassword)
    {
        bool containsSensitiveData = CanEmbedPassword(authentication, allowPassword);
        string uri = BuildSessionUri(protocol, profile, authentication, containsSensitiveData);
        return new LaunchPlan(executablePath, ["-url", uri], containsSensitiveData: containsSensitiveData);
    }

    /// <summary>
    /// Builds WinSCP session URL arguments for file-transfer and WebDAV protocols. / 为文件传输及 WebDAV 协议构建 WinSCP 会话 URL 参数。
    /// </summary>
    /// <param name="executablePath">Resolved WinSCP executable. / 已解析的 WinSCP 程序。</param>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="authentication">Connection authentication values. / 连接认证值。</param>
    /// <param name="protocol">Normalized protocol. / 规范化协议。</param>
    /// <param name="allowPassword">Whether current settings permit password arguments. / 当前设置是否允许密码参数。</param>
    /// <returns>WinSCP launch plan. / WinSCP 启动计划。</returns>
    private static LaunchPlan BuildWinScpPlan(
        string executablePath,
        ConnectionProfile profile,
        AuthenticationValues authentication,
        string protocol,
        bool allowPassword)
    {
        string scheme = protocol switch
        {
            "webdav" => "dav",
            "webdavs" => "davs",
            _ => protocol
        };
        bool containsSensitiveData = CanEmbedPassword(authentication, allowPassword);
        string remotePath = GetOption(profile, "remotePath", "path") ?? string.Empty;
        string? webDavAddress = GetOption(profile, "webDavAddress", "dav_address");
        string uri = protocol is "webdav" or "webdavs" && !string.IsNullOrWhiteSpace(webDavAddress)
            ? BuildWebDavSessionUri(scheme, webDavAddress, authentication, containsSensitiveData)
            : BuildSessionUri(scheme, profile, authentication, containsSensitiveData, remotePath);
        return new LaunchPlan(executablePath, [uri], containsSensitiveData: containsSensitiveData);
    }

    /// <summary>
    /// Builds SecureCRT SSH1, SSH2, or Telnet arguments. / 构建 SecureCRT SSH1、SSH2 或 Telnet 参数。
    /// </summary>
    /// <param name="executablePath">Resolved SecureCRT executable. / 已解析的 SecureCRT 程序。</param>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="authentication">Connection authentication values. / 连接认证值。</param>
    /// <param name="protocol">Normalized protocol. / 规范化协议。</param>
    /// <param name="allowPassword">Whether current settings permit password arguments. / 当前设置是否允许密码参数。</param>
    /// <returns>SecureCRT launch plan. / SecureCRT 启动计划。</returns>
    private static LaunchPlan BuildSecureCrtPlan(
        string executablePath,
        ConnectionProfile profile,
        AuthenticationValues authentication,
        string protocol,
        bool allowPassword)
    {
        string target = protocol == "telnet" || string.IsNullOrWhiteSpace(authentication.Username)
            ? profile.Host.Trim()
            : $"{authentication.Username}@{profile.Host.Trim()}";
        List<string> arguments = [protocol == "telnet" ? "/TELNET" : protocol == "ssh1" ? "/SSH1" : "/SSH2", target];
        arguments.Add("/P");
        arguments.Add(profile.Port.ToString(CultureInfo.InvariantCulture));

        bool containsSensitiveData = protocol != "telnet" && allowPassword && !string.IsNullOrEmpty(authentication.Password);
        if (containsSensitiveData)
        {
            arguments.Add("/PASSWORD");
            arguments.Add(authentication.Password);
        }

        return new LaunchPlan(executablePath, arguments, containsSensitiveData: containsSensitiveData);
    }

    /// <summary>
    /// Builds a safely quoted MobaXterm inner command for SSH or Telnet. / 为 SSH 或 Telnet 构建安全引用的 MobaXterm 内部命令。
    /// </summary>
    /// <param name="executablePath">Resolved MobaXterm executable. / 已解析的 MobaXterm 程序。</param>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="authentication">Connection authentication values. / 连接认证值。</param>
    /// <param name="protocol">Normalized protocol. / 规范化协议。</param>
    /// <param name="allowPassword">Whether current settings permit password arguments. / 当前设置是否允许密码参数。</param>
    /// <returns>MobaXterm launch plan. / MobaXterm 启动计划。</returns>
    private static LaunchPlan BuildMobaXtermPlan(
        string executablePath,
        ConnectionProfile profile,
        AuthenticationValues authentication,
        string protocol,
        bool allowPassword)
    {
        string port = profile.Port.ToString(CultureInfo.InvariantCulture);
        string command;
        bool containsSensitiveData = false;

        if (protocol == "telnet")
        {
            command = $"telnet {QuotePosixShell(profile.Host.Trim())} {QuotePosixShell(port)}";
        }
        else
        {
            string target = string.IsNullOrWhiteSpace(authentication.Username)
                ? profile.Host.Trim()
                : $"{authentication.Username}@{profile.Host.Trim()}";
            string? keyPath = ResolvePrivateKeyPath(profile.PrivateKeyPath);

            if (keyPath is not null)
            {
                command = $"ssh -i {QuotePosixShell(keyPath.Replace('\\', '/'))} {QuotePosixShell(target)} -p {QuotePosixShell(port)}";
            }
            else if (allowPassword && !string.IsNullOrEmpty(authentication.Password))
            {
                command = $"sshpass -p {QuotePosixShell(authentication.Password)} ssh {QuotePosixShell(target)} -p {QuotePosixShell(port)}";
                containsSensitiveData = true;
            }
            else
            {
                command = $"ssh {QuotePosixShell(target)} -p {QuotePosixShell(port)}";
            }
        }

        return new LaunchPlan(executablePath, ["-newtab", command], containsSensitiveData: containsSensitiveData);
    }

    /// <summary>
    /// Builds TightVNC, RealVNC, or UltraVNC arguments. / 构建 TightVNC、RealVNC 或 UltraVNC 参数。
    /// </summary>
    /// <param name="executablePath">Resolved VNC viewer executable. / 已解析的 VNC 查看器程序。</param>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="authentication">Connection authentication values. / 连接认证值。</param>
    /// <param name="protocol">Normalized VNC implementation. / 规范化的 VNC 实现。</param>
    /// <param name="allowPassword">Whether current settings permit password arguments. / 当前设置是否允许密码参数。</param>
    /// <returns>VNC launch plan. / VNC 启动计划。</returns>
    private static LaunchPlan BuildVncPlan(
        string executablePath,
        ConnectionProfile profile,
        AuthenticationValues authentication,
        string protocol,
        bool allowPassword)
    {
        string endpoint = FormatVncEndpoint(profile.Host, profile.Port);
        List<string> arguments = protocol == "ultravnc" ? ["-connect", endpoint] : [endpoint];
        bool containsSensitiveData = false;

        if (protocol == "tightvnc" && allowPassword && !string.IsNullOrEmpty(authentication.Password))
        {
            arguments.Add($"-password={authentication.Password}");
            containsSensitiveData = true;
        }
        else if (protocol == "ultravnc")
        {
            if (allowPassword && !string.IsNullOrEmpty(authentication.Password))
            {
                arguments.Add("/password");
                arguments.Add(authentication.Password);
                containsSensitiveData = true;
            }

            if (GetBooleanOption(profile, false, "fullscreen", "fullScreen")) arguments.Add("/fullscreen");
            if (GetBooleanOption(profile, true, "autoReconnect", "autoreconnect"))
            {
                arguments.Add("/autoreconnect");
                arguments.Add("10s");
            }

            if (GetBooleanOption(profile, false, "viewOnly", "viewonly")) arguments.Add("/viewonly");
            arguments.Add("/autoscaling");
        }

        return new LaunchPlan(executablePath, arguments, containsSensitiveData: containsSensitiveData);
    }

    /// <summary>
    /// Builds Radmin mode and display arguments without automating its credential dialog. / 构建 Radmin 模式及显示参数，但不自动操作其凭据对话框。
    /// </summary>
    /// <param name="executablePath">Resolved Radmin executable. / 已解析的 Radmin 程序。</param>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="protocol">Normalized Radmin mode. / 规范化的 Radmin 模式。</param>
    /// <returns>Radmin launch plan. / Radmin 启动计划。</returns>
    private static LaunchPlan BuildRadminPlan(string executablePath, ConnectionProfile profile, string protocol)
    {
        List<string> arguments = [$"/connect:{FormatHostPort(profile.Host, profile.Port)}"];
        string? modeSwitch = protocol switch
        {
            "view" => "/noinput",
            "telnet" => "/telnet",
            "file" => "/file",
            "shutdown" => "/shutdown",
            "chat" => "/chat",
            "voice" => "/voice",
            "message" => "/message",
            _ => null
        };

        if (modeSwitch is not null) arguments.Add(modeSwitch);
        if (GetBooleanOption(profile, false, "encrypt")) arguments.Add("/encrypt");
        if (protocol is "control" or "view")
        {
            if (GetBooleanOption(profile, false, "fullscreen", "fullScreen")) arguments.Add("/fullscreen");
            if (GetBooleanOption(profile, false, "noFullKeyboardControl", "nofullkbcontrol")) arguments.Add("/nofullkbcontrol");

            string colorDepth = (GetOption(profile, "colorDepth", "colorMode", "color_mode") ?? "24bpp").Trim().ToLowerInvariant();
            if (colorDepth is not ("24bpp" or "16bpp" or "8bpp" or "4bpp" or "2bpp" or "1bpp"))
            {
                throw new LaunchValidationException($"Invalid Radmin color depth '{colorDepth}'. / Radmin 色深“{colorDepth}”无效。");
            }

            int updates = GetIntegerOption(profile, 30, 1, 100, "updates", "frameRate");
            arguments.Add($"/{colorDepth}");
            arguments.Add($"/updates:{updates.ToString(CultureInfo.InvariantCulture)}");
        }

        return new LaunchPlan(executablePath, arguments);
    }

    /// <summary>
    /// Builds ToDesk control arguments and omits the password when current settings disallow it. / 构建 ToDesk 控制参数，并在当前设置禁止时省略密码。
    /// </summary>
    /// <param name="executablePath">Resolved ToDesk executable. / 已解析的 ToDesk 程序。</param>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="authentication">Connection authentication values. / 连接认证值。</param>
    /// <param name="allowPassword">Whether current settings permit password arguments. / 当前设置是否允许密码参数。</param>
    /// <returns>ToDesk launch plan. / ToDesk 启动计划。</returns>
    private static LaunchPlan BuildToDeskPlan(
        string executablePath,
        ConnectionProfile profile,
        AuthenticationValues authentication,
        bool allowPassword)
    {
        string deviceId = NormalizeToDeskDeviceId(profile.Host);
        List<string> arguments = ["-control", "-id", deviceId];
        bool containsSensitiveData = allowPassword && !string.IsNullOrEmpty(authentication.Password);
        if (containsSensitiveData)
        {
            arguments.Add("-passwd");
            arguments.Add(authentication.Password);
        }

        return new LaunchPlan(executablePath, arguments, containsSensitiveData: containsSensitiveData);
    }

    /// <summary>
    /// Builds a RustDesk connection plan using the documented executable argument shape. / 使用 RustDesk 程序参数格式构建连接计划。
    /// </summary>
    /// <param name="executablePath">Resolved RustDesk executable. / 已解析的 RustDesk 程序。</param>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="authentication">Connection authentication values. / 连接认证值。</param>
    /// <param name="protocol">Normalized RustDesk connection mode. / 规范化的 RustDesk 连接模式。</param>
    /// <param name="allowPassword">Whether current settings permit password arguments. / 当前设置是否允许密码参数。</param>
    /// <returns>RustDesk launch plan. / RustDesk 启动计划。</returns>
    private static LaunchPlan BuildRustDeskPlan(
        string executablePath,
        ConnectionProfile profile,
        AuthenticationValues authentication,
        string protocol,
        bool allowPassword)
    {
        string target = BuildRustDeskTarget(profile, out bool hasServerKey);
        List<string> arguments = [$"--{protocol}", target];

        // RustDesk 1.4.9 appends a second '?' when an ID containing ?key= is combined with
        // --password. Omit auto-login in that case so the official client can prompt safely.
        bool containsSensitiveData = allowPassword && !hasServerKey && !string.IsNullOrEmpty(authentication.Password);
        if (containsSensitiveData)
        {
            arguments.Add("--password");
            arguments.Add(authentication.Password);
        }

        if (GetBooleanOption(profile, false, "forceRelay", "relay", "force_relay"))
        {
            arguments.Add("--relay");
        }

        return new LaunchPlan(executablePath, arguments, containsSensitiveData: containsSensitiveData);
    }

    /// <summary>
    /// Replaces supported custom placeholders inside one already-tokenized argument. / 在一个已拆分的参数标记内替换支持的自定义占位符。
    /// </summary>
    /// <param name="token">Argument token. / 参数标记。</param>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="authentication">Connection authentication values. / 连接认证值。</param>
    /// <param name="privateKeyPath">Resolved private-key path when requested. / 请求时已解析的私钥路径。</param>
    /// <param name="allowPassword">Whether password replacement is allowed. / 是否允许替换密码。</param>
    /// <returns>Token with safe value substitution. / 安全替换值后的标记。</returns>
    private static string ReplaceCustomPlaceholders(
        string token,
        ConnectionProfile profile,
        AuthenticationValues authentication,
        string privateKeyPath,
        bool allowPassword)
    {
        return Regex.Replace(
            token,
            @"\{(username|password|ip|host|port|key)\}",
            match => match.Groups[1].Value.ToLowerInvariant() switch
            {
                "username" => authentication.Username,
                "password" when allowPassword => authentication.Password,
                "ip" or "host" => profile.Host ?? string.Empty,
                "port" => profile.Port.ToString(CultureInfo.InvariantCulture),
                "key" => privateKeyPath,
                _ => match.Value
            },
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Validates client-independent host, port, and string safety constraints. / 验证与客户端无关的主机、端口和字符串安全约束。
    /// </summary>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    private static void ValidateProfile(ConnectionProfile profile)
    {
        ValidateNoNullCharacter(profile.Name, "connection name / 连接名称");
        ValidateNoNullCharacter(profile.Host, "host or device ID / 主机或设备 ID");
        ValidateNoNullCharacter(profile.Username, "username / 用户名");
        ValidateNoNullCharacter(profile.Password, "password / 密码");
        ValidateNoNullCharacter(profile.PrivateKeyPath, "private-key path / 私钥路径");
        ValidateNoNullCharacter(profile.ExecutableOverride, "executable override / 可执行程序覆盖路径");
        ValidateNoNullCharacter(profile.CustomArguments, "custom arguments / 自定义参数");

        if (profile.Type == ConnectionType.Custom)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(profile.Host))
        {
            throw new LaunchValidationException("Host or device ID is required. / 主机或设备 ID 不能为空。");
        }

        if (profile.Host.Any(character => char.IsControl(character)))
        {
            throw new LaunchValidationException("Host or device ID contains a control character. / 主机或设备 ID 包含控制字符。");
        }

        if (profile.Type != ConnectionType.ToDesk)
        {
            string host = profile.Host.Trim();
            bool formattedRustDeskId = profile.Type == ConnectionType.RustDesk &&
                                       host.All(character => character is >= '0' and <= '9' || char.IsWhiteSpace(character));
            if (host.StartsWith("-", StringComparison.Ordinal) ||
                (!formattedRustDeskId && host.Any(char.IsWhiteSpace)) ||
                host.IndexOfAny(['/', '\\', '@', '?', '#']) >= 0)
            {
                throw new LaunchValidationException($"Host '{profile.Host}' is not valid for a client argument. / 主机“{profile.Host}”不能安全用作客户端参数。");
            }
        }

        if (profile.Type is not (ConnectionType.ToDesk or ConnectionType.RustDesk) && (profile.Port is < 1 or > 65535))
        {
            throw new LaunchValidationException($"Port '{profile.Port}' must be between 1 and 65535. / 端口“{profile.Port}”必须位于 1 到 65535 之间。");
        }
    }

    /// <summary>
    /// Rejects null characters in connection authentication values. / 拒绝连接认证值中的空字符。
    /// </summary>
    /// <param name="authentication">Connection authentication values. / 连接认证值。</param>
    private static void ValidateAuthenticationValues(AuthenticationValues authentication)
    {
        ValidateNoNullCharacter(authentication.Username, "username / 用户名");
        ValidateNoNullCharacter(authentication.Password, "password / 密码");
    }

    /// <summary>
    /// Rejects embedded null characters that Windows process and file APIs cannot represent safely. / 拒绝 Windows 进程和文件 API 无法安全表示的内嵌空字符。
    /// </summary>
    /// <param name="value">Value to inspect. / 要检查的值。</param>
    /// <param name="fieldName">Bilingual field description. / 双语字段说明。</param>
    private static void ValidateNoNullCharacter(string? value, string fieldName)
    {
        if (value?.Contains('\0') == true)
        {
            throw new LaunchValidationException($"The {fieldName} contains a null character. / {fieldName}包含空字符。");
        }
    }

    /// <summary>
    /// Normalizes and validates the selected protocol for its client type. / 为客户端类型规范化并验证所选协议。
    /// </summary>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <returns>Stable lowercase protocol identifier. / 稳定的小写协议标识。</returns>
    private static string NormalizeProtocol(ConnectionProfile profile)
    {
        IReadOnlyList<string> supported = profile.Type.GetProtocols();
        if (profile.Type == ConnectionType.Custom)
        {
            return string.Empty;
        }

        if (supported.Count == 0)
        {
            throw new LaunchValidationException(
                $"No protocols are defined for {profile.Type.ToDisplayName()}. / {profile.Type.ToDisplayName()} 未定义可用协议。");
        }

        string protocol = profile.Type.NormalizeProtocol(profile.Protocol);
        if (!supported.Contains(protocol, StringComparer.OrdinalIgnoreCase))
        {
            throw new LaunchValidationException(
                $"Protocol '{profile.Protocol}' is not supported by {profile.Type.ToDisplayName()}. / {profile.Type.ToDisplayName()} 不支持协议“{profile.Protocol}”。");
        }

        return protocol;
    }

    /// <summary>
    /// Resolves and validates an optional private-key file relative to the application directory. / 相对于应用目录解析并验证可选私钥文件。
    /// </summary>
    /// <param name="path">Configured private-key path. / 已配置的私钥路径。</param>
    /// <returns>Full path, or null when no key is configured. / 完整路径；未配置私钥时返回 null。</returns>
    private static string? ResolvePrivateKeyPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        string fullPath;
        try
        {
            fullPath = Path.IsPathRooted(expanded)
                ? Path.GetFullPath(expanded)
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, expanded));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new LaunchValidationException($"Private-key path is invalid: {path}. / 私钥路径无效：{path}。", exception);
        }

        if (!File.Exists(fullPath))
        {
            throw new LaunchValidationException($"Private-key file does not exist: {fullPath}. / 私钥文件不存在：{fullPath}。");
        }

        return fullPath;
    }

    /// <summary>
    /// Determines whether a URL-based client can include a password under current policy. / 判断 URL 型客户端在当前策略下是否可以包含密码。
    /// </summary>
    /// <param name="authentication">Connection authentication values. / 连接认证值。</param>
    /// <param name="allowPassword">Whether current settings permit password arguments. / 当前设置是否允许密码参数。</param>
    /// <returns>True when username and password may be embedded. / 可以嵌入用户名和密码时返回 true。</returns>
    private static bool CanEmbedPassword(AuthenticationValues authentication, bool allowPassword)
    {
        return allowPassword && !string.IsNullOrWhiteSpace(authentication.Username) && !string.IsNullOrEmpty(authentication.Password);
    }

    /// <summary>
    /// Builds a percent-encoded client session URI with an optional remote path. / 构建带可选远程路径且经过百分号编码的客户端会话 URI。
    /// </summary>
    /// <param name="scheme">Client URI scheme. / 客户端 URI 方案。</param>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="authentication">Connection authentication values. / 连接认证值。</param>
    /// <param name="includePassword">Whether current settings permit including the password. / 当前设置是否允许包含密码。</param>
    /// <param name="remotePath">Optional remote path. / 可选远程路径。</param>
    /// <returns>Session URI passed as one argument token. / 作为单一参数标记传递的会话 URI。</returns>
    private static string BuildSessionUri(
        string scheme,
        ConnectionProfile profile,
        AuthenticationValues authentication,
        bool includePassword,
        string remotePath = "")
    {
        return BuildSessionUri(scheme, profile.Host, profile.Port, authentication, includePassword, remotePath);
    }

    /// <summary>Builds a session URI from an explicit validated host and port. / 使用明确且已验证的主机与端口构建会话 URI。</summary>
    private static string BuildSessionUri(
        string scheme,
        string hostValue,
        int port,
        AuthenticationValues authentication,
        bool includePassword,
        string remotePath = "")
    {
        string userInfo = string.Empty;
        if (!string.IsNullOrWhiteSpace(authentication.Username))
        {
            userInfo = Uri.EscapeDataString(authentication.Username);
            if (includePassword)
            {
                userInfo += $":{Uri.EscapeDataString(authentication.Password)}";
            }

            userInfo += "@";
        }

        string host = FormatUriHost(hostValue);
        string path = EscapeRemotePath(remotePath);
        return $"{scheme}://{userInfo}{host}:{port.ToString(CultureInfo.InvariantCulture)}{path}";
    }

    /// <summary>
    /// Converts the reference manager's complete HTTP(S)/DAV(S) WebDAV address into a validated WinSCP DAV URI.
    /// / 将参考管理器使用的完整 HTTP(S)/DAV(S) WebDAV 地址转换为经过验证的 WinSCP DAV URI。
    /// </summary>
    private static string BuildWebDavSessionUri(
        string targetScheme,
        string configuredAddress,
        AuthenticationValues authentication,
        bool includePassword)
    {
        if (configuredAddress.Any(character => char.IsControl(character) || character is '\u2028' or '\u2029') ||
            !Uri.TryCreate(configuredAddress.Trim(), UriKind.Absolute, out Uri? address) ||
            string.IsNullOrWhiteSpace(address.Host))
        {
            throw new LaunchValidationException("The WinSCP WebDAV address is not a valid absolute URI. / WinSCP WebDAV 地址不是有效的绝对 URI。");
        }

        bool secure = targetScheme == "davs";
        bool schemeMatches = secure
            ? address.Scheme is "https" or "davs"
            : address.Scheme is "http" or "dav";
        if (!schemeMatches)
        {
            throw new LaunchValidationException("The WinSCP WebDAV address scheme does not match the selected HTTP/HTTPS mode. / WinSCP WebDAV 地址协议与所选 HTTP/HTTPS 模式不匹配。");
        }

        if (!string.IsNullOrEmpty(address.UserInfo) || !string.IsNullOrEmpty(address.Fragment))
        {
            throw new LaunchValidationException("The WinSCP WebDAV address must not contain embedded credentials or a fragment. / WinSCP WebDAV 地址不能包含内嵌凭据或片段。");
        }

        int port = address.Port > 0 ? address.Port : secure ? 443 : 80;
        string remotePath = address.GetComponents(UriComponents.Path, UriFormat.Unescaped);
        string sessionUri = BuildSessionUri(targetScheme, address.DnsSafeHost, port, authentication, includePassword, remotePath);
        return string.IsNullOrEmpty(address.Query) ? sessionUri : sessionUri + address.Query;
    }

    /// <summary>
    /// Formats an IPv6 or DNS host for use inside a URI authority. / 为 URI 权限部分格式化 IPv6 或 DNS 主机。
    /// </summary>
    /// <param name="host">Validated host. / 已验证的主机。</param>
    /// <returns>URI-safe host text. / URI 安全的主机文本。</returns>
    private static string FormatUriHost(string host)
    {
        string trimmed = host.Trim();
        if (IPAddress.TryParse(trimmed, out IPAddress? address))
        {
            return address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{trimmed}]" : trimmed;
        }

        try
        {
            return new IdnMapping().GetAscii(trimmed);
        }
        catch (ArgumentException exception)
        {
            throw new LaunchValidationException($"Host '{host}' is not a valid URI host. / 主机“{host}”不是有效的 URI 主机。", exception);
        }
    }

    /// <summary>
    /// Percent-encodes an optional slash-delimited remote path. / 对可选的斜杠分隔远程路径进行百分号编码。
    /// </summary>
    /// <param name="remotePath">Untrusted remote path. / 不可信的远程路径。</param>
    /// <returns>Empty text or an encoded absolute path. / 空文本或已编码的绝对路径。</returns>
    private static string EscapeRemotePath(string? remotePath)
    {
        if (string.IsNullOrWhiteSpace(remotePath))
        {
            return string.Empty;
        }

        string[] segments = remotePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return "/" + string.Join('/', segments.Select(Uri.EscapeDataString));
    }

    /// <summary>
    /// Formats a host and port for non-URI client arguments while preserving IPv6. / 为非 URI 客户端参数格式化主机和端口并保留 IPv6。
    /// </summary>
    /// <param name="host">Validated host. / 已验证的主机。</param>
    /// <param name="port">TCP port. / TCP 端口。</param>
    /// <returns>Host and port endpoint. / 主机和端口端点。</returns>
    private static string FormatHostPort(string host, int port)
    {
        string trimmed = host.Trim();
        if (IPAddress.TryParse(trimmed, out IPAddress? address) && address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            trimmed = $"[{trimmed}]";
        }

        return $"{trimmed}:{port.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Formats an explicit VNC TCP port with double-colon syntax to avoid display-number conversion. / 使用双冒号语法格式化显式 VNC TCP 端口，避免被换算为显示器编号。
    /// </summary>
    /// <param name="host">Validated VNC host. / 已验证的 VNC 主机。</param>
    /// <param name="port">Explicit VNC TCP port. / 显式 VNC TCP 端口。</param>
    /// <returns>Viewer-compatible host and port. / 与查看器兼容的主机和端口。</returns>
    private static string FormatVncEndpoint(string host, int port)
    {
        string trimmed = host.Trim();
        if (IPAddress.TryParse(trimmed, out IPAddress? address) && address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            trimmed = $"[{trimmed}]";
        }

        return $"{trimmed}::{port.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Quotes one dynamic value for the POSIX-like shell command interpreted by MobaXterm. / 为 MobaXterm 解释的类 POSIX 外壳命令引用一个动态值。
    /// </summary>
    /// <param name="value">Dynamic command value. / 动态命令值。</param>
    /// <returns>Single-quoted shell literal. / 单引号外壳字面量。</returns>
    private static string QuotePosixShell(string value)
    {
        return $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
    }

    /// <summary>
    /// Removes all whitespace from a ToDesk device identifier and validates the result. / 从 ToDesk 设备标识中移除全部空白并验证结果。
    /// </summary>
    /// <param name="host">Raw device identifier. / 原始设备标识。</param>
    /// <returns>Normalized device identifier. / 规范化设备标识。</returns>
    private static string NormalizeToDeskDeviceId(string host)
    {
        string deviceId = new(host.Where(character => !char.IsWhiteSpace(character)).ToArray());
        if (string.IsNullOrWhiteSpace(deviceId) || deviceId.StartsWith("-", StringComparison.Ordinal))
        {
            throw new LaunchValidationException("ToDesk device ID is invalid. / ToDesk 设备 ID 无效。");
        }

        return deviceId;
    }

    /// <summary>
    /// Builds a RustDesk peer target from the profile host and optional self-hosted server settings. / 使用配置主机及可选自建服务器设置构建 RustDesk 对端目标。
    /// </summary>
    /// <param name="profile">RustDesk connection profile. / RustDesk 连接配置。</param>
    /// <param name="hasServerKey">Whether the target contains a server public key. / 目标是否包含服务器公钥。</param>
    /// <returns>Validated RustDesk target. / 已验证的 RustDesk 目标。</returns>
    private static string BuildRustDeskTarget(ConnectionProfile profile, out bool hasServerKey)
    {
        string target = NormalizeRustDeskPeerTarget(profile.Host);
        string? serverOption = GetOption(profile, "server");
        string? keyOption = GetOption(profile, "serverKey", "server_key");
        ValidateRustDeskOptionControls(serverOption, "server / 服务器");
        ValidateRustDeskOptionControls(keyOption, "server key / 服务器公钥");
        string server = string.IsNullOrWhiteSpace(serverOption) ? string.Empty : serverOption.Trim();
        string serverKey = string.IsNullOrWhiteSpace(keyOption) ? string.Empty : keyOption.Trim();

        if (serverKey.Length > 0 && server.Length == 0)
        {
            throw new LaunchValidationException(
                "A RustDesk server key requires a self-hosted server address. / RustDesk 服务器公钥必须与自建服务器地址一起设置。");
        }

        if (server.Length > 0)
        {
            ValidateRustDeskServer(server);
            target = $"{target}@{server}";
        }

        hasServerKey = serverKey.Length > 0;
        if (hasServerKey)
        {
            ValidateRustDeskServerKey(serverKey);
            target = $"{target}?key={Uri.EscapeDataString(serverKey)}";
        }

        return target;
    }

    /// <summary>Removes display whitespace from a numeric RustDesk ID while preserving direct-address targets. / 从数字 RustDesk ID 中移除显示空白，同时保留直连地址目标。</summary>
    private static string NormalizeRustDeskPeerTarget(string value)
    {
        string target = value.Trim();
        return target.All(character => character is >= '0' and <= '9' || char.IsWhiteSpace(character))
            ? string.Concat(target.Where(character => character is >= '0' and <= '9'))
            : target;
    }

    /// <summary>
    /// Rejects control and Unicode line-separator characters before optional values are trimmed. / 在修剪可选值前拒绝控制字符及 Unicode 行分隔符。
    /// </summary>
    /// <param name="value">Raw option value. / 原始选项值。</param>
    /// <param name="fieldName">Bilingual field description. / 双语字段说明。</param>
    private static void ValidateRustDeskOptionControls(string? value, string fieldName)
    {
        if (value?.Any(character => char.IsControl(character) || character is '\u2028' or '\u2029') == true)
        {
            throw new LaunchValidationException(
                $"RustDesk {fieldName} contains a control character. / RustDesk {fieldName}包含控制字符。");
        }
    }

    /// <summary>
    /// Rejects RustDesk server values that could alter the target grammar. / 拒绝可能改变目标语法的 RustDesk 服务器值。
    /// </summary>
    /// <param name="server">Self-hosted server address. / 自建服务器地址。</param>
    private static void ValidateRustDeskServer(string server)
    {
        if (server.StartsWith("-", StringComparison.Ordinal) ||
            server.Any(character => char.IsControl(character) || char.IsWhiteSpace(character) || character is '\u2028' or '\u2029') ||
            server.IndexOfAny(['@', '?', '#', '&', '=', '/', '\\']) >= 0)
        {
            throw new LaunchValidationException(
                $"RustDesk server '{server}' contains an unsafe separator or control character. / RustDesk 服务器“{server}”包含不安全的分隔符或控制字符。");
        }
    }

    /// <summary>
    /// Validates a RustDesk public key before it is percent-encoded into a target. / 在将 RustDesk 公钥百分号编码到目标前进行验证。
    /// </summary>
    /// <param name="serverKey">RustDesk server public key. / RustDesk 服务器公钥。</param>
    private static void ValidateRustDeskServerKey(string serverKey)
    {
        if (serverKey.Any(character =>
                char.IsControl(character) ||
                char.IsWhiteSpace(character) ||
                character is '\u2028' or '\u2029' or '?' or '#' or '&' or '@' or '\\'))
        {
            throw new LaunchValidationException(
                "RustDesk server key contains an unsafe separator or control character. / RustDesk 服务器公钥包含不安全的分隔符或控制字符。");
        }
    }

    /// <summary>
    /// Reads the first matching client option using a case-insensitive fallback. / 使用不区分大小写的回退方式读取首个匹配的客户端选项。
    /// </summary>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="keys">Accepted option keys. / 可接受的选项键。</param>
    /// <returns>Option value or null. / 选项值或 null。</returns>
    private static string? GetOption(ConnectionProfile profile, params string[] keys)
    {
        Dictionary<string, string>? options = profile.Options;
        if (options is null)
        {
            return null;
        }

        foreach (string key in keys)
        {
            if (options.TryGetValue(key, out string? direct))
            {
                return direct;
            }

            KeyValuePair<string, string> pair = options.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(pair.Key))
            {
                return pair.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads a Boolean client option with explicit accepted values. / 使用明确接受的值读取布尔客户端选项。
    /// </summary>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="defaultValue">Value used when the option is absent. / 选项不存在时使用的值。</param>
    /// <param name="keys">Accepted option keys. / 可接受的选项键。</param>
    /// <returns>Parsed Boolean value. / 解析后的布尔值。</returns>
    private static bool GetBooleanOption(ConnectionProfile profile, bool defaultValue, params string[] keys)
    {
        string? value = GetOption(profile, keys);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => throw new LaunchValidationException($"Boolean option '{keys[0]}' has invalid value '{value}'. / 布尔选项“{keys[0]}”的值“{value}”无效。")
        };
    }

    /// <summary>
    /// Reads and range-checks an integer client option. / 读取并检查整数客户端选项范围。
    /// </summary>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="defaultValue">Value used when absent. / 不存在时使用的值。</param>
    /// <param name="minimum">Inclusive minimum. / 包含的最小值。</param>
    /// <param name="maximum">Inclusive maximum. / 包含的最大值。</param>
    /// <param name="keys">Accepted option keys. / 可接受的选项键。</param>
    /// <returns>Validated integer value. / 验证后的整数值。</returns>
    private static int GetIntegerOption(
        ConnectionProfile profile,
        int defaultValue,
        int minimum,
        int maximum,
        params string[] keys)
    {
        string? value = GetOption(profile, keys);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) || parsed < minimum || parsed > maximum)
        {
            throw new LaunchValidationException(
                $"Integer option '{keys[0]}' must be between {minimum} and {maximum}. / 整数选项“{keys[0]}”必须位于 {minimum} 到 {maximum} 之间。");
        }

        return parsed;
    }

    /// <summary>
    /// Best-effort removes only stale RDP files whose names prove they were created by this application. / 尽力仅删除文件名可证明由本应用创建的陈旧 RDP 文件。
    /// </summary>
    /// <param name="directory">Dedicated application-owned RDP directory. / 应用专用的 RDP 目录。</param>
    private static void CleanupStaleOwnedRdpFiles(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            string expectedParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
            DateTime staleBeforeUtc = DateTime.UtcNow.Subtract(StaleRdpFileAge);
            foreach (string candidate in Directory.EnumerateFiles(expectedParent, "*.rdp", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    string fullPath = Path.GetFullPath(candidate);
                    string? parent = Path.GetDirectoryName(fullPath);
                    if (parent is null ||
                        !string.Equals(
                            Path.TrimEndingDirectorySeparator(parent),
                            expectedParent,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string fileName = Path.GetFileName(fullPath);
                    if (!OwnedRdpFileNamePattern.IsMatch(fileName) || File.GetLastWriteTimeUtc(fullPath) >= staleBeforeUtc)
                    {
                        continue;
                    }

                    File.Delete(fullPath);
                }
                catch
                {
                    // Cleanup is deliberately best-effort and must never block application startup. / 清理刻意为尽力执行，绝不应阻止应用启动。
                }
            }
        }
        catch
        {
            // Directory access can fail transiently; a later startup may retry safely. / 目录访问可能暂时失败，后续启动可安全重试。
        }
    }

    /// <summary>
    /// Deletes temporary files immediately when process startup fails. / 在进程启动失败时立即删除临时文件。
    /// </summary>
    /// <param name="paths">Temporary file paths. / 临时文件路径。</param>
    private static void DeleteTemporaryFiles(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// Holds connection authentication values without mutating domain models. / 保存连接认证值且不修改领域模型。
    /// </summary>
    private sealed class AuthenticationValues
    {
        /// <summary>
        /// Initializes connection authentication values. / 初始化连接认证值。
        /// </summary>
        /// <param name="username">Resolved username. / 已解析的用户名。</param>
        /// <param name="password">Resolved password. / 已解析的密码。</param>
        public AuthenticationValues(string username, string password)
        {
            Username = username;
            Password = password;
        }

        /// <summary>Gets the resolved username. / 获取已解析的用户名。</summary>
        public string Username { get; }

        /// <summary>Gets the resolved password. / 获取已解析的密码。</summary>
        public string Password { get; }
    }
}
