using System.Text;
using RemoteHubStudio.Application;

namespace RemoteHubStudio.Infrastructure.ImportExport;

/// <summary>
/// Encodes and decodes RFC 4180-style CSV content without external dependencies. / 无外部依赖地编码和解码 RFC 4180 风格 CSV 内容。
/// </summary>
public static class CsvCodec
{
    /// <summary>Defines the maximum CSV record count, including one header plus every supported connection. / 定义 CSV 最大记录数，包括一个表头及全部支持的连接。</summary>
    public const int MaximumRecordCount = WorkspaceLimits.MaximumConnectionCount + 1;

    private const int MaximumFieldsPerRecord = 64;
    private const int MaximumFieldCharacterCount = 256 * 1024;
    private const int MaximumEncodedCharacterCount = 16 * 1024 * 1024;

    /// <summary>
    /// Encodes rows as UTF-8 CSV text. / 将多行数据编码为 UTF-8 CSV 文本。
    /// </summary>
    /// <param name="rows">Rows to encode. / 要编码的行。</param>
    /// <returns>CSV document text. / CSV 文档文本。</returns>
    public static string Encode(IEnumerable<IReadOnlyList<string?>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        StringBuilder builder = new();
        int recordCount = 0;
        foreach (IReadOnlyList<string?> row in rows)
        {
            EnsureRecordCapacity(recordCount);
            ValidateEncodedRow(row);
            int encodedRecordLength = GetEncodedRecordCharacterCount(row);
            if (builder.Length > MaximumEncodedCharacterCount - encodedRecordLength)
            {
                throw new InvalidDataException($"The encoded CSV exceeds {MaximumEncodedCharacterCount} characters. / 编码后的 CSV 超过 {MaximumEncodedCharacterCount} 个字符。");
            }

            builder.AppendLine(string.Join(',', row.Select(EscapeField)));
            recordCount++;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Calculates one encoded record length before the output buffer grows. / 在输出缓冲区增长前计算一条编码记录的长度。
    /// </summary>
    /// <param name="row">Validated source record. / 已验证的源记录。</param>
    /// <returns>Encoded characters including the platform line ending. / 包含平台换行符的编码字符数。</returns>
    private static int GetEncodedRecordCharacterCount(IReadOnlyList<string?> row)
    {
        long characterCount = Math.Max(0, row.Count - 1) + Environment.NewLine.Length;
        for (int index = 0; index < row.Count; index++)
        {
            string field = row[index] ?? string.Empty;
            bool requiresQuotes = field.IndexOfAny([',', '"', '\r', '\n']) >= 0;
            characterCount += field.Length;
            if (requiresQuotes)
            {
                characterCount += 2;
                characterCount += field.Count(character => character == '"');
            }

            if (characterCount > MaximumEncodedCharacterCount)
            {
                throw new InvalidDataException($"The encoded CSV exceeds {MaximumEncodedCharacterCount} characters. / 编码后的 CSV 超过 {MaximumEncodedCharacterCount} 个字符。");
            }
        }

        return (int)characterCount;
    }

    /// <summary>
    /// Validates encoded field-count and field-length bounds before growing the output buffer. / 在扩展输出缓冲区前验证编码字段数量与字段长度上限。
    /// </summary>
    /// <param name="row">Source record to validate. / 要验证的源记录。</param>
    private static void ValidateEncodedRow(IReadOnlyList<string?> row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Count > MaximumFieldsPerRecord)
        {
            throw new InvalidDataException($"A CSV record exceeds {MaximumFieldsPerRecord} fields. / CSV 记录超过 {MaximumFieldsPerRecord} 个字段。");
        }

        for (int index = 0; index < row.Count; index++)
        {
            if ((row[index]?.Length ?? 0) > MaximumFieldCharacterCount)
            {
                throw new InvalidDataException($"A CSV field exceeds {MaximumFieldCharacterCount} characters. / CSV 字段超过 {MaximumFieldCharacterCount} 个字符。");
            }
        }
    }

    /// <summary>
    /// Parses CSV text, including quoted commas and line breaks. / 解析 CSV 文本，包括带引号的逗号和换行。
    /// </summary>
    /// <param name="text">CSV document text. / CSV 文档文本。</param>
    /// <returns>Parsed rows and fields. / 解析后的行与字段。</returns>
    /// <exception cref="FormatException">Thrown when a quoted field is not terminated. / 引用字段未结束时抛出。</exception>
    public static IReadOnlyList<IReadOnlyList<string>> Decode(string text)
    {
        List<IReadOnlyList<string>> rows = [];
        List<string> row = [];
        StringBuilder field = new(capacity: 256, maxCapacity: MaximumFieldCharacterCount);
        bool quoted = false;

        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];
            if (quoted)
            {
                if (current == '"' && index + 1 < text.Length && text[index + 1] == '"')
                {
                    AppendFieldCharacter(field, '"');
                    index++;
                }
                else if (current == '"')
                {
                    quoted = false;
                }
                else
                {
                    AppendFieldCharacter(field, current);
                }

                continue;
            }

            if (current == '"' && field.Length == 0)
            {
                quoted = true;
            }
            else if (current == ',')
            {
                AddField(row, field);
            }
            else if (current is '\r' or '\n')
            {
                EnsureRecordCapacity(rows.Count);
                AddField(row, field);
                rows.Add(row);
                row = [];
                if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }
            }
            else
            {
                AppendFieldCharacter(field, current);
            }
        }

        if (quoted)
        {
            throw new FormatException("CSV contains an unterminated quoted field. / CSV 包含未结束的引用字段。");
        }

        if (field.Length > 0 || row.Count > 0)
        {
            EnsureRecordCapacity(rows.Count);
            AddField(row, field);
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// Appends one decoded character while bounding field growth before allocation. / 在分配前限制字段增长，并追加一个已解码字符。
    /// </summary>
    /// <param name="field">Field buffer. / 字段缓冲区。</param>
    /// <param name="value">Character to append. / 要追加的字符。</param>
    private static void AppendFieldCharacter(StringBuilder field, char value)
    {
        if (field.Length >= MaximumFieldCharacterCount)
        {
            throw new InvalidDataException($"A CSV field exceeds {MaximumFieldCharacterCount} characters. / CSV 字段超过 {MaximumFieldCharacterCount} 个字符。");
        }

        field.Append(value);
    }

    /// <summary>
    /// Materializes one bounded field only after verifying the record field limit. / 在验证记录字段上限后才实例化一个受限字段。
    /// </summary>
    /// <param name="row">Current record fields. / 当前记录字段。</param>
    /// <param name="field">Field buffer. / 字段缓冲区。</param>
    private static void AddField(List<string> row, StringBuilder field)
    {
        if (row.Count >= MaximumFieldsPerRecord)
        {
            throw new InvalidDataException($"A CSV record exceeds {MaximumFieldsPerRecord} fields. / CSV 记录超过 {MaximumFieldsPerRecord} 个字段。");
        }

        row.Add(field.ToString());
        field.Clear();
    }

    /// <summary>
    /// Verifies another CSV record can be added before allocating its fields. / 在分配记录字段前验证是否还可添加一条 CSV 记录。
    /// </summary>
    /// <param name="recordCount">Number of records already decoded. / 已解码的记录数。</param>
    private static void EnsureRecordCapacity(int recordCount)
    {
        if (recordCount >= MaximumRecordCount)
        {
            throw new InvalidDataException($"The CSV document exceeds {MaximumRecordCount} records. / CSV 文档超过 {MaximumRecordCount} 条记录。");
        }
    }

    /// <summary>
    /// Escapes one field when it contains CSV control characters. / 当字段包含 CSV 控制字符时进行转义。
    /// </summary>
    /// <param name="value">Field value. / 字段值。</param>
    /// <returns>Escaped field text. / 转义后的字段文本。</returns>
    private static string EscapeField(string? value)
    {
        string field = value ?? string.Empty;
        return field.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{field.Replace("\"", "\"\"")}\""
            : field;
    }
}
