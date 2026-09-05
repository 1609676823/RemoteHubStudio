namespace RemoteHubStudio.Localization;

/// <summary>
/// Describes one validated language pack shown in the language selector. / 描述语言选择器中显示的一个已验证语言包。
/// </summary>
/// <param name="Code">Canonical BCP-47 locale code. / 规范化的 BCP-47 区域标识。</param>
/// <param name="Name">Language name in English. / 语言的英文名称。</param>
/// <param name="NativeName">Language name written in that language. / 语言的本地名称。</param>
/// <param name="Authors">Optional translator credits. / 可选的翻译者信息。</param>
public sealed record LanguageInfo(
    string Code,
    string Name,
    string NativeName,
    IReadOnlyList<string>? Authors = null)
{
    /// <summary>Gets a compact bilingual-friendly display name. / 获取适合双语显示的紧凑名称。</summary>
    public string DisplayName =>
        string.IsNullOrWhiteSpace(Name) ||
        string.Equals(Name, NativeName, StringComparison.OrdinalIgnoreCase)
            ? NativeName
            : $"{NativeName} ({Name})";

    /// <inheritdoc />
    public override string ToString() => DisplayName;
}
