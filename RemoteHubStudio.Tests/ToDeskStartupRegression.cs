using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Infrastructure.Launch;

namespace RemoteHubStudio.Tests;

/// <summary>Exercises client startup before a real harmless command is dispatched. / 验证客户端启动完成后才派发真实的无害命令。</summary>
internal static class ToDeskStartupRegression
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(5);
    private static readonly TimeSpan StabilityPeriod = TimeSpan.FromMilliseconds(30);

    /// <summary>Writes proof that the connection command was actually started. / 写入连接命令确实已启动的凭据。</summary>
    internal static int RunProbe(string[] args)
    {
        File.AppendAllText(args[1], "connection command started" + Environment.NewLine);
        File.WriteAllText(args[1] + ".completed", "complete");
        return 0;
    }

    /// <summary>Runs an invisible message loop that can stall after its first idle. / 运行首次空闲后可受控卡顿的不可见消息循环。</summary>
    internal static int RunWindowProbe(string[] args)
    {
        Exception? failure = null;
        Thread windowThread = new(() =>
        {
            try
            {
                using EventWaitHandle ready = EventWaitHandle.OpenExisting(args[1] + ".Ready");
                using EventWaitHandle stall = EventWaitHandle.OpenExisting(args[1] + ".Stall");
                using EventWaitHandle stalled = EventWaitHandle.OpenExisting(args[1] + ".Stalled");
                using EventWaitHandle resume = EventWaitHandle.OpenExisting(args[1] + ".Resume");
                using EventWaitHandle exit = EventWaitHandle.OpenExisting(args[1] + ".Exit");
                using Form window = new() { ShowInTaskbar = false, Text = "ToDesk startup test helper" };
                _ = window.Handle;
                using System.Windows.Forms.Timer timer = new() { Interval = 10 };
                timer.Tick += (_, _) =>
                {
                    if (stall.WaitOne(0))
                    {
                        stalled.Set();
                        // STA-managed waits can pump sent messages and therefore do not reproduce a frozen UI.
                        // / STA 托管等待可能继续处理发送的消息，因此用不会泵送消息的原生等待模拟界面卡死。
                        if (WaitForSingleObject(resume.SafeWaitHandle, 15000) != 0)
                        {
                            failure = new TimeoutException("The test did not release its simulated startup stall.");
                            System.Windows.Forms.Application.ExitThread();
                        }
                    }
                    if (exit.WaitOne(0)) System.Windows.Forms.Application.ExitThread();
                };
                EventHandler idle = (_, _) => ready.Set();
                System.Windows.Forms.Application.Idle += idle;
                try
                {
                    timer.Start();
                    System.Windows.Forms.Application.Run();
                }
                finally
                {
                    System.Windows.Forms.Application.Idle -= idle;
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        windowThread.SetApartmentState(ApartmentState.STA);
        windowThread.Start();
        windowThread.Join();
        if (failure is not null) throw new InvalidOperationException("The invisible client probe failed.", failure);
        return 0;
    }

    /// <summary>Runs startup ordering, failure, cancellation, handoff, and concurrency checks. / 运行启动顺序、失败、取消、进程接力与并发检查。</summary>
    internal static async Task RunAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "RemoteHubStudio.ToDeskTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            TestPlanFlags(directory);
            await TestColdAndWarmLaunchAsync(directory);
            await TestSlowStartupRecoveryAsync(directory);
            await TestProcessMaturityAsync(directory);
            await TestLiveWindowResponsivenessAsync();
            await TestProcessHandoffAsync();
            await TestFailedStartupAsync(directory);
            await TestCancellationAsync(directory);
            await TestCancellationDuringProbeAsync();
            await TestConcurrentStartupAsync(directory);
            await TestOtherClientAsync(directory);
            Console.WriteLine("TODESK_STARTUP_OK (slow recovery, process maturity, live responsiveness, ordering, handoff, failures, cancellation, concurrency)");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void TestPlanFlags(string directory)
    {
        FakeClient client = new();
        ConnectionLaunchService service = new(directory, client.CreateStartup());
        ConnectionProfile profile = new()
        {
            Name = "ToDesk startup regression",
            Type = ConnectionType.ToDesk,
            Protocol = "device",
            Host = "123 456 789",
            Password = "space and \"quoted\" password",
            ExecutableOverride = Environment.ProcessPath!
        };
        LaunchPlan plan = service.CreatePlan(profile, new AppSettings { AllowPasswordInCommandLine = true });
        Require(plan.RequiresToDeskStartup && plan.ContainsSensitiveData &&
                plan.Arguments.SequenceEqual(["-control", "-id", "123456789", "-passwd", profile.Password]),
            "ToDesk startup metadata changed its normalized ID or password arguments.");
        plan = service.CreatePlan(profile, new AppSettings { AllowPasswordInCommandLine = false });
        Require(plan.RequiresToDeskStartup && !plan.ContainsSensitiveData &&
                plan.Arguments.SequenceEqual(["-control", "-id", "123456789"]),
            "Disabling password arguments bypassed ToDesk startup or exposed the password.");
        profile.CustomArguments = "-control -id {host}";
        Require(service.CreatePlan(profile, new AppSettings()).RequiresToDeskStartup,
            "ToDesk custom arguments bypassed startup preparation.");
        profile.Type = ConnectionType.Custom;
        Require(!service.CreatePlan(profile, new AppSettings()).RequiresToDeskStartup,
            "A custom client incorrectly acquired ToDesk startup requirements.");
        profile.Type = ConnectionType.Putty;
        profile.Protocol = "ssh";
        profile.Host = "example.invalid";
        profile.CustomArguments = string.Empty;
        Require(!service.CreatePlan(profile, new AppSettings()).RequiresToDeskStartup,
            "Another built-in client incorrectly acquired ToDesk startup requirements.");
        Require(client.StartCount == 0, "Creating a launch plan started an external client.");
    }

    private static async Task TestColdAndWarmLaunchAsync(string directory)
    {
        FakeClient client = new();
        ConnectionLaunchService service = new(directory, client.CreateStartup());
        string coldMarker = Path.Combine(directory, "cold-start.txt");
        Task<Process> coldLaunch = service.StartAsync(CreateProbePlan(coldMarker));
        await client.Started.Task.WaitAsync(TestTimeout);
        Require(client.LastStartedPath == Environment.ProcessPath, "Bootstrap did not use the resolved connection executable.");
        await RequireStillWaitingAsync(coldLaunch, coldMarker);
        client.State = new ToDeskClientState(101, true);
        await coldLaunch.WaitAsync(TestTimeout);
        await WaitForMarkerAsync(coldMarker);
        Require(client.StartCount == 1, "A cold connection did not start exactly one client.");

        string warmMarker = Path.Combine(directory, "warm-start.txt");
        ConnectionProfile warmProfile = new()
        {
            Name = "Warm ToDesk",
            Type = ConnectionType.ToDesk,
            Host = "123456789",
            ExecutableOverride = Environment.ProcessPath!,
            CustomArguments = string.Join(" ", GetProbeArguments(warmMarker).Select(QuoteArgument))
        };
        await service.LaunchAsync(warmProfile, new AppSettings()).WaitAsync(TestTimeout);
        await WaitForMarkerAsync(warmMarker);
        Require(client.StartCount == 1, "An already-running ToDesk client was started again.");
    }

    private static async Task TestSlowStartupRecoveryAsync(string directory)
    {
        TimeSpan stability = TimeSpan.FromMilliseconds(160);
        FakeClient client = new();
        ConnectionLaunchService service = new(directory, client.CreateStartup(
            readyStabilityPeriod: stability, minimumProcessAge: TimeSpan.FromSeconds(10)));
        string marker = Path.Combine(directory, "slow-recovery.txt");
        Task<Process> launch = service.StartAsync(CreateProbePlan(marker));
        await client.Started.Task.WaitAsync(TestTimeout);

        // A message loop can briefly respond before initialization stalls again.
        // / 消息循环可能短暂响应，随后继续因初始化而卡顿。
        client.State = new ToDeskClientState(101, true, TimeSpan.FromSeconds(11));
        await RequireStillWaitingAsync(launch, marker);
        client.State = new ToDeskClientState(101, false, TimeSpan.FromSeconds(11));
        await Task.Delay(stability * 2);
        Require(!launch.IsCompleted && !File.Exists(marker),
            "The first idle observation let a command run while startup was still stalled.");

        Stopwatch recoveredFor = Stopwatch.StartNew();
        client.State = new ToDeskClientState(101, true, TimeSpan.FromSeconds(12));
        await launch.WaitAsync(TestTimeout);
        Require(recoveredFor.Elapsed >= stability - PollInterval,
            "The recovered client inherited readiness time accumulated before its stall.");
        await WaitForMarkerAsync(marker + ".completed");
        await Task.Delay(stability);
        Require(client.StartCount == 1 && File.ReadAllLines(marker).Length == 1,
            "Slow startup recovery must dispatch exactly once without a second connection request.");
    }

    private static async Task TestProcessMaturityAsync(string directory)
    {
        TimeSpan minimumAge = TimeSpan.FromSeconds(10);
        FakeClient young = new() { State = new ToDeskClientState(301, true, TimeSpan.FromSeconds(1)) };
        ConnectionLaunchService service = new(directory, young.CreateStartup(minimumProcessAge: minimumAge));
        string marker = Path.Combine(directory, "young-client.txt");
        Task<Process> launch = service.StartAsync(CreateProbePlan(marker));
        await young.Inspected.Task.WaitAsync(TestTimeout);
        await RequireStillWaitingAsync(launch, marker);
        Require(young.StartCount == 0, "An already-running but young client was launched again.");
        young.State = new ToDeskClientState(301, true, minimumAge);
        await launch.WaitAsync(TestTimeout);
        await WaitForMarkerAsync(marker + ".completed");

        FakeClient warm = new() { State = new ToDeskClientState(302, true, TimeSpan.FromMinutes(5)) };
        Stopwatch warmWait = Stopwatch.StartNew();
        await warm.CreateStartup(minimumProcessAge: minimumAge)
            .EnsureRunningAsync(Environment.ProcessPath!).WaitAsync(TestTimeout);
        Require(warm.StartCount == 0 && warmWait.Elapsed < TimeSpan.FromSeconds(1),
            "A mature responsive client unnecessarily waited through the cold-start grace period.");
    }

    private static async Task TestLiveWindowResponsivenessAsync()
    {
        string prefix = "Local\\RemoteHubStudio.ToDeskWindowProbe." + Guid.NewGuid().ToString("N");
        using EventWaitHandle ready = new(false, EventResetMode.ManualReset, prefix + ".Ready");
        using EventWaitHandle stall = new(false, EventResetMode.AutoReset, prefix + ".Stall");
        using EventWaitHandle stalled = new(false, EventResetMode.ManualReset, prefix + ".Stalled");
        using EventWaitHandle resume = new(false, EventResetMode.ManualReset, prefix + ".Resume");
        using EventWaitHandle exit = new(false, EventResetMode.ManualReset, prefix + ".Exit");
        ProcessStartInfo start = new(Environment.ProcessPath!)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--todesk-window-probe");
        start.ArgumentList.Add(prefix);
        using Process helper = Process.Start(start) ?? throw new InvalidOperationException("The window probe did not start.");
        try
        {
            Require(await Task.Run(() => ready.WaitOne(TestTimeout)), "The invisible test window never reached its first idle.");
            Require(ToDeskClientProbe.HasResponsiveWindow(helper.Id),
                "An initialized hidden client window was incorrectly treated as unready.");

            stall.Set();
            Require(await Task.Run(() => stalled.WaitOne(TestTimeout)), "The test window did not enter its controlled startup stall.");
            // This console-hosted helper exposes first-idle via an event; Windows rejects WaitForInputIdle on a console image.
            // / 此控制台宿主辅助进程通过事件暴露首次空闲；Windows 不支持对控制台程序调用 WaitForInputIdle。
            Require(ready.WaitOne(0), "The probe lost its first-idle observation during the stall.");
            Require(!ToDeskClientProbe.HasResponsiveWindow(helper.Id),
                "A previously idle but currently stalled window was incorrectly treated as ready.");

            resume.Set();
            Stopwatch recovery = Stopwatch.StartNew();
            while (!ToDeskClientProbe.HasResponsiveWindow(helper.Id) && recovery.Elapsed < TestTimeout)
                await Task.Delay(10);
            Require(ToDeskClientProbe.HasResponsiveWindow(helper.Id),
                "The client probe failed to recognize automatic recovery from a startup stall.");
        }
        finally
        {
            resume.Set();
            exit.Set();
            if (!helper.WaitForExit(5000)) helper.Kill();
            await helper.WaitForExitAsync().WaitAsync(TestTimeout);
        }
        Require(helper.ExitCode == 0, "The window probe failed: " + await helper.StandardError.ReadToEndAsync());
    }

    private static async Task TestProcessHandoffAsync()
    {
        FakeClient client = new() { State = new ToDeskClientState(201, false) };
        ToDeskClientStartup startup = client.CreateStartup();
        Task waiting = startup.EnsureRunningAsync(Environment.ProcessPath!);
        await client.Inspected.Task.WaitAsync(TestTimeout);
        client.State = null;
        await RequireStillWaitingAsync(waiting);
        client.State = new ToDeskClientState(202, false);
        await RequireStillWaitingAsync(waiting);
        client.State = new ToDeskClientState(202, true);
        await waiting.WaitAsync(TestTimeout);
        Require(client.StartCount == 0, "Process handoff launched a second client instead of waiting for the replacement.");

        Stopwatch originalAge = new();
        Stopwatch replacementAge = new();
        int observations = 0;
        TimeSpan stability = TimeSpan.FromMilliseconds(80);
        ToDeskClientStartup replacingReadyClient = new(
            _ =>
            {
                int observation = Interlocked.Increment(ref observations);
                if (observation == 1)
                {
                    return new ToDeskClientState(203, true);
                }
                if (observation == 2)
                {
                    originalAge.Start();
                    return new ToDeskClientState(203, true);
                }
                if (observation == 3 && originalAge.Elapsed < stability / 2)
                {
                    return new ToDeskClientState(203, true);
                }
                if (!replacementAge.IsRunning) replacementAge.Start();
                return new ToDeskClientState(204, true);
            },
            _ => throw new InvalidOperationException("An existing client must not be bootstrapped."),
            TestTimeout, TimeSpan.FromMilliseconds(15), stability);
        await replacingReadyClient.EnsureRunningAsync(Environment.ProcessPath!).WaitAsync(TestTimeout);
        Require(replacementAge.IsRunning && replacementAge.Elapsed >= stability - TimeSpan.FromMilliseconds(5),
            "A replacement process inherited the previous process's readiness interval.");

        Stopwatch replacementWindowAge = new();
        int windowObservations = 0;
        ToDeskClientStartup replacingReadyWindow = new(
            _ =>
            {
                if (Interlocked.Increment(ref windowObservations) <= 3)
                    return new ToDeskClientState(205, true, default, 1);
                if (!replacementWindowAge.IsRunning) replacementWindowAge.Start();
                return new ToDeskClientState(205, true, default, 2);
            },
            _ => throw new InvalidOperationException("An existing client must not be bootstrapped."),
            TestTimeout, TimeSpan.FromMilliseconds(15), stability);
        await replacingReadyWindow.EnsureRunningAsync(Environment.ProcessPath!).WaitAsync(TestTimeout);
        Require(replacementWindowAge.Elapsed >= stability - TimeSpan.FromMilliseconds(5),
            "A replacement window inherited the old window's readiness interval in the same process.");
    }

    private static async Task TestFailedStartupAsync(string directory)
    {
        FakeClient client = new();
        ConnectionLaunchService service = new(directory, client.CreateStartup(TimeSpan.FromMilliseconds(100)));
        string timeoutMarker = Path.Combine(directory, "timeout-command.txt");
        await RequireThrowsAsync<InvalidOperationException>(() => service.StartAsync(CreateProbePlan(timeoutMarker)));
        Require(client.StartCount == 1 && !File.Exists(timeoutMarker), "Startup timeout dispatched the connection command or retried bootstrap.");

        ToDeskClientStartup failingStartup = new(
            _ => null,
            _ => throw new InvalidOperationException("Simulated client startup failure."),
            TestTimeout, PollInterval, StabilityPeriod);
        service = new ConnectionLaunchService(directory, failingStartup);
        string failureMarker = Path.Combine(directory, "failed-command.txt");
        await RequireThrowsAsync<InvalidOperationException>(() => service.StartAsync(CreateProbePlan(failureMarker)));
        await Task.Delay(50);
        Require(!File.Exists(timeoutMarker) && !File.Exists(failureMarker), "A failed startup still dispatched a connection command.");
    }

    private static async Task TestCancellationAsync(string directory)
    {
        FakeClient client = new();
        ConnectionLaunchService service = new(directory, client.CreateStartup());
        string marker = Path.Combine(directory, "cancelled-command.txt");
        using CancellationTokenSource cancellation = new();
        Task<Process> launch = service.StartAsync(CreateProbePlan(marker), cancellation.Token);
        await client.Started.Task.WaitAsync(TestTimeout);
        cancellation.Cancel();
        await RequireThrowsAsync<OperationCanceledException>(() => launch);
        await Task.Delay(50);
        Require(!File.Exists(marker), "Cancelling startup still dispatched the connection command.");

        string preCancelledMarker = Path.Combine(directory, "pre-cancelled-command.txt");
        await RequireThrowsAsync<OperationCanceledException>(() => service.StartAsync(CreateProbePlan(preCancelledMarker), cancellation.Token));
        Require(client.StartCount == 1 && !File.Exists(preCancelledMarker), "A pre-cancelled request started a client or command.");

        client.State = new ToDeskClientState(101, true);
        string retryMarker = Path.Combine(directory, "after-cancellation.txt");
        await service.StartAsync(CreateProbePlan(retryMarker)).WaitAsync(TestTimeout);
        await WaitForMarkerAsync(retryMarker);
        Require(client.StartCount == 1, "Cancelling a wait discarded the already-started client.");
    }

    private static async Task TestCancellationDuringProbeAsync()
    {
        using CancellationTokenSource beforeBootstrap = new();
        bool bootstrapped = false;
        ToDeskClientStartup startup = new(
            _ => { beforeBootstrap.Cancel(); return null; },
            _ => bootstrapped = true,
            TestTimeout, PollInterval, StabilityPeriod);
        await RequireThrowsAsync<OperationCanceledException>(() =>
            startup.EnsureRunningAsync(Environment.ProcessPath!, beforeBootstrap.Token));
        Require(!bootstrapped, "Cancellation during inspection still opened the client.");

        using CancellationTokenSource beforeReady = new();
        int observations = 0;
        startup = new(
            _ =>
            {
                if (++observations == 2) beforeReady.Cancel();
                return new ToDeskClientState(401, true);
            },
            _ => throw new InvalidOperationException("A running client must not be bootstrapped."),
            TestTimeout, PollInterval, TimeSpan.Zero);
        await RequireThrowsAsync<OperationCanceledException>(() =>
            startup.EnsureRunningAsync(Environment.ProcessPath!, beforeReady.Token));
    }

    private static async Task TestConcurrentStartupAsync(string directory)
    {
        FakeClient client = new();
        ConnectionLaunchService service = new(directory, client.CreateStartup());
        string[] markers = Enumerable.Range(0, 3).Select(index => Path.Combine(directory, $"concurrent-{index}.txt")).ToArray();
        Task<Process>[] launches = markers.Select(marker => service.StartAsync(CreateProbePlan(marker))).ToArray();
        await client.Started.Task.WaitAsync(TestTimeout);
        await RequireStillWaitingAsync(Task.WhenAll(launches));
        Require(client.StartCount == 1 && markers.All(marker => !File.Exists(marker)),
            "Concurrent connections bootstrapped more than one client or dispatched before readiness.");
        client.State = new ToDeskClientState(101, true);
        await Task.WhenAll(launches).WaitAsync(TestTimeout);
        await Task.WhenAll(markers.Select(WaitForMarkerAsync));
        Require(client.StartCount == 1, "Queued connections redundantly restarted the ready client.");
    }

    private static async Task TestOtherClientAsync(string directory)
    {
        ToDeskClientStartup startup = new(
            _ => throw new InvalidOperationException("Non-ToDesk commands must not inspect ToDesk."),
            _ => throw new InvalidOperationException("Non-ToDesk commands must not start ToDesk."),
            TestTimeout, PollInterval, StabilityPeriod);
        ConnectionLaunchService service = new(directory, startup);
        string marker = Path.Combine(directory, "other-client.txt");
        await service.StartAsync(CreateProbePlan(marker, requiresToDeskStartup: false)).WaitAsync(TestTimeout);
        await WaitForMarkerAsync(marker);
    }

    private static LaunchPlan CreateProbePlan(string marker, bool requiresToDeskStartup = true) =>
        new(Environment.ProcessPath!, GetProbeArguments(marker), requiresToDeskStartup: requiresToDeskStartup);

    private static IEnumerable<string> GetProbeArguments(string marker)
    {
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            yield return Assembly.GetExecutingAssembly().Location;
        }
        yield return "--todesk-command-probe";
        yield return marker;
    }

    private static string QuoteArgument(string argument) => $"\"{argument}\"";

    private static async Task WaitForMarkerAsync(string marker)
    {
        Stopwatch watch = Stopwatch.StartNew();
        while (!File.Exists(marker) && watch.Elapsed < TestTimeout) await Task.Delay(10);
        Require(File.Exists(marker), "The connection command did not run after the client became ready.");
    }

    private static async Task RequireStillWaitingAsync(Task launch, string? marker = null)
    {
        await Task.Delay(75);
        Require(!launch.IsCompleted && (marker is null || !File.Exists(marker)),
            "The connection command did not wait for a ready client.");
    }

    private static async Task RequireThrowsAsync<TException>(Func<Task> action) where TException : Exception
    {
        try
        {
            await action().WaitAsync(TestTimeout);
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(TException).Name} from unsuccessful client startup.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(Microsoft.Win32.SafeHandles.SafeWaitHandle handle, uint milliseconds);

    private sealed class FakeClient
    {
        private readonly object _gate = new();
        private ToDeskClientState? _state;
        private int _startCount;
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Inspected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal string? LastStartedPath { get; private set; }
        internal int StartCount => Volatile.Read(ref _startCount);
        internal ToDeskClientState? State
        {
            get { lock (_gate) return _state; }
            set { lock (_gate) _state = value; }
        }

        internal ToDeskClientStartup CreateStartup(
            TimeSpan? timeout = null,
            TimeSpan? readyStabilityPeriod = null,
            TimeSpan minimumProcessAge = default) => new(
            _ =>
            {
                ToDeskClientState? state = State;
                Inspected.TrySetResult();
                return state;
            },
            path =>
            {
                LastStartedPath = path;
                Interlocked.Increment(ref _startCount);
                State = new ToDeskClientState(101, false);
                Started.TrySetResult();
            },
            timeout ?? TestTimeout, PollInterval, readyStabilityPeriod ?? StabilityPeriod, minimumProcessAge);
    }
}
