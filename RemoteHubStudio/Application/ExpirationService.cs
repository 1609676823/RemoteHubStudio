using RemoteHubStudio.Domain;

namespace RemoteHubStudio.Application;

/// <summary>
/// Classifies connection expiration dates for display and reminders. / 对连接到期日期进行分类，用于显示与提醒。
/// </summary>
public sealed class ExpirationService
{
    /// <summary>
    /// Classifies one connection relative to the supplied local date. / 按给定本地日期对一条连接进行到期分类。
    /// </summary>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="today">Current local date. / 当前本地日期。</param>
    /// <param name="warningDays">Warning threshold in days. / 预警天数阈值。</param>
    /// <returns>Expiration classification. / 到期分类。</returns>
    public ExpirationState Classify(ConnectionProfile profile, DateTime today, int warningDays)
    {
        if (profile.ExpiresOn is null)
        {
            return ExpirationState.NotSet;
        }

        int remainingDays = (profile.ExpiresOn.Value.Date - today.Date).Days;
        if (remainingDays < 0)
        {
            return ExpirationState.Expired;
        }

        if (remainingDays == 0)
        {
            return ExpirationState.Today;
        }

        return remainingDays <= Math.Max(0, warningDays) ? ExpirationState.ExpiringSoon : ExpirationState.Healthy;
    }

    /// <summary>
    /// Gets the signed number of days until expiration. / 获取距离到期日的有符号天数。
    /// </summary>
    /// <param name="profile">Connection profile. / 连接配置。</param>
    /// <param name="today">Current local date. / 当前本地日期。</param>
    /// <returns>Days remaining, or null when no date is set. / 剩余天数；未设置日期时为空。</returns>
    public int? GetRemainingDays(ConnectionProfile profile, DateTime today)
    {
        return profile.ExpiresOn is DateTime expiration
            ? (expiration.Date - today.Date).Days
            : null;
    }
}

/// <summary>
/// Defines expiration display states. / 定义到期显示状态。
/// </summary>
public enum ExpirationState
{
    NotSet,
    Healthy,
    ExpiringSoon,
    Today,
    Expired
}
