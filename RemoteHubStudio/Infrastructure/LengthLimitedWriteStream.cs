namespace RemoteHubStudio.Infrastructure;

/// <summary>
/// Stops a write before the delegated stream can exceed a fixed physical byte limit. / 在委托流超过固定物理字节上限前停止写入。
/// </summary>
internal sealed class LengthLimitedWriteStream : Stream
{
    private readonly Stream _innerStream;
    private readonly long _maximumLength;
    private readonly bool _leaveOpen;
    private long _writtenLength;

    /// <summary>
    /// Initializes a bounded write-only stream. / 初始化受限的只写流。
    /// </summary>
    /// <param name="innerStream">Destination stream. / 目标流。</param>
    /// <param name="maximumLength">Maximum bytes that may be written. / 可写入的最大字节数。</param>
    /// <param name="leaveOpen">Whether disposal leaves the destination open. / 释放时是否保留目标流打开。</param>
    public LengthLimitedWriteStream(Stream innerStream, long maximumLength, bool leaveOpen = false)
    {
        _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
        if (!innerStream.CanWrite)
        {
            throw new ArgumentException("The destination stream is not writable. / 目标流不可写。", nameof(innerStream));
        }

        if (maximumLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLength));
        }

        _maximumLength = maximumLength;
        _leaveOpen = leaveOpen;
        _writtenLength = innerStream.CanSeek ? innerStream.Position : 0;
    }

    /// <summary>Gets whether this write-only wrapper supports reading. / 获取此只写包装器是否支持读取。</summary>
    public override bool CanRead => false;

    /// <summary>Gets whether this forward-only wrapper supports seeking. / 获取此前向包装器是否支持定位。</summary>
    public override bool CanSeek => false;

    /// <summary>Gets whether this wrapper supports writing. / 获取此包装器是否支持写入。</summary>
    public override bool CanWrite => true;

    /// <summary>Gets the number of bytes accepted by this wrapper. / 获取此包装器已接受的字节数。</summary>
    public override long Length => _writtenLength;

    /// <summary>Gets the current accepted-byte position and rejects repositioning. / 获取当前已接受字节位置，并拒绝重新定位。</summary>
    public override long Position
    {
        get => _writtenLength;
        set => throw new NotSupportedException();
    }

    /// <summary>Flushes buffered destination bytes. / 刷新目标中已缓冲的字节。</summary>
    public override void Flush() => _innerStream.Flush();

    /// <summary>Asynchronously flushes buffered destination bytes. / 异步刷新目标中已缓冲的字节。</summary>
    /// <param name="cancellationToken">Cancellation token. / 取消令牌。</param>
    /// <returns>A task representing the flush. / 表示刷新操作的任务。</returns>
    public override Task FlushAsync(CancellationToken cancellationToken) => _innerStream.FlushAsync(cancellationToken);

    /// <summary>Writes a bounded segment of a byte array. / 写入字节数组中的受限片段。</summary>
    /// <param name="buffer">Source buffer. / 源缓冲区。</param>
    /// <param name="offset">Source offset. / 源偏移量。</param>
    /// <param name="count">Byte count. / 字节数。</param>
    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureWriteFits(count);
        _innerStream.Write(buffer, offset, count);
        _writtenLength += count;
    }

    /// <summary>Writes a bounded read-only byte span. / 写入受限的只读字节跨度。</summary>
    /// <param name="buffer">Source bytes. / 源字节。</param>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        EnsureWriteFits(buffer.Length);
        _innerStream.Write(buffer);
        _writtenLength += buffer.Length;
    }

    /// <summary>Asynchronously writes a bounded byte-array segment. / 异步写入受限的字节数组片段。</summary>
    /// <param name="buffer">Source buffer. / 源缓冲区。</param>
    /// <param name="offset">Source offset. / 源偏移量。</param>
    /// <param name="count">Byte count. / 字节数。</param>
    /// <param name="cancellationToken">Cancellation token. / 取消令牌。</param>
    /// <returns>A task representing the write. / 表示写入操作的任务。</returns>
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        EnsureWriteFits(count);
        await _innerStream.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        _writtenLength += count;
    }

    /// <summary>Asynchronously writes bounded read-only bytes. / 异步写入受限的只读字节。</summary>
    /// <param name="buffer">Source bytes. / 源字节。</param>
    /// <param name="cancellationToken">Cancellation token. / 取消令牌。</param>
    /// <returns>A value task representing the write. / 表示写入操作的值任务。</returns>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        EnsureWriteFits(buffer.Length);
        await _innerStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        _writtenLength += buffer.Length;
    }

    /// <summary>Rejects reads because this wrapper is write-only. / 拒绝读取，因为此包装器只写。</summary>
    /// <param name="buffer">Unused destination buffer. / 未使用的目标缓冲区。</param>
    /// <param name="offset">Unused destination offset. / 未使用的目标偏移量。</param>
    /// <param name="count">Unused byte count. / 未使用的字节数。</param>
    /// <returns>This method does not return. / 此方法不会返回。</returns>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>Rejects seeking because this wrapper is forward-only. / 拒绝定位，因为此包装器仅向前写入。</summary>
    /// <param name="offset">Unused offset. / 未使用的偏移量。</param>
    /// <param name="origin">Unused origin. / 未使用的起点。</param>
    /// <returns>This method does not return. / 此方法不会返回。</returns>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <summary>Rejects explicit length changes. / 拒绝显式更改长度。</summary>
    /// <param name="value">Unused requested length. / 未使用的请求长度。</param>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <summary>
    /// Rejects a write that would cross the configured byte limit. / 拒绝会跨过已配置字节上限的写入。
    /// </summary>
    /// <param name="count">Pending write byte count. / 待写入字节数。</param>
    private void EnsureWriteFits(int count)
    {
        if (count < 0 || _writtenLength > _maximumLength - count)
        {
            throw new InvalidDataException($"The output exceeds {_maximumLength} bytes. / 输出超过 {_maximumLength} 字节。");
        }
    }

    /// <summary>Disposes the destination unless configured to leave it open. / 除非配置为保持打开，否则释放目标流。</summary>
    /// <param name="disposing">Whether managed resources should be disposed. / 是否应释放托管资源。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
        {
            _innerStream.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Asynchronously disposes the destination unless configured to leave it open. / 除非配置为保持打开，否则异步释放目标流。</summary>
    /// <returns>A value task representing disposal. / 表示释放操作的值任务。</returns>
    public override async ValueTask DisposeAsync()
    {
        if (!_leaveOpen)
        {
            await _innerStream.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }
}
