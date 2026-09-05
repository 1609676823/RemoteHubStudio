using System.Globalization;

namespace RemoteHubStudio.Localization;

/// <summary>Validates and canonicalizes locale identifiers shared by localization storage and loading. / 验证并规范化本地化存储与加载共用的区域标识。</summary>
internal static class LanguageTag
{
    internal const int MaximumLength = 64;

    /// <summary>Returns a canonical .NET-recognized BCP-47 tag, or <see langword="null"/> for invalid input. / 返回 .NET 可识别的规范 BCP-47 标识，输入无效时返回 <see langword="null"/>。</summary>
    internal static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string candidate = value.Trim();
        if (candidate.Length > MaximumLength || !HasBcp47Shape(candidate))
        {
            return null;
        }

        try
        {
            CultureInfo culture = CultureInfo.GetCultureInfo(candidate);
            return string.IsNullOrEmpty(culture.Name) ? null : culture.Name;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    private static bool HasBcp47Shape(string value)
    {
        ReadOnlySpan<char> remaining = value.AsSpan();
        int subtagIndex = 0;
        while (!remaining.IsEmpty)
        {
            int separator = remaining.IndexOf('-');
            ReadOnlySpan<char> subtag = separator < 0 ? remaining : remaining[..separator];
            if (subtag.IsEmpty || subtag.Length > 8)
            {
                return false;
            }

            foreach (char character in subtag)
            {
                bool asciiLetter = character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
                bool asciiDigit = character is >= '0' and <= '9';
                if ((!asciiLetter && !asciiDigit) || (subtagIndex == 0 && !asciiLetter))
                {
                    return false;
                }
            }

            if (subtagIndex == 0 && subtag.Length is < 2 or > 8)
            {
                return false;
            }

            subtagIndex++;
            if (separator < 0)
            {
                break;
            }

            remaining = remaining[(separator + 1)..];
        }

        return subtagIndex > 0;
    }
}
