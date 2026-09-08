using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using RemoteHubStudio.Configuration;

namespace RemoteHubStudio.Infrastructure;

/// <summary>
/// Coordinates one interactive instance and forwards later launches to the first window. / 协调单个交互实例，并将后续启动请求转发给第一个窗口。
/// </summary>
public sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activationEvent;
    private readonly string _processIdMapName;
    private readonly MemoryMappedFile? _processIdMap;
    private RegisteredWaitHandle? _registeredWait;
    private volatile bool _disposed;

    /// <summary>
    /// Creates named synchronization objects for RemoteHubStudio. / 为 RemoteHubStudio 创建命名同步对象。
    /// </summary>
    public SingleInstanceCoordinator()
        : this(ProductInfo.SingleInstanceMutexName, ProductInfo.SingleInstanceActivationEventName)
    {
    }

    internal SingleInstanceCoordinator(string mutexName, string activationEventName)
    {
        _processIdMapName = mutexName + ".ProcessId";
        _mutex = new Mutex(false, mutexName);
        try
        {
            try
            {
                IsPrimaryInstance = _mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                // A crashed owner must not prevent the next launch from taking over.
                // / 原实例崩溃后，下次启动仍能接管互斥体。
                IsPrimaryInstance = true;
            }

            _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, activationEventName);
            if (IsPrimaryInstance)
            {
                _processIdMap = MemoryMappedFile.CreateOrOpen(_processIdMapName, sizeof(int));
                using MemoryMappedViewAccessor view = _processIdMap.CreateViewAccessor();
                view.Write(0, Environment.ProcessId);
            }
        }
        catch
        {
            _activationEvent?.Dispose();
            _processIdMap?.Dispose();
            if (IsPrimaryInstance) _mutex.ReleaseMutex();
            _mutex.Dispose();
            throw;
        }
    }

    /// <summary>Gets whether this process owns the primary application instance. / 获取当前进程是否拥有主应用实例。</summary>
    public bool IsPrimaryInstance { get; }

    /// <summary>Occurs on a worker thread when a later process asks to activate the main window. / 当后续进程请求激活主窗口时在工作线程发生。</summary>
    public event EventHandler? ActivationRequested;

    /// <summary>
    /// Starts listening for activation requests in the primary process. / 在主进程中开始监听激活请求。
    /// </summary>
    public void StartListening()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsPrimaryInstance || _registeredWait is not null)
        {
            return;
        }

        _registeredWait = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            OnActivationSignaled,
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    /// <summary>
    /// Signals the primary instance to restore and activate its main window. / 通知主实例恢复并激活其主窗口。
    /// </summary>
    public void SignalPrimaryInstance()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsPrimaryInstance)
        {
            AllowPrimaryForegroundActivation();
            _activationEvent.Set();
        }
    }

    private void AllowPrimaryForegroundActivation()
    {
        // The primary may still be between acquiring its mutex and publishing its PID.
        // The event remains signaled until listening begins, even during slow startup.
        // / 首实例可能刚取得互斥体，尚未发布 PID；即使启动较慢，事件也会保留至开始监听。
        Stopwatch startupWait = Stopwatch.StartNew();
        do
        {
            try
            {
                using MemoryMappedFile map = MemoryMappedFile.OpenExisting(_processIdMapName, MemoryMappedFileRights.Read);
                using MemoryMappedViewAccessor view = map.CreateViewAccessor(0, sizeof(int), MemoryMappedFileAccess.Read);
                int processId = view.ReadInt32(0);
                if (processId > 0)
                {
                    WindowActivation.AllowSetForegroundWindow(processId);
                    return;
                }
            }
            catch (FileNotFoundException)
            {
            }

            Thread.Sleep(20);
        } while (startupWait.Elapsed < TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Releases named synchronization resources. / 释放命名同步资源。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _registeredWait?.Unregister(null);
        _registeredWait = null;
        _activationEvent.Dispose();
        _processIdMap?.Dispose();
        if (IsPrimaryInstance)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }
        }

        _mutex.Dispose();
    }

    /// <summary>
    /// Publishes a named-event activation request to subscribers. / 将命名事件激活请求发布给订阅者。
    /// </summary>
    /// <param name="state">Unused callback state. / 未使用的回调状态。</param>
    /// <param name="timedOut">Whether the registered wait timed out. / 注册等待是否超时。</param>
    private void OnActivationSignaled(object? state, bool timedOut)
    {
        if (!timedOut && !_disposed)
        {
            ActivationRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
