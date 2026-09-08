namespace RemoteHubStudio.Infrastructure;

/// <summary>
/// Reads a file through one stable handle after enforcing its physical size bound. / 在强制执行物理大小上限后，通过同一稳定句柄读取文件。
/// </summary>
internal static class BoundedFileReader
{
    /// <summary>
    /// Opens and reads one file without a path-level size-check race. / 在不产生路径级大小检查竞态的情况下打开并读取文件。
    /// </summary>
    /// <param name="filePath">Source file path. / 源文件路径。</param>
    /// <param name="maximumLength">Maximum accepted physical bytes. / 可接受的最大物理字节数。</param>
    /// <param name="cancellationToken">Cancellation token. / 取消令牌。</param>
    /// <returns>The exact bytes read from the stable handle. / 从稳定句柄读取的精确字节。</returns>
    public static async Task<byte[]> ReadAllBytesAsync(
        string filePath,
        long maximumLength,
        CancellationToken cancellationToken)
    {
        if (maximumLength is < 0 or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLength));
        }

        await using FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        long length = stream.Length;
        if (length < 0 || length > maximumLength)
        {
            throw new InvalidDataException($"The file exceeds {maximumLength} bytes. / 文件超过 {maximumLength} 字节。");
        }

        byte[] content = GC.AllocateUninitializedArray<byte>((int)length);
        await stream.ReadExactlyAsync(content, cancellationToken).ConfigureAwait(false);
        byte[] trailingByte = new byte[1];
        if (await stream.ReadAsync(trailingByte, cancellationToken).ConfigureAwait(false) != 0)
        {
            throw new InvalidDataException($"The file changed while being read or exceeds {maximumLength} bytes. / 文件在读取期间发生变化或超过 {maximumLength} 字节。");
        }

        return content;
    }
}
