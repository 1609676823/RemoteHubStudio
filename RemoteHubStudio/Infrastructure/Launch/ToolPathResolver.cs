using RemoteHubStudio.Domain;

namespace RemoteHubStudio.Infrastructure.Launch;

/// <summary>
/// Resolves built-in and configured client executables without shell lookup. / 在不依赖命令行外壳查找的情况下解析内置及已配置的客户端程序。
/// </summary>
internal static class ToolPathResolver
{
    /// <summary>
    /// Resolves the executable for a connection profile using override, settings, and trusted search locations. / 依次使用配置覆盖、应用设置和可信搜索位置解析连接程序。
    /// </summary>
    /// <param name="profile">Connection profile being launched. / 正在启动的连接配置。</param>
    /// <param name="settings">Current application settings. / 当前应用设置。</param>
    /// <param name="normalizedProtocol">Normalized protocol used to select protocol-specific clients. / 用于选择协议专用客户端的规范化协议。</param>
    /// <returns>Existing executable path. / 存在的可执行文件路径。</returns>
    /// <exception cref="LaunchValidationException">Thrown when no executable can be resolved. / 无法解析可执行文件时抛出。</exception>
    public static string Resolve(ConnectionProfile profile, AppSettings settings, string normalizedProtocol)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(settings);

        if (!string.IsNullOrWhiteSpace(profile.ExecutableOverride))
        {
            return ResolveRequiredCandidate(profile.ExecutableOverride, "profile executable override / 配置专属程序路径");
        }

        if (profile.Type == ConnectionType.RemoteDesktop)
        {
            string systemMstsc = Path.Combine(Environment.SystemDirectory, "mstsc.exe");
            if (File.Exists(systemMstsc))
            {
                return systemMstsc;
            }
        }

        string key = profile.Type == ConnectionType.Custom ? "custom" : profile.Type.GetToolPathKey(normalizedProtocol);
        string? configuredPath = FindConfiguredPath(settings.ToolPaths, key);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return ResolveRequiredCandidate(configuredPath, $"tool setting '{key}' / 工具设置“{key}”");
        }

        string? legacyVncMismatch = null;
        if (profile.Type == ConnectionType.Vnc)
        {
            string? legacyVncPath = FindConfiguredPath(settings.ToolPaths, "vnc");
            if (!string.IsNullOrWhiteSpace(legacyVncPath))
            {
                string expectedExecutable = GetExpectedVncExecutableName(normalizedProtocol);
                if (HasExpectedExecutableName(legacyVncPath, expectedExecutable))
                {
                    return ResolveRequiredCandidate(
                        legacyVncPath,
                        $"legacy VNC tool setting for '{normalizedProtocol}' / 用于“{normalizedProtocol}”的旧版 VNC 工具设置");
                }

                legacyVncMismatch =
                    $"The legacy VNC tool setting was ignored because protocol '{normalizedProtocol}' requires '{expectedExecutable}'. " +
                    $"Configure tool setting '{key}' with the matching viewer. / 已忽略旧版 VNC 工具设置，因为协议“{normalizedProtocol}”必须使用“{expectedExecutable}”。" +
                    $"请为工具设置“{key}”选择匹配的查看器。";
            }
        }

        foreach (string candidate in GetConventionalExecutableNames(profile.Type, normalizedProtocol))
        {
            string? resolved = TryResolveCandidate(candidate);
            if (resolved is not null)
            {
                RejectRemoteExecutableCandidate(resolved, "trusted executable search path / 可信程序搜索路径");
                return resolved;
            }
        }

        string displayName = profile.Type.ToDisplayName();
        if (legacyVncMismatch is not null)
        {
            throw new LaunchValidationException(legacyVncMismatch);
        }

        throw new LaunchValidationException(
            $"Executable for {displayName} is not configured or installed on PATH. / 未配置 {displayName} 的程序路径，PATH 中也未找到该程序。");
    }

    /// <summary>
    /// Gets the only executable name accepted for a normalized VNC implementation. / 获取规范化 VNC 实现唯一允许的可执行文件名。
    /// </summary>
    /// <param name="normalizedProtocol">Normalized VNC protocol identifier. / 规范化的 VNC 协议标识。</param>
    /// <returns>Expected viewer executable file name. / 预期的查看器可执行文件名。</returns>
    private static string GetExpectedVncExecutableName(string normalizedProtocol) => normalizedProtocol switch
    {
        "realvnc" => "vncviewer.exe",
        "ultravnc" => "uvncviewer.exe",
        _ => "tvnviewer.exe"
    };

    /// <summary>
    /// Safely checks whether a legacy configured path names the exact viewer required by a protocol. / 安全检查旧版配置路径是否指定协议所需的精确查看器。
    /// </summary>
    /// <param name="candidate">Legacy configured executable path or name. / 旧版配置的可执行文件路径或名称。</param>
    /// <param name="expectedExecutable">Required executable file name. / 所需的可执行文件名。</param>
    /// <returns>True only for an exact case-insensitive file-name match. / 仅当文件名不区分大小写精确匹配时返回 true。</returns>
    private static bool HasExpectedExecutableName(string candidate, string expectedExecutable)
    {
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Contains('\0'))
        {
            return false;
        }

        try
        {
            string expanded = Environment.ExpandEnvironmentVariables(candidate.Trim().Trim('"'));
            return string.Equals(Path.GetFileName(expanded), expectedExecutable, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Finds a configured path using a case-insensitive fallback for deserialized dictionaries. / 使用不区分大小写的回退方式在反序列化字典中查找配置路径。
    /// </summary>
    /// <param name="toolPaths">Configured tool paths. / 已配置的工具路径。</param>
    /// <param name="key">Tool identifier. / 工具标识。</param>
    /// <returns>Configured path or null. / 配置路径或 null。</returns>
    private static string? FindConfiguredPath(Dictionary<string, string>? toolPaths, string key)
    {
        if (toolPaths is null || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (toolPaths.TryGetValue(key, out string? direct))
        {
            return direct;
        }

        return toolPaths.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
    }

    /// <summary>
    /// Resolves a caller-selected executable and throws a user-facing error when it is invalid. / 解析调用方选择的程序，并在路径无效时抛出用户可读错误。
    /// </summary>
    /// <param name="candidate">Configured path or executable name. / 已配置的路径或程序名。</param>
    /// <param name="sourceDescription">Description of the configuration source. / 配置来源说明。</param>
    /// <returns>Existing executable path. / 存在的可执行文件路径。</returns>
    private static string ResolveRequiredCandidate(string candidate, string sourceDescription)
    {
        RejectInvalidExecutableCandidate(candidate, sourceDescription);
        RejectShellScriptCandidate(candidate, sourceDescription);
        RejectRemoteExecutableCandidate(candidate, sourceDescription);
        string? resolved = TryResolveCandidate(candidate);
        if (resolved is not null)
        {
            RejectRemoteExecutableCandidate(resolved, sourceDescription);
            return resolved;
        }

        throw new LaunchValidationException(
            $"The executable from {sourceDescription} does not exist: {candidate}. / 来自{sourceDescription}的可执行文件不存在：{candidate}。");
    }

    /// <summary>
    /// Rejects empty or null-containing executable candidates before calling path APIs. / 在调用路径 API 前拒绝空值或含空字符的可执行程序候选项。
    /// </summary>
    /// <param name="candidate">Configured executable candidate. / 已配置的可执行程序候选项。</param>
    /// <param name="sourceDescription">Bilingual configuration source. / 双语配置来源。</param>
    private static void RejectInvalidExecutableCandidate(string candidate, string sourceDescription)
    {
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Contains('\0'))
        {
            throw new LaunchValidationException(
                $"The executable from {sourceDescription} is empty or contains an invalid null character. / 来自{sourceDescription}的可执行程序为空或包含无效空字符。");
        }
    }

    /// <summary>
    /// Rejects UNC executables so an imported or configured profile cannot launch remote code. / 拒绝 UNC 可执行文件，防止导入或已配置的连接启动远程代码。
    /// </summary>
    /// <param name="candidate">Configured or resolved executable path. / 已配置或已解析的可执行程序路径。</param>
    /// <param name="sourceDescription">Bilingual configuration source. / 双语配置来源。</param>
    private static void RejectRemoteExecutableCandidate(string candidate, string sourceDescription)
    {
        string expanded = Environment.ExpandEnvironmentVariables(candidate.Trim().Trim('"'));
        if (expanded.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new LaunchValidationException(
                $"The executable from {sourceDescription} uses a remote UNC path, which is not allowed: {candidate}. / 来自{sourceDescription}的可执行程序使用了不允许的远程 UNC 路径：{candidate}。");
        }
    }

    /// <summary>
    /// Rejects batch scripts because launch plans intentionally do not invoke a command shell. / 拒绝批处理脚本，因为启动计划会有意不调用命令外壳。
    /// </summary>
    /// <param name="candidate">Configured executable candidate. / 已配置的可执行文件候选项。</param>
    /// <param name="sourceDescription">Bilingual configuration source. / 双语配置来源。</param>
    private static void RejectShellScriptCandidate(string candidate, string sourceDescription)
    {
        string expanded = Environment.ExpandEnvironmentVariables(candidate.Trim().Trim('"'));
        string extension = Path.GetExtension(expanded);
        if (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
        {
            throw new LaunchValidationException(
                $"The file from {sourceDescription} is a shell script, which is not supported by shell-free launching: {candidate}. / 来自{sourceDescription}的文件是批处理脚本，不受无外壳启动支持：{candidate}。");
        }
    }

    /// <summary>
    /// Resolves an absolute, app-relative, or PATH-based executable candidate. / 解析绝对路径、应用相对路径或 PATH 中的候选程序。
    /// </summary>
    /// <param name="candidate">Path or executable name. / 路径或程序名。</param>
    /// <returns>Resolved full path, or null when absent. / 解析后的完整路径；不存在时返回 null。</returns>
    private static string? TryResolveCandidate(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        string expanded = Environment.ExpandEnvironmentVariables(candidate.Trim().Trim('"'));

        try
        {
            if (Path.IsPathRooted(expanded))
            {
                string absolute = Path.GetFullPath(expanded);
                return File.Exists(absolute) ? absolute : null;
            }

            if (expanded.Contains(Path.DirectorySeparatorChar) || expanded.Contains(Path.AltDirectorySeparatorChar))
            {
                string appRelative = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, expanded));
                return File.Exists(appRelative) ? appRelative : null;
            }

            foreach (string directory in EnumerateTrustedSearchDirectories())
            {
                string path = Path.Combine(directory, expanded);
                if (File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        return null;
    }

    /// <summary>
    /// Enumerates the application directory and explicit PATH entries while excluding implicit current-directory lookup. / 枚举应用目录和显式 PATH 项，并排除隐式当前目录查找。
    /// </summary>
    /// <returns>Distinct directories eligible for executable lookup. / 可用于查找程序的不重复目录。</returns>
    private static IEnumerable<string> EnumerateTrustedSearchDirectories()
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        if (seen.Add(AppContext.BaseDirectory))
        {
            yield return AppContext.BaseDirectory;
        }

        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVariable))
        {
            yield break;
        }

        foreach (string rawDirectory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string directory = Environment.ExpandEnvironmentVariables(rawDirectory.Trim().Trim('"'));
            if (directory.Length > 0 && Path.IsPathRooted(directory) && Directory.Exists(directory) && seen.Add(directory))
            {
                yield return directory;
            }
        }
    }

    /// <summary>
    /// Gets conventional executable names used only for explicit PATH searches. / 获取仅用于显式 PATH 搜索的常规程序名。
    /// </summary>
    /// <param name="type">Connection client type. / 连接客户端类型。</param>
    /// <param name="normalizedProtocol">Normalized protocol used to select an implementation-specific executable. / 用于选择特定实现程序的规范化协议。</param>
    /// <returns>Candidate executable names in preference order. / 按优先级排列的候选程序名。</returns>
    private static IReadOnlyList<string> GetConventionalExecutableNames(ConnectionType type, string normalizedProtocol) => type switch
    {
        ConnectionType.RemoteDesktop => ["mstsc.exe"],
        ConnectionType.Putty => ["putty.exe"],
        ConnectionType.Xshell => ["Xshell.exe"],
        ConnectionType.Xftp => ["Xftp.exe"],
        ConnectionType.WinScp => ["WinSCP.exe"],
        ConnectionType.SecureCrt => ["SecureCRT.exe"],
        ConnectionType.MobaXterm => ["MobaXterm.exe"],
        ConnectionType.Vnc when normalizedProtocol == "realvnc" => ["vncviewer.exe"],
        ConnectionType.Vnc when normalizedProtocol == "ultravnc" => ["uvncviewer.exe"],
        ConnectionType.Vnc => ["tvnviewer.exe"],
        ConnectionType.Radmin => ["Radmin.exe", "RadminViewer.exe"],
        ConnectionType.ToDesk => ["ToDesk.exe"],
        ConnectionType.RustDesk => ["rustdesk.exe"],
        _ => []
    };
}
