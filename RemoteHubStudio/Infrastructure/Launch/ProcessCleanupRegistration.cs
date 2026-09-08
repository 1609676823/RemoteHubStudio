using System.Diagnostics;

namespace RemoteHubStudio.Infrastructure.Launch;

/// <summary>
/// Owns process-exit cleanup and handle disposal as one race-free lifetime. / 将进程退出清理与句柄释放作为一个无竞态的生命周期管理。
/// </summary>
internal sealed class ProcessCleanupRegistration
{
    private readonly Process _process;
    private readonly string[] _temporaryFiles;
    private int _cleanupStarted;

    /// <summary>
    /// Initializes and arms cleanup for a started process. / 为已启动进程初始化并启用清理。
    /// </summary>
    /// <param name="process">Started process to observe. / 要监视的已启动进程。</param>
    /// <param name="temporaryFiles">Temporary files to delete. / 要删除的临时文件。</param>
    public ProcessCleanupRegistration(Process process, IEnumerable<string> temporaryFiles)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(temporaryFiles);
        _process = process;
        _temporaryFiles = temporaryFiles.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
        _process.Exited += HandleProcessExited;
        _process.EnableRaisingEvents = true;
    }

    /// <summary>
    /// Handles processes that exited before the observer was fully attached. / 处理在监视器完全挂接前已退出的进程。
    /// </summary>
    public void CleanupIfAlreadyExited()
    {
        try
        {
            if (_process.HasExited)
            {
                BeginCleanup();
            }
        }
        catch (InvalidOperationException)
        {
            BeginCleanup();
        }
    }

    /// <summary>
    /// Handles process exit and starts asynchronous temporary-file cleanup. / 处理进程退出并开始异步清理临时文件。
    /// </summary>
    /// <param name="sender">Process event source. / 进程事件源。</param>
    /// <param name="eventArgs">Exit event arguments. / 退出事件参数。</param>
    private void HandleProcessExited(object? sender, EventArgs eventArgs)
    {
        BeginCleanup();
    }

    /// <summary>
    /// Ensures cleanup begins once and detaches the exit handler. / 确保清理只开始一次并解除退出事件处理器。
    /// </summary>
    private void BeginCleanup()
    {
        if (Interlocked.Exchange(ref _cleanupStarted, 1) != 0)
        {
            return;
        }

        _process.Exited -= HandleProcessExited;
        _ = Task.Run(CleanupAndDisposeAsync);
    }

    /// <summary>
    /// Removes launch artifacts before releasing the only process handle owner. / 在释放唯一的进程句柄所有者前删除启动文件。
    /// </summary>
    /// <returns>A task representing cleanup and handle release. / 表示清理与句柄释放的任务。</returns>
    private async Task CleanupAndDisposeAsync()
    {
        try
        {
            await DeleteFilesWithRetryAsync(_temporaryFiles).ConfigureAwait(false);
        }
        finally
        {
            _process.Dispose();
        }
    }

    /// <summary>
    /// Deletes temporary files with short retries for clients that briefly retain file handles. / 对短暂占用文件句柄的客户端进行少量重试后删除临时文件。
    /// </summary>
    /// <param name="paths">Temporary file paths. / 临时文件路径。</param>
    /// <returns>A task representing cleanup completion. / 表示清理完成的任务。</returns>
    private static async Task DeleteFilesWithRetryAsync(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    break;
                }
                catch (IOException) when (attempt < 4)
                {
                    await Task.Delay(250).ConfigureAwait(false);
                }
                catch (UnauthorizedAccessException) when (attempt < 4)
                {
                    await Task.Delay(250).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    break;
                }
                catch (UnauthorizedAccessException)
                {
                    break;
                }
            }
        }
    }
}
