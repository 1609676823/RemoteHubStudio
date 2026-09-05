using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using RemoteHubStudio.Configuration;
using RemoteHubStudio.Localization;
using RemoteHubStudio.UI.Dialogs;

namespace RemoteHubStudio.Tests;

/// <summary>Checks product metadata when the application is hosted by another executable. / 检查应用由其他可执行文件承载时的产品元数据。</summary>
internal static class ProductInfoRegression
{
    public static void Run()
    {
        Assembly productAssembly = typeof(ProductInfo).Assembly;
        Require(Assembly.GetEntryAssembly() != productAssembly, "This regression must run from a separate host assembly.");
        Require(ProductInfo.Name != Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyProductAttribute>()?.Product,
            "Product information was read from the test host.");

        VerifyValue(ProductInfo.Name, productAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product, "Product");
        VerifyValue(ProductInfo.Publisher, productAssembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company, "Company");
        VerifyValue(ProductInfo.Description, productAssembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description, "Description");
        VerifyValue(ProductInfo.Copyright, productAssembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright, "Copyright");
        VerifyValue(ProductInfo.InformationalVersion,
            productAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, "InformationalVersion");
        int buildMetadataStart = ProductInfo.InformationalVersion.IndexOf('+');
        string expectedVersion = buildMetadataStart < 0
            ? ProductInfo.InformationalVersion
            : ProductInfo.InformationalVersion[..buildMetadataStart];
        Require(ProductInfo.Version == expectedVersion, "The display version must retain prerelease labels and omit build metadata.");

        AssemblyMetadataAttribute[] metadata = productAssembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToArray();
        (string Key, string Value)[] values =
        [
            ("Authors", ProductInfo.Authors),
            ("RepositoryUrl", ProductInfo.RepositoryUrl),
            ("ProjectUrl", ProductInfo.ProjectUrl),
            ("IssuesUrl", ProductInfo.IssuesUrl),
            ("ReleasesUrl", ProductInfo.ReleasesUrl),
            ("License", ProductInfo.License),
            ("LicenseUrl", ProductInfo.LicenseUrl)
        ];
        foreach ((string key, string value) in values)
        {
            AssemblyMetadataAttribute[] matches = metadata.Where(attribute => attribute.Key == key).ToArray();
            Require(matches.Length == 1, $"Expected exactly one generated {key} attribute.");
            VerifyValue(value, matches[0].Value, key);
        }

        VerifyAboutDialog();
    }

    private static void VerifyAboutDialog()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            string originalLanguage = L.RequestedLanguage;
            try
            {
                foreach (string language in new[] { "en", "zh-Hans" })
                {
                    L.SetLanguage(language);
                    using AboutForm preview = new();
                    Require(ReadControl(preview, "_projectLinkButton").Tag is null,
                        "The designer preview must not contain a hard-coded product URL.");
                    using AboutForm runtime = (AboutForm)Activator.CreateInstance(
                        typeof(AboutForm), BindingFlags.Instance | BindingFlags.NonPublic,
                        binder: null, args: [true], culture: null)!;
                    Require(runtime.Text == L.Format("About.WindowTitle", ProductInfo.Name), "The About title used stale product metadata.");
                    Require(ReadControl(runtime, "_productNameLabel").Text == ProductInfo.Name, "The About product name used host metadata.");
                    Require(ReadControl(runtime, "_detailsLabel").Text == L.Format("About.ProductDetails",
                        ProductInfo.Version, ProductInfo.Publisher, ProductInfo.License, ProductInfo.Copyright),
                        "The About details were not populated from the built product metadata.");
                    Require(Equals(ReadControl(runtime, "_projectLinkButton").Tag, ProductInfo.ProjectUrl) &&
                        Equals(ReadControl(runtime, "_issuesLinkButton").Tag, ProductInfo.IssuesUrl) &&
                        Equals(ReadControl(runtime, "_releasesLinkButton").Tag, ProductInfo.ReleasesUrl) &&
                        Equals(ReadControl(runtime, "_licenseLinkButton").Tag, ProductInfo.LicenseUrl),
                        "The About links were not populated from the built product metadata.");
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                L.SetLanguage(originalLanguage);
            }
        }) { IsBackground = true, Name = "Product metadata regression" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Require(thread.Join(TimeSpan.FromSeconds(30)), "Product metadata regression timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static Control ReadControl(AboutForm form, string name) =>
        (Control)typeof(AboutForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;

    private static void VerifyValue(string actual, string? expected, string name) =>
        Require(!string.IsNullOrWhiteSpace(expected) && actual == expected, $"Missing or inconsistent generated {name} metadata.");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
