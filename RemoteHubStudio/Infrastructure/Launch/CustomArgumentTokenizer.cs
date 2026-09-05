using System.Text;

namespace RemoteHubStudio.Infrastructure.Launch;

/// <summary>
/// Tokenizes user-authored argument templates without invoking a command shell. / 在不调用命令行外壳的情况下拆分用户编写的参数模板。
/// </summary>
internal static class CustomArgumentTokenizer
{
    /// <summary>
    /// Splits an argument template while honoring single quotes, double quotes, and escaped quote characters. / 拆分参数模板，并识别单引号、双引号和转义引号字符。
    /// </summary>
    /// <param name="template">Argument template to tokenize. / 要拆分的参数模板。</param>
    /// <returns>Individual argument tokens with grouping quotes removed. / 移除分组引号后的独立参数标记。</returns>
    /// <exception cref="LaunchValidationException">Thrown when a quote is not closed. / 引号未闭合时抛出。</exception>
    public static IReadOnlyList<string> Tokenize(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return [];
        }

        List<string> arguments = [];
        StringBuilder current = new();
        bool inSingleQuotes = false;
        bool inDoubleQuotes = false;
        bool tokenStarted = false;

        for (int index = 0; index < template.Length; index++)
        {
            char character = template[index];

            if (character == '\'' && !inDoubleQuotes)
            {
                inSingleQuotes = !inSingleQuotes;
                tokenStarted = true;
                continue;
            }

            if (character == '"' && !inSingleQuotes)
            {
                inDoubleQuotes = !inDoubleQuotes;
                tokenStarted = true;
                continue;
            }

            if (character == '\\' && index + 1 < template.Length && IsEscapable(template[index + 1], inSingleQuotes, inDoubleQuotes))
            {
                current.Append(template[++index]);
                tokenStarted = true;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inSingleQuotes && !inDoubleQuotes)
            {
                AddToken(arguments, current, ref tokenStarted);
                continue;
            }

            current.Append(character);
            tokenStarted = true;
        }

        if (inSingleQuotes || inDoubleQuotes)
        {
            throw new LaunchValidationException("Custom arguments contain an unclosed quote. / 自定义参数包含未闭合的引号。");
        }

        AddToken(arguments, current, ref tokenStarted);
        return arguments;
    }

    /// <summary>
    /// Determines whether a backslash should escape the following character in the current quote context. / 判断反斜杠在当前引号上下文中是否应转义后续字符。
    /// </summary>
    /// <param name="nextCharacter">Character after the backslash. / 反斜杠后的字符。</param>
    /// <param name="inSingleQuotes">Whether the parser is inside single quotes. / 解析器是否位于单引号内。</param>
    /// <param name="inDoubleQuotes">Whether the parser is inside double quotes. / 解析器是否位于双引号内。</param>
    /// <returns>True when the next character is escaped. / 后续字符应被转义时返回 true。</returns>
    private static bool IsEscapable(char nextCharacter, bool inSingleQuotes, bool inDoubleQuotes)
    {
        return (nextCharacter == '\'' && inSingleQuotes) || (nextCharacter == '"' && inDoubleQuotes);
    }

    /// <summary>
    /// Appends the current token, including an intentionally empty quoted token, and resets parser state. / 追加当前标记（包括有意保留的空引号标记）并重置解析状态。
    /// </summary>
    /// <param name="arguments">Destination argument collection. / 目标参数集合。</param>
    /// <param name="current">Current token buffer. / 当前标记缓冲区。</param>
    /// <param name="tokenStarted">Whether a token has started. / 是否已经开始一个标记。</param>
    private static void AddToken(List<string> arguments, StringBuilder current, ref bool tokenStarted)
    {
        if (!tokenStarted)
        {
            return;
        }

        arguments.Add(current.ToString());
        current.Clear();
        tokenStarted = false;
    }
}
