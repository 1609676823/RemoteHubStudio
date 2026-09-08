using RemoteHubStudio.Infrastructure.Monitoring;
using RemoteHubStudio.UI.Main;

namespace RemoteHubStudio.Tests;

/// <summary>
/// Covers generation isolation and pending-state cleanup for overlapping status checks. / 覆盖重叠状态检测的代数隔离与等待状态清理。
/// </summary>
internal static class ConnectionStatusBatchRegression
{
    /// <summary>
    /// Runs status batch replacement and empty-view regression checks. / 运行状态批次替换与空视图回归检查。
    /// </summary>
    public static void Run()
    {
        Guid first = Guid.NewGuid();
        Guid shared = Guid.NewGuid();
        Guid replacement = Guid.NewGuid();
        ConnectionStatusBatchState state = new();

        long firstGeneration = state.BeginBatch([first, shared]);
        Assert(state.GetState(first) == ReachabilityState.Checking, "First batch did not mark its connection as checking. / 首批次未将其连接标记为检测中。");

        long replacementGeneration = state.BeginBatch([shared, replacement]);
        Assert(state.GetState(first) == ReachabilityState.Unknown, "Replaced batch left a stale checking status. / 被替换批次留下了陈旧检测中状态。");
        Assert(state.GetState(shared) == ReachabilityState.Checking && state.GetState(replacement) == ReachabilityState.Checking, "Replacement batch did not own exactly its visible connections. / 替代批次未准确拥有其可见连接。");

        bool staleApplied = state.TryApplyResults(firstGeneration,
        [
            new ConnectionStatus { ConnectionId = first, State = ReachabilityState.Reachable }
        ]);
        Assert(!staleApplied && state.GetState(first) == ReachabilityState.Unknown, "A stale batch overwrote current status state. / 陈旧批次覆盖了当前状态。");

        bool currentApplied = state.TryApplyResults(replacementGeneration,
        [
            new ConnectionStatus { ConnectionId = shared, State = ReachabilityState.Reachable }
        ]);
        Assert(currentApplied && state.GetState(shared) == ReachabilityState.Reachable, "Current batch result was not applied. / 当前批次结果未被应用。");

        long emptyGeneration = state.BeginBatch([]);
        Assert(emptyGeneration > replacementGeneration, "Empty-view batch did not advance generation. / 空视图批次未推进代数。");
        Assert(state.GetState(shared) == ReachabilityState.Reachable, "Completed status was discarded while replacing a batch. / 替换批次时丢弃了已完成状态。");
        Assert(state.GetState(replacement) == ReachabilityState.Unknown, "Empty-view batch left an unresolved checking status. / 空视图批次留下了未完成的检测中状态。");
        Assert(!state.TryFinishBatch(replacementGeneration), "A replaced batch was allowed to finish the current state. / 被替换批次获准结束当前状态。");
        Assert(state.TryFinishBatch(emptyGeneration), "The current empty-view batch could not finish. / 当前空视图批次无法结束。");
    }

    /// <summary>
    /// Throws when a status batch regression assertion fails. / 状态批次回归断言失败时抛出异常。
    /// </summary>
    /// <param name="condition">Condition expected to be true. / 预期为真的条件。</param>
    /// <param name="message">Bilingual failure message. / 双语失败消息。</param>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
