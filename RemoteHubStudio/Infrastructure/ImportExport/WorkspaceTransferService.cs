using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RemoteHubStudio.Application;
using RemoteHubStudio.Configuration;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Infrastructure;
using RemoteHubStudio.Infrastructure.Persistence;

namespace RemoteHubStudio.Infrastructure.ImportExport;

/// <summary>
/// Imports and exports portable RemoteHubStudio JSON and CSV documents. / 导入和导出可移植的 RemoteHubStudio JSON 与 CSV 文档。
/// </summary>
public sealed class WorkspaceTransferService
{
    /// <summary>Defines the shared maximum size for every successfully imported or exported portable file. / 定义每个成功导入或导出的便携文件共享大小上限。</summary>
    public const long MaximumTransferFileLength = 16L * 1024L * 1024L;

    private const int PortableEnvelopeSchema = 1;
    private const int MaximumJsonStringTokenBytes = 4 * 1024 * 1024;
    private const long MaximumJsonStringBudgetBytes = 15L * 1024L * 1024L;
    private const string CsvFormatHeader = "RemoteHubStudioCsvVersion";
    private const string CsvFormatVersion = "2";
    private const string CsvOptionsHeader = "OptionsJson";
    private const string CsvRdpOptionsHeader = "RdpOptionsJson";
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly JsonSerializerOptions _compactJsonOptions;

    /// <summary>
    /// Initializes portable document serialization settings. / 初始化可移植文档序列化设置。
    /// </summary>
    public WorkspaceTransferService()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        _compactJsonOptions = new JsonSerializerOptions(_jsonOptions) { WriteIndented = false };
    }

    /// <summary>
    /// Exports a workspace to JSON and excludes passwords unless explicitly requested. / 将工作区导出为 JSON，除非明确请求，否则排除密码。
    /// </summary>
    /// <param name="document">Workspace document. / 工作区文档。</param>
    /// <param name="filePath">Destination file. / 目标文件。</param>
    /// <param name="includeSecrets">Whether passwords are included. / 是否包含密码。</param>
    /// <param name="cancellationToken">Cancellation token. / 取消令牌。</param>
    public async Task ExportJsonAsync(
        AppDataDocument document,
        string filePath,
        bool includeSecrets,
        CancellationToken cancellationToken = default)
    {
        WorkspaceLimits.ValidateDocument(document);
        WorkspaceContentLimits.ValidateDocument(document);
        GroupGraphValidator.Validate(document.Groups);
        AppDataDocument exportDocument = CloneDocument(document);
        if (!includeSecrets)
        {
            RemoveSecrets(exportDocument);
        }

        exportDocument.SchemaVersion = AppDataDocument.CurrentSchemaVersion;
        PortableWorkspaceEnvelope envelope = new()
        {
            Format = ProductInfo.WorkspaceFormatId,
            Schema = PortableEnvelopeSchema,
            ExportedAt = DateTime.UtcNow,
            Data = CreatePortableData(exportDocument)
        };

        string destinationPath = Path.GetFullPath(filePath);
        string temporaryFilePath = CreateTemporaryExportFilePath(destinationPath);
        try
        {
            await using (FileStream stream = new(
                             temporaryFilePath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await using LengthLimitedWriteStream boundedStream = new(
                    stream,
                    MaximumTransferFileLength,
                    leaveOpen: true);
                await JsonSerializer.SerializeAsync(boundedStream, envelope, _jsonOptions, cancellationToken);
                await boundedStream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            CommitBoundedExport(temporaryFilePath, destinationPath);
            temporaryFilePath = string.Empty;
        }
        finally
        {
            DeleteTemporaryExportFileIfPresent(temporaryFilePath);
        }
    }

    /// <summary>
    /// Imports a portable JSON workspace. / 导入可移植 JSON 工作区。
    /// </summary>
    /// <param name="filePath">Source file. / 源文件。</param>
    /// <param name="cancellationToken">Cancellation token. / 取消令牌。</param>
    /// <returns>Imported data document. / 导入的数据文档。</returns>
    public Task<AppDataDocument> ImportJsonAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return ImportJsonAsync(filePath, trustLaunchConfiguration: false, cancellationToken);
    }

    /// <summary>
    /// Imports portable JSON while optionally retaining executable launch configuration from a trusted source. / 导入便携 JSON，并可选保留来自可信源的可执行启动配置。
    /// </summary>
    /// <param name="filePath">Source file. / 源文件。</param>
    /// <param name="trustLaunchConfiguration">Whether executable overrides and custom arguments are explicitly trusted. / 是否明确信任程序覆盖与自定义参数。</param>
    /// <param name="cancellationToken">Cancellation token. / 取消令牌。</param>
    /// <returns>Imported data document. / 导入的数据文档。</returns>
    public async Task<AppDataDocument> ImportJsonAsync(
        string filePath,
        bool trustLaunchConfiguration,
        CancellationToken cancellationToken = default)
    {
        byte[] jsonBytes = await BoundedFileReader.ReadAllBytesAsync(
            filePath,
            MaximumTransferFileLength,
            cancellationToken).ConfigureAwait(false);
        try
        {
            WorkspaceJsonPreflight.Validate(
                jsonBytes,
                maximumDepth: 64,
                MaximumJsonStringTokenBytes,
                MaximumJsonStringBudgetBytes);
            using JsonDocument jsonDocument = JsonDocument.Parse(
                jsonBytes,
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 64 });
            JsonElement root = jsonDocument.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("The JSON file does not contain a workspace object. / JSON 文件不包含工作区对象。");
            }

            AppDataDocument document;
            if (TryGetJsonProperty(root, "format", out JsonElement formatElement))
            {
                if (formatElement.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException("The workspace format identifier is invalid. / 工作区格式标识无效。");
                }

                string format = formatElement.GetString() ?? string.Empty;
                if (string.Equals(format, WorkspaceEnvelope.FormatIdentifier, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Local or encrypted workspace envelopes cannot be imported as portable files. / 本地或加密工作区信封不能作为便携文件导入。");
                }

                if (!string.Equals(format, ProductInfo.WorkspaceFormatId, StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"Unknown workspace format '{format}'. / 未知的工作区格式“{format}”。");
                }

                document = ReadPortableEnvelope(root);
            }
            else
            {
                if (!LooksLikeLegacyDocument(root))
                {
                    throw new InvalidDataException("The JSON file is not a legacy portable workspace document. / JSON 文件不是旧版便携工作区文档。");
                }

                ValidateJsonEntityLimits(root);
                document = root.Deserialize<AppDataDocument>(_jsonOptions)
                           ?? throw new InvalidDataException("The JSON file does not contain a workspace. / JSON 文件不包含工作区数据。");
            }

            ValidateDocumentSchema(document);
            ValidateImportedEntityLimits(document);
            NormalizeDocument(document);
            WorkspaceContentLimits.ValidateDocument(document);
            GroupGraphValidator.Validate(document.Groups);
            if (!trustLaunchConfiguration)
            {
                SanitizeImportedActiveConfiguration(document.Connections);
            }

            return document;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The JSON workspace is malformed. / JSON 工作区格式损坏。", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(jsonBytes);
        }
    }

    /// <summary>
    /// Exports connections to CSV and excludes passwords unless explicitly requested. / 将连接导出为 CSV，除非明确请求，否则排除密码。
    /// </summary>
    /// <param name="document">Workspace document. / 工作区文档。</param>
    /// <param name="filePath">Destination file. / 目标文件。</param>
    /// <param name="includeSecrets">Whether passwords are included. / 是否包含密码。</param>
    /// <param name="cancellationToken">Cancellation token. / 取消令牌。</param>
    public async Task ExportCsvAsync(
        AppDataDocument document,
        string filePath,
        bool includeSecrets,
        CancellationToken cancellationToken = default)
    {
        WorkspaceLimits.ValidateDocument(document);
        WorkspaceContentLimits.ValidateDocument(document);
        GroupGraphValidator.Validate(document.Groups);
        Dictionary<Guid, string> groupNames = document.Groups.ToDictionary(group => group.Id, group => group.Name);
        List<IReadOnlyList<string?>> rows =
        [
            ["Name", "Group", "Type", "Protocol", "Host", "Port", "Username", "Password", "PrivateKeyPath", "ExpiresOn", "Notes", "Favorite", "Executable", "Arguments", CsvFormatHeader, CsvOptionsHeader, CsvRdpOptionsHeader]
        ];

        foreach (ConnectionProfile profile in document.Connections)
        {
            string?[] exportedRow =
            [
                profile.Name,
                profile.GroupId is Guid groupId ? groupNames.GetValueOrDefault(groupId, string.Empty) : string.Empty,
                profile.Type.ToString(),
                profile.Protocol,
                profile.Host,
                profile.Port.ToString(CultureInfo.InvariantCulture),
                profile.Username,
                includeSecrets ? profile.Password : string.Empty,
                profile.PrivateKeyPath,
                profile.ExpiresOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                profile.Notes,
                profile.IsFavorite.ToString(CultureInfo.InvariantCulture),
                profile.ExecutableOverride,
                profile.CustomArguments,
                CsvFormatVersion,
                profile.Options is { Count: > 0 }
                    ? JsonSerializer.Serialize(profile.Options, _compactJsonOptions)
                    : string.Empty,
                HasNonDefaultRdpOptions(profile.Rdp)
                    ? JsonSerializer.Serialize(profile.Rdp, _compactJsonOptions)
                    : string.Empty
            ];
            rows.Add(exportedRow.Select(ProtectSpreadsheetFormula).ToArray());
        }

        string destinationPath = Path.GetFullPath(filePath);
        string temporaryFilePath = CreateTemporaryExportFilePath(destinationPath);
        try
        {
            string csv = CsvCodec.Encode(rows);
            await using (FileStream stream = new(
                             temporaryFilePath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            await using (LengthLimitedWriteStream boundedStream = new(
                             stream,
                             MaximumTransferFileLength,
                             leaveOpen: true))
            await using (StreamWriter writer = new(
                             boundedStream,
                             new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
                             bufferSize: 81920,
                             leaveOpen: true))
            {
                await writer.WriteAsync(csv.AsMemory(), cancellationToken).ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                await boundedStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            CommitBoundedExport(temporaryFilePath, destinationPath);
            temporaryFilePath = string.Empty;
        }
        finally
        {
            DeleteTemporaryExportFileIfPresent(temporaryFilePath);
        }
    }

    /// <summary>
    /// Imports connections from current or legacy CSV column names. / 从当前或旧版 CSV 列名导入连接。
    /// </summary>
    /// <param name="filePath">Source file. / 源文件。</param>
    /// <param name="cancellationToken">Cancellation token. / 取消令牌。</param>
    /// <returns>Parsed import result. / 解析后的导入结果。</returns>
    public Task<ImportResult> ImportCsvAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return ImportCsvAsync(filePath, trustLaunchConfiguration: false, cancellationToken);
    }

    /// <summary>
    /// Imports CSV while optionally retaining executable launch configuration from a trusted source. / 导入 CSV，并可选保留来自可信源的可执行启动配置。
    /// </summary>
    /// <param name="filePath">Source file. / 源文件。</param>
    /// <param name="trustLaunchConfiguration">Whether executable overrides and custom arguments are explicitly trusted. / 是否明确信任程序覆盖与自定义参数。</param>
    /// <param name="cancellationToken">Cancellation token. / 取消令牌。</param>
    /// <returns>Parsed import result. / 解析后的导入结果。</returns>
    public async Task<ImportResult> ImportCsvAsync(
        string filePath,
        bool trustLaunchConfiguration,
        CancellationToken cancellationToken = default)
    {
        byte[] csvBytes = await BoundedFileReader.ReadAllBytesAsync(
            filePath,
            MaximumTransferFileLength,
            cancellationToken).ConfigureAwait(false);
        string csv;
        try
        {
            csv = DecodePortableText(csvBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(csvBytes);
        }

        IReadOnlyList<IReadOnlyList<string>> rows = CsvCodec.Decode(csv);
        ImportResult result = new();
        if (rows.Count == 0)
        {
            return result;
        }

        Dictionary<string, int> header = BuildHeader(rows[0]);
        ValidateRequiredCsvHeaders(header);
        bool usesNativeEncoding = header.ContainsKey(CsvFormatHeader);
        Dictionary<string, ConnectionGroup> groups = new(StringComparer.OrdinalIgnoreCase);
        for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            IReadOnlyList<string> row = usesNativeEncoding
                ? RestoreSpreadsheetRow(rows[rowIndex])
                : rows[rowIndex];
            try
            {
                string nativeVersion = usesNativeEncoding
                    ? ReadValue(row, header, CsvFormatHeader)
                    : string.Empty;
                if (usesNativeEncoding && nativeVersion is not ("1" or CsvFormatVersion))
                {
                    throw new FormatException("The RemoteHubStudio CSV row version is missing or unsupported. / RemoteHubStudio CSV 行版本缺失或不受支持。");
                }

                bool supportsExtendedOptions = usesNativeEncoding &&
                                               string.Equals(nativeVersion, CsvFormatVersion, StringComparison.Ordinal);

                string groupName = ReadValue(row, header, "Group", "分类名称", "分类");
                string typeLabel = ReadValue(row, header, "Type", "连接类型", "类型");
                ConnectionType type = ParseConnectionType(typeLabel);
                string explicitProtocol = ReadValue(row, header, "Protocol", "协议");
                string importedProtocol = string.IsNullOrWhiteSpace(explicitProtocol)
                    ? ReadCombinedTypeProtocol(typeLabel)
                    : explicitProtocol;
                string customArguments = ReadPreservedValue(row, header, "Arguments", "自定义规则");
                if (string.Equals(importedProtocol.Trim(), "自定义", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(customArguments))
                {
                    importedProtocol = type.GetProtocols().FirstOrDefault() ?? string.Empty;
                }

                string normalizedProtocol = NormalizeProtocol(importedProtocol, type);
                int port = ParsePort(
                    ReadValue(row, header, "Port", "端口"),
                    type.GetDefaultPort(normalizedProtocol));
                ConnectionProfile profile = new()
                {
                    Name = ReadRequiredValue(row, header, "Name", "名称", "服务器名称"),
                    Type = type,
                    Protocol = normalizedProtocol,
                    Host = ReadRequiredValue(row, header, "Host", "主机", "IP"),
                    Port = port,
                    Username = ReadValue(row, header, "Username", "用户名"),
                    Password = ReadPreservedValue(row, header, "Password", "密码"),
                    PrivateKeyPath = ReadValue(row, header, "PrivateKeyPath"),
                    ExpiresOn = ParseDate(ReadValue(row, header, "ExpiresOn", "到期时间")),
                    Notes = ReadPreservedValue(row, header, "Notes", "服务器备注", "备注"),
                    IsFavorite = bool.TryParse(ReadValue(row, header, "Favorite"), out bool favorite) && favorite,
                    ExecutableOverride = ReadValue(row, header, "Executable"),
                    CustomArguments = customArguments,
                    Options = supportsExtendedOptions
                        ? ParseCsvOptions(ReadPreservedValue(row, header, CsvOptionsHeader))
                        : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    Rdp = supportsExtendedOptions
                        ? ParseCsvRdpOptions(ReadPreservedValue(row, header, CsvRdpOptionsHeader))
                        : new RdpOptions()
                };
                ValidateImportedPort(profile);
                if (!string.IsNullOrWhiteSpace(groupName) && !string.Equals(groupName, "全部分类", StringComparison.OrdinalIgnoreCase))
                {
                    if (!groups.TryGetValue(groupName, out ConnectionGroup? group))
                    {
                        group = new ConnectionGroup { Name = groupName, SortOrder = groups.Count };
                        groups.Add(group.Name, group);
                        result.Groups.Add(group);
                    }

                    profile.GroupId = group.Id;
                }

                bool removedLaunchConfiguration = !trustLaunchConfiguration &&
                                                  SanitizeImportedActiveConfiguration([profile]);
                result.Connections.Add(profile);
                if (removedLaunchConfiguration)
                {
                    result.ModifiedRowCount++;
                    result.Warnings.Add($"Row {rowIndex + 1}: Active launch, endpoint-routing, key, or RDP-redirection settings were disabled for safety. / 第 {rowIndex + 1} 行：已为安全起见禁用主动启动、目标路由、私钥或 RDP 重定向设置。");
                }
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException)
            {
                result.SkippedRowCount++;
                result.Warnings.Add($"Row {rowIndex + 1}: {exception.Message} / 第 {rowIndex + 1} 行：{exception.Message}");
            }
        }

        WorkspaceLimits.ValidateCounts(result.Groups.Count, result.Connections.Count);
        WorkspaceContentLimits.ValidateDocument(new AppDataDocument
        {
            Groups = result.Groups,
            Connections = result.Connections
        });
        GroupGraphValidator.Validate(result.Groups);
        return result;
    }

    /// <summary>
    /// Decodes bounded portable text with UTF-8 as the default and BOM-based Unicode detection. / 使用 UTF-8 作为默认编码并按 BOM 检测 Unicode 来解码受限便携文本。
    /// </summary>
    /// <param name="content">Bounded file bytes from one stable handle. / 从单一稳定句柄读取的受限文件字节。</param>
    /// <returns>Decoded text. / 解码后的文本。</returns>
    private static string DecodePortableText(byte[] content)
    {
        using MemoryStream stream = new(content, writable: false);
        using StreamReader reader = new(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 81920,
            leaveOpen: false);
        return reader.ReadToEnd();
    }

    /// <summary>Restores version-2 client options while leaving older CSV rows at their defaults. / 恢复版本 2 的客户端选项，并让旧版 CSV 行继续使用默认值。</summary>
    /// <param name="json">Serialized option dictionary, or an empty legacy field. / 序列化选项字典，或旧版空字段。</param>
    /// <returns>A case-insensitive option dictionary. / 不区分大小写的选项字典。</returns>
    private Dictionary<string, string> ParseCsvOptions(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            Dictionary<string, string>? parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json, _jsonOptions);
            if (parsed is null || parsed.Any(option => string.IsNullOrWhiteSpace(option.Key) || option.Value is null))
            {
                throw new FormatException("CSV client options must be a JSON object with non-empty string keys and values. / CSV 客户端选项必须是键和值均有效的 JSON 对象。");
            }

            return new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException exception)
        {
            throw new FormatException("CSV client options contain malformed JSON. / CSV 客户端选项包含损坏的 JSON。", exception);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("CSV client options contain duplicate or invalid keys. / CSV 客户端选项包含重复或无效键。", exception);
        }
    }

    /// <summary>Restores version-2 RDP settings while leaving older CSV rows at their defaults. / 恢复版本 2 的 RDP 设置，并让旧版 CSV 行继续使用默认值。</summary>
    /// <param name="json">Serialized RDP settings, or an empty legacy field. / 序列化 RDP 设置，或旧版空字段。</param>
    /// <returns>Restored RDP settings. / 恢复后的 RDP 设置。</returns>
    private RdpOptions ParseCsvRdpOptions(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new RdpOptions();
        }

        try
        {
            return JsonSerializer.Deserialize<RdpOptions>(json, _jsonOptions)
                   ?? throw new FormatException("CSV RDP options must be a JSON object. / CSV RDP 选项必须是 JSON 对象。");
        }
        catch (JsonException exception)
        {
            throw new FormatException("CSV RDP options contain malformed JSON. / CSV RDP 选项包含损坏的 JSON。", exception);
        }
    }

    /// <summary>Reports whether an RDP object differs from the domain defaults and therefore needs a CSV payload. / 报告 RDP 对象是否不同于领域默认值，因而需要写入 CSV 载荷。</summary>
    private static bool HasNonDefaultRdpOptions(RdpOptions? options)
    {
        return options is not null &&
               (!options.FullScreen ||
                options.UseAllMonitors ||
                options.DesktopWidth != 1440 ||
                options.DesktopHeight != 900 ||
                options.ColorDepth != 32 ||
                !options.DisplayConnectionBar ||
                !options.EnableCompression ||
                options.KeyboardHookMode != RdpKeyboardHookMode.FullScreenOnly ||
                !options.RedirectClipboard ||
                options.RedirectDrives ||
                options.RedirectPrinters ||
                options.RedirectSmartCards ||
                options.RedirectComPorts ||
                options.RedirectPosDevices ||
                options.RedirectCameras ||
                options.RedirectMicrophone ||
                options.AudioMode != RdpAudioMode.Local ||
                options.AdministrativeSession ||
                options.PromptForCredentials ||
                options.DisableWallpaper ||
                !options.AutoReconnect);
    }

    /// <summary>
    /// Builds a case-insensitive CSV header lookup. / 构建不区分大小写的 CSV 表头索引。
    /// </summary>
    /// <param name="headerRow">Header row. / 表头行。</param>
    /// <returns>Header-to-index lookup. / 表头到索引的映射。</returns>
    private static Dictionary<string, int> BuildHeader(IReadOnlyList<string> headerRow)
    {
        Dictionary<string, int> header = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < headerRow.Count; index++)
        {
            string name = headerRow[index].Trim().TrimStart('\uFEFF');
            if (!header.ContainsKey(name))
            {
                header.Add(name, index);
            }
        }

        return header;
    }

    /// <summary>
    /// Reads the first matching column value from a row. / 从一行中读取首个匹配列的值。
    /// </summary>
    /// <param name="row">CSV row. / CSV 行。</param>
    /// <param name="header">Header lookup. / 表头索引。</param>
    /// <param name="names">Accepted column names. / 可接受的列名。</param>
    /// <returns>The value or an empty string. / 字段值或空字符串。</returns>
    private static string ReadValue(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> header, params string[] names)
    {
        foreach (string name in names)
        {
            if (header.TryGetValue(name, out int index) && index < row.Count)
            {
                return row[index].Trim();
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Reads a CSV value without removing meaningful leading or trailing whitespace. / 读取 CSV 字段值且不移除有意义的首尾空白。
    /// </summary>
    /// <param name="row">CSV row. / CSV 行。</param>
    /// <param name="header">Header lookup. / 表头索引。</param>
    /// <param name="names">Accepted column names. / 可接受的列名。</param>
    /// <returns>The untrimmed value or an empty string. / 未裁剪的字段值或空字符串。</returns>
    private static string ReadPreservedValue(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> header, params string[] names)
    {
        foreach (string name in names)
        {
            if (header.TryGetValue(name, out int index) && index < row.Count)
            {
                return row[index];
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Reads and validates a required structural CSV field. / 读取并验证必需的结构化 CSV 字段。
    /// </summary>
    /// <param name="row">CSV row. / CSV 行。</param>
    /// <param name="header">Header lookup. / 表头索引。</param>
    /// <param name="names">Accepted column names. / 可接受的列名。</param>
    /// <returns>The trimmed required value. / 裁剪后的必需值。</returns>
    private static string ReadRequiredValue(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> header, params string[] names)
    {
        string value = ReadValue(row, header, names);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException($"Required field '{names[0]}' is empty. / 必需字段“{names[0]}”为空。");
        }

        return value;
    }

    /// <summary>
    /// Maps current and legacy client labels to a stable connection type. / 将当前及旧版客户端标签映射为稳定连接类型。
    /// </summary>
    /// <param name="value">Imported type label. / 导入的类型标签。</param>
    /// <returns>Mapped connection type. / 映射后的连接类型。</returns>
    private static ConnectionType ParseConnectionType(string value)
    {
        int separatorIndex = value.IndexOfAny(['-', '–', '—']);
        string clientLabel = separatorIndex < 0 ? value.Trim() : value[..separatorIndex].Trim();
        if (Enum.TryParse(clientLabel, true, out ConnectionType parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        string normalized = clientLabel.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            "rdp" or "mstsc" or "remotedesktop" or "远程桌面" => ConnectionType.RemoteDesktop,
            "putty" or "puty" => ConnectionType.Putty,
            "xshell" => ConnectionType.Xshell,
            "xftp" => ConnectionType.Xftp,
            "winscp" => ConnectionType.WinScp,
            "securecrt" => ConnectionType.SecureCrt,
            "mobaxterm" or "moba" => ConnectionType.MobaXterm,
            "vnc" => ConnectionType.Vnc,
            "radmin" => ConnectionType.Radmin,
            "todesk" => ConnectionType.ToDesk,
            "rustdesk" or "rustdeskclient" => ConnectionType.RustDesk,
            "custom" or "自定义" or "自定义命令" => ConnectionType.Custom,
            _ => throw new FormatException($"Unknown connection type '{value}'. / 未知的连接类型“{value}”。")
        };
    }

    /// <summary>
    /// Parses a port and falls back to the client default. / 解析端口，并在无值时使用客户端默认端口。
    /// </summary>
    /// <param name="value">Imported port text. / 导入的端口文本。</param>
    /// <param name="fallback">Fallback port. / 备用端口。</param>
    /// <returns>Validated port. / 验证后的端口。</returns>
    private static int ParsePort(string value, int fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int port) || port is < 0 or > 65535)
        {
            throw new FormatException($"Invalid port '{value}'. / 端口“{value}”无效。");
        }

        return port;
    }

    /// <summary>
    /// Parses an optional ISO or local date. / 解析可选的 ISO 或本地日期。
    /// </summary>
    /// <param name="value">Imported date text. / 导入的日期文本。</param>
    /// <returns>Parsed date or null. / 解析后的日期或空值。</returns>
    private static DateTime? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime current) ||
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out current))
        {
            return current.Date;
        }

        throw new FormatException($"Invalid expiration date '{value}'. / 到期日期“{value}”无效。");
    }

    /// <summary>
    /// Normalizes a protocol value and supplies a client default. / 规范化协议值并提供客户端默认值。
    /// </summary>
    /// <param name="value">Imported protocol. / 导入的协议。</param>
    /// <param name="type">Connection type. / 连接类型。</param>
    /// <returns>Normalized protocol. / 规范化后的协议。</returns>
    private static string NormalizeProtocol(string value, ConnectionType type)
    {
        string protocol = type.NormalizeProtocol(value);

        IReadOnlyList<string> supportedProtocols = type.GetProtocols();
        if (supportedProtocols.Count > 0 && !supportedProtocols.Contains(protocol, StringComparer.OrdinalIgnoreCase))
        {
            throw new FormatException($"Protocol '{value}' is not supported by {type}. / {type} 不支持协议“{value}”。");
        }

        return protocol;
    }

    /// <summary>
    /// Extracts the protocol or action suffix from a legacy combined type label. / 从旧版组合类型标签中提取协议或动作后缀。
    /// </summary>
    /// <param name="typeLabel">Combined type label. / 组合类型标签。</param>
    /// <returns>The suffix, or an empty string when absent. / 后缀；不存在时返回空字符串。</returns>
    private static string ReadCombinedTypeProtocol(string typeLabel)
    {
        int separatorIndex = typeLabel.IndexOfAny(['-', '–', '—']);
        return separatorIndex >= 0 && separatorIndex + 1 < typeLabel.Length
            ? typeLabel[(separatorIndex + 1)..].Trim()
            : string.Empty;
    }

    /// <summary>
    /// Validates that a CSV document exposes the required name, type, and host columns. / 验证 CSV 文档是否包含必需的名称、类型和主机列。
    /// </summary>
    /// <param name="header">Header lookup. / 表头索引。</param>
    private static void ValidateRequiredCsvHeaders(IReadOnlyDictionary<string, int> header)
    {
        bool hasName = HasAnyHeader(header, "Name", "名称", "服务器名称");
        bool hasType = HasAnyHeader(header, "Type", "连接类型", "类型");
        bool hasHost = HasAnyHeader(header, "Host", "主机", "IP");
        if (!hasName || !hasType || !hasHost)
        {
            throw new InvalidDataException("The CSV file must contain recognized Name, Type, and Host columns. / CSV 文件必须包含可识别的名称、类型和主机列。");
        }
    }

    /// <summary>
    /// Reports whether a header lookup contains any accepted column name. / 报告表头索引是否包含任一可接受的列名。
    /// </summary>
    /// <param name="header">Header lookup. / 表头索引。</param>
    /// <param name="names">Accepted column names. / 可接受的列名。</param>
    /// <returns>True when at least one name is present. / 至少存在一个名称时返回 true。</returns>
    private static bool HasAnyHeader(IReadOnlyDictionary<string, int> header, params string[] names)
    {
        return names.Any(header.ContainsKey);
    }

    /// <summary>
    /// Validates imported port rules for clients with and without fixed network ports. / 验证有固定网络端口和无固定端口客户端的导入端口规则。
    /// </summary>
    /// <param name="profile">Imported connection profile. / 导入的连接配置。</param>
    private static void ValidateImportedPort(ConnectionProfile profile)
    {
        if (profile.Type.GetDefaultPort() > 0 && profile.Port == 0)
        {
            throw new FormatException("A network connection port must be between 1 and 65535. / 网络连接端口必须介于 1 到 65535 之间。");
        }
    }

    /// <summary>
    /// Prefixes spreadsheet formula-leading characters so exported CSV cells remain inert. / 为电子表格公式起始字符添加前缀，使导出 CSV 单元格保持静态。
    /// </summary>
    /// <param name="value">Raw exported value. / 原始导出值。</param>
    /// <returns>A spreadsheet-safe value. / 对电子表格安全的值。</returns>
    private static string ProtectSpreadsheetFormula(string? value)
    {
        string field = value ?? string.Empty;
        return field.Length > 0 && (field[0] == '\'' || IsSpreadsheetFormulaPrefix(field[0]))
            ? "'" + field
            : field;
    }

    /// <summary>
    /// Restores every cell in a versioned RemoteHubStudio CSV row after spreadsheet-safe export encoding. / 在电子表格安全导出编码后，恢复带版本 RemoteHubStudio CSV 行中的全部单元格。
    /// </summary>
    /// <param name="row">Encoded CSV row. / 已编码的 CSV 行。</param>
    /// <returns>A losslessly restored row. / 无损恢复的数据行。</returns>
    private static IReadOnlyList<string> RestoreSpreadsheetRow(IReadOnlyList<string> row)
    {
        return row.Select(RestoreSpreadsheetCell).ToArray();
    }

    /// <summary>
    /// Reverses the apostrophe escape used only by versioned RemoteHubStudio CSV exports. / 反转仅由带版本 RemoteHubStudio CSV 导出使用的撤号转义。
    /// </summary>
    /// <param name="value">Encoded cell value. / 已编码的单元格值。</param>
    /// <returns>The original cell value. / 原始单元格值。</returns>
    private static string RestoreSpreadsheetCell(string value)
    {
        if (value.StartsWith("''", StringComparison.Ordinal))
        {
            return value[1..];
        }

        return value.Length > 1 && value[0] == '\'' && IsSpreadsheetFormulaPrefix(value[1])
            ? value[1..]
            : value;
    }

    /// <summary>
    /// Identifies characters that common spreadsheet programs may interpret as a formula prefix. / 识别常见电子表格程序可能解释为公式前缀的字符。
    /// </summary>
    /// <param name="character">First cell character. / 单元格首字符。</param>
    /// <returns>True when the character requires neutralization. / 字符需要中和时返回 true。</returns>
    private static bool IsSpreadsheetFormulaPrefix(char character)
    {
        return character is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n';
    }

    /// <summary>
    /// Rejects portable files that exceed the shared import/export size contract. / 拒绝超过共享导入导出大小契约的便携文件。
    /// </summary>
    /// <param name="filePath">Portable file path. / 便携文件路径。</param>
    private static void ValidateTransferFileSize(string filePath)
    {
        long length = new FileInfo(filePath).Length;
        if (length > MaximumTransferFileLength)
        {
            throw new InvalidDataException($"The portable file exceeds {MaximumTransferFileLength} bytes. / 便携文件超过 {MaximumTransferFileLength} 字节。");
        }
    }

    /// <summary>
    /// Creates a unique same-directory temporary path so export promotion remains on one volume. / 创建唯一的同目录临时路径，使导出提升保持在同一卷内。
    /// </summary>
    /// <param name="destinationPath">Absolute destination file path. / 绝对目标文件路径。</param>
    /// <returns>A unique temporary export path. / 唯一的临时导出路径。</returns>
    private static string CreateTemporaryExportFilePath(string destinationPath)
    {
        string? directoryPath = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("The export destination directory is invalid. / 导出目标目录无效。", nameof(destinationPath));
        }

        return Path.Combine(
            directoryPath,
            $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
    }

    /// <summary>
    /// Verifies a completed export and atomically promotes it over the requested destination. / 验证已完成导出并将其原子提升以覆盖请求的目标文件。
    /// </summary>
    /// <param name="temporaryFilePath">Completed temporary export. / 已完成的临时导出文件。</param>
    /// <param name="destinationPath">Final export destination. / 最终导出目标。</param>
    private static void CommitBoundedExport(string temporaryFilePath, string destinationPath)
    {
        ValidateTransferFileSize(temporaryFilePath);
        File.Move(temporaryFilePath, destinationPath, overwrite: true);
    }

    /// <summary>
    /// Best-effort removes an uncommitted temporary export without obscuring the original failure. / 尽力删除未提交的临时导出文件，且不掩盖原始失败。
    /// </summary>
    /// <param name="temporaryFilePath">Temporary path, or an empty string after promotion. / 临时路径；提升后为空字符串。</param>
    private static void DeleteTemporaryExportFileIfPresent(string temporaryFilePath)
    {
        if (string.IsNullOrWhiteSpace(temporaryFilePath))
        {
            return;
        }

        try
        {
            File.Delete(temporaryFilePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Cleanup is best effort; the caller's original export failure remains primary. / 清理为尽力而为；调用方的原始导出失败保持为主要错误。
        }
    }

    /// <summary>
    /// Reads one property from a JSON object without case sensitivity. / 从 JSON 对象中不区分大小写地读取一个属性。
    /// </summary>
    /// <param name="element">JSON object. / JSON 对象。</param>
    /// <param name="propertyName">Property name. / 属性名。</param>
    /// <param name="value">Property value when found. / 找到时的属性值。</param>
    /// <returns>True when the property exists. / 属性存在时返回 true。</returns>
    private static bool TryGetJsonProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Identifies a raw document produced by legacy portable JSON export. / 识别由旧版便携 JSON 导出生成的原始文档。
    /// </summary>
    /// <param name="root">Root JSON object. / JSON 根对象。</param>
    /// <returns>True when a known document property is present. / 存在已知文档属性时返回 true。</returns>
    private static bool LooksLikeLegacyDocument(JsonElement root)
    {
        return TryGetJsonProperty(root, "schemaVersion", out _) ||
               TryGetJsonProperty(root, "settings", out _) ||
               TryGetJsonProperty(root, "groups", out _) ||
               TryGetJsonProperty(root, "connections", out _);
    }

    /// <summary>
    /// Deserializes and validates a portable workspace envelope. / 反序列化并验证便携工作区信封。
    /// </summary>
    /// <param name="root">Envelope JSON object. / 信封 JSON 对象。</param>
    /// <returns>The portable workspace data. / 便携工作区数据。</returns>
    private AppDataDocument ReadPortableEnvelope(JsonElement root)
    {
        if (!TryGetJsonProperty(root, "schema", out JsonElement schemaElement) ||
            schemaElement.ValueKind != JsonValueKind.Number ||
            !schemaElement.TryGetInt32(out int schema) ||
            schema < 1)
        {
            throw new InvalidDataException("The portable workspace envelope schema is missing or invalid. / 便携工作区信封架构缺失或无效。");
        }

        if (schema > PortableEnvelopeSchema)
        {
            throw new InvalidDataException("The portable workspace envelope was created by a newer version. / 便携工作区信封由更高版本创建。");
        }

        if (!TryGetJsonProperty(root, "exportedAt", out JsonElement exportedAtElement) ||
            exportedAtElement.ValueKind != JsonValueKind.String ||
            !exportedAtElement.TryGetDateTime(out _))
        {
            throw new InvalidDataException("The portable workspace export timestamp is missing or invalid. / 便携工作区导出时间缺失或无效。");
        }

        if (!TryGetJsonProperty(root, "data", out JsonElement dataElement) || dataElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The portable workspace envelope has no data object. / 便携工作区信封缺少数据对象。");
        }

        ValidateJsonEntityLimits(dataElement);
        PortableWorkspaceData portableData = dataElement.Deserialize<PortableWorkspaceData>(_jsonOptions)
            ?? throw new InvalidDataException("The portable workspace data is empty. / 便携工作区数据为空。");
        return CreateDocumentFromPortableData(portableData);
    }

    /// <summary>
    /// Rejects oversized JSON entity arrays before model objects are allocated. / 在分配模型对象前拒绝超限的 JSON 实体数组。
    /// </summary>
    /// <param name="data">Workspace data JSON object. / 工作区数据 JSON 对象。</param>
    private static void ValidateJsonEntityLimits(JsonElement data)
    {
        int groupCount = GetJsonEntityCount(data, "groups");
        int connectionCount = GetJsonEntityCount(data, "connections");
        ValidateEntityCounts(groupCount, connectionCount);
    }

    /// <summary>
    /// Reads an entity-array length without materializing its model objects. / 在不实例化模型对象的情况下读取实体数组长度。
    /// </summary>
    /// <param name="data">Workspace data JSON object. / 工作区数据 JSON 对象。</param>
    /// <param name="propertyName">Entity-array property name. / 实体数组属性名。</param>
    /// <returns>The array length, or zero for an absent or null property. / 数组长度；属性缺失或为空时返回零。</returns>
    private static int GetJsonEntityCount(JsonElement data, string propertyName)
    {
        JsonElement collection = default;
        bool found = false;
        foreach (JsonProperty property in data.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (found)
            {
                throw new InvalidDataException($"The workspace contains duplicate '{propertyName}' collections. / 工作区包含重复的“{propertyName}”集合。");
            }

            found = true;
            collection = property.Value;
        }

        if (!found || collection.ValueKind == JsonValueKind.Null)
        {
            return 0;
        }

        return collection.ValueKind == JsonValueKind.Array ? collection.GetArrayLength() : 0;
    }

    /// <summary>
    /// Rechecks deserialized workspace collection counts at the model boundary. / 在模型边界重新检查已反序列化的工作区集合数量。
    /// </summary>
    /// <param name="document">Deserialized workspace document. / 已反序列化的工作区文档。</param>
    private static void ValidateImportedEntityLimits(AppDataDocument document)
    {
        WorkspaceLimits.ValidateDocument(document);
    }

    /// <summary>
    /// Enforces per-collection workspace entity limits. / 强制执行工作区各集合的实体数量上限。
    /// </summary>
    /// <param name="groupCount">Group count. / 分类数量。</param>
    /// <param name="connectionCount">Connection count. / 连接数量。</param>
    private static void ValidateEntityCounts(int groupCount, int connectionCount)
    {
        WorkspaceLimits.ValidateCounts(groupCount, connectionCount);
    }

    /// <summary>
    /// Rejects workspace data schemas newer than this build understands. / 拒绝高于当前版本可理解范围的工作区数据架构。
    /// </summary>
    /// <param name="document">Imported data document. / 导入的数据文档。</param>
    private static void ValidateDocumentSchema(AppDataDocument document)
    {
        if (document.SchemaVersion < 1)
        {
            throw new InvalidDataException("The workspace data schema is invalid. / 工作区数据架构无效。");
        }

        if (document.SchemaVersion > AppDataDocument.CurrentSchemaVersion)
        {
            throw new InvalidDataException("The workspace was created by a newer version. / 工作区由更高版本创建。");
        }
    }

    /// <summary>
    /// Disables imported executable, endpoint-routing, key, argument, and RDP-redirection settings until explicitly trusted. / 在明确信任前，禁用导入的程序、目标路由、私钥、参数与 RDP 重定向设置。
    /// </summary>
    /// <param name="connections">Imported connections. / 导入的连接。</param>
    /// <returns>True when at least one setting was removed. / 至少移除一项设置时返回 true。</returns>
    private static bool SanitizeImportedActiveConfiguration(IEnumerable<ConnectionProfile> connections)
    {
        bool removed = false;
        foreach (ConnectionProfile connection in connections)
        {
            removed |= !string.IsNullOrEmpty(connection.ExecutableOverride) ||
                       !string.IsNullOrEmpty(connection.CustomArguments) ||
                       !string.IsNullOrEmpty(connection.PrivateKeyPath);
            connection.ExecutableOverride = string.Empty;
            connection.CustomArguments = string.Empty;
            connection.PrivateKeyPath = string.Empty;

            // Clear endpoint aliases regardless of the imported type. Otherwise a hostile file can
            // hide them on another type and activate them later when the user changes the type.
            removed |= RemoveOptions(connection.Options, "webDavAddress", "dav_address");
            removed |= RemoveOptions(
                connection.Options,
                "server",
                "serverKey",
                "server_key",
                "forceRelay",
                "relay",
                "force_relay");

            RdpOptions rdp = connection.Rdp ??= new RdpOptions();
            if (connection.Type == ConnectionType.RemoteDesktop)
            {
                removed |= rdp.RedirectClipboard ||
                           rdp.RedirectDrives ||
                           rdp.RedirectPrinters ||
                           rdp.RedirectSmartCards ||
                           rdp.RedirectComPorts ||
                           rdp.RedirectPosDevices ||
                           rdp.RedirectCameras ||
                           rdp.RedirectMicrophone ||
                           rdp.AdministrativeSession;
                rdp.RedirectClipboard = false;
                rdp.RedirectDrives = false;
                rdp.RedirectPrinters = false;
                rdp.RedirectSmartCards = false;
                rdp.RedirectComPorts = false;
                rdp.RedirectPosDevices = false;
                rdp.RedirectCameras = false;
                rdp.RedirectMicrophone = false;
                rdp.AdministrativeSession = false;
            }
        }

        return removed;
    }

    /// <summary>Removes case-insensitive option aliases and reports whether any were present. / 删除不区分大小写的选项别名，并报告是否存在被删除项。</summary>
    private static bool RemoveOptions(IDictionary<string, string>? options, params string[] keys)
    {
        if (options is null || options.Count == 0)
        {
            return false;
        }

        bool removed = false;
        foreach (string key in keys)
        {
            removed |= options.Remove(key);
        }

        return removed;
    }

    /// <summary>
    /// Projects a workspace into portable data without machine-local settings or window state. / 将工作区投影为不含本机设置或窗口状态的便携数据。
    /// </summary>
    /// <param name="document">Detached export document. / 已分离的导出文档。</param>
    /// <returns>Portable workspace data. / 便携工作区数据。</returns>
    private static PortableWorkspaceData CreatePortableData(AppDataDocument document)
    {
        return new PortableWorkspaceData
        {
            SchemaVersion = document.SchemaVersion,
            Groups = document.Groups,
            Connections = document.Connections
        };
    }

    /// <summary>
    /// Restores an application document from portable data while supplying fresh machine-local defaults. / 从便携数据恢复应用文档，并提供全新的本机默认设置。
    /// </summary>
    /// <param name="portableData">Deserialized portable data. / 已反序列化的便携数据。</param>
    /// <returns>An application workspace document. / 应用工作区文档。</returns>
    private static AppDataDocument CreateDocumentFromPortableData(PortableWorkspaceData portableData)
    {
        return new AppDataDocument
        {
            SchemaVersion = portableData.SchemaVersion,
            Settings = new AppSettings(),
            Groups = portableData.Groups ?? [],
            Connections = portableData.Connections ?? []
        };
    }

    /// <summary>
    /// Creates a detached copy before secrets are removed for export. / 在导出移除秘密前创建独立副本。
    /// </summary>
    /// <param name="document">Source document. / 源文档。</param>
    /// <returns>Detached document copy. / 独立文档副本。</returns>
    private AppDataDocument CloneDocument(AppDataDocument document)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, _jsonOptions);
        return JsonSerializer.Deserialize<AppDataDocument>(bytes, _jsonOptions)
               ?? throw new InvalidOperationException("Unable to clone the workspace. / 无法复制工作区。");
    }

    /// <summary>
    /// Removes passwords from a portable export. / 从可移植导出中移除密码。
    /// </summary>
    /// <param name="document">Export document. / 导出文档。</param>
    private static void RemoveSecrets(AppDataDocument document)
    {
        foreach (ConnectionProfile connection in document.Connections)
        {
            connection.Password = string.Empty;
        }
    }

    /// <summary>
    /// Restores required collections and defaults after deserialization. / 反序列化后恢复必需集合与默认值。
    /// </summary>
    /// <param name="document">Imported document. / 导入文档。</param>
    private static void NormalizeDocument(AppDataDocument document)
    {
        document.Settings ??= new AppSettings();
        document.Groups ??= [];
        document.Connections ??= [];
        ValidateImportedEntityShape(document);
        foreach (ConnectionProfile profile in document.Connections)
        {
            profile.Protocol = profile.Type.NormalizeProtocol(profile.Protocol);
            profile.Rdp ??= new RdpOptions();
            profile.Options = profile.Options is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(profile.Options, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Rejects null entity entries before import normalization or launch-policy processing. / 在导入规范化或启动策略处理前拒绝空实体条目。
    /// </summary>
    /// <param name="document">Imported workspace document. / 导入的工作区文档。</param>
    private static void ValidateImportedEntityShape(AppDataDocument document)
    {
        if (document.Groups.Any(group => group is null) ||
            document.Connections.Any(connection => connection is null))
        {
            throw new InvalidDataException("The imported workspace contains a null entity entry. / 导入的工作区包含空实体条目。");
        }
    }

    /// <summary>
    /// Defines the versioned envelope used only for portable workspace transfer. / 定义仅用于便携工作区传输的版本化信封。
    /// </summary>
    private sealed class PortableWorkspaceEnvelope
    {
        /// <summary>Gets or sets the portable format identifier. / 获取或设置便携格式标识。</summary>
        public string Format { get; set; } = ProductInfo.WorkspaceFormatId;

        /// <summary>Gets or sets the portable envelope schema. / 获取或设置便携信封架构版本。</summary>
        public int Schema { get; set; } = PortableEnvelopeSchema;

        /// <summary>Gets or sets the UTC export timestamp. / 获取或设置 UTC 导出时间。</summary>
        public DateTime ExportedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Gets or sets the portable workspace document. / 获取或设置便携工作区文档。</summary>
        public PortableWorkspaceData Data { get; set; } = new();
    }

    /// <summary>
    /// Contains only entities that are meaningful across machines. / 仅包含跨设备有意义的实体。
    /// </summary>
    private sealed class PortableWorkspaceData
    {
        /// <summary>Gets or sets the application data schema version. / 获取或设置应用数据架构版本。</summary>
        public int SchemaVersion { get; set; } = AppDataDocument.CurrentSchemaVersion;

        /// <summary>Gets or sets portable connection groups. / 获取或设置便携连接分类。</summary>
        public List<ConnectionGroup>? Groups { get; set; } = [];

        /// <summary>Gets or sets portable connection profiles. / 获取或设置便携连接配置。</summary>
        public List<ConnectionProfile>? Connections { get; set; } = [];
    }
}
