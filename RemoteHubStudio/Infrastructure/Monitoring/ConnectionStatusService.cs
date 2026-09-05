using System.Net.NetworkInformation;
using RemoteHubStudio.Domain;

namespace RemoteHubStudio.Infrastructure.Monitoring;

/// <summary>
/// Performs bounded, cancellable ICMP status checks for connection profiles. / 为连接配置执行有界且可取消的 ICMP 状态检测。
/// </summary>
public sealed class ConnectionStatusService
{
    /// <summary>
    /// Gets the minimum supported ping timeout in milliseconds. / 获取支持的最小 Ping 超时毫秒数。
    /// </summary>
    public const int MinPingTimeout = 100;

    /// <summary>
    /// Gets the maximum supported ping timeout in milliseconds. / 获取支持的最大 Ping 超时毫秒数。
    /// </summary>
    public const int MaxPingTimeout = 60000;

    /// <summary>
    /// Gets the minimum supported status-check concurrency. / 获取支持的最小状态检测并发数。
    /// </summary>
    public const int MinConcurrency = 1;

    /// <summary>
    /// Gets the maximum supported status-check concurrency. / 获取支持的最大状态检测并发数。
    /// </summary>
    public const int MaxConcurrency = 128;

    /// <summary>
    /// Checks one host without treating a blocked ICMP response as proof of downtime. / 检测一台主机，并且不把 ICMP 被阻止视为宕机证据。
    /// </summary>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="timeoutMilliseconds">Timeout in milliseconds. / 超时毫秒数。</param>
    /// <param name="cancellationToken">Cancellation token. / 取消令牌。</param>
    /// <returns>Reachability result. / 可达性结果。</returns>
    public async Task<ConnectionStatus> CheckAsync(
        ConnectionProfile profile,
        int timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        ConnectionStatus status = new()
        {
            ConnectionId = profile.Id,
            State = ReachabilityState.Checking,
            CheckedAtUtc = DateTime.UtcNow
        };

        if (profile.Type is ConnectionType.Custom or ConnectionType.ToDesk or ConnectionType.RustDesk)
        {
            status.State = ReachabilityState.NotApplicable;
            status.Message = "ICMP check is not applicable / ICMP 检测不适用";
            return status;
        }

        if (string.IsNullOrWhiteSpace(profile.Host))
        {
            status.State = ReachabilityState.InvalidAddress;
            status.Message = "No network address / 无网络地址";
            return status;
        }

        try
        {
            using Ping ping = new();
            PingReply reply = await ping.SendPingAsync(
                profile.Host,
                TimeSpan.FromMilliseconds(Math.Clamp(timeoutMilliseconds, MinPingTimeout, MaxPingTimeout)),
                cancellationToken: cancellationToken);
            status.CheckedAtUtc = DateTime.UtcNow;
            if (reply.Status == IPStatus.Success)
            {
                status.State = ReachabilityState.Reachable;
                status.LatencyMilliseconds = reply.RoundtripTime;
                status.Message = $"{reply.RoundtripTime} ms";
            }
            else
            {
                status.State = ReachabilityState.NoIcmpResponse;
                status.Message = $"{reply.Status} · ICMP 无响应";
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PingException exception)
        {
            status.State = ReachabilityState.NoIcmpResponse;
            status.Message = $"{exception.InnerException?.Message ?? exception.Message} · ICMP 无响应";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            status.State = ReachabilityState.InvalidAddress;
            status.Message = $"{exception.Message} · 地址无效";
        }

        return status;
    }

    /// <summary>
    /// Checks multiple connections with a configurable concurrency ceiling. / 使用可配置的并发上限检测多条连接。
    /// </summary>
    /// <param name="profiles">Profiles to check. / 要检测的配置。</param>
    /// <param name="timeoutMilliseconds">Per-host timeout in milliseconds. / 单主机超时毫秒数。</param>
    /// <param name="maximumConcurrency">Maximum simultaneous checks. / 最大并发检测数。</param>
    /// <param name="progress">Optional progress callback. / 可选进度回调。</param>
    /// <param name="cancellationToken">Cancellation token. / 取消令牌。</param>
    /// <returns>Results in completion order. / 按完成顺序返回结果。</returns>
    public async Task<IReadOnlyList<ConnectionStatus>> CheckManyAsync(
        IEnumerable<ConnectionProfile> profiles,
        int timeoutMilliseconds,
        int maximumConcurrency,
        IProgress<ConnectionStatus>? progress = null,
        CancellationToken cancellationToken = default)
    {
        List<ConnectionStatus> results = [];
        object synchronizationRoot = new();
        ParallelOptions options = new()
        {
            MaxDegreeOfParallelism = Math.Clamp(maximumConcurrency, MinConcurrency, MaxConcurrency),
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(profiles, options, async (profile, token) =>
        {
            ConnectionStatus result = await CheckAsync(profile, timeoutMilliseconds, token);
            lock (synchronizationRoot)
            {
                results.Add(result);
            }

            progress?.Report(result);
        });

        return results;
    }
}
