using System.Security;
using System.Text.Json;
using RemoteHubStudio.Infrastructure.Persistence;

namespace RemoteHubStudio.Localization;

/// <summary>
/// Loads and atomically saves the UI language independently from workspace data. / 独立于工作区数据加载并原子保存 UI 语言偏好。
/// </summary>
public sealed class LanguagePreferenceStore
{
    private const int PreferenceSchemaVersion = 1;
    private const int MaximumPreferenceFileBytes = 16 * 1024;

    private readonly AppDataPaths _paths;

    /// <summary>Initializes a store in the application's default portable data directory. / 在应用默认便携数据目录中初始化存储。</summary>
    public LanguagePreferenceStore()
        : this(new AppDataPaths())
    {
    }

    /// <summary>Initializes a store using explicit application data paths. / 使用显式应用数据路径初始化存储。</summary>
    /// <param name="paths">Portable application data paths. / 便携式应用数据路径。</param>
    public LanguagePreferenceStore(AppDataPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    /// <summary>Gets the preference document path. / 获取偏好文档路径。</summary>
    public string FilePath => _paths.LanguagePreferenceFilePath;

    /// <summary>
    /// Loads a validated canonical locale or <see cref="L.SystemLanguage"/>; corrupt or inaccessible preferences are ignored. / 加载已验证的规范区域标识或 <see cref="L.SystemLanguage"/>；损坏或无法访问的偏好将被忽略。
    /// </summary>
    /// <returns>The saved preference, or <see langword="null"/> when unavailable or invalid. / 已保存的偏好，不可用或无效时返回 <see langword="null"/>。</returns>
    public string? Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            byte[]? bytes = ReadLimited(FilePath, MaximumPreferenceFileBytes);
            if (bytes is null)
            {
                return null;
            }

            using JsonDocument document = JsonDocument.Parse(
                bytes,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 4
                });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            int? schemaVersion = null;
            string? language = null;
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    return null;
                }

                switch (property.Name)
                {
                    case "schemaVersion" when property.Value.ValueKind == JsonValueKind.Number &&
                                                   property.Value.TryGetInt32(out int parsedVersion):
                        schemaVersion = parsedVersion;
                        break;

                    case "language" when property.Value.ValueKind == JsonValueKind.String:
                        language = property.Value.GetString();
                        break;

                    default:
                        return null;
                }
            }

            return schemaVersion == PreferenceSchemaVersion
                ? NormalizePreference(language)
                : null;
        }
        catch (Exception exception) when (IsExpectedStorageFailure(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// Atomically saves one validated preference. / 原子保存一个已验证的偏好。
    /// </summary>
    /// <param name="preference">A BCP-47 locale, <see cref="L.SystemLanguage"/>, or blank for system language. / BCP-47 区域标识、<see cref="L.SystemLanguage"/>，或表示跟随系统的空值。</param>
    /// <returns><see langword="true"/> when the replacement reached its final path; otherwise <see langword="false"/>. / 替换成功到达最终路径时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public bool Save(string? preference)
    {
        string normalized = string.IsNullOrWhiteSpace(preference)
            ? L.SystemLanguage
            : NormalizePreference(preference) ?? string.Empty;
        if (normalized.Length == 0)
        {
            return false;
        }

        string? temporaryPath = null;
        try
        {
            Directory.CreateDirectory(_paths.DataDirectory);
            temporaryPath = _paths.CreateAtomicLanguagePreferenceFilePath();
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
                new PreferenceDocument(PreferenceSchemaVersion, normalized),
                SerializerOptions);
            if (bytes.Length > MaximumPreferenceFileBytes)
            {
                return false;
            }

            using (FileStream stream = new(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, FilePath, overwrite: true);
            temporaryPath = null;
            return true;
        }
        catch (Exception exception) when (IsExpectedStorageFailure(exception))
        {
            return false;
        }
        finally
        {
            if (temporaryPath is not null)
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
        }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static string? NormalizePreference(string? preference)
    {
        if (string.IsNullOrWhiteSpace(preference))
        {
            return null;
        }

        string candidate = preference.Trim();
        return candidate.Equals(L.SystemLanguage, StringComparison.OrdinalIgnoreCase)
            ? L.SystemLanguage
            : LanguageTag.Normalize(candidate);
    }

    private static byte[]? ReadLimited(string filePath, int maximumBytes)
    {
        using FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        if (stream.CanSeek && stream.Length > maximumBytes)
        {
            return null;
        }

        using MemoryStream buffer = new();
        byte[] chunk = new byte[4 * 1024];
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

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (IsExpectedStorageFailure(exception))
        {
            // Best-effort cleanup must not turn a UI preference into a startup dependency.
        }
    }

    private static bool IsExpectedStorageFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException or
            JsonException or ArgumentException or NotSupportedException;

    private sealed record PreferenceDocument(int SchemaVersion, string Language);
}
