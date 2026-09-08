using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Time.Testing;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Infrastructure.Launch;

namespace RemoteHubStudio.Tests;

/// <summary>Exercises bounded, cancellable launches without contacting a remote client. / 不访问远端客户端，验证有时限且可取消的启动流程。</summary>
internal static class ConnectionLaunchCancellationRegression
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromMilliseconds(350);

    /// <summary>Runs a harmless child until its owning test releases it. / 运行无害子进程，直到所属测试释放它。</summary>
    internal static int RunHoldProbe(string[] args)
    {
        using EventWaitHandle ready = EventWaitHandle.OpenExisting(args[1] + ".Ready");
        using EventWaitHandle exit = EventWaitHandle.OpenExisting(args[1] + ".Exit");
        ready.Set();
        return exit.WaitOne(TimeSpan.FromSeconds(15)) ? 0 : 2;
    }

    /// <summary>Runs cancellation, timeout, late completion, and configuration regressions. / 运行取消、超时、延迟完成及配置回归测试。</summary>
    internal static async Task RunAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "RemoteHubStudio.LaunchCancellationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            TestEnvironmentParsing();
            await TestEveryClientCancellationAsync(directory);
            await TestCancellationBeforeLinkedPropagationAsync(directory);
            await TestTimeoutIncludesPlanCreationAsync(directory);
            await TestEnvironmentIsReadAtConstructionAsync(directory);
            await TestPrecancelledStartCleansPlanAsync(directory);
            await TestStalledToDeskProbeAsync(directory, cancel: true);
            await TestStalledToDeskProbeAsync(directory, cancel: false);
            await TestQueuedToDeskDeadlineAsync(directory);
            await TestLateNativeStartAsync(directory, cancel: true);
            await TestLateNativeStartAsync(directory, cancel: false);
            await TestCompletedLaunchSurvivesCancellationAsync(directory);
            Console.WriteLine("CONNECTION_LAUNCH_CANCELLATION_OK (all clients, blocked planning/probing/start, deadlines, late cleanup, environment)");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void TestEnvironmentParsing()
    {
        Require(ConnectionLaunchOptions.EnvironmentVariableName == "REMOTEHUBSTUDIO_CONNECTION_TIMEOUT_SECONDS",
            "The documented connection timeout variable changed unexpectedly.");
        foreach ((string? value, int seconds) in new (string?, int)[]
        {
            (null, 15), (string.Empty, 15), (" ", 15), ("invalid", 15),
            ("0", 15), ("-1", 15), ("3601", 15), ("999999999999", 15), ("1.5", 15),
            ("1", 1), ("120", 120), ("3600", 3600)
        })
        {
            ConnectionLaunchOptions options = ConnectionLaunchOptions.FromEnvironment(name =>
            {
                Require(name == ConnectionLaunchOptions.EnvironmentVariableName, "Timeout parsing requested the wrong environment variable.");
                return value;
            });
            Require(options.Timeout == TimeSpan.FromSeconds(seconds), $"Unexpected timeout for environment value '{value ?? "<missing>"}'.");
        }
    }

    private static async Task TestEveryClientCancellationAsync(string directory)
    {
        foreach (ConnectionType type in Enum.GetValues<ConnectionType>())
        {
            using BlockingStage stage = new();
            using CancellationTokenSource cancellation = new();
            int dispatches = 0;
            string artifact = Path.Combine(directory, $"cancel-{type}.tmp");
            ConnectionLaunchService service = new(directory, ReadyStartup(), TestTimeout,
                startProcess: _ =>
                {
                    Interlocked.Increment(ref dispatches);
                    throw new InvalidOperationException("A canceled client must never dispatch a command.");
                },
                planFactory: (profile, _) => stage.Run(() =>
                {
                    Require(profile.Type == type, "The requested connection type changed during planning.");
                    File.WriteAllText(artifact, "temporary connection material");
                    return new LaunchPlan(Environment.ProcessPath!, [], [artifact], requiresToDeskStartup: type == ConnectionType.ToDesk);
                }));
            Task<Process>? launch = null;
            try
            {
                Stopwatch call = Stopwatch.StartNew();
                launch = service.LaunchAsync(new ConnectionProfile { Type = type }, new AppSettings(), cancellation.Token);
                Require(call.Elapsed < TimeSpan.FromSeconds(1), $"{type} planning blocked the calling thread.");
                await stage.Entered.Task.WaitAsync(TestTimeout);
                cancellation.Cancel();
                await RequireCanceledAsync(launch, cancellation.Token);
                Require(!stage.Returned.Task.IsCompleted, $"{type} cancellation waited for the blocked planner.");
            }
            finally
            {
                stage.Release();
                await stage.Returned.Task.WaitAsync(TestTimeout);
                if (launch is not null) await ObserveAsync(launch);
            }

            await WaitUntilAsync(() => !File.Exists(artifact), $"{type} retained a plan created after cancellation.");
            Require(Volatile.Read(ref dispatches) == 0, $"{type} sent a connection command after cancellation.");
        }
    }

    private static async Task TestTimeoutIncludesPlanCreationAsync(string directory)
    {
        using BlockingStage stage = new();
        FakeTimeProvider clock = new();
        int dispatches = 0;
        string artifact = Path.Combine(directory, "timed-out-plan.tmp");
        ConnectionLaunchService service = new(directory, ReadyStartup(), LaunchTimeout,
            startProcess: _ =>
            {
                Interlocked.Increment(ref dispatches);
                throw new InvalidOperationException("An expired launch must not dispatch.");
            },
            planFactory: (_, _) => stage.Run(() =>
            {
                File.WriteAllText(artifact, "late temporary material");
                return new LaunchPlan(Environment.ProcessPath!, [], [artifact]);
            }),
            timeProvider: clock);
        Task<Process> launch = service.LaunchAsync(new ConnectionProfile(), new AppSettings());
        try
        {
            await stage.Entered.Task.WaitAsync(TestTimeout);
            // Observe the intended stalled stage before advancing its deadline. A short wall-clock
            // timeout can fire while CI is still scheduling the worker, leaving Entered unset forever.
            // / 先确认进入目标阻塞阶段，再推进截止时间，避免 CI 尚在调度工作线程时就超时，永远无法进入该阶段。
            clock.Advance(LaunchTimeout - TimeSpan.FromMilliseconds(1));
            Require(!launch.IsCompleted, "Plan creation expired before its configured deadline.");
            clock.Advance(TimeSpan.FromMilliseconds(1));
            await RequireTimeoutAsync(launch);
            Require(!stage.Returned.Task.IsCompleted, "The overall deadline waited for blocked plan creation.");
        }
        finally
        {
            stage.Release();
            await stage.Returned.Task.WaitAsync(TestTimeout);
            await ObserveAsync(launch);
        }
        await WaitUntilAsync(() => !File.Exists(artifact), "A plan created after its deadline retained temporary files.");
        Require(Volatile.Read(ref dispatches) == 0, "Planning resumed after timeout and sent a connection command.");
    }

    private static async Task TestCancellationBeforeLinkedPropagationAsync(string directory)
    {
        using BlockingStage planner = new();
        using BlockingStage callback = new();
        using CancellationTokenSource cancellation = new();
        int dispatches = 0;
        string artifact = Path.Combine(directory, "cancellation-propagation-gap.tmp");
        ConnectionLaunchService service = new(directory, ReadyStartup(), TestTimeout,
            startProcess: _ =>
            {
                Interlocked.Increment(ref dispatches);
                throw new InvalidOperationException("A launch must inspect caller cancellation before dispatching.");
            },
            planFactory: (_, _) => planner.Run(() =>
            {
                File.WriteAllText(artifact, "temporary connection material");
                return new LaunchPlan(Environment.ProcessPath!, [], [artifact]);
            }));
        Task<Process> launch = service.LaunchAsync(new ConnectionProfile(), new AppSettings(), cancellation.Token);
        CancellationTokenRegistration blocker = default;
        Task? cancellationTask = null;
        try
        {
            await planner.Entered.Task.WaitAsync(TestTimeout);
            // Registrations run in reverse order. Hold the newer caller callback so CancelAsync has
            // marked the caller token, but cannot yet propagate cancellation to the launch's linked token.
            // / 回调按逆序执行：阻塞后注册的回调，使原始令牌已取消，但尚未传播至连接的关联令牌。
            blocker = cancellation.Token.Register(() => callback.Run(() => true));
            cancellationTask = cancellation.CancelAsync();
            await callback.Entered.Task.WaitAsync(TestTimeout);
            Require(cancellation.IsCancellationRequested && !cancellationTask.IsCompleted,
                "The test did not hold cancellation between the original and linked token callbacks.");

            planner.Release();
            await planner.Returned.Task.WaitAsync(TestTimeout);
            await RequireCanceledAsync(launch, cancellation.Token);
            Require(!callback.Returned.Task.IsCompleted,
                "Launch cancellation waited for an unrelated caller-token callback to finish.");
            await WaitUntilAsync(() => !File.Exists(artifact),
                "Cancellation before linked-token propagation retained temporary connection material.");
            Require(Volatile.Read(ref dispatches) == 0,
                "A command was dispatched after caller cancellation while linked-token propagation was delayed.");
        }
        finally
        {
            planner.Release();
            callback.Release();
            await planner.Returned.Task.WaitAsync(TestTimeout);
            if (cancellationTask is not null) await cancellationTask.WaitAsync(TestTimeout);
            await blocker.DisposeAsync();
            await ObserveAsync(launch);
        }
    }

    private static async Task TestEnvironmentIsReadAtConstructionAsync(string directory)
    {
        using BlockingStage stage = new();
        FakeTimeProvider clock = new();
        string variable = ConnectionLaunchOptions.EnvironmentVariableName;
        string? original = Environment.GetEnvironmentVariable(variable);
        ConnectionLaunchService service;
        try
        {
            Environment.SetEnvironmentVariable(variable, "1");
            service = new ConnectionLaunchService(directory, ReadyStartup(),
                planFactory: (_, _) => stage.Run(() => new LaunchPlan(Environment.ProcessPath!, [])),
                startProcess: _ => throw new InvalidOperationException("An expired environment-configured launch must not dispatch."),
                timeProvider: clock);
            Environment.SetEnvironmentVariable(variable, "3600");
            Task<Process> launch = service.LaunchAsync(new ConnectionProfile(), new AppSettings());
            try
            {
                await stage.Entered.Task.WaitAsync(TestTimeout);
                clock.Advance(TimeSpan.FromMilliseconds(999));
                Require(!launch.IsCompleted, "The environment-configured launch expired too early.");
                clock.Advance(TimeSpan.FromMilliseconds(1));
                await RequireTimeoutAsync(launch);
                Require(!stage.Returned.Task.IsCompleted, "A service reread the timeout after construction.");
            }
            finally
            {
                stage.Release();
                await stage.Returned.Task.WaitAsync(TestTimeout);
                await ObserveAsync(launch);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, original);
        }
    }

    private static async Task TestPrecancelledStartCleansPlanAsync(string directory)
    {
        string artifact = Path.Combine(directory, "pre-canceled.tmp");
        File.WriteAllText(artifact, "unused temporary material");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        ConnectionLaunchService service = new(directory, ReadyStartup(), TestTimeout,
            startProcess: _ => throw new InvalidOperationException("A pre-canceled plan must not start."));
        try
        {
            await service.StartAsync(new LaunchPlan(Environment.ProcessPath!, [], [artifact]), cancellation.Token);
            throw new InvalidOperationException("A pre-canceled launch unexpectedly succeeded.");
        }
        catch (OperationCanceledException exception)
        {
            Require(exception.CancellationToken == cancellation.Token, "Pre-cancellation lost the caller's cancellation token.");
        }
        await WaitUntilAsync(() => !File.Exists(artifact), "A pre-canceled launch retained temporary material.");
    }

    private static async Task TestStalledToDeskProbeAsync(string directory, bool cancel)
    {
        using BlockingStage stage = new();
        using CancellationTokenSource cancellation = new();
        FakeTimeProvider clock = new();
        int dispatches = 0;
        ToDeskClientStartup startup = new(
            _ => stage.Run(() => new ToDeskClientState(123, true)),
            _ => throw new InvalidOperationException("The fake existing ToDesk must not be restarted."),
            TestTimeout, TimeSpan.FromMilliseconds(5), TimeSpan.Zero);
        ConnectionLaunchService service = new(directory, startup, cancel ? TestTimeout : LaunchTimeout,
            startProcess: _ =>
            {
                Interlocked.Increment(ref dispatches);
                throw new InvalidOperationException("A canceled or timed-out probe must not dispatch.");
            },
            timeProvider: clock);
        string artifact = Path.Combine(directory, $"probe-{cancel}.tmp");
        File.WriteAllText(artifact, "temporary connection material");
        Task<Process> launch = service.StartAsync(new LaunchPlan(Environment.ProcessPath!, [], [artifact], requiresToDeskStartup: true), cancellation.Token);
        try
        {
            await stage.Entered.Task.WaitAsync(TestTimeout);
            if (cancel)
            {
                cancellation.Cancel();
                await RequireCanceledAsync(launch, cancellation.Token);
            }
            else
            {
                clock.Advance(LaunchTimeout);
                await RequireTimeoutAsync(launch);
            }
            Require(!stage.Returned.Task.IsCompleted, "A frozen ToDesk probe held the caller after stop/timeout.");
        }
        finally
        {
            stage.Release();
            await stage.Returned.Task.WaitAsync(TestTimeout);
            await ObserveAsync(launch);
        }
        await WaitUntilAsync(() => !File.Exists(artifact), "An interrupted ToDesk preparation retained temporary files.");
        Require(Volatile.Read(ref dispatches) == 0, "A formerly stalled ToDesk probe sent a command after stop/timeout.");
    }

    private static async Task TestQueuedToDeskDeadlineAsync(string directory)
    {
        using BlockingStage stage = new();
        using CancellationTokenSource firstCancellation = new();
        ToDeskClientStartup startup = new(
            _ => stage.Run(() => new ToDeskClientState(123, true)),
            _ => throw new InvalidOperationException("The fake existing ToDesk must not be restarted."),
            TestTimeout, TimeSpan.FromMilliseconds(5), TimeSpan.Zero);
        ConnectionLaunchService firstService = new(directory, startup, TestTimeout);
        // Keep one real-clock deadline check: expiry must also work if the queued worker has not
        // started yet, so this case deliberately does not wait for that worker to enter a stage.
        // / 保留真实时钟验证：排队任务即使尚未启动也必须超时，因此本用例不要求它先进入某个阶段。
        ConnectionLaunchService queuedService = new(directory, startup, LaunchTimeout,
            startProcess: _ => throw new InvalidOperationException("A queued launch must not outlive its deadline."));
        LaunchPlan plan = new(Environment.ProcessPath!, [], requiresToDeskStartup: true);
        Task<Process> first = firstService.StartAsync(plan, firstCancellation.Token);
        try
        {
            await stage.Entered.Task.WaitAsync(TestTimeout);
            await RequireTimeoutAsync(queuedService.StartAsync(plan));
            Require(!stage.Returned.Task.IsCompleted, "Queued timeout waited for another request's startup gate.");
            firstCancellation.Cancel();
            await RequireCanceledAsync(first, firstCancellation.Token);
        }
        finally
        {
            firstCancellation.Cancel();
            stage.Release();
            await stage.Returned.Task.WaitAsync(TestTimeout);
            await ObserveAsync(first);
        }
    }

    private static async Task TestLateNativeStartAsync(string directory, bool cancel)
    {
        using HoldProbe unrelated = new();
        using Process unrelatedHandle = unrelated.Start();
        using HoldProbe late = new();
        using BlockingStage stage = new();
        using CancellationTokenSource cancellation = new();
        FakeTimeProvider clock = new();
        string artifact = Path.Combine(directory, $"native-{cancel}.tmp");
        File.WriteAllText(artifact, "temporary native launch material");
        ConnectionLaunchService service = new(directory, ReadyStartup(), cancel ? TestTimeout : TimeSpan.FromSeconds(2),
            startProcess: _ =>
            {
                Process started = late.Start();
                return stage.Run(() => started);
            },
            timeProvider: clock);
        Task<Process> launch = service.StartAsync(new LaunchPlan(Environment.ProcessPath!, [], [artifact]), cancellation.Token);
        try
        {
            await stage.Entered.Task.WaitAsync(TestTimeout);
            if (cancel)
            {
                cancellation.Cancel();
                await RequireCanceledAsync(launch, cancellation.Token);
            }
            else
            {
                clock.Advance(TimeSpan.FromSeconds(2));
                await RequireTimeoutAsync(launch);
            }
            Require(!stage.Returned.Task.IsCompleted, "Stop/timeout waited for the blocked native start operation.");
            Require(!unrelated.HasExited, "Stopping a launch terminated an unrelated existing client.");
        }
        finally
        {
            stage.Release();
            await stage.Returned.Task.WaitAsync(TestTimeout);
            await ObserveAsync(launch);
        }
        await WaitUntilAsync(() => late.HasExited, "A newly started process returned after cancellation and was left running.");
        await WaitUntilAsync(() => !File.Exists(artifact), "Late native startup retained temporary connection files.");
        Require(!unrelated.HasExited, "Late startup cleanup terminated an unrelated existing client.");
    }

    private static async Task TestCompletedLaunchSurvivesCancellationAsync(string directory)
    {
        using HoldProbe client = new();
        using CancellationTokenSource cancellation = new();
        string artifact = Path.Combine(directory, "completed-launch.tmp");
        File.WriteAllText(artifact, "temporary active connection material");
        ConnectionLaunchService service = new(directory, ReadyStartup(), TestTimeout, startProcess: _ => client.Start());
        await service.StartAsync(new LaunchPlan(Environment.ProcessPath!, [], [artifact]), cancellation.Token).WaitAsync(TestTimeout);
        cancellation.Cancel();
        await Task.Delay(50);
        Require(!client.HasExited, "Canceling after successful handoff terminated an established client.");
        Require(File.Exists(artifact), "Successful handoff removed its temporary material before the client exited.");
        client.RequestExit();
        await WaitUntilAsync(() => client.HasExited && !File.Exists(artifact), "Successful startup lost normal process-exit cleanup.");
    }

    private static ToDeskClientStartup ReadyStartup() => new(
        _ => new ToDeskClientState(123, true),
        _ => throw new InvalidOperationException("The fake ready client must not be restarted."),
        TestTimeout, TimeSpan.FromMilliseconds(5), TimeSpan.Zero);

    private static async Task RequireCanceledAsync(Task launch, CancellationToken token)
    {
        try
        {
            await launch.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException exception)
        {
            Require(exception.CancellationToken == token, "The launch lost the caller's cancellation token.");
            return;
        }
        throw new InvalidOperationException("The stopped launch did not report cancellation promptly.");
    }

    private static async Task RequireTimeoutAsync(Task launch)
    {
        // Do not catch WaitAsync's own timeout as success: the launch task itself must fault with TimeoutException.
        // / 不把测试等待超时误认为业务超时：必须由连接任务自身抛出 TimeoutException。
        await Task.WhenAny(launch, Task.Delay(TestTimeout));
        Require(launch.IsCompleted, "The launch exceeded its overall deadline without completing.");
        try
        {
            await launch;
        }
        catch (TimeoutException)
        {
            return;
        }
        throw new InvalidOperationException("An expired launch did not report TimeoutException.");
    }

    private static async Task ObserveAsync(Task task)
    {
        try { await task.WaitAsync(TestTimeout); }
        catch (OperationCanceledException) { }
        catch (TimeoutException) when (task.IsCompleted) { }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string failure)
    {
        Stopwatch watch = Stopwatch.StartNew();
        while (!condition() && watch.Elapsed < TestTimeout) await Task.Delay(10);
        Require(condition(), failure);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>Bounds every simulated synchronous stall and always exposes a release path. / 为每个同步卡顿模拟设置上限，并始终提供释放路径。</summary>
    private sealed class BlockingStage : IDisposable
    {
        private readonly ManualResetEventSlim _resume = new(false);
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Returned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal T Run<T>(Func<T> continuation)
        {
            Entered.TrySetResult();
            try
            {
                if (!_resume.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("The test did not release its simulated synchronous stall.");
                return continuation();
            }
            finally
            {
                Returned.TrySetResult();
            }
        }

        internal void Release() => _resume.Set();
        public void Dispose() => _resume.Dispose();
    }

    /// <summary>Owns only a test helper and an independent observer handle. / 仅拥有测试辅助进程及独立的观察句柄。</summary>
    private sealed class HoldProbe : IDisposable
    {
        private readonly string _eventName = @"Local\RemoteHubStudio.LaunchCancellation." + Guid.NewGuid().ToString("N");
        private readonly EventWaitHandle _ready;
        private readonly EventWaitHandle _exit;
        private Process? _observer;

        internal HoldProbe()
        {
            _ready = new EventWaitHandle(false, EventResetMode.ManualReset, _eventName + ".Ready");
            _exit = new EventWaitHandle(false, EventResetMode.ManualReset, _eventName + ".Exit");
        }

        internal Process Start()
        {
            ProcessStartInfo info = new(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            }
            info.ArgumentList.Add("--connection-launch-hold-probe");
            info.ArgumentList.Add(_eventName);
            Process process = Process.Start(info) ?? throw new InvalidOperationException("The harmless launch helper did not start.");
            _observer = Process.GetProcessById(process.Id);
            if (!_ready.WaitOne(TestTimeout))
            {
                process.Dispose();
                throw new TimeoutException("The harmless launch helper did not become ready.");
            }
            return process;
        }

        internal bool HasExited => _observer?.HasExited ?? false;
        internal void RequestExit() => _exit.Set();

        public void Dispose()
        {
            _exit.Set();
            if (_observer is not null)
            {
                if (!_observer.WaitForExit(2000))
                {
                    _observer.Kill(entireProcessTree: true);
                    _observer.WaitForExit(2000);
                }
                _observer.Dispose();
            }
            _ready.Dispose();
            _exit.Dispose();
        }
    }
}
