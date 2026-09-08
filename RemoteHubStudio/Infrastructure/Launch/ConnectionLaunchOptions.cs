using System.Globalization;

namespace RemoteHubStudio.Infrastructure.Launch;

/// <summary>Reads the overall connection-launch deadline once when the service is created at app startup. / 应用启动创建服务时，一次性读取连接启动总超时。</summary>
internal sealed record ConnectionLaunchOptions(TimeSpan Timeout)
{
    // PowerShell example (set BEFORE starting a fresh application process):
    // / PowerShell 示例（启动新的应用进程之前设置）：
    // $env:REMOTEHUBSTUDIO_CONNECTION_TIMEOUT_SECONDS = '90'
    // .\RemoteHubStudio.exe
    // Units are whole seconds. This covers plan creation, client readiness, and Process.Start for EVERY client.
    // / 单位为整数秒，覆盖所有客户端的参数生成、就绪等待和 Process.Start；不限制已建立的远程会话时长。
    // Change DefaultTimeoutSeconds here to change the default; invalid/missing/out-of-range values use that default.
    // / 日后修改默认值只需调整下面的 DefaultTimeoutSeconds；缺失、无效或越界的环境变量均回退到默认值。
    internal const string EnvironmentVariableName = "REMOTEHUBSTUDIO_CONNECTION_TIMEOUT_SECONDS";

    // ===== 默认连接超时配置（所有连接类型共用，单位：秒） =====
    // 快速查找：搜索“默认连接超时”或 DefaultTimeoutSeconds 即可定位这里。
    // 修改下面的数值后，重新编译并启动应用即可更改默认超时。
    // 合法的 REMOTEHUBSTUDIO_CONNECTION_TIMEOUT_SECONDS 环境变量优先于此默认值；
    // 环境变量在启动时读取，修改后需先退出旧实例再启动。
    // / Default for every connection type; rebuild after editing. A valid startup environment value takes precedence.
    internal const int DefaultTimeoutSeconds = 15;

    internal const int MinimumTimeoutSeconds = 1;
    internal const int MaximumTimeoutSeconds = 3600;

    internal static ConnectionLaunchOptions FromEnvironment(Func<string, string?>? readVariable = null)
    {
        string? value = (readVariable ?? Environment.GetEnvironmentVariable)(EnvironmentVariableName);
        int seconds = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int configured) &&
            configured is >= MinimumTimeoutSeconds and <= MaximumTimeoutSeconds
                ? configured
                : DefaultTimeoutSeconds;
        return new ConnectionLaunchOptions(TimeSpan.FromSeconds(seconds));
    }
}
