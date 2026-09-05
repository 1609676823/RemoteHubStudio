using RemoteHubStudio.Domain;

namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

/// <summary>Creates a fresh type-specific options page for every supported connection type. / 为每个受支持的连接类型创建全新的专属选项子页。</summary>
public static class ConnectionTypeOptionsPageFactory
{
    /// <summary>Creates one page for a connection type. / 为连接类型创建一个子页。</summary>
    /// <param name="type">Connection type. / 连接类型。</param>
    /// <returns>A new caller-owned options page. / 由调用方拥有的新选项子页。</returns>
    public static ConnectionTypeOptionsPage Create(ConnectionType type) => type switch
    {
        ConnectionType.RemoteDesktop => new RdpConnectionTypeOptionsPage(),
        ConnectionType.Putty => new PuttyConnectionTypeOptionsPage(),
        ConnectionType.Xshell => new XshellConnectionTypeOptionsPage(),
        ConnectionType.Xftp => new XftpConnectionTypeOptionsPage(),
        ConnectionType.WinScp => new WinScpConnectionTypeOptionsPage(),
        ConnectionType.SecureCrt => new SecureCrtConnectionTypeOptionsPage(),
        ConnectionType.MobaXterm => new MobaXtermConnectionTypeOptionsPage(),
        ConnectionType.Vnc => new VncConnectionTypeOptionsPage(),
        ConnectionType.Radmin => new RadminConnectionTypeOptionsPage(),
        ConnectionType.ToDesk => new ToDeskConnectionTypeOptionsPage(),
        ConnectionType.RustDesk => new RustDeskConnectionTypeOptionsPage(),
        ConnectionType.Custom => new CustomConnectionTypeOptionsPage(),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported connection type. / 不支持的连接类型。")
    };

    /// <summary>
    /// Creates exactly one page for every currently defined <see cref="ConnectionType"/> value.
    /// / 为当前定义的每个 <see cref="ConnectionType"/> 值各创建一个子页。
    /// </summary>
    /// <returns>A newly allocated type-to-page dictionary. / 新分配的类型到子页字典。</returns>
    public static IReadOnlyDictionary<ConnectionType, ConnectionTypeOptionsPage> CreateAll()
    {
        Dictionary<ConnectionType, ConnectionTypeOptionsPage> pages = [];
        try
        {
            foreach (ConnectionType type in Enum.GetValues<ConnectionType>())
            {
                ConnectionTypeOptionsPage page = Create(type);
                if (page.Type != type)
                {
                    page.Dispose();
                    throw new InvalidOperationException(
                        $"The options-page factory returned '{page.Type}' for '{type}'. / 选项子页工厂为“{type}”返回了“{page.Type}”。");
                }

                pages.Add(type, page);
            }

            return pages;
        }
        catch
        {
            foreach (ConnectionTypeOptionsPage page in pages.Values)
            {
                page.Dispose();
            }

            throw;
        }
    }
}
