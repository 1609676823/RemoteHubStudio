using RemoteHubStudio.Application;
using RemoteHubStudio.Domain;

namespace RemoteHubStudio.Tests;

/// <summary>
/// Provides independently callable checks for central workspace entity limits and combined-merge protection. / 提供可独立调用的中央工作区实体上限与合并防护检查。
/// </summary>
internal static class WorkspaceLimitsRegression
{
    /// <summary>
    /// Runs individual and combined-document boundary checks. / 运行单项与组合文档边界检查。
    /// </summary>
    public static void Run()
    {
        TestExplicitCountBoundaries();
        TestCombinedDocumentsCannotBypassLimits();
    }

    /// <summary>
    /// Verifies exact count limits are accepted while per-type overflow is rejected. / 验证恰好达到数量上限时可接受，而单类超限会被拒绝。
    /// </summary>
    private static void TestExplicitCountBoundaries()
    {
        WorkspaceLimits.ValidateCounts(
            WorkspaceLimits.MaximumGroupCount,
            WorkspaceLimits.MaximumConnectionCount);
        AssertInvalidData(
            () => WorkspaceLimits.ValidateCounts(WorkspaceLimits.MaximumGroupCount + 1L, 0),
            "An oversized group count was accepted. / 超限的分类数被接受。");
        AssertInvalidData(
            () => WorkspaceLimits.ValidateCounts(0, WorkspaceLimits.MaximumConnectionCount + 1L),
            "An oversized connection count was accepted. / 超限的连接数被接受。");
    }

    /// <summary>
    /// Verifies two individually valid workspaces cannot exceed a collection limit when merged. / 验证两份单独有效的工作区合并时不能绕过集合上限。
    /// </summary>
    private static void TestCombinedDocumentsCannotBypassLimits()
    {
        AppDataDocument existing = new()
        {
            Connections = Enumerable.Repeat(
                    new ConnectionProfile(),
                    WorkspaceLimits.MaximumConnectionCount)
                .ToList()
        };
        AppDataDocument incoming = new()
        {
            Connections = [new ConnectionProfile()]
        };

        WorkspaceLimits.ValidateDocument(existing);
        WorkspaceLimits.ValidateDocument(incoming);
        AssertInvalidData(
            () => WorkspaceLimits.ValidateCombined(existing, incoming),
            "Two individually valid imports bypassed the connection limit. / 两份单独有效的导入绕过了连接数量上限。");
    }

    /// <summary>
    /// Verifies an action throws InvalidDataException. / 验证操作抛出 InvalidDataException。
    /// </summary>
    /// <param name="action">Action to execute. / 要执行的操作。</param>
    /// <param name="message">Failure message. / 失败消息。</param>
    private static void AssertInvalidData(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}
