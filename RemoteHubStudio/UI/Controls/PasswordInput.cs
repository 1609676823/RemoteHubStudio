using System.ComponentModel;
using System.Windows.Forms;

namespace RemoteHubStudio.UI.Controls;

/// <summary>
/// Provides an AntdUI password input with a built-in reveal button. / 提供带内置显示按钮的 AntdUI 密码输入框。
/// </summary>
public sealed class PasswordInput : AntdUI.Input
{
    /// <summary>
    /// Initializes a concealed password input and wires the suffix action. / 初始化隐藏密码的输入框并绑定后缀操作。
    /// </summary>
    public PasswordInput()
    {
        UseSystemPasswordChar = true;
        PasswordCopy = false;
        PasswordPaste = true;
        SuffixSvg = "EyeOutlined";
        SuffixClick += HandleSuffixClick;
    }

    /// <summary>
    /// Gets or sets whether the password is currently visible. / 获取或设置密码当前是否可见。
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool PasswordVisible
    {
        get => !UseSystemPasswordChar;
        set => SetPasswordVisibility(value);
    }

    /// <summary>
    /// Applies the requested password visibility and matching icon. / 应用指定的密码可见性及对应图标。
    /// </summary>
    /// <param name="visible">Whether plain text should be shown. / 是否显示明文。</param>
    private void SetPasswordVisibility(bool visible)
    {
        UseSystemPasswordChar = !visible;
        SuffixSvg = visible ? "EyeInvisibleOutlined" : "EyeOutlined";
    }

    /// <summary>
    /// Toggles password visibility when the suffix icon is clicked. / 单击后缀图标时切换密码可见性。
    /// </summary>
    /// <param name="sender">Event sender. / 事件发送者。</param>
    /// <param name="e">Mouse event data. / 鼠标事件数据。</param>
    private void HandleSuffixClick(object? sender, MouseEventArgs e)
    {
        SetPasswordVisibility(!PasswordVisible);
    }
}
