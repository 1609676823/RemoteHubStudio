using Microsoft.Win32;
using RemoteHubStudio.Domain;

namespace RemoteHubStudio.Infrastructure.ImportExport;

/// <summary>
/// Reads the current Windows user's Remote Desktop MRU history. / 读取当前 Windows 用户的远程桌面最近使用记录。
/// </summary>
public sealed class LegacyRdpHistoryImporter
{
    private const string RegistryPath = @"SOFTWARE\Microsoft\Terminal Server Client\Default";

    /// <summary>
    /// Imports unique Remote Desktop addresses from the Windows registry. / 从 Windows 注册表导入不重复的远程桌面地址。
    /// </summary>
    /// <returns>Connection profiles created from MRU entries. / 由最近使用记录创建的连接配置。</returns>
    public IReadOnlyList<ConnectionProfile> Import()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryPath, false);
        if (key is null)
        {
            return [];
        }

        HashSet<string> addresses = new(StringComparer.OrdinalIgnoreCase);
        List<ConnectionProfile> profiles = [];
        foreach (string valueName in key.GetValueNames().Where(name => name.StartsWith("MRU", StringComparison.OrdinalIgnoreCase)).OrderBy(name => name))
        {
            string? raw = key.GetValue(valueName) as string;
            if (string.IsNullOrWhiteSpace(raw) || !addresses.Add(raw.Trim()))
            {
                continue;
            }

            (string host, int port) = ParseAddress(raw.Trim());
            profiles.Add(new ConnectionProfile
            {
                Name = $"RDP · {host}",
                Type = ConnectionType.RemoteDesktop,
                Protocol = "rdp",
                Host = host,
                Port = port
            });
        }

        return profiles;
    }

    /// <summary>
    /// Splits an MRU address while preserving bracketed IPv6 hosts. / 拆分最近使用地址，同时保留带方括号的 IPv6 主机。
    /// </summary>
    /// <param name="address">MRU address. / 最近使用地址。</param>
    /// <returns>Host and port. / 主机与端口。</returns>
    private static (string Host, int Port) ParseAddress(string address)
    {
        if (address.StartsWith('['))
        {
            int closingBracket = address.IndexOf(']');
            if (closingBracket > 0)
            {
                string ipv6Host = address[1..closingBracket];
                string remainder = address[(closingBracket + 1)..];
                return remainder.StartsWith(':') && TryParsePort(remainder[1..], out int ipv6Port)
                    ? (ipv6Host, ipv6Port)
                    : (ipv6Host, 3389);
            }
        }

        int separator = address.LastIndexOf(':');
        if (separator > 0 && address.IndexOf(':') == separator)
        {
            return TryParsePort(address[(separator + 1)..], out int port)
                ? (address[..separator], port)
                : (address[..separator], 3389);
        }

        return (address, 3389);
    }

    /// <summary>
    /// Parses a valid TCP port from an RDP history suffix. / 从 RDP 历史后缀中解析有效的 TCP 端口。
    /// </summary>
    /// <param name="value">Port text. / 端口文本。</param>
    /// <param name="port">Validated parsed port. / 已验证的解析端口。</param>
    /// <returns>True when the value is between 1 and 65535. / 值介于 1 与 65535 之间时返回 true。</returns>
    private static bool TryParsePort(string value, out int port)
    {
        return int.TryParse(value, out port) && port is >= 1 and <= 65535;
    }
}
