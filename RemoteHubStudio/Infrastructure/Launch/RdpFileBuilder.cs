using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using RemoteHubStudio.Domain;

namespace RemoteHubStudio.Infrastructure.Launch;

/// <summary>
/// Creates unique, line-safe Remote Desktop files with optional CurrentUser DPAPI credentials. / 创建唯一且防行注入的远程桌面文件，并可选择写入当前用户 DPAPI 凭据。
/// </summary>
internal static class RdpFileBuilder
{
    private static readonly Encoding RdpEncoding = new UnicodeEncoding(false, true, true);

    /// <summary>
    /// Creates one unique RDP file for a connection. / 为一个连接创建唯一的 RDP 文件。
    /// </summary>
    /// <param name="directory">Dedicated temporary RDP directory. / 专用 RDP 临时目录。</param>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="username">Resolved username. / 已解析的用户名。</param>
    /// <param name="password">Resolved password. / 已解析的密码。</param>
    /// <param name="allowPassword">Whether current settings permit writing password material. / 当前设置是否允许写入密码材料。</param>
    /// <returns>Full path of the created file. / 已创建文件的完整路径。</returns>
    public static string CreateFile(
        string directory,
        ConnectionProfile profile,
        string username,
        string password,
        bool allowPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(profile);

        Directory.CreateDirectory(directory);
        string safeName = CreateSafeFileStem(profile.Name);
        string path = Path.Combine(directory, $"{safeName}-{Guid.NewGuid():N}.rdp");
        bool includePassword = allowPassword && !string.IsNullOrEmpty(password);
        bool promptForCredentials = profile.Rdp?.PromptForCredentials == true || (!allowPassword && !string.IsNullOrEmpty(password));
        List<string> lines = CreateLines(profile, username, password, includePassword, promptForCredentials);

        try
        {
            using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using StreamWriter writer = new(stream, RdpEncoding);
            foreach (string line in lines)
            {
                writer.WriteLine(line);
            }

            return path;
        }
        catch
        {
            TryDelete(path);
            throw;
        }
    }

    /// <summary>
    /// Builds validated RDP property lines without accepting newline characters from profile data. / 构建经过验证的 RDP 属性行，并拒绝配置数据注入换行符。
    /// </summary>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="username">Resolved username. / 已解析的用户名。</param>
    /// <param name="password">Resolved password. / 已解析的密码。</param>
    /// <param name="includePassword">Whether a DPAPI password line is included. / 是否包含 DPAPI 密码行。</param>
    /// <param name="promptForCredentials">Whether MSTSC should prompt for credentials. / MSTSC 是否应提示输入凭据。</param>
    /// <returns>RDP file lines. / RDP 文件行。</returns>
    private static List<string> CreateLines(
        ConnectionProfile profile,
        string username,
        string password,
        bool includePassword,
        bool promptForCredentials)
    {
        RdpOptions options = profile.Rdp ?? new RdpOptions();
        int width = Math.Clamp(options.DesktopWidth, 320, 16384);
        int height = Math.Clamp(options.DesktopHeight, 200, 16384);
        int colorDepth = options.ColorDepth is 15 or 16 or 24 or 32 ? options.ColorDepth : 32;
        int audioMode = (int)options.AudioMode is >= 0 and <= 2 ? (int)options.AudioMode : (int)RdpAudioMode.Local;
        int keyboardHook = (int)options.KeyboardHookMode is >= 0 and <= 2
            ? (int)options.KeyboardHookMode
            : (int)RdpKeyboardHookMode.FullScreenOnly;
        string endpoint = FormatEndpoint(profile.Host, profile.Port);

        List<string> lines =
        [
            $"screen mode id:i:{ToFlag(options.FullScreen) + 1}",
            $"use multimon:i:{ToFlag(options.UseAllMonitors)}",
            $"desktopwidth:i:{width.ToString(CultureInfo.InvariantCulture)}",
            $"desktopheight:i:{height.ToString(CultureInfo.InvariantCulture)}",
            "smart sizing:i:1",
            $"session bpp:i:{colorDepth.ToString(CultureInfo.InvariantCulture)}",
            $"compression:i:{ToFlag(options.EnableCompression)}",
            $"keyboardhook:i:{keyboardHook.ToString(CultureInfo.InvariantCulture)}",
            $"displayconnectionbar:i:{ToFlag(options.DisplayConnectionBar)}",
            $"audiocapturemode:i:{ToFlag(options.RedirectMicrophone)}",
            $"audiomode:i:{audioMode.ToString(CultureInfo.InvariantCulture)}",
            $"username:s:{SanitizeLineValue(username)}",
            $"full address:s:{SanitizeLineValue(endpoint)}",
            $"disable wallpaper:i:{ToFlag(options.DisableWallpaper)}",
            $"redirectclipboard:i:{ToFlag(options.RedirectClipboard)}",
            $"redirectprinters:i:{ToFlag(options.RedirectPrinters)}",
            $"redirectsmartcards:i:{ToFlag(options.RedirectSmartCards)}",
            $"redirectcomports:i:{ToFlag(options.RedirectComPorts)}",
            $"redirectposdevices:i:{ToFlag(options.RedirectPosDevices)}",
            $"camerastoredirect:s:{(options.RedirectCameras ? "*" : string.Empty)}",
            $"drivestoredirect:s:{(options.RedirectDrives ? "*" : string.Empty)}",
            $"autoreconnection enabled:i:{ToFlag(options.AutoReconnect)}",
            "authentication level:i:2",
            $"prompt for credentials:i:{ToFlag(promptForCredentials)}",
            "negotiate security layer:i:1",
            "remoteapplicationmode:i:0",
            "gatewayhostname:s:",
            "gatewayusagemethod:i:4",
            "gatewaycredentialssource:i:4",
            "gatewayprofileusagemethod:i:0"
        ];

        if (includePassword)
        {
            lines.Add($"password 51:b:{ProtectPassword(password)}");
        }

        return lines;
    }

    /// <summary>
    /// Formats a host and port while preserving IPv6 syntax. / 格式化主机和端口并保留正确的 IPv6 语法。
    /// </summary>
    /// <param name="host">Host name or IP address. / 主机名或 IP 地址。</param>
    /// <param name="port">TCP port. / TCP 端口。</param>
    /// <returns>RDP endpoint text. / RDP 端点文本。</returns>
    private static string FormatEndpoint(string host, int port)
    {
        string trimmedHost = host.Trim();
        if (IPAddress.TryParse(trimmedHost, out IPAddress? address) && address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            trimmedHost = $"[{trimmedHost}]";
        }

        return $"{trimmedHost}:{port.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Protects a password with Windows DPAPI scoped to the current user and formats it for password 51. / 使用限定为当前用户的 Windows DPAPI 保护密码，并格式化为 password 51 数据。
    /// </summary>
    /// <param name="password">Plaintext password permitted by current settings. / 当前设置允许使用的明文密码。</param>
    /// <returns>Uppercase hexadecimal DPAPI payload. / 大写十六进制 DPAPI 载荷。</returns>
    private static string ProtectPassword(string password)
    {
        byte[] plaintext = Encoding.Unicode.GetBytes(password);
        byte[] protectedBytes = [];

        try
        {
            protectedBytes = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
            return Convert.ToHexString(protectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (protectedBytes.Length > 0)
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }
        }
    }

    /// <summary>
    /// Removes control and Unicode line-separator characters from an RDP string value. / 从 RDP 字符串值中移除控制字符和 Unicode 行分隔符。
    /// </summary>
    /// <param name="value">Untrusted profile value. / 不可信的配置值。</param>
    /// <returns>Single-line safe value. / 安全的单行值。</returns>
    private static string SanitizeLineValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new(value.Length);
        foreach (char character in value)
        {
            builder.Append(char.IsControl(character) || character is '\u2028' or '\u2029' ? ' ' : character);
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    /// Creates a bounded file-name stem from a user-visible connection name. / 根据用户可见连接名称创建长度受限的文件名主体。
    /// </summary>
    /// <param name="name">Connection name. / 连接名称。</param>
    /// <returns>Safe nonempty file-name stem. / 安全且非空的文件名主体。</returns>
    private static string CreateSafeFileStem(string? name)
    {
        HashSet<char> invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        StringBuilder builder = new();

        foreach (char character in name ?? string.Empty)
        {
            if (builder.Length >= 48)
            {
                break;
            }

            builder.Append(invalidCharacters.Contains(character) || char.IsControl(character) ? '_' : character);
        }

        string result = builder.ToString().Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(result) ? "connection" : result;
    }

    /// <summary>
    /// Converts a Boolean option to the RDP integer representation. / 将布尔选项转换为 RDP 整数表示。
    /// </summary>
    /// <param name="value">Boolean value. / 布尔值。</param>
    /// <returns>One for true, otherwise zero. / true 返回 1，否则返回 0。</returns>
    private static int ToFlag(bool value)
    {
        return value ? 1 : 0;
    }

    /// <summary>
    /// Best-effort deletes a partially written temporary file. / 尽力删除写入失败的临时文件。
    /// </summary>
    /// <param name="path">Temporary file path. / 临时文件路径。</param>
    private static void TryDelete(string path)
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
