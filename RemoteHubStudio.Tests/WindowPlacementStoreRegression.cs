using System.Drawing;
using RemoteHubStudio.Infrastructure.Persistence;

namespace RemoteHubStudio.Tests;

/// <summary>Checks that reset bounds survive new store instances and malformed files never break startup. / 验证重置边界可跨存储实例保留，且损坏文件不影响启动。</summary>
internal static class WindowPlacementStoreRegression
{
    internal static void Run()
    {
        string directory = Path.Combine(Path.GetTempPath(), "RemoteHubStudio.WindowPlacementTests", Guid.NewGuid().ToString("N"));
        AppDataPaths paths = new(directory);
        string filePath = Path.Combine(directory, "window-placement.json");
        WindowPlacementStore store = new(paths);
        try
        {
            Require(store.LoadDefaultBounds() is null, "A missing reset preference must use the startup fallback.");
            Rectangle first = new(-1500, 40, 1280, 800);
            Require(store.SaveDefaultBounds(first), "The reset preference could not be saved.");
            Require(new WindowPlacementStore(paths).LoadDefaultBounds() == first,
                "A new store instance did not restore the exact saved reset coordinates and dimensions.");

            foreach (Rectangle invalid in new Rectangle[] { new(0, 0, 0, 800), new(0, 0, 1280, -1), new(int.MaxValue, 0, 1, 800), new(0, int.MaxValue, 1280, 1) })
            {
                Require(!store.SaveDefaultBounds(invalid), "Invalid or overflowing bounds were accepted.");
                Require(new WindowPlacementStore(paths).LoadDefaultBounds() == first,
                    "An invalid save replaced the previous valid reset preference.");
            }

            Rectangle replacement = new(320, 116, 1280, 800);
            Require(store.SaveDefaultBounds(replacement) && new WindowPlacementStore(paths).LoadDefaultBounds() == replacement,
                "An existing reset preference was not replaced durably.");

            foreach (string malformed in new[]
            {
                "", "{", "null", "[]", "{}",
                "{\"version\":2,\"x\":320,\"y\":116,\"width\":1280,\"height\":800}",
                "{\"version\":1,\"x\":320,\"y\":116,\"width\":0,\"height\":800}",
                "{\"version\":1,\"x\":320,\"y\":116,\"width\":1280,\"height\":-1}",
                "{\"version\":1,\"x\":2147483648,\"y\":116,\"width\":1280,\"height\":800}",
                "{\"version\":1,\"x\":2147483647,\"y\":116,\"width\":1,\"height\":800}",
                "{\"version\":1,\"x\":320,\"y\":2147483647,\"width\":1280,\"height\":1}",
                "{\"version\":1,\"x\":320,\"y\":116,\"width\":\"1280\",\"height\":800}",
                "{\"version\":1,\"x\":320,\"y\":116,\"width\":1280}",
                "{\"version\":1,\"x\":320,\"x\":321,\"y\":116,\"width\":1280,\"height\":800}",
                "{\"version\":1,\"x\":320,\"y\":116,\"width\":1280,\"height\":800,\"other\":1}",
                "{\"version\":1,\"x\":[[[1]]],\"y\":116,\"width\":1280,\"height\":800}"
            })
            {
                File.WriteAllText(filePath, malformed);
                Require(new WindowPlacementStore(paths).LoadDefaultBounds() is null,
                    "A malformed reset preference did not fall back safely.");
            }

            File.WriteAllText(filePath, new string(' ', 4097));
            Require(store.LoadDefaultBounds() is null, "An oversized reset preference was accepted.");
            Require(store.SaveDefaultBounds(first) && new WindowPlacementStore(paths).LoadDefaultBounds() == first,
                "A malformed preference could not be replaced by valid reset bounds.");

            File.Delete(filePath);
            Directory.CreateDirectory(filePath);
            Require(store.LoadDefaultBounds() is null && !store.SaveDefaultBounds(first),
                "An inaccessible preference path must return a non-fatal storage failure.");
            Require(!Directory.EnumerateFiles(directory, "*.tmp").Any(), "A failed save left an atomic-write temporary file behind.");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        Console.WriteLine("WINDOW_PLACEMENT_STORE_OK (cross-instance reset bounds, atomic replacement, malformed/oversized files, storage failure)");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
