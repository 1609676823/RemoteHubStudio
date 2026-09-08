using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RemoteHubStudio.Infrastructure.Launch;

/// <summary>Checks current desktop-client responsiveness rather than its first input-idle transition. / 检查桌面客户端的当前响应状态，而非首次消息循环空闲状态。</summary>
internal static class ToDeskClientProbe
{
    private const uint WmNull = 0;
    private const uint HeartbeatTimeoutMilliseconds = 200;
    private const uint SmtoBlock = 0x0001;
    private const uint SmtoAbortIfHung = 0x0002;
    private const uint SmtoErrorOnExit = 0x0020;

    /// <summary>Finds this installation's desktop process in the current Windows session. / 查找当前 Windows 会话中属于此安装路径的桌面进程。</summary>
    internal static ToDeskClientState? Inspect(string executablePath)
    {
        string fullPath = Path.GetFullPath(executablePath);
        using Process currentProcess = Process.GetCurrentProcess();
        int sessionId = currentProcess.SessionId;
        Process[] processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(fullPath));
        ToDeskClientState? initializingClient = null;
        try
        {
            foreach (Process process in processes)
            {
                try
                {
                    if (process.HasExited || process.SessionId != sessionId ||
                        !string.Equals(process.MainModule?.FileName, fullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    TimeSpan runningFor = DateTime.UtcNow - process.StartTime.ToUniversalTime();
                    bool isReady = HasResponsiveWindow(process.Id, out ulong windowSignature) && !process.HasExited;
                    ToDeskClientState state = new(process.Id, isReady, runningFor, windowSignature);
                    if (isReady)
                    {
                        return state;
                    }

                    initializingClient ??= state;
                }
                catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // The client may exit or hand off to another process during inspection.
                    // / 客户端可能在检查期间退出或交接到另一个进程。
                }
            }
        }
        finally
        {
            foreach (Process process in processes)
            {
                process.Dispose();
            }
        }

        return initializingClient;
    }

    /// <summary>Requires a fresh response from every window thread, including hidden tray windows. / 要求每个窗口线程实时响应，包括隐藏的托盘窗口。</summary>
    internal static bool HasResponsiveWindow(int processId)
        => HasResponsiveWindow(processId, out _);

    private static bool HasResponsiveWindow(int processId, out ulong windowSignature)
    {
        Dictionary<uint, nint> windowsByThread = [];
        List<nint> windows = [];
        windowSignature = 0;
        bool enumerated = EnumWindows((window, _) =>
        {
            uint threadId = GetWindowThreadProcessId(window, out uint ownerId);
            if (ownerId == (uint)processId && threadId != 0)
            {
                windowsByThread.TryAdd(threadId, window);
                windows.Add(window);
            }

            return true;
        }, nint.Zero);

        if (!enumerated || windowsByThread.Count == 0)
        {
            return false;
        }

        foreach ((uint threadId, nint window) in windowsByThread)
        {
            // A responsive helper thread must not conceal a stalled main UI thread.
            // / 辅助线程响应正常不能掩盖主界面线程卡顿。
            if (GetWindowThreadProcessId(window, out uint ownerId) != threadId || ownerId != (uint)processId ||
                SendMessageTimeout(window, WmNull, 0, 0, SmtoBlock | SmtoAbortIfHung | SmtoErrorOnExit,
                    HeartbeatTimeoutMilliseconds, out _) == nint.Zero)
            {
                return false;
            }
        }

        // Restart stabilization when a splash screen is replaced or a new UI window appears in the same process.
        // / 同一进程中的启动画面被替换或出现新界面窗口时，也重新等待稳定。
        ulong signature = 14695981039346656037UL;
        foreach (nint window in windows.OrderBy(window => window.ToInt64()))
        {
            signature = unchecked((signature ^ (ulong)window.ToInt64()) * 1099511628211UL);
        }
        windowSignature = signature;
        return true;
    }

    private delegate bool EnumWindowsCallback(nint window, nint parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint SendMessageTimeout(nint window, uint message, nuint wParam, nint lParam,
        uint flags, uint timeoutMilliseconds, out nuint result);
}
