using System.Runtime.InteropServices;

namespace RemoteHubStudio.Infrastructure;

/// <summary>Uses Windows foreground activation without changing the user's topmost preference. / 使用 Windows 前台激活机制，不改变用户的置顶状态。</summary>
internal static class WindowActivation
{
    // The newly launched foreground process delegates activation to the existing process.
    // https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-allowsetforegroundwindow
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AllowSetForegroundWindow(int processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetLastActivePopup(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    internal static void Activate(IntPtr window)
    {
        // Restore focus to an open settings/editor dialog instead of its disabled owner.
        // / 设置或编辑对话框打开时，将焦点交还该对话框。
        IntPtr popup = GetLastActivePopup(window);
        SetForegroundWindow(popup != IntPtr.Zero && IsWindowVisible(popup) ? popup : window);
    }
}
