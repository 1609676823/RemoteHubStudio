using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using RemoteHubStudio.Configuration;
using RemoteHubStudio.Localization;
using RemoteHubStudio.UI.Controls;

namespace RemoteHubStudio.UI.Dialogs;

/// <summary>
/// Displays product metadata and standard open-source links. / 显示产品元数据与标准开源链接。
/// </summary>
[DesignerCategory("Form")]
public sealed partial class AboutForm : ResponsiveDialogWindow
{
    /// <summary>
    /// Initializes the designer preview with metadata placeholders. / 使用元数据占位文本初始化设计器预览。
    /// </summary>
    public AboutForm()
    {
        InitializeComponent();
        L.Apply(this);
        RegisterSection(_identityLayout, _identitySection);
        RegisterSection(_linksFlow, _linksSection);
    }

    /// <summary>
    /// Initializes the runtime product metadata after the designer-only constructor has
    /// built the static visual tree. / 在仅供设计器使用的构造函数建立静态可视树后初始化运行时产品信息。
    /// </summary>
    /// <param name="initializeProductInformation">Whether to load product metadata. / 是否加载产品信息。</param>
    internal AboutForm(bool initializeProductInformation)
        : this()
    {
        if (initializeProductInformation)
        {
            ApplyProductInformation();
        }
    }

    /// <summary>
    /// Applies assembly metadata and public links without placing runtime expressions in designer code. / 应用程序集元数据与公开链接，避免在设计器代码中放入运行时表达式。
    /// </summary>
    private void ApplyProductInformation()
    {
        Text = L.Format("About.WindowTitle", ProductInfo.Name);
        Header.Text = Text;
        _productNameLabel.Text = ProductInfo.Name;
        _detailsLabel.Text = L.Format(
            "About.ProductDetails",
            ProductInfo.Version,
            ProductInfo.Publisher,
            ProductInfo.License,
            ProductInfo.Copyright);
        _projectLinkButton.Tag = ProductInfo.ProjectUrl;
        _issuesLinkButton.Tag = ProductInfo.IssuesUrl;
        _releasesLinkButton.Tag = ProductInfo.ReleasesUrl;
        _licenseLinkButton.Tag = ProductInfo.LicenseUrl;
    }

    /// <summary>
    /// Opens the tagged public URL in the user's default browser. / 在用户默认浏览器中打开已标记的公开 URL。
    /// </summary>
    /// <param name="sender">Event sender. / 事件发送者。</param>
    /// <param name="e">Event data. / 事件数据。</param>
    private void HandleOpenLinkClick(object? sender, EventArgs e)
    {
        if (sender is not Control { Tag: string url } || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            AntdUI.Message.error(this, L.Format("About.OpenLinkFailed", exception.Message));
        }
    }

    /// <summary>
    /// Closes the about dialog. / 关闭关于对话框。
    /// </summary>
    /// <param name="sender">Event sender. / 事件发送者。</param>
    /// <param name="e">Event data. / 事件数据。</param>
    private void HandleCloseClick(object? sender, EventArgs e)
    {
        CompleteDialog(DialogResult.OK);
    }
}
