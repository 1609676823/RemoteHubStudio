using System.Text.Json;
using RemoteHubStudio.Application;

namespace RemoteHubStudio.Infrastructure;

/// <summary>
/// Performs allocation-light structural checks before untrusted workspace JSON reaches a DOM or model. / 在不可信工作区 JSON 进入 DOM 或模型前执行低分配结构检查。
/// </summary>
internal static class WorkspaceJsonPreflight
{
    /// <summary>Defines the maximum JSON token count in one workspace representation. / 定义单个工作区表示中的 JSON token 最大数量。</summary>
    public const int MaximumTokenCount = 4_000_000;

    /// <summary>Defines the maximum number of arrays in one workspace representation. / 定义单个工作区表示中的数组最大数量。</summary>
    public const int MaximumArrayCount = 100_000;

    /// <summary>Defines the maximum aggregate number of direct array elements. / 定义所有数组直接元素的最大聚合数量。</summary>
    public const int MaximumArrayElementCount = 500_000;

    private const int MaximumStructuralPropertyNameByteCount = 128;

    /// <summary>
    /// Scans one complete UTF-8 JSON value while bounding tokens, arrays, strings, nesting, and entity collections. / 扫描一个完整 UTF-8 JSON 值，同时限制 token、数组、字符串、嵌套及实体集合。
    /// </summary>
    /// <param name="utf8Json">Contiguous UTF-8 JSON bytes. / 连续的 UTF-8 JSON 字节。</param>
    /// <param name="maximumDepth">Maximum accepted JSON nesting depth. / 可接受的 JSON 最大嵌套深度。</param>
    /// <param name="maximumStringTokenBytes">Maximum encoded bytes in one string value. / 单个字符串值的最大编码字节数。</param>
    /// <param name="maximumTotalStringTokenBytes">Maximum aggregate encoded string bytes. / 字符串编码字节的最大聚合值。</param>
    /// <param name="maximumRootPayloadStringTokenBytes">Optional larger bound for an encoded root-envelope payload. / 根信封编码载荷的可选较大上限。</param>
    public static void Validate(
        ReadOnlySpan<byte> utf8Json,
        int maximumDepth,
        int maximumStringTokenBytes,
        long maximumTotalStringTokenBytes,
        int maximumRootPayloadStringTokenBytes = 0)
    {
        if (maximumDepth <= 0 || maximumStringTokenBytes <= 0 || maximumTotalStringTokenBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDepth));
        }

        Utf8JsonReader reader = new(
            utf8Json,
            new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = maximumDepth
            });
        List<ArrayFrame> arrays = [];
        EntityCollection pendingCollection = EntityCollection.None;
        bool pendingRootPayload = false;
        int tokenCount = 0;
        int arrayCount = 0;
        int arrayElementCount = 0;
        long totalStringBytes = 0;
        int groupCount = 0;
        int connectionCount = 0;

        while (reader.Read())
        {
            tokenCount++;
            if (tokenCount > MaximumTokenCount)
            {
                throw new InvalidDataException($"The JSON document exceeds {MaximumTokenCount} tokens. / JSON 文档超过 {MaximumTokenCount} 个 token。");
            }

            CountDirectArrayElement(reader.TokenType, reader.CurrentDepth, arrays, ref arrayElementCount);
            if (arrayElementCount > MaximumArrayElementCount)
            {
                throw new InvalidDataException($"The JSON document exceeds {MaximumArrayElementCount} array elements. / JSON 文档超过 {MaximumArrayElementCount} 个数组元素。");
            }

            if (reader.TokenType is JsonTokenType.PropertyName or JsonTokenType.String)
            {
                int stringByteCount = reader.HasValueSequence
                    ? checked((int)reader.ValueSequence.Length)
                    : reader.ValueSpan.Length;
                bool rootPayloadValue = reader.TokenType == JsonTokenType.String && pendingRootPayload;
                int tokenLimit = reader.TokenType == JsonTokenType.PropertyName
                    ? maximumStringTokenBytes
                    : rootPayloadValue && maximumRootPayloadStringTokenBytes > 0
                        ? maximumRootPayloadStringTokenBytes
                        : maximumStringTokenBytes;
                if (stringByteCount > tokenLimit)
                {
                    throw new InvalidDataException($"A JSON string exceeds {tokenLimit} encoded bytes. / JSON 字符串超过 {tokenLimit} 个编码字节。");
                }

                if (!rootPayloadValue)
                {
                    totalStringBytes += stringByteCount;
                    if (totalStringBytes > maximumTotalStringTokenBytes)
                    {
                        throw new InvalidDataException($"The JSON document exceeds {maximumTotalStringTokenBytes} encoded string bytes. / JSON 文档字符串编码总量超过 {maximumTotalStringTokenBytes} 字节。");
                    }
                }
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueIsEscaped)
                {
                    if (reader.ValueSpan.Length <= MaximumStructuralPropertyNameByteCount)
                    {
                        string propertyName = reader.GetString() ?? string.Empty;
                        pendingCollection = ReadEntityCollection(propertyName);
                        pendingRootPayload = reader.CurrentDepth == 1 &&
                                             string.Equals(propertyName, "payload", StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        pendingCollection = EntityCollection.None;
                        pendingRootPayload = false;
                    }
                }
                else
                {
                    pendingCollection = ReadEntityCollection(reader.ValueSpan);
                    pendingRootPayload = reader.CurrentDepth == 1 && EqualsAsciiIgnoreCase(reader.ValueSpan, "payload"u8);
                }

                continue;
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                arrayCount++;
                if (arrayCount > MaximumArrayCount)
                {
                    throw new InvalidDataException($"The JSON document exceeds {MaximumArrayCount} arrays. / JSON 文档超过 {MaximumArrayCount} 个数组。");
                }

                arrays.Add(new ArrayFrame(reader.CurrentDepth, pendingCollection));
                pendingCollection = EntityCollection.None;
                pendingRootPayload = false;
                continue;
            }

            if (reader.TokenType == JsonTokenType.EndArray)
            {
                ArrayFrame completed = arrays[^1];
                arrays.RemoveAt(arrays.Count - 1);
                AddEntityCount(
                    completed.Collection,
                    completed.ElementCount,
                    ref groupCount,
                    ref connectionCount);
                pendingCollection = EntityCollection.None;
                pendingRootPayload = false;
                continue;
            }

            pendingCollection = EntityCollection.None;
            pendingRootPayload = false;
        }

        WorkspaceLimits.ValidateCounts(groupCount, connectionCount);
    }

    /// <summary>
    /// Counts a value beginning directly inside the active array without recursively traversing it. / 在不递归遍历的情况下统计直接位于活动数组内开始的值。
    /// </summary>
    /// <param name="tokenType">Current token type. / 当前 token 类型。</param>
    /// <param name="currentDepth">Current reader depth. / 当前读取器深度。</param>
    /// <param name="arrays">Open array frames. / 已打开的数组帧。</param>
    /// <param name="totalElementCount">Aggregate direct element count. / 直接元素聚合数量。</param>
    private static void CountDirectArrayElement(
        JsonTokenType tokenType,
        int currentDepth,
        List<ArrayFrame> arrays,
        ref int totalElementCount)
    {
        if (arrays.Count == 0 || tokenType is JsonTokenType.EndArray or JsonTokenType.EndObject or JsonTokenType.PropertyName)
        {
            return;
        }

        int frameIndex = arrays.Count - 1;
        ArrayFrame frame = arrays[frameIndex];
        if (currentDepth != frame.Depth + 1)
        {
            return;
        }

        frame.ElementCount++;
        arrays[frameIndex] = frame;
        totalElementCount++;
    }

    /// <summary>
    /// Maps an unescaped ASCII property name to a bounded entity collection. / 将未转义 ASCII 属性名映射为受限实体集合。
    /// </summary>
    /// <param name="propertyName">UTF-8 property-name bytes. / UTF-8 属性名字节。</param>
    /// <returns>The recognized collection kind. / 识别出的集合类型。</returns>
    private static EntityCollection ReadEntityCollection(ReadOnlySpan<byte> propertyName)
    {
        if (EqualsAsciiIgnoreCase(propertyName, "groups"u8))
        {
            return EntityCollection.Groups;
        }

        return EqualsAsciiIgnoreCase(propertyName, "connections"u8)
            ? EntityCollection.Connections
            : EntityCollection.None;
    }

    /// <summary>
    /// Maps one safely bounded decoded property name to an entity collection. / 将一个已安全限制并解码的属性名映射为实体集合。
    /// </summary>
    /// <param name="propertyName">Decoded property name. / 已解码的属性名。</param>
    /// <returns>The recognized collection kind. / 识别出的集合类型。</returns>
    private static EntityCollection ReadEntityCollection(string propertyName)
    {
        if (string.Equals(propertyName, "groups", StringComparison.OrdinalIgnoreCase))
        {
            return EntityCollection.Groups;
        }

        return string.Equals(propertyName, "connections", StringComparison.OrdinalIgnoreCase)
            ? EntityCollection.Connections
            : EntityCollection.None;
    }

    /// <summary>
    /// Compares UTF-8 ASCII bytes without case sensitivity or text allocation. / 在不区分大小写且不分配文本的情况下比较 UTF-8 ASCII 字节。
    /// </summary>
    /// <param name="left">Candidate bytes. / 候选字节。</param>
    /// <param name="rightLowercase">Lower-case ASCII bytes. / 小写 ASCII 字节。</param>
    /// <returns>True when the values match ignoring ASCII case. / 忽略 ASCII 大小写后匹配时返回 true。</returns>
    private static bool EqualsAsciiIgnoreCase(ReadOnlySpan<byte> left, ReadOnlySpan<byte> rightLowercase)
    {
        if (left.Length != rightLowercase.Length)
        {
            return false;
        }

        for (int index = 0; index < left.Length; index++)
        {
            byte value = left[index];
            if (value is >= (byte)'A' and <= (byte)'Z')
            {
                value = (byte)(value + ('a' - 'A'));
            }

            if (value != rightLowercase[index])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Adds one completed entity-array count and immediately enforces central limits. / 累加一个已完成实体数组的数量并立即执行中央限额。
    /// </summary>
    /// <param name="collection">Completed collection kind. / 已完成的集合类型。</param>
    /// <param name="elementCount">Direct elements in the array. / 数组中的直接元素数。</param>
    /// <param name="groupCount">Running group count. / 正在累加的分类数。</param>
    /// <param name="connectionCount">Running connection count. / 正在累加的连接数。</param>
    private static void AddEntityCount(
        EntityCollection collection,
        int elementCount,
        ref int groupCount,
        ref int connectionCount)
    {
        switch (collection)
        {
            case EntityCollection.Groups:
                groupCount = checked(groupCount + elementCount);
                break;
            case EntityCollection.Connections:
                connectionCount = checked(connectionCount + elementCount);
                break;
        }

        WorkspaceLimits.ValidateCounts(groupCount, connectionCount);
    }

    private enum EntityCollection
    {
        None,
        Groups,
        Connections
    }

    /// <summary>
    /// Tracks one open JSON array and its recognized workspace collection kind. / 跟踪一个已打开的 JSON 数组及其识别出的工作区集合类型。
    /// </summary>
    /// <param name="depth">Reader depth at which the array began. / 数组开始时的读取器深度。</param>
    /// <param name="collection">Recognized workspace collection kind. / 识别出的工作区集合类型。</param>
    private struct ArrayFrame(int depth, EntityCollection collection)
    {
        public int Depth { get; } = depth;

        public EntityCollection Collection { get; } = collection;

        public int ElementCount { get; set; }
    }
}
