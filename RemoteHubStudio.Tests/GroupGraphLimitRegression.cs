using RemoteHubStudio.Application;
using RemoteHubStudio.Domain;

namespace RemoteHubStudio.Tests;

/// <summary>
/// Provides independently callable regression checks for bounded, non-recursive group-graph validation. / 提供可独立调用的受限非递归分类图验证回归检查。
/// </summary>
internal static class GroupGraphLimitRegression
{
    /// <summary>
    /// Runs long-chain, long-cycle, depth-boundary, and identifier-reference checks. / 运行长链、长循环、深度边界与标识引用检查。
    /// </summary>
    public static void Run()
    {
        TestLongChain();
        TestLongCycle();
        TestDepthBoundary();
        TestIdentifierAndReferenceValidation();
    }

    /// <summary>
    /// Verifies a 50,000-node chain is rejected without recursive traversal or stack exhaustion. / 验证 50,000 节点长链会在不递归且不耗尽堆栈的情况下被拒绝。
    /// </summary>
    private static void TestLongChain()
    {
        List<ConnectionGroup> groups = CreateChain(50_000);
        groups.Reverse();
        AssertInvalidData(
            () => GroupGraphValidator.Validate(groups),
            "A 50,000-node group chain was accepted. / 50,000 节点分类长链被接受。");
    }

    /// <summary>
    /// Verifies a 50,000-node cycle is detected by the explicit traversal stack. / 验证 50,000 节点循环会被显式遍历栈检测。
    /// </summary>
    private static void TestLongCycle()
    {
        List<ConnectionGroup> groups = CreateChain(50_000);
        groups[0].ParentId = groups[^1].Id;
        AssertInvalidData(
            () => GroupGraphValidator.Validate(groups),
            "A 50,000-node group cycle was accepted. / 50,000 节点分类循环被接受。");
    }

    /// <summary>
    /// Verifies exactly 64 levels are valid and the 65th level is rejected. / 验证恰好 64 层有效，且第 65 层会被拒绝。
    /// </summary>
    private static void TestDepthBoundary()
    {
        GroupGraphValidator.Validate(CreateChain(GroupGraphValidator.MaximumDepth));
        AssertInvalidData(
            () => GroupGraphValidator.Validate(CreateChain(GroupGraphValidator.MaximumDepth + 1)),
            "A 65-level group chain was accepted. / 65 层分类链被接受。");
    }

    /// <summary>
    /// Verifies empty identifiers, duplicate identifiers, and missing parents are rejected. / 验证空标识、重复标识与缺失父分类会被拒绝。
    /// </summary>
    private static void TestIdentifierAndReferenceValidation()
    {
        AssertInvalidData(
            () => GroupGraphValidator.Validate([new ConnectionGroup { Id = Guid.Empty }]),
            "An empty group identifier was accepted. / 空分类标识被接受。");

        Guid duplicateId = Guid.NewGuid();
        AssertInvalidData(
            () => GroupGraphValidator.Validate(
            [
                new ConnectionGroup { Id = duplicateId },
                new ConnectionGroup { Id = duplicateId }
            ]),
            "A duplicate group identifier was accepted. / 重复分类标识被接受。");

        AssertInvalidData(
            () => GroupGraphValidator.Validate(
            [
                new ConnectionGroup { Id = Guid.NewGuid(), ParentId = Guid.NewGuid() }
            ]),
            "A missing group parent was accepted. / 缺失的父分类被接受。");
    }

    /// <summary>
    /// Creates a root-first group chain of the requested depth. / 创建指定深度的根优先分类链。
    /// </summary>
    /// <param name="count">Number of groups and resulting nesting depth. / 分类数量及结果嵌套深度。</param>
    /// <returns>The generated chain. / 生成的分类链。</returns>
    private static List<ConnectionGroup> CreateChain(int count)
    {
        List<ConnectionGroup> groups = new(count);
        Guid? parentId = null;
        for (int index = 0; index < count; index++)
        {
            ConnectionGroup group = new()
            {
                Id = Guid.NewGuid(),
                Name = $"Group {index}",
                ParentId = parentId
            };
            groups.Add(group);
            parentId = group.Id;
        }

        return groups;
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
