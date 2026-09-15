using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using RemoteHubStudio.Application;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Infrastructure;
using RemoteHubStudio.Infrastructure.Security;

namespace RemoteHubStudio.Infrastructure.Persistence;

/// <summary>
/// Stores the complete workspace in a versioned JSON envelope with atomic backup rotation. / 使用带版本 JSON 信封和原子备份轮换保存完整工作区。
/// </summary>
public sealed class JsonWorkspaceRepository : IWorkspaceRepository
{
    private const long MaximumWorkspaceFileLength = 64L * 1024L * 1024L;
    private const long MaximumProtectedCorruptArtifactFileLength = 96L * 1024L * 1024L;
    private const int MaximumSerializedStringTokenBytes = 4 * 1024 * 1024;
    private const long MaximumSerializedStringBudgetBytes = 48L * 1024L * 1024L;
    private const int MaximumProtectedPayloadStringBytes = 48 * 1024 * 1024;
    private const int MaximumProtectedWorkspacePlaintextLength = 32 * 1024 * 1024;
    private const int MaximumProtectedWorkspaceDataLength = 36 * 1024 * 1024;
    private const int MaximumProtectedCorruptArtifactDataLength = 70 * 1024 * 1024;
    private const int MaximumCorruptArtifactCount = 32;
    private const long MaximumCorruptArtifactTotalLength = 256L * 1024L * 1024L;
    private static readonly byte[] CorruptArtifactVerificationMagic = Encoding.ASCII.GetBytes("RHS-CORRUPT-V2");
    private static readonly Regex CorruptArtifactFileNamePattern = new(
        @"^workspace\.corrupt\.[0-9]{17}(?:-[0-9a-f]{32})?\.json$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private readonly AppDataPaths _paths;
    private readonly IWorkspaceDataProtector _protector;
    private readonly JsonSerializerOptions _contentJsonOptions;
    private readonly JsonSerializerOptions _envelopeJsonOptions;
    private readonly SemaphoreSlim _ioGate = new(1, 1);

    /// <summary>
    /// Initializes a repository using the application-relative portable data directory and DPAPI profile. / 使用相对于应用程序的便携数据目录和 DPAPI 配置初始化仓储。
    /// </summary>
    public JsonWorkspaceRepository()
        : this(new AppDataPaths(), new DpapiCurrentUserProtector())
    {
    }

    /// <summary>
    /// Initializes a repository with explicit path and protection services. / 使用显式路径和保护服务初始化仓储。
    /// </summary>
    /// <param name="paths">Application data paths. / 应用数据路径。</param>
    /// <param name="protector">Workspace data protector. / 工作区数据保护器。</param>
    public JsonWorkspaceRepository(AppDataPaths paths, IWorkspaceDataProtector protector)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _contentJsonOptions = CreateJsonOptions(writeIndented: false);
        _envelopeJsonOptions = CreateJsonOptions(writeIndented: true);
    }

    /// <summary>
    /// Loads the primary workspace or recovers the previous revision from its backup. / 加载主工作区，或从备份恢复上一个版本。
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>The loaded workspace and recovery information. / 已加载的工作区及恢复信息。</returns>
    public async Task<WorkspaceLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _paths.EnsureDirectoriesExist();

            bool primaryExists = File.Exists(_paths.WorkspaceFilePath);
            bool backupExists = File.Exists(_paths.BackupFilePath);
            if (!primaryExists && !backupExists)
            {
                return new WorkspaceLoadResult(CreateNewDocument());
            }

            Exception? primaryFailure = null;
            if (primaryExists)
            {
                try
                {
                    AppDataDocument primaryDocument = await ReadDocumentAsync(
                        _paths.WorkspaceFilePath,
                        cancellationToken).ConfigureAwait(false);
                    await EnforceCorruptArtifactProtectionAsync(
                        primaryDocument,
                        cancellationToken).ConfigureAwait(false);
                    return new WorkspaceLoadResult(primaryDocument);
                }
                catch (WorkspaceCompatibilityException)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    primaryFailure = exception;
                }
            }
            else
            {
                primaryFailure = new FileNotFoundException(
                    "The primary workspace file is missing. / 主工作区文件不存在。",
                    _paths.WorkspaceFilePath);
            }

            if (!backupExists)
            {
                throw new WorkspacePersistenceException(
                    "The primary workspace could not be loaded and no backup is available. / 无法加载主工作区，且没有可用备份。",
                    primaryFailure!);
            }

            AppDataDocument backupDocument;
            try
            {
                backupDocument = await ReadDocumentAsync(
                    _paths.BackupFilePath,
                    cancellationToken).ConfigureAwait(false);
                await EnforceCorruptArtifactProtectionAsync(
                    backupDocument,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception backupFailure)
            {
                throw new WorkspacePersistenceException(
                    "Neither the primary workspace nor its backup could be loaded. / 主工作区及其备份均无法加载。",
                    new AggregateException(primaryFailure!, backupFailure));
            }

            Exception reportedFailure = primaryFailure!;
            try
            {
                await RestorePrimaryFromBackupAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception restoreFailure)
            {
                reportedFailure = new AggregateException(primaryFailure!, restoreFailure);
            }

            return new WorkspaceLoadResult(backupDocument, recoveredFromBackup: true, reportedFailure);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    /// <summary>
    /// Saves a complete workspace through a same-volume temporary file and rotates the previous file to backup. / 通过同卷临时文件保存完整工作区，并将旧文件轮换为备份。
    /// </summary>
    /// <param name="document">Workspace document to save. / 要保存的工作区文档。</param>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>A task that represents the save operation. / 表示保存操作的任务。</returns>
    public async Task SaveAsync(AppDataDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporaryFilePath = null;
        try
        {
            _paths.EnsureDirectoriesExist();
            ValidateDocumentGraph(document);
            document.SchemaVersion = AppDataDocument.CurrentSchemaVersion;

            bool protectsPreviouslyReadableWorkspace = false;
            if (document.Settings.EncryptionEnabled && File.Exists(_paths.WorkspaceFilePath))
            {
                AppDataDocument previousDocument = await ReadDocumentAsync(
                    _paths.WorkspaceFilePath,
                    cancellationToken).ConfigureAwait(false);
                protectsPreviouslyReadableWorkspace = !previousDocument.Settings.EncryptionEnabled;
            }
            else if (document.Settings.EncryptionEnabled && File.Exists(_paths.BackupFilePath))
            {
                protectsPreviouslyReadableWorkspace = true;
            }

            await EnforceCorruptArtifactProtectionAsync(document, cancellationToken).ConfigureAwait(false);
            WorkspaceEnvelope envelope = CreateEnvelope(document);
            temporaryFilePath = _paths.CreateAtomicTemporaryFilePath();
            await WriteEnvelopeAsync(temporaryFilePath, envelope, cancellationToken).ConfigureAwait(false);
            ValidateWorkspaceFileLength(temporaryFilePath);
            await CommitTemporaryFileAsync(
                temporaryFilePath,
                protectsPreviouslyReadableWorkspace,
                cancellationToken).ConfigureAwait(false);
            temporaryFilePath = null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new WorkspacePersistenceException(
                "The workspace could not be saved atomically. / 无法以原子方式保存工作区。",
                exception);
        }
        finally
        {
            if (temporaryFilePath is not null)
            {
                DeleteTemporaryFileIfPresent(temporaryFilePath);
            }

            _ioGate.Release();
        }
    }

    /// <summary>
    /// Creates an unencrypted default workspace for a first run. / 为首次运行创建默认未加密工作区。
    /// </summary>
    /// <returns>A new default workspace document. / 新的默认工作区文档。</returns>
    private static AppDataDocument CreateNewDocument()
    {
        return new AppDataDocument
        {
            SchemaVersion = AppDataDocument.CurrentSchemaVersion,
            Settings = new AppSettings
            {
                EncryptionEnabled = false
            }
        };
    }

    /// <summary>
    /// Creates serializer settings shared by envelope and workspace documents. / 创建信封与工作区文档共享的序列化设置。
    /// </summary>
    /// <param name="writeIndented">Whether output should be human-readable. / 输出是否应便于人工阅读。</param>
    /// <returns>Configured JSON serializer options. / 配置后的 JSON 序列化选项。</returns>
    private static JsonSerializerOptions CreateJsonOptions(bool writeIndented)
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = null,
            PropertyNameCaseInsensitive = true,
            WriteIndented = writeIndented,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false,
            MaxDepth = 128,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    /// <summary>
    /// Creates either a readable or DPAPI-protected envelope according to application settings. / 根据应用设置创建可读或 DPAPI 保护的信封。
    /// </summary>
    /// <param name="document">Workspace document to wrap. / 要封装的工作区文档。</param>
    /// <returns>The disk envelope. / 磁盘信封。</returns>
    private WorkspaceEnvelope CreateEnvelope(AppDataDocument document)
    {
        WorkspaceEnvelope envelope = new()
        {
            Format = WorkspaceEnvelope.FormatIdentifier,
            SchemaVersion = WorkspaceEnvelope.CurrentSchemaVersion,
            SavedAtUtc = DateTime.UtcNow
        };

        if (!document.Settings.EncryptionEnabled)
        {
            envelope.Protection = WorkspaceEnvelope.NoProtectionScheme;
            envelope.Data = JsonSerializer.SerializeToElement(document, _contentJsonOptions);
            return envelope;
        }

        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(document, _contentJsonOptions);
        byte[]? protectedData = null;
        try
        {
            if (plaintext.Length > MaximumProtectedWorkspacePlaintextLength)
            {
                throw new InvalidDataException(
                    $"The protected workspace plaintext exceeds {MaximumProtectedWorkspacePlaintextLength} bytes. / 受保护工作区明文超过 {MaximumProtectedWorkspacePlaintextLength} 字节。");
            }

            protectedData = _protector.Protect(plaintext);
            if (protectedData.Length > MaximumProtectedWorkspaceDataLength)
            {
                throw new InvalidDataException(
                    $"The protected workspace payload exceeds {MaximumProtectedWorkspaceDataLength} bytes. / 受保护工作区载荷超过 {MaximumProtectedWorkspaceDataLength} 字节。");
            }

            envelope.Protection = _protector.Scheme;
            envelope.Payload = Convert.ToBase64String(protectedData);
            return envelope;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (protectedData is not null)
            {
                CryptographicOperations.ZeroMemory(protectedData);
            }
        }
    }

    /// <summary>
    /// Writes and flushes one complete envelope to a new temporary file. / 将完整信封写入并刷新到新的临时文件。
    /// </summary>
    /// <param name="filePath">Unique temporary file path. / 唯一临时文件路径。</param>
    /// <param name="envelope">Envelope to serialize. / 要序列化的信封。</param>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>A task that represents the write operation. / 表示写入操作的任务。</returns>
    private async Task WriteEnvelopeAsync(
        string filePath,
        WorkspaceEnvelope envelope,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await using LengthLimitedWriteStream boundedStream = new(
            stream,
            MaximumWorkspaceFileLength,
            leaveOpen: true);
        await JsonSerializer.SerializeAsync(
            boundedStream,
            envelope,
            _envelopeJsonOptions,
            cancellationToken).ConfigureAwait(false);
        await boundedStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    /// <summary>
    /// Atomically promotes a flushed temporary file and preserves a policy-compatible backup. / 原子提升已刷新的临时文件，并保留符合当前保护策略的备份。
    /// </summary>
    /// <param name="temporaryFilePath">Flushed temporary file path. / 已刷新的临时文件路径。</param>
    /// <param name="protectsPreviouslyReadableWorkspace">Whether this save enables encryption over a readable primary. / 本次保存是否为可读主文件启用加密。</param>
    /// <param name="cancellationToken">Token used while preparing the protected backup. / 准备受保护备份时使用的取消令牌。</param>
    /// <returns>A task representing the durable promotion. / 表示持久提升的任务。</returns>
    private async Task CommitTemporaryFileAsync(
        string temporaryFilePath,
        bool protectsPreviouslyReadableWorkspace,
        CancellationToken cancellationToken)
    {
        if (File.Exists(_paths.WorkspaceFilePath))
        {
            if (protectsPreviouslyReadableWorkspace)
            {
                string protectedBackupPath = _paths.CreateAtomicTemporaryFilePath();
                try
                {
                    await CopyFileDurablyAsync(
                        temporaryFilePath,
                        protectedBackupPath,
                        cancellationToken).ConfigureAwait(false);
                    File.Move(protectedBackupPath, _paths.BackupFilePath, overwrite: true);
                    protectedBackupPath = string.Empty;
                    File.Replace(
                        temporaryFilePath,
                        _paths.WorkspaceFilePath,
                        destinationBackupFileName: null,
                        ignoreMetadataErrors: true);
                }
                finally
                {
                    if (!string.IsNullOrEmpty(protectedBackupPath))
                    {
                        DeleteTemporaryFileIfPresent(protectedBackupPath);
                    }
                }

                return;
            }

            File.Replace(
                temporaryFilePath,
                _paths.WorkspaceFilePath,
                _paths.BackupFilePath,
                ignoreMetadataErrors: true);
            return;
        }

        if (protectsPreviouslyReadableWorkspace && File.Exists(_paths.BackupFilePath))
        {
            string protectedBackupPath = _paths.CreateAtomicTemporaryFilePath();
            try
            {
                await CopyFileDurablyAsync(
                    temporaryFilePath,
                    protectedBackupPath,
                    cancellationToken).ConfigureAwait(false);
                File.Move(protectedBackupPath, _paths.BackupFilePath, overwrite: true);
                protectedBackupPath = string.Empty;
            }
            finally
            {
                if (!string.IsNullOrEmpty(protectedBackupPath))
                {
                    DeleteTemporaryFileIfPresent(protectedBackupPath);
                }
            }
        }

        File.Move(temporaryFilePath, _paths.WorkspaceFilePath);
    }

    /// <summary>
    /// Reads, validates, decrypts, and normalizes one workspace file. / 读取、验证、解密并规范化一个工作区文件。
    /// </summary>
    /// <param name="filePath">Workspace or backup file path. / 工作区或备份文件路径。</param>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>The normalized workspace document. / 规范化后的工作区文档。</returns>
    private async Task<AppDataDocument> ReadDocumentAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        byte[] jsonBytes = await BoundedFileReader.ReadAllBytesAsync(
            filePath,
            MaximumWorkspaceFileLength,
            cancellationToken).ConfigureAwait(false);
        try
        {
            WorkspaceJsonPreflight.Validate(
                jsonBytes,
                maximumDepth: 128,
                MaximumSerializedStringTokenBytes,
                MaximumSerializedStringBudgetBytes,
                MaximumProtectedPayloadStringBytes);
            using JsonDocument jsonDocument = JsonDocument.Parse(
                jsonBytes,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 128
                });

            JsonElement root = jsonDocument.RootElement;
            AppDataDocument document;
            bool encrypted;

            if (TryReadFormat(root, out string? format))
            {
                if (!string.Equals(format, WorkspaceEnvelope.FormatIdentifier, StringComparison.Ordinal))
                {
                    throw new WorkspaceCompatibilityException(
                        $"Unsupported workspace format '{format}'. / 不支持的工作区格式“{format}”。");
                }

                WorkspaceEnvelope envelope = root.Deserialize<WorkspaceEnvelope>(_envelopeJsonOptions)
                    ?? throw new InvalidDataException("The workspace envelope is empty. / 工作区信封为空。");
                ValidateEnvelopeVersion(envelope.SchemaVersion);
                encrypted = !string.Equals(
                    envelope.Protection,
                    WorkspaceEnvelope.NoProtectionScheme,
                    StringComparison.OrdinalIgnoreCase);
                document = ReadEnvelopeDocument(envelope);
            }
            else
            {
                if (!LooksLikeLegacyDocument(root))
                {
                    throw new InvalidDataException(
                        "The JSON file is neither a workspace envelope nor a legacy workspace document. / JSON 文件既不是工作区信封，也不是旧版工作区文档。");
                }

                document = root.Deserialize<AppDataDocument>(_contentJsonOptions)
                    ?? throw new InvalidDataException("The legacy workspace document is empty. / 旧版工作区文档为空。");
                encrypted = false;
            }

            return NormalizeDocument(document, encrypted);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(jsonBytes);
        }
    }

    /// <summary>
    /// Rejects a workspace envelope that cannot be read back under the repository size contract. / 拒绝无法在仓储大小契约下重新读取的工作区信封。
    /// </summary>
    /// <param name="filePath">Workspace envelope path to measure. / 要测量的工作区信封路径。</param>
    private static void ValidateWorkspaceFileLength(string filePath)
    {
        long length = new FileInfo(filePath).Length;
        if (length > MaximumWorkspaceFileLength)
        {
            throw new InvalidDataException(
                $"The workspace file exceeds {MaximumWorkspaceFileLength} bytes. / 工作区文件超过 {MaximumWorkspaceFileLength} 字节。");
        }
    }

    /// <summary>
    /// Reads a workspace document from an already validated envelope. / 从已验证的信封中读取工作区文档。
    /// </summary>
    /// <param name="envelope">Validated workspace envelope. / 已验证的工作区信封。</param>
    /// <returns>The deserialized workspace document. / 反序列化后的工作区文档。</returns>
    private AppDataDocument ReadEnvelopeDocument(WorkspaceEnvelope envelope)
    {
        if (string.Equals(
            envelope.Protection,
            WorkspaceEnvelope.NoProtectionScheme,
            StringComparison.OrdinalIgnoreCase))
        {
            if (envelope.Data is null)
            {
                throw new InvalidDataException("The readable workspace payload is missing. / 可读工作区载荷缺失。");
            }

            return envelope.Data.Value.Deserialize<AppDataDocument>(_contentJsonOptions)
                ?? throw new InvalidDataException("The readable workspace payload is empty. / 可读工作区载荷为空。");
        }

        if (!string.Equals(envelope.Protection, _protector.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            throw new WorkspaceCompatibilityException(
                $"Unsupported workspace protection scheme '{envelope.Protection}'. / 不支持的工作区保护方案“{envelope.Protection}”。");
        }

        if (string.IsNullOrWhiteSpace(envelope.Payload))
        {
            throw new InvalidDataException("The protected workspace payload is missing. / 受保护的工作区载荷缺失。");
        }

        byte[] protectedData = Convert.FromBase64String(envelope.Payload);
        byte[]? plaintext = null;
        try
        {
            plaintext = _protector.Unprotect(protectedData);
            if (plaintext.Length > MaximumProtectedWorkspacePlaintextLength)
            {
                throw new InvalidDataException(
                    $"The protected workspace plaintext exceeds {MaximumProtectedWorkspacePlaintextLength} bytes. / 受保护工作区明文超过 {MaximumProtectedWorkspacePlaintextLength} 字节。");
            }

            WorkspaceJsonPreflight.Validate(
                plaintext,
                maximumDepth: 128,
                MaximumSerializedStringTokenBytes,
                MaximumSerializedStringBudgetBytes);
            return JsonSerializer.Deserialize<AppDataDocument>(plaintext, _contentJsonOptions)
                ?? throw new InvalidDataException("The protected workspace payload is empty. / 受保护的工作区载荷为空。");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedData);
            if (plaintext is not null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
    }

    /// <summary>
    /// Extracts the optional format property without deserializing an untrusted envelope. / 在不反序列化不可信信封的情况下提取可选格式属性。
    /// </summary>
    /// <param name="root">Root JSON element. / JSON 根元素。</param>
    /// <param name="format">Extracted format value. / 提取出的格式值。</param>
    /// <returns>True when a string format property exists. / 存在字符串格式属性时返回 true。</returns>
    private static bool TryReadFormat(JsonElement root, out string? format)
    {
        format = null;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("format", out JsonElement formatElement) ||
            formatElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        format = formatElement.GetString();
        return true;
    }

    /// <summary>
    /// Determines whether a root object resembles the pre-envelope workspace shape. / 判断根对象是否类似信封引入前的工作区结构。
    /// </summary>
    /// <param name="root">Root JSON element. / JSON 根元素。</param>
    /// <returns>True when known legacy workspace properties are present. / 存在已知旧版工作区属性时返回 true。</returns>
    private static bool LooksLikeLegacyDocument(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return root.TryGetProperty("schemaVersion", out _) ||
               root.TryGetProperty("settings", out _) ||
               root.TryGetProperty("connections", out _) ||
               root.TryGetProperty("groups", out _);
    }

    /// <summary>
    /// Rejects future envelope versions to prevent destructive downgrade writes. / 拒绝未来信封版本，防止破坏性的降级写入。
    /// </summary>
    /// <param name="schemaVersion">Envelope schema version read from disk. / 从磁盘读取的信封架构版本。</param>
    private static void ValidateEnvelopeVersion(int schemaVersion)
    {
        if (schemaVersion <= 0)
        {
            throw new InvalidDataException("The workspace envelope version is invalid. / 工作区信封版本无效。");
        }

        if (schemaVersion > WorkspaceEnvelope.CurrentSchemaVersion)
        {
            throw new WorkspaceCompatibilityException(
                $"Workspace envelope version {schemaVersion} is newer than supported version {WorkspaceEnvelope.CurrentSchemaVersion}. / 工作区信封版本 {schemaVersion} 高于当前支持的版本 {WorkspaceEnvelope.CurrentSchemaVersion}。");
        }
    }

    /// <summary>
    /// Migrates basic legacy defaults and restores non-null collection invariants. / 迁移基础旧版默认值并恢复集合非空约束。
    /// </summary>
    /// <param name="document">Deserialized workspace document. / 反序列化后的工作区文档。</param>
    /// <param name="encrypted">Whether the containing envelope was protected. / 外层信封是否受到保护。</param>
    /// <returns>The normalized current document. / 规范化后的当前文档。</returns>
    private static AppDataDocument NormalizeDocument(AppDataDocument document, bool encrypted)
    {
        if (document.SchemaVersion > AppDataDocument.CurrentSchemaVersion)
        {
            throw new WorkspaceCompatibilityException(
                $"Workspace data version {document.SchemaVersion} is newer than supported version {AppDataDocument.CurrentSchemaVersion}. / 工作区数据版本 {document.SchemaVersion} 高于当前支持的版本 {AppDataDocument.CurrentSchemaVersion}。");
        }

        if (document.SchemaVersion <= 0)
        {
            document.SchemaVersion = 1;
        }

        document.SchemaVersion = AppDataDocument.CurrentSchemaVersion;
        document.Settings ??= new AppSettings();
        document.Settings.EncryptionEnabled = encrypted;
        document.Settings.ToolPaths = document.Settings.ToolPaths is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(document.Settings.ToolPaths, StringComparer.OrdinalIgnoreCase);
        document.Groups ??= [];
        document.Connections ??= [];

        ValidateDocumentGraph(document);

        for (int index = 0; index < document.Groups.Count; index++)
        {
            ConnectionGroup group = document.Groups[index];
            group.Name ??= string.Empty;
            group.Color ??= "#1677FF";
        }

        for (int index = 0; index < document.Connections.Count; index++)
        {
            ConnectionProfile connection = document.Connections[index];
            connection.Name ??= string.Empty;
            connection.Protocol = connection.Type.NormalizeProtocol(connection.Protocol);
            connection.Host ??= string.Empty;
            connection.Username ??= string.Empty;
            connection.Password ??= string.Empty;
            connection.PrivateKeyPath ??= string.Empty;
            connection.Notes ??= string.Empty;
            connection.ExecutableOverride ??= string.Empty;
            connection.CustomArguments ??= string.Empty;
            connection.Rdp ??= new RdpOptions();
            connection.Options = connection.Options is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(connection.Options, StringComparer.OrdinalIgnoreCase);
        }

        return document;
    }

    /// <summary>
    /// Validates entity identifiers and every internal reference before data reaches the UI. / 在数据进入界面前验证实体标识及全部内部引用。
    /// </summary>
    /// <param name="document">Normalized workspace collection container. / 已规范化集合容器的工作区。</param>
    private static void ValidateDocumentGraph(AppDataDocument document)
    {
        WorkspaceLimits.ValidateDocument(document);
        WorkspaceContentLimits.ValidateDocument(document);
        GroupGraphValidator.Validate(document.Groups);
        Dictionary<Guid, ConnectionGroup> groupsById = document.Groups.ToDictionary(group => group.Id);

        HashSet<Guid> connectionIds = [];
        for (int index = 0; index < document.Connections.Count; index++)
        {
            ConnectionProfile? connection = document.Connections[index];
            if (connection is null)
            {
                throw new InvalidDataException($"Connection entry {index} is null. / 第 {index} 个连接条目为空。");
            }

            if (connection.Id == Guid.Empty)
            {
                throw new InvalidDataException("A connection identifier is empty. / 存在空的连接标识。");
            }

            if (!connectionIds.Add(connection.Id))
            {
                throw new InvalidDataException($"Duplicate connection identifier '{connection.Id}'. / 连接标识“{connection.Id}”重复。");
            }

            if (!Enum.IsDefined(connection.Type))
            {
                throw new InvalidDataException($"Connection type value '{connection.Type}' is invalid. / 连接类型值“{connection.Type}”无效。");
            }

            if (connection.GroupId is Guid groupId && !groupsById.ContainsKey(groupId))
            {
                throw new InvalidDataException($"Connection '{connection.Id}' references missing group '{groupId}'. / 连接“{connection.Id}”引用了不存在的分类“{groupId}”。");
            }

            if (connection.Port is < 0 or > 65535)
            {
                throw new InvalidDataException($"Connection '{connection.Id}' has an invalid port '{connection.Port}'. / 连接“{connection.Id}”的端口“{connection.Port}”无效。");
            }
        }

    }

    /// <summary>
    /// Protects every precisely named damaged-workspace artifact whenever local encryption is active. / 本地加密启用时，保护每个名称精确匹配的损坏工作区保留文件。
    /// </summary>
    /// <param name="document">Workspace whose selected protection policy is being enforced. / 正在执行所选保护策略的工作区。</param>
    /// <param name="cancellationToken">Token used to cancel artifact migration. / 用于取消保留文件迁移的令牌。</param>
    /// <returns>A task representing policy enforcement. / 表示策略执行的任务。</returns>
    private async Task EnforceCorruptArtifactProtectionAsync(
        AppDataDocument document,
        CancellationToken cancellationToken)
    {
        if (!document.Settings.EncryptionEnabled || !Directory.Exists(_paths.DataDirectory))
        {
            return;
        }

        List<string> artifactPaths = [];
        long artifactTotalLength = 0;
        foreach (string candidate in Directory.EnumerateFiles(
                     _paths.DataDirectory,
                     "workspace.corrupt.*.json",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath = Path.GetFullPath(candidate);
            string? directory = Path.GetDirectoryName(fullPath);
            string fileName = Path.GetFileName(fullPath);
            if (!string.Equals(directory, _paths.DataDirectory, StringComparison.OrdinalIgnoreCase) ||
                !CorruptArtifactFileNamePattern.IsMatch(fileName))
            {
                continue;
            }

            artifactPaths.Add(fullPath);
            if (artifactPaths.Count > MaximumCorruptArtifactCount)
            {
                throw new InvalidDataException(
                    $"Damaged-workspace artifact migration exceeds {MaximumCorruptArtifactCount} files. / 损坏工作区保留文件迁移超过 {MaximumCorruptArtifactCount} 个文件。");
            }

            long artifactLength = new FileInfo(fullPath).Length;
            if (artifactLength < 0 || artifactTotalLength > MaximumCorruptArtifactTotalLength - artifactLength)
            {
                throw new InvalidDataException(
                    $"Damaged-workspace artifact migration exceeds {MaximumCorruptArtifactTotalLength} bytes. / 损坏工作区保留文件迁移超过 {MaximumCorruptArtifactTotalLength} 字节。");
            }

            artifactTotalLength += artifactLength;

            FileAttributes attributes = File.GetAttributes(fullPath);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException(
                    $"Damaged-workspace artifact '{fileName}' is a reparse point and cannot be migrated safely. / 损坏工作区保留文件“{fileName}”是重解析点，无法安全迁移。");
            }

        }

        foreach (string fullPath in artifactPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await LooksLikeProtectedCorruptArtifactAsync(fullPath, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            await CreateProtectedCorruptArtifactAsync(
                fullPath,
                fullPath,
                replaceDestination: true,
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Recognizes a strict protected-artifact envelope without decrypting its complete payload on every load. / 在每次加载时不解密完整载荷即可识别严格的受保护文件信封。
    /// </summary>
    /// <param name="filePath">Artifact to inspect. / 要检查的保留文件。</param>
    /// <param name="cancellationToken">Token used to cancel inspection. / 用于取消检查的令牌。</param>
    /// <returns>True only for a structurally complete protected artifact. / 仅对结构完整的受保护保留文件返回 true。</returns>
    private async Task<bool> LooksLikeProtectedCorruptArtifactAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            byte[] jsonBytes = await BoundedFileReader.ReadAllBytesAsync(
                filePath,
                MaximumProtectedCorruptArtifactFileLength,
                cancellationToken).ConfigureAwait(false);
            try
            {
                if (jsonBytes.Length == 0)
                {
                    return false;
                }

                using JsonDocument jsonDocument = JsonDocument.Parse(
                    jsonBytes,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = false,
                        CommentHandling = JsonCommentHandling.Disallow,
                        MaxDepth = 16
                    });

                JsonElement root = jsonDocument.RootElement;
                if (!HasExactCorruptArtifactShape(root))
                {
                    return false;
                }

                JsonElement payload = root.GetProperty("payload");
                if (!string.Equals(root.GetProperty("format").GetString(), CorruptWorkspaceArtifactEnvelope.FormatIdentifier, StringComparison.Ordinal) ||
                    root.GetProperty("schemaVersion").GetInt32() != CorruptWorkspaceArtifactEnvelope.CurrentSchemaVersion ||
                    !string.Equals(root.GetProperty("protection").GetString(), _protector.Scheme, StringComparison.OrdinalIgnoreCase) ||
                    !root.GetProperty("originalLength").TryGetInt64(out long originalLength) ||
                    originalLength is < 0 or > MaximumWorkspaceFileLength ||
                    payload.ValueKind != JsonValueKind.String ||
                    payload.ValueEquals(string.Empty) ||
                    !TryReadCorruptArtifactPayloadBinding(jsonBytes, out byte[] payloadHash, out byte[] verificationProtected))
                {
                    return false;
                }

                byte[]? verificationPlaintext = null;
                try
                {
                    verificationPlaintext = _protector.Unprotect(verificationProtected);
                    return VerifyCorruptArtifactBinding(verificationPlaintext, originalLength, payloadHash);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(payloadHash);
                    CryptographicOperations.ZeroMemory(verificationProtected);
                    if (verificationPlaintext is not null)
                    {
                        CryptographicOperations.ZeroMemory(verificationPlaintext);
                    }
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(jsonBytes);
            }
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidDataException or CryptographicException)
        {
            return false;
        }
    }

    /// <summary>
    /// Requires exactly the known protected-artifact properties so extra readable data cannot bypass migration. / 要求仅包含已知受保护文件属性，防止额外可读数据绕过迁移。
    /// </summary>
    /// <param name="root">Candidate JSON root. / 候选 JSON 根元素。</param>
    /// <returns>True when every expected property occurs exactly once. / 每个预期属性恰好出现一次时返回 true。</returns>
    private static bool HasExactCorruptArtifactShape(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        HashSet<string> expectedProperties = new(StringComparer.Ordinal)
        {
            "format",
            "schemaVersion",
            "preservedAtUtc",
            "protection",
            "originalLength",
            "payload",
            "verificationPayload"
        };
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!expectedProperties.Remove(property.Name))
            {
                return false;
            }
        }

        return expectedProperties.Count == 0;
    }

    /// <summary>
    /// Writes a DPAPI-style protected copy of raw damaged bytes and atomically installs it. / 写入受 DPAPI 风格保护的损坏原始字节副本，并以原子方式安装。
    /// </summary>
    /// <param name="sourcePath">Raw damaged file to preserve. / 要保留的原始损坏文件。</param>
    /// <param name="destinationPath">Final protected artifact path. / 最终受保护保留文件路径。</param>
    /// <param name="replaceDestination">Whether an existing legacy artifact is replaced in place. / 是否原位替换现有旧版保留文件。</param>
    /// <param name="cancellationToken">Token used to cancel reading or writing. / 用于取消读取或写入的令牌。</param>
    /// <returns>A task representing the durable protected copy. / 表示持久受保护副本的任务。</returns>
    private async Task CreateProtectedCorruptArtifactAsync(
        string sourcePath,
        string destinationPath,
        bool replaceDestination,
        CancellationToken cancellationToken)
    {
        byte[] plaintext = await BoundedFileReader.ReadAllBytesAsync(
            sourcePath,
            MaximumWorkspaceFileLength,
            cancellationToken).ConfigureAwait(false);
        byte[]? protectedData = null;
        string temporaryFilePath = _paths.CreateAtomicTemporaryFilePath();
        try
        {
            protectedData = _protector.Protect(plaintext);
            if (protectedData.Length > MaximumProtectedCorruptArtifactDataLength)
            {
                throw new InvalidDataException(
                    $"The protected damaged-workspace payload exceeds {MaximumProtectedCorruptArtifactDataLength} bytes. / 受保护损坏工作区载荷超过 {MaximumProtectedCorruptArtifactDataLength} 字节。");
            }

            string payload = Convert.ToBase64String(protectedData);
            byte[] verificationPlaintext = CreateCorruptArtifactVerification(payload, plaintext.LongLength);
            byte[]? verificationProtected = null;
            try
            {
                verificationProtected = _protector.Protect(verificationPlaintext);
                CorruptWorkspaceArtifactEnvelope envelope = new()
                {
                    Protection = _protector.Scheme,
                    OriginalLength = plaintext.LongLength,
                    Payload = payload,
                    VerificationPayload = Convert.ToBase64String(verificationProtected)
                };
                await WriteCorruptArtifactEnvelopeAsync(
                    temporaryFilePath,
                    envelope,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(verificationPlaintext);
                if (verificationProtected is not null)
                {
                    CryptographicOperations.ZeroMemory(verificationProtected);
                }
            }

            if (replaceDestination)
            {
                File.Replace(
                    temporaryFilePath,
                    destinationPath,
                    destinationBackupFileName: null,
                    ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryFilePath, destinationPath);
            }

            temporaryFilePath = string.Empty;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (protectedData is not null)
            {
                CryptographicOperations.ZeroMemory(protectedData);
            }

            if (!string.IsNullOrEmpty(temporaryFilePath))
            {
                DeleteTemporaryFileIfPresent(temporaryFilePath);
            }
        }
    }

    /// <summary>
    /// Creates a small protected verification record binding artifact length and encoded payload hash. / 创建绑定保留文件长度与编码载荷哈希的小型受保护校验记录。
    /// </summary>
    /// <param name="payload">Canonical Base64 payload produced by this repository. / 此仓储生成的规范 Base64 载荷。</param>
    /// <param name="originalLength">Original damaged byte length. / 原始损坏字节长度。</param>
    /// <returns>Verification plaintext to protect separately. / 要单独保护的校验明文。</returns>
    private static byte[] CreateCorruptArtifactVerification(string payload, long originalLength)
    {
        byte[] payloadHash = HashAsciiPayload(payload);
        byte[] verification = GC.AllocateUninitializedArray<byte>(
            CorruptArtifactVerificationMagic.Length + sizeof(long) + payloadHash.Length);
        CorruptArtifactVerificationMagic.CopyTo(verification, 0);
        BinaryPrimitives.WriteInt64LittleEndian(
            verification.AsSpan(CorruptArtifactVerificationMagic.Length, sizeof(long)),
            originalLength);
        payloadHash.CopyTo(verification, CorruptArtifactVerificationMagic.Length + sizeof(long));
        CryptographicOperations.ZeroMemory(payloadHash);
        return verification;
    }

    /// <summary>
    /// Hashes canonical ASCII Base64 text without allocating another payload-sized byte array. / 对规范 ASCII Base64 文本进行哈希，且不再分配一个载荷大小的字节数组。
    /// </summary>
    /// <param name="payload">Canonical Base64 payload. / 规范 Base64 载荷。</param>
    /// <returns>SHA-256 hash bytes. / SHA-256 哈希字节。</returns>
    private static byte[] HashAsciiPayload(string payload)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        const int ChunkLength = 4096;
        byte[] buffer = GC.AllocateUninitializedArray<byte>(ChunkLength);
        try
        {
            for (int offset = 0; offset < payload.Length; offset += ChunkLength)
            {
                int count = Math.Min(ChunkLength, payload.Length - offset);
                for (int index = 0; index < count; index++)
                {
                    char character = payload[offset + index];
                    if (character > 0x7F)
                    {
                        throw new InvalidDataException("The protected artifact payload is not canonical Base64. / 受保护保留文件载荷不是规范 Base64。");
                    }

                    buffer[index] = (byte)character;
                }

                hash.AppendData(buffer, 0, count);
            }

            return hash.GetHashAndReset();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    /// <summary>
    /// Reads and hashes the large payload in-place while decoding only the small verification payload. / 原位读取并哈希大型载荷，同时仅解码小型校验载荷。
    /// </summary>
    /// <param name="jsonBytes">Bounded artifact JSON bytes. / 受限保留文件 JSON 字节。</param>
    /// <param name="payloadHash">Hash of canonical encoded payload text. / 规范编码载荷文本的哈希。</param>
    /// <param name="verificationProtected">Decoded protected verification bytes. / 解码后的受保护校验字节。</param>
    /// <returns>True when both payload fields are canonical and complete. / 两个载荷字段均规范且完整时返回 true。</returns>
    private static bool TryReadCorruptArtifactPayloadBinding(
        ReadOnlySpan<byte> jsonBytes,
        out byte[] payloadHash,
        out byte[] verificationProtected)
    {
        payloadHash = [];
        verificationProtected = [];
        Utf8JsonReader reader = new(jsonBytes, new JsonReaderOptions { MaxDepth = 16 });
        string pendingProperty = string.Empty;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1)
            {
                if (!reader.ValueIsEscaped && reader.ValueTextEquals("payload"u8))
                {
                    pendingProperty = "payload";
                }
                else if (!reader.ValueIsEscaped && reader.ValueTextEquals("verificationPayload"u8))
                {
                    pendingProperty = "verificationPayload";
                }
                else
                {
                    pendingProperty = string.Empty;
                }

                continue;
            }

            if (reader.TokenType != JsonTokenType.String || reader.CurrentDepth != 1 || reader.ValueIsEscaped)
            {
                pendingProperty = string.Empty;
                continue;
            }

            if (pendingProperty == "payload")
            {
                ReadOnlySpan<byte> payload = reader.ValueSpan;
                if (!IsCanonicalBase64(payload))
                {
                    return false;
                }

                payloadHash = SHA256.HashData(payload);
            }
            else if (pendingProperty == "verificationPayload")
            {
                if (reader.ValueSpan.Length is 0 or > 4096 || !IsCanonicalBase64(reader.ValueSpan))
                {
                    return false;
                }

                verificationProtected = Convert.FromBase64String(Encoding.ASCII.GetString(reader.ValueSpan));
            }

            pendingProperty = string.Empty;
        }

        return payloadHash.Length == SHA256.HashSizeInBytes && verificationProtected.Length > 0;
    }

    /// <summary>
    /// Verifies canonical unescaped Base64 syntax without decoding the large payload. / 在不解码大型载荷的情况下验证规范且未转义的 Base64 语法。
    /// </summary>
    /// <param name="value">Encoded UTF-8 bytes. / 编码后的 UTF-8 字节。</param>
    /// <returns>True for canonical Base64 syntax. / 符合规范 Base64 语法时返回 true。</returns>
    private static bool IsCanonicalBase64(ReadOnlySpan<byte> value)
    {
        if (value.Length == 0 || value.Length % 4 != 0)
        {
            return false;
        }

        int paddingStart = value.Length;
        while (paddingStart > 0 && value[paddingStart - 1] == (byte)'=')
        {
            paddingStart--;
        }

        int paddingCount = value.Length - paddingStart;
        if (paddingCount > 2)
        {
            return false;
        }

        for (int index = 0; index < paddingStart; index++)
        {
            byte character = value[index];
            if (!(character is >= (byte)'A' and <= (byte)'Z' or
                  >= (byte)'a' and <= (byte)'z' or
                  >= (byte)'0' and <= (byte)'9' or
                  (byte)'+' or (byte)'/'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks a decrypted small verification record against artifact length and payload hash. / 根据保留文件长度与载荷哈希检查已解密的小型校验记录。
    /// </summary>
    /// <param name="verification">Decrypted verification bytes. / 已解密的校验字节。</param>
    /// <param name="originalLength">Declared original byte length. / 声明的原始字节长度。</param>
    /// <param name="payloadHash">Observed encoded-payload hash. / 观察到的编码载荷哈希。</param>
    /// <returns>True when the binding is exact. / 绑定完全匹配时返回 true。</returns>
    private static bool VerifyCorruptArtifactBinding(
        ReadOnlySpan<byte> verification,
        long originalLength,
        ReadOnlySpan<byte> payloadHash)
    {
        int expectedLength = CorruptArtifactVerificationMagic.Length + sizeof(long) + SHA256.HashSizeInBytes;
        return verification.Length == expectedLength &&
               verification[..CorruptArtifactVerificationMagic.Length].SequenceEqual(CorruptArtifactVerificationMagic) &&
               BinaryPrimitives.ReadInt64LittleEndian(
                   verification.Slice(CorruptArtifactVerificationMagic.Length, sizeof(long))) == originalLength &&
               CryptographicOperations.FixedTimeEquals(
                   verification[(CorruptArtifactVerificationMagic.Length + sizeof(long))..],
                   payloadHash);
    }

    /// <summary>
    /// Serializes and durably flushes one protected damaged-workspace artifact. / 序列化并持久刷新一个受保护的损坏工作区保留文件。
    /// </summary>
    /// <param name="filePath">Unique temporary output path. / 唯一临时输出路径。</param>
    /// <param name="envelope">Protected artifact envelope. / 受保护的保留文件信封。</param>
    /// <param name="cancellationToken">Token used to cancel writing. / 用于取消写入的令牌。</param>
    /// <returns>A task representing the durable write. / 表示持久写入的任务。</returns>
    private async Task WriteCorruptArtifactEnvelopeAsync(
        string filePath,
        CorruptWorkspaceArtifactEnvelope envelope,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await using LengthLimitedWriteStream boundedStream = new(
            stream,
            MaximumProtectedCorruptArtifactFileLength,
            leaveOpen: true);
        await JsonSerializer.SerializeAsync(
            boundedStream,
            envelope,
            _envelopeJsonOptions,
            cancellationToken).ConfigureAwait(false);
        await boundedStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    /// <summary>
    /// Restores a valid backup to the primary path while preserving a damaged primary copy. / 将有效备份恢复到主路径，同时保留损坏的主文件副本。
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>A task that represents the recovery write. / 表示恢复写入的任务。</returns>
    private async Task RestorePrimaryFromBackupAsync(CancellationToken cancellationToken)
    {
        string temporaryFilePath = _paths.CreateAtomicTemporaryFilePath();
        Exception? preservationFailure = null;
        try
        {
            if (File.Exists(_paths.WorkspaceFilePath))
            {
                try
                {
                    await CreateProtectedCorruptArtifactAsync(
                        _paths.WorkspaceFilePath,
                        _paths.CreateCorruptFilePath(),
                        replaceDestination: false,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    preservationFailure = exception;
                }
            }

            await CopyFileDurablyAsync(
                _paths.BackupFilePath,
                temporaryFilePath,
                cancellationToken).ConfigureAwait(false);
            File.Move(temporaryFilePath, _paths.WorkspaceFilePath, overwrite: true);
            temporaryFilePath = string.Empty;

            if (preservationFailure is not null)
            {
                throw new WorkspacePersistenceException(
                    "The workspace was restored, but its damaged bytes could not be preserved securely. / 工作区已恢复，但无法安全保留其损坏字节。",
                    preservationFailure);
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(temporaryFilePath))
            {
                DeleteTemporaryFileIfPresent(temporaryFilePath);
            }
        }
    }

    /// <summary>
    /// Copies a recovery source to a new file and flushes it to disk. / 将恢复源复制到新文件并刷新到磁盘。
    /// </summary>
    /// <param name="sourcePath">Source backup path. / 源备份路径。</param>
    /// <param name="destinationPath">Unique destination path. / 唯一目标路径。</param>
    /// <param name="cancellationToken">Token used to cancel the operation. / 用于取消操作的令牌。</param>
    /// <returns>A task that represents the durable copy. / 表示持久复制的任务。</returns>
    private static async Task CopyFileDurablyAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using FileStream source = new(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using FileStream destination = new(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough);

        await source.CopyToAsync(destination, 64 * 1024, cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        destination.Flush(flushToDisk: true);
    }

    /// <summary>
    /// Deletes only the explicitly supplied repository temporary file when it remains. / 仅在仓储临时文件仍存在时删除明确指定的文件。
    /// </summary>
    /// <param name="temporaryFilePath">Exact temporary file path. / 精确的临时文件路径。</param>
    private static void DeleteTemporaryFileIfPresent(string temporaryFilePath)
    {
        if (File.Exists(temporaryFilePath))
        {
            File.Delete(temporaryFilePath);
        }
    }
}
