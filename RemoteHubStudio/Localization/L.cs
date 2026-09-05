using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using RemoteHubStudio.Infrastructure.Persistence;

namespace RemoteHubStudio.Localization;

/// <summary>
/// Provides bounded JSON language-pack loading, culture fallback, formatting, and WinForms control localization. / 提供有上限的 JSON 语言包加载、区域回退、格式化与 WinForms 控件本地化。
/// </summary>
public static class L
{
    /// <summary>Preference value that follows <see cref="CultureInfo.CurrentUICulture"/>. / 跟随 <see cref="CultureInfo.CurrentUICulture"/> 的偏好值。</summary>
    public const string SystemLanguage = "system";

    private const string EnglishLanguage = "en";
    private const int SupportedPackSchemaVersion = 1;
    private const int MaximumPackBytes = 512 * 1024;
    private const int MaximumExternalPackFiles = 128;
    private const int MaximumStringCount = 5_000;
    private const int MaximumStringValueLength = 4_096;
    private const int MaximumStringKeyLength = 256;
    private const int MaximumLanguageNameLength = 128;
    private const int MaximumAuthorCount = 32;
    private const int MaximumAuthorLength = 128;
    private const int MaximumSchemaUriLength = 2_048;

    private static readonly object SyncRoot = new();
    private static readonly object MutationRoot = new();
    private static readonly string[] ReflectedStringProperties =
    [
        "PlaceholderText",
        "SubText",
        "EmptyText",
        "ToggleText"
    ];
    private static readonly string HostUiLanguage = DetectSystemLanguage();

    private static LocalizationSnapshot _snapshot = LocalizationSnapshot.Empty;
    private static IReadOnlyList<LanguageInfo> _availableLanguages =
        Array.AsReadOnly([new LanguageInfo(EnglishLanguage, "English", "English")]);
    private static string[] _lookupLanguages = [EnglishLanguage];
    private static string _requestedLanguage = SystemLanguage;
    private static string _currentLanguage = EnglishLanguage;
    private static string _detectedSystemLanguage = EnglishLanguage;
    private static bool _initialized;

    /// <summary>Raised after a language selection is initialized or changed. / 在语言选择初始化或更改后触发。</summary>
    public static event EventHandler? LanguageChanged;

    /// <summary>Gets all structurally valid embedded and external language packs. / 获取所有结构有效的内嵌及外置语言包。</summary>
    public static IReadOnlyList<LanguageInfo> AvailableLanguages
    {
        get
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                return _availableLanguages;
            }
        }
    }

    /// <summary>Gets the saved/requested locale, retaining <see cref="SystemLanguage"/> when following the OS. / 获取已保存或请求的区域标识；跟随操作系统时保留 <see cref="SystemLanguage"/>。</summary>
    public static string Requested
    {
        get
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                return _requestedLanguage;
            }
        }
    }

    /// <summary>Compatibility alias for <see cref="Requested"/>. / <see cref="Requested"/> 的兼容别名。</summary>
    public static string RequestedLanguage => Requested;

    /// <summary>Gets the effective loaded locale after BCP-47 fallback. / 获取经 BCP-47 回退后实际加载的区域标识。</summary>
    public static string Current
    {
        get
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                return _currentLanguage;
            }
        }
    }

    /// <summary>Compatibility alias for <see cref="Current"/>. / <see cref="Current"/> 的兼容别名。</summary>
    public static string CurrentLanguage => Current;

    /// <summary>
    /// Reloads embedded, program-directory, and default data-directory packs, then selects a language. / 重新加载内嵌、程序目录及默认数据目录语言包，然后选择语言。
    /// </summary>
    /// <param name="preference">A BCP-47 locale, <see cref="SystemLanguage"/>, or blank for system language. / BCP-47 区域标识、<see cref="SystemLanguage"/>，或表示跟随系统的空值。</param>
    public static void Initialize(string? preference = null)
    {
        Initialize(preference, new AppDataPaths());
    }

    /// <summary>
    /// Reloads language packs using explicit data paths, primarily for portable hosts and tests. / 使用显式数据路径重新加载语言包，主要供便携式宿主与测试使用。
    /// </summary>
    /// <param name="preference">Requested language preference. / 请求的语言偏好。</param>
    /// <param name="paths">Application data paths containing the writable Languages directory. / 包含可写 Languages 目录的应用数据路径。</param>
    public static void Initialize(string? preference, AppDataPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        lock (MutationRoot)
        {
            InitializeCore(preference, paths);
        }
    }

    private static void InitializeCore(string? preference, AppDataPaths paths)
    {

        string detectedSystemLanguage = HostUiLanguage;
        LocalizationSnapshot snapshot;
        try
        {
            snapshot = LoadSnapshot(paths);
        }
        catch (Exception exception) when (IsRecoverablePackFailure(exception))
        {
            ReportPackFailure("Unable to initialize language packs.", exception);
            snapshot = LocalizationSnapshot.Empty;
        }

        string currentLanguage;
        lock (SyncRoot)
        {
            _detectedSystemLanguage = detectedSystemLanguage;
            _snapshot = snapshot;
            _availableLanguages = CreateAvailableLanguages(snapshot);
            _initialized = true;
            SelectLanguageNoLock(preference);
            currentLanguage = _currentLanguage;
        }

        ApplyUiCulture(currentLanguage);
        RaiseLanguageChanged();
    }

    /// <summary>Selects one of the already loaded packs without re-reading disk. / 在不重新读取磁盘的情况下选择一个已加载语言包。</summary>
    /// <param name="preference">Requested language preference. / 请求的语言偏好。</param>
    public static void SetLanguage(string? preference)
    {
        lock (MutationRoot)
        {
            string? currentLanguage = null;
            lock (SyncRoot)
            {
                if (_initialized)
                {
                    SelectLanguageNoLock(preference);
                    currentLanguage = _currentLanguage;
                }
            }

            if (currentLanguage is null)
            {
                InitializeCore(preference, new AppDataPaths());
                return;
            }

            ApplyUiCulture(currentLanguage);
            RaiseLanguageChanged();
        }
    }

    /// <summary>Attempts to resolve a key through the selected fallback chain. / 尝试通过已选回退链解析键。</summary>
    /// <param name="key">Stable localization key. / 稳定的本地化键。</param>
    /// <param name="value">Resolved value when found. / 找到时的解析值。</param>
    /// <returns>Whether a pack contains the key. / 是否有语言包包含该键。</returns>
    public static bool TryGet(string key, out string value)
    {
        EnsureInitialized();
        if (string.IsNullOrEmpty(key))
        {
            value = string.Empty;
            return false;
        }

        LocalizationSnapshot snapshot;
        string[] lookupLanguages;
        lock (SyncRoot)
        {
            snapshot = _snapshot;
            lookupLanguages = _lookupLanguages;
        }

        return TryResolve(snapshot, lookupLanguages, key, out value);
    }

    /// <summary>Resolves a key, returning the key itself only when no validated pack defines it. / 解析键，仅在没有已验证语言包定义时返回键本身。</summary>
    /// <param name="key">Stable localization key. / 稳定的本地化键。</param>
    /// <returns>The localized value or the missing key. / 本地化值或缺失键。</returns>
    public static string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return key ?? string.Empty;
        }

        return TryGet(key, out string value) ? value : key;
    }

    /// <summary>Formats a validated localized composite-format string with the current UI culture. / 使用当前 UI 区域设置格式化已验证的本地化复合格式字符串。</summary>
    /// <param name="key">Stable localization key. / 稳定的本地化键。</param>
    /// <param name="arguments">Composite-format arguments. / 复合格式参数。</param>
    /// <returns>The formatted localized value, or a safe English/key fallback. / 格式化的本地化值，或安全的英文/键回退值。</returns>
    public static string Format(string key, params object?[] arguments)
    {
        arguments ??= [];
        string localized = Get(key);
        try
        {
            return string.Format(CultureInfo.CurrentUICulture, localized, arguments);
        }
        catch (FormatException)
        {
            string english = GetEnglish(key);
            try
            {
                return string.Format(CultureInfo.GetCultureInfo(EnglishLanguage), english, arguments);
            }
            catch (FormatException)
            {
                return english;
            }
        }
    }

    /// <summary>
    /// Applies existing <c>&lt;scope&gt;.&lt;control-name&gt;.&lt;property&gt;</c> keys to a WinForms control tree. / 将已存在的 <c>&lt;scope&gt;.&lt;控件名&gt;.&lt;属性&gt;</c> 键应用到 WinForms 控件树。
    /// </summary>
    /// <remarks>
    /// The root uses <c>$this</c>. Supported properties are Text, AccessibleName, AccessibleDescription,
    /// PlaceholderText, SubText, EmptyText, and ToggleText. Missing keys never overwrite designer values.
    /// / 根控件使用 <c>$this</c>。支持的属性为 Text、AccessibleName、AccessibleDescription、PlaceholderText、SubText、EmptyText 与 ToggleText。缺失键绝不覆盖设计器值。
    /// </remarks>
    /// <param name="root">Root form or user control. / 根窗体或用户控件。</param>
    /// <param name="scope">Optional key scope; the root type name is used when omitted. / 可选键作用域；省略时使用根类型名。</param>
    public static void Apply(Control root, string? scope = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        EnsureInitialized();

        string effectiveScope = string.IsNullOrWhiteSpace(scope)
            ? root.GetType().Name
            : scope.Trim().Trim('.');
        if (effectiveScope.Length == 0)
        {
            effectiveScope = root.GetType().Name;
        }

        Queue<(Control Control, bool IsRoot)> pending = new();
        HashSet<Control> visited = new(ReferenceEqualityComparer.Instance);
        pending.Enqueue((root, true));
        while (pending.Count > 0)
        {
            (Control control, bool isRoot) = pending.Dequeue();
            if (!visited.Add(control))
            {
                continue;
            }

            string controlName = isRoot ? "$this" : control.Name;
            if (!string.IsNullOrWhiteSpace(controlName))
            {
                ApplyStandardProperties(control, effectiveScope, controlName);
            }

            foreach (Control child in control.Controls)
            {
                pending.Enqueue((child, false));
            }
        }
    }

    private static void ApplyStandardProperties(Control control, string scope, string controlName)
    {
        TryApplyValue(control, scope, controlName, nameof(Control.Text), value => control.Text = value);
        TryApplyValue(control, scope, controlName, nameof(Control.AccessibleName), value => control.AccessibleName = value);
        TryApplyValue(control, scope, controlName, nameof(Control.AccessibleDescription), value => control.AccessibleDescription = value);

        Type controlType = control.GetType();
        foreach (string propertyName in ReflectedStringProperties)
        {
            string key = BuildControlKey(scope, controlName, propertyName);
            if (!TryGet(key, out string value))
            {
                continue;
            }

            try
            {
                PropertyInfo? property = controlType.GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public);
                if (property?.CanWrite == true &&
                    property.PropertyType == typeof(string) &&
                    property.GetIndexParameters().Length == 0)
                {
                    property.SetValue(control, value);
                }
            }
            catch (Exception exception)
            {
                ReportPackFailure($"Unable to apply localization key '{key}'.", exception);
            }
        }
    }

    private static void TryApplyValue(
        Control control,
        string scope,
        string controlName,
        string propertyName,
        Action<string> apply)
    {
        string key = BuildControlKey(scope, controlName, propertyName);
        if (!TryGet(key, out string value))
        {
            return;
        }

        try
        {
            apply(value);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ReportPackFailure($"Unable to apply localization key '{key}' to '{control.GetType().Name}'.", exception);
        }
    }

    private static string BuildControlKey(string scope, string controlName, string propertyName) =>
        $"{scope}.{controlName}.{propertyName}";

    private static LocalizationSnapshot LoadSnapshot(AppDataPaths paths)
    {
        Dictionary<string, MutableLanguagePack> embeddedPacks = LoadEmbeddedPacks();

        Dictionary<string, string> englishBaseline = new(StringComparer.Ordinal);
        if (embeddedPacks.TryGetValue(EnglishLanguage, out MutableLanguagePack? embeddedEnglish))
        {
            foreach ((string key, string value) in embeddedEnglish.Strings)
            {
                if (TryGetPlaceholderSignature(value, out _))
                {
                    englishBaseline[key] = value;
                }
                else
                {
                    ReportPackFailure($"Embedded English key '{key}' has an invalid composite format and was ignored.");
                }
            }
        }
        else
        {
            ReportPackFailure("The embedded English language baseline was not found.");
        }

        foreach (MutableLanguagePack pack in embeddedPacks.Values)
        {
            RemoveIncompatibleStrings(pack, englishBaseline, "embedded pack");
        }

        Dictionary<string, MutableLanguagePack> combinedPacks =
            embeddedPacks.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Clone(),
                StringComparer.OrdinalIgnoreCase);

        string programLanguageDirectory = Path.Combine(AppContext.BaseDirectory, AppDataPaths.LanguagesDirectoryName);
        MergeLayer(combinedPacks, LoadExternalPacks(programLanguageDirectory, englishBaseline));

        if (!PathsEqual(programLanguageDirectory, paths.LanguagesDirectory))
        {
            MergeLayer(combinedPacks, LoadExternalPacks(paths.LanguagesDirectory, englishBaseline));
        }

        Dictionary<string, LanguagePack> packs = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string code, MutableLanguagePack pack) in combinedPacks)
        {
            packs[code] = pack.ToLanguagePack();
        }

        if (!packs.ContainsKey(EnglishLanguage))
        {
            packs[EnglishLanguage] = new LanguagePack(
                new LanguageInfo(EnglishLanguage, "English", "English"),
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        return new LocalizationSnapshot(packs, englishBaseline);
    }

    private static Dictionary<string, MutableLanguagePack> LoadEmbeddedPacks()
    {
        Dictionary<string, MutableLanguagePack> packs = new(StringComparer.OrdinalIgnoreCase);
        Assembly assembly = typeof(L).Assembly;
        string[] resourceNames;
        try
        {
            resourceNames = assembly.GetManifestResourceNames();
        }
        catch (Exception exception) when (IsRecoverablePackFailure(exception))
        {
            ReportPackFailure("Unable to enumerate embedded language packs.", exception);
            return packs;
        }

        foreach (string resourceName in resourceNames
                     .Where(IsLanguageResourceName)
                     .OrderBy(name => name, StringComparer.Ordinal))
        {
            try
            {
                using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                if (stream is null)
                {
                    continue;
                }

                byte[]? bytes = ReadLimited(stream, MaximumPackBytes);
                if (bytes is null)
                {
                    ReportPackFailure($"Embedded language pack '{resourceName}' exceeds {MaximumPackBytes} bytes.");
                    continue;
                }

                string? expectedLocale = GetEmbeddedResourceLocale(resourceName);
                if (expectedLocale is null)
                {
                    ReportPackFailure($"Embedded language pack filename '{resourceName}' is not a valid BCP-47 locale.");
                    continue;
                }

                ParsedLanguagePack? parsed = ParsePack(bytes, resourceName, expectedLocale);
                if (parsed is null)
                {
                    continue;
                }

                if (!packs.TryAdd(parsed.Info.Code, new MutableLanguagePack(parsed)))
                {
                    ReportPackFailure($"Duplicate embedded locale '{parsed.Info.Code}' was ignored.");
                }
            }
            catch (Exception exception) when (IsRecoverablePackFailure(exception))
            {
                ReportPackFailure($"Unable to load embedded language pack '{resourceName}'.", exception);
            }
        }

        return packs;
    }

    private static Dictionary<string, MutableLanguagePack> LoadExternalPacks(
        string languageDirectory,
        IReadOnlyDictionary<string, string> englishBaseline)
    {
        Dictionary<string, MutableLanguagePack> packs = new(StringComparer.OrdinalIgnoreCase);
        string[] files;
        try
        {
            if (!Directory.Exists(languageDirectory))
            {
                return packs;
            }

            files = Directory.EnumerateFiles(languageDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .Where(path => !Path.GetFileName(path).EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Take(MaximumExternalPackFiles + 1)
                .ToArray();
            if (files.Length > MaximumExternalPackFiles)
            {
                ReportPackFailure(
                    $"Language directory '{languageDirectory}' exceeds {MaximumExternalPackFiles} JSON packs; extras were ignored.");
                files = files[..MaximumExternalPackFiles];
            }
        }
        catch (Exception exception) when (IsRecoverablePackFailure(exception))
        {
            ReportPackFailure($"Unable to enumerate language directory '{languageDirectory}'.", exception);
            return packs;
        }

        foreach (string filePath in files)
        {
            try
            {
                string? expectedLocale = LanguageTag.Normalize(Path.GetFileNameWithoutExtension(filePath));
                if (expectedLocale is null)
                {
                    ReportPackFailure($"Language pack filename '{Path.GetFileName(filePath)}' is not a valid BCP-47 locale.");
                    continue;
                }

                using FileStream stream = new(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                byte[]? bytes = ReadLimited(stream, MaximumPackBytes);
                if (bytes is null)
                {
                    ReportPackFailure($"Language pack '{filePath}' exceeds {MaximumPackBytes} bytes.");
                    continue;
                }

                ParsedLanguagePack? parsed = ParsePack(bytes, filePath, expectedLocale);
                if (parsed is null)
                {
                    continue;
                }

                MutableLanguagePack pack = new(parsed);
                RemoveIncompatibleStrings(pack, englishBaseline, filePath);
                if (!packs.TryAdd(pack.Info.Code, pack))
                {
                    ReportPackFailure($"Duplicate locale '{pack.Info.Code}' in '{languageDirectory}' was ignored.");
                }
            }
            catch (Exception exception) when (IsRecoverablePackFailure(exception))
            {
                ReportPackFailure($"Unable to load language pack '{filePath}'.", exception);
            }
        }

        return packs;
    }

    private static ParsedLanguagePack? ParsePack(byte[] bytes, string source, string? expectedLocale)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(
                bytes,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 8
                });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return InvalidPack(source, "the root must be a JSON object.");
            }

            int? schemaVersion = null;
            string? locale = null;
            string? name = null;
            string? nativeName = null;
            IReadOnlyList<string> authors = Array.Empty<string>();
            JsonElement stringsElement = default;
            bool hasStrings = false;
            HashSet<string> rootProperties = new(StringComparer.Ordinal);

            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!rootProperties.Add(property.Name))
                {
                    return InvalidPack(source, $"duplicate root property '{property.Name}'.");
                }

                switch (property.Name)
                {
                    case "$schema":
                        if (property.Value.ValueKind != JsonValueKind.String ||
                            (property.Value.GetString()?.Length ?? 0) > MaximumSchemaUriLength)
                        {
                            return InvalidPack(source, $"'$schema' must be a string no longer than {MaximumSchemaUriLength} characters.");
                        }

                        break;

                    case "schemaVersion":
                        if (property.Value.ValueKind != JsonValueKind.Number ||
                            !property.Value.TryGetInt32(out int parsedVersion))
                        {
                            return InvalidPack(source, "'schemaVersion' must be an integer.");
                        }

                        schemaVersion = parsedVersion;
                        break;

                    case "locale":
                        locale = ReadMetadataString(property.Value);
                        break;

                    case "name":
                        name = ReadMetadataString(property.Value);
                        break;

                    case "nativeName":
                        nativeName = ReadMetadataString(property.Value);
                        break;

                    case "authors":
                        if (!TryReadAuthors(property.Value, out authors))
                        {
                            return InvalidPack(
                                source,
                                $"'authors' must contain at most {MaximumAuthorCount} non-empty strings of at most {MaximumAuthorLength} characters.");
                        }

                        break;

                    case "strings":
                        stringsElement = property.Value;
                        hasStrings = true;
                        break;

                    default:
                        return InvalidPack(source, $"unknown root property '{property.Name}'.");
                }
            }

            if (schemaVersion != SupportedPackSchemaVersion)
            {
                return InvalidPack(
                    source,
                    $"unsupported schema version '{schemaVersion?.ToString(CultureInfo.InvariantCulture) ?? "missing"}'.");
            }

            string? normalizedLocale = LanguageTag.Normalize(locale);
            if (normalizedLocale is null)
            {
                return InvalidPack(source, "'locale' is not a valid .NET-recognized BCP-47 locale.");
            }

            if (expectedLocale is not null &&
                !normalizedLocale.Equals(expectedLocale, StringComparison.OrdinalIgnoreCase))
            {
                return InvalidPack(source, $"'locale' must match filename locale '{expectedLocale}'.");
            }

            name = NormalizeMetadata(name, MaximumLanguageNameLength);
            nativeName = NormalizeMetadata(nativeName, MaximumLanguageNameLength);
            if (name is null || nativeName is null)
            {
                return InvalidPack(
                    source,
                    $"'name' and 'nativeName' must be non-empty strings no longer than {MaximumLanguageNameLength} characters.");
            }

            if (!hasStrings || stringsElement.ValueKind != JsonValueKind.Object)
            {
                return InvalidPack(source, "'strings' must be a JSON object.");
            }

            Dictionary<string, string> strings = new(StringComparer.Ordinal);
            HashSet<string> seenKeys = new(StringComparer.Ordinal);
            int stringCount = 0;
            foreach (JsonProperty property in stringsElement.EnumerateObject())
            {
                stringCount++;
                if (stringCount > MaximumStringCount)
                {
                    return InvalidPack(source, $"'strings' exceeds {MaximumStringCount} entries.");
                }

                if (!seenKeys.Add(property.Name))
                {
                    return InvalidPack(source, $"'strings' contains duplicate key '{property.Name}'.");
                }

                if (string.IsNullOrWhiteSpace(property.Name) ||
                    property.Name.Length > MaximumStringKeyLength ||
                    property.Name.Any(char.IsControl))
                {
                    return InvalidPack(source, $"'strings' contains an invalid key (maximum {MaximumStringKeyLength} characters).");
                }

                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    return InvalidPack(source, $"value for '{property.Name}' must be a string.");
                }

                string value = property.Value.GetString() ?? string.Empty;
                if (value.Length > MaximumStringValueLength || value.Contains('\0'))
                {
                    return InvalidPack(
                        source,
                        $"value for '{property.Name}' exceeds {MaximumStringValueLength} characters or contains NUL.");
                }

                strings[property.Name] = value;
            }

            return new ParsedLanguagePack(
                new LanguageInfo(normalizedLocale, name, nativeName, authors),
                strings);
        }
        catch (JsonException exception)
        {
            ReportPackFailure($"Language pack '{source}' contains invalid JSON.", exception);
            return null;
        }
    }

    private static bool TryReadAuthors(JsonElement element, out IReadOnlyList<string> authors)
    {
        authors = Array.Empty<string>();
        if (element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        List<string> result = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (result.Count >= MaximumAuthorCount || item.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            string? author = NormalizeMetadata(item.GetString(), MaximumAuthorLength);
            if (author is null)
            {
                return false;
            }

            result.Add(author);
        }

        authors = result.AsReadOnly();
        return true;
    }

    private static void RemoveIncompatibleStrings(
        MutableLanguagePack pack,
        IReadOnlyDictionary<string, string> englishBaseline,
        string source)
    {
        int removed = 0;
        foreach (string key in pack.Strings.Keys.ToArray())
        {
            string translated = pack.Strings[key];
            if (!englishBaseline.TryGetValue(key, out string? english) ||
                !TryGetPlaceholderSignature(english, out Dictionary<string, int>? englishSignature) ||
                !TryGetPlaceholderSignature(translated, out Dictionary<string, int>? translatedSignature) ||
                !PlaceholderSignaturesEqual(englishSignature, translatedSignature))
            {
                pack.Strings.Remove(key);
                removed++;
            }
        }

        if (removed > 0)
        {
            ReportPackFailure(
                $"Language pack '{pack.Info.Code}' from {source} contained {removed} unknown or placeholder-incompatible string(s); those entries were ignored.");
        }
    }

    private static bool TryGetPlaceholderSignature(string value, out Dictionary<string, int> signature)
    {
        signature = new Dictionary<string, int>(StringComparer.Ordinal);
        try
        {
            _ = CompositeFormat.Parse(value);
        }
        catch (FormatException)
        {
            return false;
        }

        int index = 0;
        while (index < value.Length)
        {
            if (value[index] == '{')
            {
                int placeholderStart = index;
                if (index + 1 < value.Length && value[index + 1] == '{')
                {
                    index += 2;
                    continue;
                }

                index++;
                while (index < value.Length && char.IsWhiteSpace(value[index]))
                {
                    index++;
                }

                int numberStart = index;
                while (index < value.Length && char.IsAsciiDigit(value[index]))
                {
                    index++;
                }

                if (numberStart == index ||
                    !int.TryParse(
                        value.AsSpan(numberStart, index - numberStart),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out _))
                {
                    return false;
                }

                bool inFormatSpecifier = false;
                bool closed = false;
                while (index < value.Length)
                {
                    if (value[index] == ':' && !inFormatSpecifier)
                    {
                        inFormatSpecifier = true;
                        index++;
                        continue;
                    }

                    if (inFormatSpecifier &&
                        value[index] == '{' &&
                        index + 1 < value.Length &&
                        value[index + 1] == '{')
                    {
                        index += 2;
                        continue;
                    }

                    if (inFormatSpecifier &&
                        value[index] == '}' &&
                        index + 1 < value.Length &&
                        value[index + 1] == '}')
                    {
                        index += 2;
                        continue;
                    }

                    if (value[index] == '}')
                    {
                        index++;
                        closed = true;
                        break;
                    }

                    index++;
                }

                if (!closed)
                {
                    return false;
                }

                string formatItem = value[placeholderStart..index];
                signature[formatItem] = signature.GetValueOrDefault(formatItem) + 1;
                continue;
            }

            if (value[index] == '}' &&
                index + 1 < value.Length &&
                value[index + 1] == '}')
            {
                index += 2;
                continue;
            }

            index++;
        }

        return true;
    }

    private static bool PlaceholderSignaturesEqual(
        IReadOnlyDictionary<string, int> left,
        IReadOnlyDictionary<string, int> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach ((string placeholder, int count) in left)
        {
            if (!right.TryGetValue(placeholder, out int rightCount) || rightCount != count)
            {
                return false;
            }
        }

        return true;
    }

    private static void MergeLayer(
        Dictionary<string, MutableLanguagePack> target,
        IReadOnlyDictionary<string, MutableLanguagePack> layer)
    {
        foreach ((string code, MutableLanguagePack incoming) in layer)
        {
            if (!target.TryGetValue(code, out MutableLanguagePack? existing))
            {
                target[code] = incoming.Clone();
                continue;
            }

            existing.Info = incoming.Info;
            foreach ((string key, string value) in incoming.Strings)
            {
                existing.Strings[key] = value;
            }
        }
    }

    private static void EnsureInitialized()
    {
        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }
        }

        lock (MutationRoot)
        {
            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }
            }

            // At design time this executes lazily inside DesignToolsServer and intentionally
            // observes the designer host's CurrentUICulture without reading runtime settings.
            InitializeCore(preference: null, new AppDataPaths());
        }
    }

    private static bool TryResolve(
        LocalizationSnapshot snapshot,
        IReadOnlyList<string> lookupLanguages,
        string key,
        out string value)
    {
        foreach (string language in lookupLanguages)
        {
            if (snapshot.Packs.TryGetValue(language, out LanguagePack? pack) &&
                pack.Strings.TryGetValue(key, out value!))
            {
                return true;
            }
        }

        return snapshot.EnglishBaseline.TryGetValue(key, out value!);
    }

    private static void SelectLanguageNoLock(string? preference)
    {
        _requestedLanguage = NormalizeRequestedLanguage(preference);
        string effectiveLanguage = _requestedLanguage == SystemLanguage
            ? _detectedSystemLanguage
            : _requestedLanguage;
        List<string> lookupLanguages = [];
        foreach (string candidate in BuildFallbackChain(effectiveLanguage))
        {
            if (_snapshot.Packs.TryGetValue(candidate, out LanguagePack? pack) &&
                !lookupLanguages.Contains(pack.Info.Code, StringComparer.OrdinalIgnoreCase))
            {
                lookupLanguages.Add(pack.Info.Code);
            }
        }

        if (!lookupLanguages.Contains(EnglishLanguage, StringComparer.OrdinalIgnoreCase))
        {
            lookupLanguages.Add(EnglishLanguage);
        }

        _lookupLanguages = lookupLanguages.ToArray();
        _currentLanguage = _lookupLanguages[0];
    }

    private static IReadOnlyList<LanguageInfo> CreateAvailableLanguages(LocalizationSnapshot snapshot)
    {
        LanguageInfo[] languages = snapshot.Packs.Values
            .Select(pack => pack.Info)
            .OrderBy(
                info => info.Code.Equals(EnglishLanguage, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(info => info.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(info => info.Code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Array.AsReadOnly(languages);
    }

    private static IReadOnlyList<string> BuildFallbackChain(string language)
    {
        string normalized = LanguageTag.Normalize(language) ?? EnglishLanguage;
        List<string> result = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        void Add(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
            {
                result.Add(value);
            }
        }

        Add(normalized);
        string? chineseScript = GetChineseScriptFallback(normalized);
        Add(chineseScript);
        AddCultureParents(normalized, Add);
        if (chineseScript is not null)
        {
            AddCultureParents(chineseScript, Add);
        }

        Add(EnglishLanguage);
        return result;
    }

    private static void AddCultureParents(string locale, Action<string?> add)
    {
        try
        {
            CultureInfo parent = CultureInfo.GetCultureInfo(locale).Parent;
            while (!string.IsNullOrEmpty(parent.Name))
            {
                add(parent.Name);
                parent = parent.Parent;
            }
        }
        catch (CultureNotFoundException)
        {
            // LanguageTag.Normalize rejects invalid cultures before this point.
        }
    }

    private static string? GetChineseScriptFallback(string language)
    {
        string[] subtags = language.Split('-');
        if (subtags.Length == 0 || !subtags[0].Equals("zh", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        foreach (string subtag in subtags.Skip(1))
        {
            if (subtag.Equals("Hans", StringComparison.OrdinalIgnoreCase))
            {
                return "zh-Hans";
            }

            if (subtag.Equals("Hant", StringComparison.OrdinalIgnoreCase))
            {
                return "zh-Hant";
            }
        }

        foreach (string subtag in subtags.Skip(1))
        {
            if (subtag.Equals("TW", StringComparison.OrdinalIgnoreCase) ||
                subtag.Equals("HK", StringComparison.OrdinalIgnoreCase) ||
                subtag.Equals("MO", StringComparison.OrdinalIgnoreCase))
            {
                return "zh-Hant";
            }

            if (subtag.Equals("CN", StringComparison.OrdinalIgnoreCase) ||
                subtag.Equals("SG", StringComparison.OrdinalIgnoreCase) ||
                subtag.Equals("MY", StringComparison.OrdinalIgnoreCase))
            {
                return "zh-Hans";
            }
        }

        return "zh-Hans";
    }

    private static string NormalizeRequestedLanguage(string? preference)
    {
        if (string.IsNullOrWhiteSpace(preference) ||
            preference.Trim().Equals(SystemLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return SystemLanguage;
        }

        return LanguageTag.Normalize(preference) ?? SystemLanguage;
    }

    private static string DetectSystemLanguage() =>
        LanguageTag.Normalize(CultureInfo.CurrentUICulture.Name) ?? EnglishLanguage;

    private static string GetEnglish(string key)
    {
        LocalizationSnapshot snapshot;
        lock (SyncRoot)
        {
            snapshot = _snapshot;
        }

        if (snapshot.Packs.TryGetValue(EnglishLanguage, out LanguagePack? englishPack) &&
            englishPack.Strings.TryGetValue(key, out string? english))
        {
            return english;
        }

        return snapshot.EnglishBaseline.TryGetValue(key, out string? baseline) ? baseline : key;
    }

    private static void ApplyUiCulture(string language)
    {
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.GetCultureInfo(EnglishLanguage);
        }

        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        try
        {
            AntdUI.Localization.SetLanguage(culture.Name);
        }
        catch (Exception exception)
        {
            ReportPackFailure($"Unable to apply AntdUI language '{culture.Name}'.", exception);
        }
    }

    private static void RaiseLanguageChanged()
    {
        EventHandler? handlers = LanguageChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler handler in handlers.GetInvocationList())
        {
            try
            {
                handler(null, EventArgs.Empty);
            }
            catch (Exception exception)
            {
                Trace.TraceError($"A language change handler failed: {exception}");
            }
        }
    }

    private static byte[]? ReadLimited(Stream stream, int maximumBytes)
    {
        if (stream.CanSeek && stream.Length > maximumBytes)
        {
            return null;
        }

        using MemoryStream buffer = new();
        byte[] chunk = new byte[16 * 1024];
        while (true)
        {
            int read = stream.Read(chunk, 0, chunk.Length);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > maximumBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }
    }

    private static string? ReadMetadataString(JsonElement element) =>
        element.ValueKind == JsonValueKind.String ? element.GetString() : null;

    private static string? NormalizeMetadata(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length <= maximumLength && !trimmed.Any(char.IsControl)
            ? trimmed
            : null;
    }

    private static bool IsLanguageResourceName(string resourceName)
    {
        string normalized = resourceName.Replace('\\', '.').Replace('/', '.');
        return normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
               !normalized.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase) &&
               (normalized.StartsWith("Languages.", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains(".Languages.", StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetEmbeddedResourceLocale(string resourceName)
    {
        string normalized = resourceName.Replace('\\', '.').Replace('/', '.');
        const string marker = ".Languages.";
        int markerIndex = normalized.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        int localeStart = markerIndex >= 0
            ? markerIndex + marker.Length
            : normalized.StartsWith("Languages.", StringComparison.OrdinalIgnoreCase)
                ? "Languages.".Length
                : -1;
        if (localeStart < 0 || !normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string locale = normalized[localeStart..^".json".Length];
        return locale.Contains('.') ? null : LanguageTag.Normalize(locale);
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return Path.GetFullPath(left).Equals(Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or SecurityException)
        {
            return left.Equals(right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static ParsedLanguagePack? InvalidPack(string source, string reason)
    {
        ReportPackFailure($"Language pack '{source}' is invalid: {reason}");
        return null;
    }

    private static bool IsRecoverablePackFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException or JsonException or
            ArgumentException or NotSupportedException or InvalidDataException;

    private static void ReportPackFailure(string message, Exception? exception = null)
    {
        try
        {
            Trace.TraceWarning(exception is null ? message : $"{message} {exception.Message}");
        }
        catch
        {
            // Diagnostics must never make localization a startup dependency.
        }
    }

    private sealed record ParsedLanguagePack(LanguageInfo Info, Dictionary<string, string> Strings);

    private sealed record LanguagePack(LanguageInfo Info, IReadOnlyDictionary<string, string> Strings);

    private sealed class MutableLanguagePack
    {
        internal MutableLanguagePack(ParsedLanguagePack pack)
        {
            Info = pack.Info;
            foreach ((string key, string value) in pack.Strings)
            {
                Strings[key] = value;
            }
        }

        private MutableLanguagePack(LanguageInfo info)
        {
            Info = info;
        }

        internal LanguageInfo Info { get; set; }

        internal Dictionary<string, string> Strings { get; } = new(StringComparer.Ordinal);

        internal MutableLanguagePack Clone()
        {
            MutableLanguagePack clone = new(Info);
            foreach ((string key, string value) in Strings)
            {
                clone.Strings[key] = value;
            }

            return clone;
        }

        internal LanguagePack ToLanguagePack() =>
            new(
                Info,
                new Dictionary<string, string>(Strings, StringComparer.Ordinal));
    }

    private sealed record LocalizationSnapshot(
        IReadOnlyDictionary<string, LanguagePack> Packs,
        IReadOnlyDictionary<string, string> EnglishBaseline)
    {
        internal static LocalizationSnapshot Empty { get; } = new(
            new Dictionary<string, LanguagePack>(StringComparer.OrdinalIgnoreCase)
            {
                [EnglishLanguage] = new(
                    new LanguageInfo(EnglishLanguage, "English", "English"),
                    new Dictionary<string, string>(StringComparer.Ordinal))
            },
            new Dictionary<string, string>(StringComparer.Ordinal));
    }
}
