using System.Globalization;
using System.Text;
using RemoteHubStudio.Infrastructure.Persistence;
using RemoteHubStudio.Localization;

namespace RemoteHubStudio.Tests;

/// <summary>
/// Provides directly callable regression checks for language-pack loading, fallback, validation, and preference persistence. / 为语言包加载、回退、验证与偏好持久化提供可直接调用的回归检查。
/// </summary>
internal static class LocalizationRegression
{
    /// <summary>Runs all localization regressions without requiring a test framework. / 在不需要测试框架的情况下运行全部本地化回归检查。</summary>
    public static void Run()
    {
        using TemporaryDirectoryScope scope = new();
        AppDataPaths paths = new(scope.Path);
        Directory.CreateDirectory(paths.LanguagesDirectory);

        CultureInfo originalUiCulture = Thread.CurrentThread.CurrentUICulture;
        CultureInfo? originalDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            VerifyEmbeddedPacksAndScriptFallback(paths);
            VerifyExternalPackAndParentFallback(paths);
            VerifyConcurrentSelectionCoherence();
            VerifyInvalidPacksAreContained(paths);
            VerifyHighestPriorityDataOverride(paths);
            VerifyPreferenceStore(paths);
        }
        finally
        {
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
            CultureInfo.DefaultThreadCurrentUICulture = originalDefaultUiCulture;
            L.Initialize(L.SystemLanguage, new AppDataPaths());
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
            CultureInfo.DefaultThreadCurrentUICulture = originalDefaultUiCulture;
            try
            {
                AntdUI.Localization.SetLanguage(originalUiCulture.Name);
            }
            catch
            {
                // The regression must restore process culture even when this AntdUI build lacks that locale.
            }
        }
    }

    private static void VerifyEmbeddedPacksAndScriptFallback(AppDataPaths paths)
    {
        L.Initialize(L.SystemLanguage, paths);
        string expectedSystemLanguage = L.CurrentLanguage;

        L.Initialize("en", paths);
        Assert(
            L.AvailableLanguages.Any(language => language.Code.Equals("en", StringComparison.OrdinalIgnoreCase)) &&
            L.AvailableLanguages.Any(language => language.Code.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase)),
            "Embedded English and Simplified Chinese packs were not discovered. / 未发现内嵌英文与简体中文语言包。");
        string englishCancel = L.Get("Common.Cancel");

        L.Initialize("zh-CN", paths);
        Assert(L.Requested == "zh-CN" && L.RequestedLanguage == L.Requested,
            "Requested locale normalization was not retained. / 未保留规范化的请求区域标识。");
        Assert(L.Current == "zh-Hans" && L.CurrentLanguage == L.Current,
            "zh-CN did not fall back to the embedded zh-Hans script pack. / zh-CN 未回退到内嵌 zh-Hans 脚本语言包。");
        Assert(L.Get("Common.Cancel") != englishCancel,
            "Simplified Chinese lookup unexpectedly returned the English value. / 简体中文查找意外返回了英文值。");
        Assert(L.Get("Regression.Missing.Key") == "Regression.Missing.Key",
            "A missing key did not remain visible for diagnostics. / 缺失键未保持可见以便诊断。");

        L.Initialize("en", paths);
        L.Initialize(L.SystemLanguage, paths);
        Assert(
            L.CurrentLanguage == expectedSystemLanguage,
            "Reloading after an explicit language mistook the app-selected UI culture for the host system language. / 显式选择语言后重新加载时错误地把应用 UI 区域当成了宿主系统语言。");
    }

    private static void VerifyExternalPackAndParentFallback(AppDataPaths paths)
    {
        WritePack(
            paths,
            "fr.json",
            """
            {
              "schemaVersion": 1,
              "locale": "fr",
              "name": "French",
              "nativeName": "Français",
              "authors": ["Regression"],
              "strings": {
                "Common.Cancel": "Annuler",
                "Localization.ItemsProcessed": "{0} élément(s) traité(s)."
              }
            }
            """);

        L.Initialize("fr-CA", paths);
        Assert(L.Requested == "fr-CA" && L.Current == "fr",
            "A regional locale did not fall back to its available neutral parent. / 区域语言未回退到可用的中性父语言。");
        Assert(L.Get("Common.Cancel") == "Annuler",
            "The external language pack was not loaded from data/Languages. / 未从 data/Languages 加载外置语言包。");
        Assert(L.Format("Localization.ItemsProcessed", 7).Contains('7'),
            "A valid translated placeholder failed to format. / 有效翻译占位符格式化失败。");
    }

    private static void VerifyInvalidPacksAreContained(AppDataPaths paths)
    {
        WritePack(
            paths,
            "de.json",
            """
            { "schemaVersion": 1, "locale": "es", "name": "German", "nativeName": "Deutsch", "strings": {} }
            """);
        WritePack(
            paths,
            "it.json",
            """
            {
              "schemaVersion": 1,
              "locale": "it",
              "name": "Italian",
              "nativeName": "Italiano",
              "strings": { "Common.Cancel": "Annulla", "Common.Cancel": "Duplicato" }
            }
            """);
        WritePack(
            paths,
            "ja.json",
            """
            {
              "schemaVersion": 1,
              "locale": "ja",
              "name": "Japanese",
              "nativeName": "日本語",
              "strings": { "Localization.ItemsProcessed": "{1} 件を処理しました。" }
            }
            """);

        string oversized =
            "{\"schemaVersion\":1,\"locale\":\"ko\",\"name\":\"Korean\",\"nativeName\":\"Korean\",\"strings\":{\"Common.Cancel\":\"" +
            new string('x', 513 * 1024) +
            "\"}}";
        WritePack(paths, "ko.json", oversized);

        L.Initialize("ja", paths);
        Assert(!L.AvailableLanguages.Any(language => language.Code is "de" or "es" or "it" or "ko"),
            "A locale mismatch, duplicate key, or oversized pack escaped validation. / locale 不匹配、重复键或超限语言包逃过了验证。");
        Assert(L.Current == "ja",
            "A structurally valid partial pack was not retained. / 结构有效的部分语言包未被保留。");

        L.Initialize("en", paths);
        string englishFormat = L.Get("Localization.ItemsProcessed");
        L.SetLanguage("ja");
        Assert(L.Get("Localization.ItemsProcessed") == englishFormat,
            "A placeholder-incompatible translation did not fall back to English. / 占位符不兼容的翻译未回退到英文。");
    }

    private static void VerifyConcurrentSelectionCoherence()
    {
        Parallel.For(
            0,
            32,
            index => L.SetLanguage(index % 2 == 0 ? "en" : "zh-CN"));

        Assert(
            CultureInfo.DefaultThreadCurrentUICulture?.Name == L.CurrentLanguage &&
            AntdUI.Localization.CurrentLanguage == L.CurrentLanguage,
            "Concurrent language selection left the catalog and process UI cultures out of sync. / 并发语言选择导致语言目录与进程 UI 区域状态不一致。");
    }

    private static void VerifyHighestPriorityDataOverride(AppDataPaths paths)
    {
        WritePack(
            paths,
            "en.json",
            """
            {
              "schemaVersion": 1,
              "locale": "en",
              "name": "English (data override)",
              "nativeName": "English",
              "strings": { "Common.Cancel": "Data override cancel" }
            }
            """);

        L.Initialize("en", paths);
        Assert(L.Get("Common.Cancel") == "Data override cancel",
            "The data/Languages layer did not override the bundled/program layer. / data/Languages 层未覆盖内置/程序目录层。");
    }

    private static void VerifyPreferenceStore(AppDataPaths paths)
    {
        LanguagePreferenceStore store = new(paths);
        Assert(store.FilePath == paths.LanguagePreferenceFilePath,
            "Language preference storage is not connected to AppDataPaths. / 语言偏好存储未接入 AppDataPaths。");
        Assert(store.Save("zh-cn") && store.Load() == "zh-CN",
            "A valid locale was not atomically saved and canonicalized. / 有效区域标识未被原子保存并规范化。");
        Assert(!Directory.EnumerateFiles(paths.DataDirectory, $".{AppDataPaths.LanguagePreferenceFileName}.*.tmp").Any(),
            "An atomic preference temporary file was left behind. / 原子偏好写入遗留了临时文件。");
        Assert(!store.Save("not_a_bcp47_locale") && store.Load() == "zh-CN",
            "An invalid locale replaced the last valid preference. / 无效区域标识覆盖了上一个有效偏好。");

        File.WriteAllText(
            store.FilePath,
            "{\"schemaVersion\":1,\"language\":\"en\",\"language\":\"zh-CN\"}",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Assert(store.Load() is null,
            "A duplicate preference property escaped validation. / 重复偏好属性逃过了验证。");
    }

    private static void WritePack(AppDataPaths paths, string fileName, string contents)
    {
        File.WriteAllText(
            Path.Combine(paths.LanguagesDirectory, fileName),
            contents,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class TemporaryDirectoryScope : IDisposable
    {
        private readonly string _testRoot;

        internal TemporaryDirectoryScope()
        {
            _testRoot = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RemoteHubStudio.Localization.Tests"));
            Path = System.IO.Path.Combine(_testRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            string fullPath = System.IO.Path.GetFullPath(Path);
            string requiredPrefix = _testRoot.TrimEnd(System.IO.Path.DirectorySeparatorChar) +
                                    System.IO.Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
            }
        }
    }
}
