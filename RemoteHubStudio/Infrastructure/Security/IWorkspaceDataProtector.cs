namespace RemoteHubStudio.Infrastructure.Security;

/// <summary>
/// Protects and unprotects serialized workspace bytes. / 保护和解保护已序列化的工作区字节。
/// </summary>
public interface IWorkspaceDataProtector
{
    /// <summary>Gets the stable protection scheme identifier stored in the JSON envelope. / 获取写入 JSON 信封的稳定保护方案标识。</summary>
    string Scheme { get; }

    /// <summary>
    /// Protects plaintext workspace bytes for the current user. / 为当前用户保护工作区明文字节。
    /// </summary>
    /// <param name="plaintext">Plaintext bytes to protect. / 要保护的明文字节。</param>
    /// <returns>Protected bytes. / 受保护的字节。</returns>
    byte[] Protect(byte[] plaintext);

    /// <summary>
    /// Unprotects workspace bytes for the current user. / 为当前用户解保护工作区字节。
    /// </summary>
    /// <param name="protectedData">Protected bytes to decrypt. / 要解密的受保护字节。</param>
    /// <returns>Unprotected plaintext bytes. / 解保护后的明文字节。</returns>
    byte[] Unprotect(byte[] protectedData);
}
