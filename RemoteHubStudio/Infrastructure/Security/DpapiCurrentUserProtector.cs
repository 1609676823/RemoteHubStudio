using System.Security.Cryptography;
using System.Text;

namespace RemoteHubStudio.Infrastructure.Security;

/// <summary>
/// Protects workspace data with Windows DPAPI scoped to the current user. / 使用限定为当前用户的 Windows DPAPI 保护工作区数据。
/// </summary>
public sealed class DpapiCurrentUserProtector : IWorkspaceDataProtector
{
    private static readonly byte[] DefaultEntropy = Encoding.UTF8.GetBytes("RemoteHubStudio.Workspace.v1");
    private readonly byte[] _entropy;

    /// <summary>
    /// Initializes a protector with RemoteHubStudio-specific optional entropy. / 使用 RemoteHubStudio 专用可选熵初始化保护器。
    /// </summary>
    public DpapiCurrentUserProtector()
        : this(DefaultEntropy)
    {
    }

    /// <summary>
    /// Initializes a protector with caller-supplied optional entropy. / 使用调用方提供的可选熵初始化保护器。
    /// </summary>
    /// <param name="entropy">Additional non-secret entropy used by DPAPI. / DPAPI 使用的附加非秘密熵。</param>
    public DpapiCurrentUserProtector(byte[] entropy)
    {
        ArgumentNullException.ThrowIfNull(entropy);
        _entropy = (byte[])entropy.Clone();
    }

    /// <summary>Gets the stable DPAPI protection scheme identifier. / 获取稳定的 DPAPI 保护方案标识。</summary>
    public string Scheme => "dpapi-current-user";

    /// <summary>
    /// Protects plaintext bytes with the current Windows user profile. / 使用当前 Windows 用户配置文件保护明文字节。
    /// </summary>
    /// <param name="plaintext">Plaintext workspace bytes. / 工作区明文字节。</param>
    /// <returns>DPAPI-protected bytes. / DPAPI 保护后的字节。</returns>
    public byte[] Protect(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return ProtectedData.Protect(plaintext, _entropy, DataProtectionScope.CurrentUser);
    }

    /// <summary>
    /// Unprotects bytes with the current Windows user profile. / 使用当前 Windows 用户配置文件解保护字节。
    /// </summary>
    /// <param name="protectedData">DPAPI-protected workspace bytes. / DPAPI 保护的工作区字节。</param>
    /// <returns>Unprotected plaintext workspace bytes. / 解保护后的工作区明文字节。</returns>
    public byte[] Unprotect(byte[] protectedData)
    {
        ArgumentNullException.ThrowIfNull(protectedData);
        return ProtectedData.Unprotect(protectedData, _entropy, DataProtectionScope.CurrentUser);
    }
}
