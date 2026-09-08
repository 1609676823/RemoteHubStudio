using System.Security;
using System.Text.Json;

namespace RemoteHubStudio.Infrastructure.Persistence;

/// <summary>Persists the default reset rectangle independently from the last window position and connection data. / 独立于最近窗口位置和连接数据保存默认重置边界。</summary>
public sealed class WindowPlacementStore
{
    private const int Version = 1;
    private const int MaximumFileBytes = 4096;
    private readonly string _filePath;

    /// <summary>Creates a store in the application's portable data directory. / 在应用的便携数据目录中创建存储。</summary>
    public WindowPlacementStore(AppDataPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _filePath = Path.Combine(paths.DataDirectory, "window-placement.json");
    }

    /// <summary>Loads valid physical reset bounds, returning null when missing, damaged or inaccessible. / 加载有效的物理重置边界，缺失、损坏或无法访问时返回 null。</summary>
    public Rectangle? LoadDefaultBounds()
    {
        try
        {
            using FileStream stream = new(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > MaximumFileBytes) return null;
            byte[] bytes = new byte[MaximumFileBytes + 1];
            int length = 0;
            while (length < bytes.Length)
            {
                int read = stream.Read(bytes, length, bytes.Length - length);
                if (read == 0) break;
                length += read;
            }
            if (length > MaximumFileBytes) return null;

            using JsonDocument document = JsonDocument.Parse(bytes.AsMemory(0, length), new JsonDocumentOptions { MaxDepth = 2 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            Dictionary<string, int> values = new(StringComparer.Ordinal);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (property.Name is not ("version" or "x" or "y" or "width" or "height") ||
                    property.Value.ValueKind != JsonValueKind.Number ||
                    !property.Value.TryGetInt32(out int value) || !values.TryAdd(property.Name, value)) return null;
            }
            if (values.Count != 5 || values["version"] != Version) return null;
            Rectangle bounds = new(values["x"], values["y"], values["width"], values["height"]);
            return IsValid(bounds) ? bounds : null;
        }
        catch (Exception exception) when (IsExpectedStorageFailure(exception))
        {
            return null;
        }
    }

    /// <summary>Atomically saves valid physical reset bounds; returns false on invalid bounds or a storage failure. / 原子保存有效的物理重置边界，边界无效或存储失败时返回 false。</summary>
    public bool SaveDefaultBounds(Rectangle bounds)
    {
        if (!IsValid(bounds)) return false;
        string? temporaryPath = null;
        try
        {
            string directory = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(directory);
            temporaryPath = Path.Combine(directory, $".window-placement.{Guid.NewGuid():N}.tmp");
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                version = Version,
                x = bounds.X,
                y = bounds.Y,
                width = bounds.Width,
                height = bounds.Height
            });
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, _filePath, overwrite: true);
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
                try { File.Delete(temporaryPath); }
                catch (Exception exception) when (IsExpectedStorageFailure(exception)) { }
            }
        }
    }

    private static bool IsValid(Rectangle bounds) => bounds.Width > 0 && bounds.Height > 0 &&
        (long)bounds.X + bounds.Width <= int.MaxValue && (long)bounds.Y + bounds.Height <= int.MaxValue;

    private static bool IsExpectedStorageFailure(Exception exception) => exception is
        IOException or UnauthorizedAccessException or SecurityException or JsonException or ArgumentException or NotSupportedException;
}
