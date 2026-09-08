using System.ComponentModel;
using System.Diagnostics;

namespace RemoteHubStudio.Infrastructure.Launch;

/// <summary>Describes the matching desktop client's initialization state. / 描述匹配的桌面客户端初始化状态。</summary>
internal readonly record struct ToDeskClientState(
    int ProcessId, bool IsReady, TimeSpan RunningFor = default, ulong WindowSignature = 0);

/// <summary>Starts ToDesk separately before dispatching a control command. / 在发送控制命令前单独启动 ToDesk。</summary>
internal sealed class ToDeskClientStartup
{
    internal static ToDeskClientStartup Shared { get; } = CreateDefault(ConnectionLaunchOptions.FromEnvironment().Timeout);

    /// <summary>Uses the same configurable deadline as other clients; grace/stability times are tuned here. / 与其他客户端共用可配置超时；初始化缓冲与稳定时间在此调整。</summary>
    internal static ToDeskClientStartup CreateDefault(TimeSpan timeout) => new(
        ToDeskClientProbe.Inspect, StartClient, timeout, TimeSpan.FromMilliseconds(250),
        TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10));

    private readonly SemaphoreSlim _startupGate = new(1, 1);
    private readonly Func<string, ToDeskClientState?> _inspectClient;
    private readonly Action<string> _startClient;
    private readonly TimeSpan _startupTimeout;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _readyStabilityPeriod;
    private readonly TimeSpan _minimumProcessAge;

    /// <summary>Initializes startup with process operations and bounded polling intervals. / 使用进程操作和有界轮询间隔初始化启动器。</summary>
    internal ToDeskClientStartup(
        Func<string, ToDeskClientState?> inspectClient,
        Action<string> startClient,
        TimeSpan startupTimeout,
        TimeSpan pollInterval,
        TimeSpan readyStabilityPeriod,
        TimeSpan minimumProcessAge = default)
    {
        ArgumentNullException.ThrowIfNull(inspectClient);
        ArgumentNullException.ThrowIfNull(startClient);
        if (startupTimeout <= TimeSpan.Zero || pollInterval <= TimeSpan.Zero ||
            readyStabilityPeriod < TimeSpan.Zero || minimumProcessAge < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(startupTimeout));
        }

        _inspectClient = inspectClient;
        _startClient = startClient;
        _startupTimeout = startupTimeout;
        _pollInterval = pollInterval;
        _readyStabilityPeriod = readyStabilityPeriod;
        _minimumProcessAge = minimumProcessAge;
    }

    /// <summary>Waits for a stable desktop client, sharing startup across concurrent requests. / 等待桌面客户端稳定就绪，并让并发请求共享启动过程。</summary>
    internal Task EnsureRunningAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        // A bounded native heartbeat can still block its caller briefly. Keep every probe off the UI thread.
        // / 有超时限制的原生心跳仍会短暂阻塞调用线程，因此所有检查都在后台进行。
        return Task.Run(() => WaitForClientAsync(executablePath, cancellationToken), cancellationToken);
    }

    private async Task WaitForClientAsync(string executablePath, CancellationToken cancellationToken)
    {
        await _startupGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stopwatch timeout = Stopwatch.StartNew();
            ToDeskClientState? state = _inspectClient(executablePath);
            cancellationToken.ThrowIfCancellationRequested();
            if (state is null)
            {
                // Bootstrap without device IDs or passwords; only the later command connects.
                // / 预启动不携带设备 ID 或密码，仅由后续命令发起连接。
                try
                {
                    _startClient(executablePath);
                }
                catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
                {
                    throw new InvalidOperationException(
                        $"Unable to open ToDesk at '{executablePath}'. Check its path and startup permissions. / 无法打开 ToDesk：“{executablePath}”。请检查程序路径及启动权限。",
                        exception);
                }
            }

            int? readyProcessId = null;
            ulong readyWindowSignature = 0;
            TimeSpan readySince = default;
            while (timeout.Elapsed < _startupTimeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                state = _inspectClient(executablePath);
                cancellationToken.ThrowIfCancellationRequested();
                if (timeout.Elapsed >= _startupTimeout)
                {
                    break;
                }

                if (state is { IsReady: true } ready && ready.RunningFor >= _minimumProcessAge)
                {
                    if (readyProcessId != ready.ProcessId || readyWindowSignature != ready.WindowSignature)
                    {
                        readyProcessId = ready.ProcessId;
                        readyWindowSignature = ready.WindowSignature;
                        readySince = timeout.Elapsed;
                    }

                    // Fresh heartbeats must remain healthy after the startup grace period. Any stall resets this interval.
                    // / 初始化缓冲期过后，实时心跳还必须持续正常；任何卡顿都会重置稳定计时。
                    if (timeout.Elapsed - readySince >= _readyStabilityPeriod)
                    {
                        return;
                    }
                }
                else
                {
                    readyProcessId = null;
                }

                TimeSpan remaining = _startupTimeout - timeout.Elapsed;
                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining < _pollInterval ? remaining : _pollInterval, cancellationToken).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException(
                "ToDesk did not finish initializing or remained unresponsive; the connection command was not sent. / ToDesk 初始化或响应检查超时，尚未发送连接命令。请检查 ToDesk 是否能正常运行。");
        }
        finally
        {
            _startupGate.Release();
        }
    }

    /// <summary>Opens the executable with an empty argument list and releases only its handle. / 使用空参数列表打开程序，并仅释放进程句柄。</summary>
    private static void StartClient(string executablePath)
    {
        using Process process = new() { StartInfo = new LaunchPlan(executablePath, []).CreateStartInfo() };
        if (!process.Start())
        {
            throw new InvalidOperationException("The ToDesk client process did not start. / ToDesk 客户端进程未能启动。");
        }
    }
}
