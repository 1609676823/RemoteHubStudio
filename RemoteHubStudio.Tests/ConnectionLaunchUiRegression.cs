using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Time.Testing;
using System.Windows.Forms;
using RemoteHubStudio.Application;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Infrastructure;
using RemoteHubStudio.Infrastructure.ImportExport;
using RemoteHubStudio.Infrastructure.Launch;
using RemoteHubStudio.Infrastructure.Monitoring;
using RemoteHubStudio.Infrastructure.Persistence;
using RemoteHubStudio.Localization;
using RemoteHubStudio.UI.Main;

namespace RemoteHubStudio.Tests;

/// <summary>Exercises stop, timeout, and safe exit through the live WinForms event loop without opening a remote client. / 使用真实 WinForms 消息循环验证停止、超时与安全退出，不打开远程客户端。</summary>
internal static class ConnectionLaunchUiRegression
{
    internal static void Run()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            string language = L.RequestedLanguage;
            void RunChecks(object? sender, EventArgs e)
            {
                System.Windows.Forms.Application.Idle -= RunChecks;
                try
                {
                    foreach (string locale in new[] { "en", "zh-Hans" })
                    {
                        L.SetLanguage(locale);
                        VerifyStopForEveryClient();
                    }

                    VerifyImmediateExit(minimizeToTray: false);
                    VerifyImmediateExit(minimizeToTray: true);
                    VerifyPendingSaveExit();
                    VerifyUnrelatedPrimaryOperationExit();
                    VerifyTimeoutRestoresControls();
                }
                catch (Exception exception) { failure = exception; }
                finally
                {
                    L.SetLanguage(language);
                    System.Windows.Forms.Application.ExitThread();
                }
            }

            // DoEvents alone uninstalls WinForms' synchronization context after each temporary loop.
            // Keep a real outer loop so async event handlers resume atomically on the UI thread.
            // / 单独使用 DoEvents 会在临时循环结束后卸载 WinForms 同步上下文；保留真正的外层循环，
            // 确保异步事件处理程序在 UI 线程完整恢复控件状态。
            System.Windows.Forms.Application.Idle += RunChecks;
            System.Windows.Forms.Application.Run();
        }) { IsBackground = true, Name = "Connection launch UI regression" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60))) throw new TimeoutException("Connection launch UI regression timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        Console.WriteLine("CONNECTION_LAUNCH_UI_OK (all client types, 2 languages, normal/narrow/overflow stop button, cancellation, timeout, immediate exit, tray and pending saves)");
    }

    private static void VerifyStopForEveryClient()
    {
        using Fixture fixture = new(TimeSpan.FromSeconds(20));
        foreach (ConnectionProfile profile in fixture.Repository.Profiles)
        {
            BlockedPlan attempt = fixture.BeginConnection(profile);
            CancellationToken token = Field<CancellationTokenSource>(fixture.Form, "_launchCancellation").Token;
            foreach (Size size in new[] { new Size(1180, 760), new Size(760, 480) })
            {
                fixture.Form.ClientSize = ScaleSize(fixture.Form, size);
                Pump();
                VerifyVisibleStopButton(fixture.Form, profile.Type, size);
                if (ReferenceEquals(profile, fixture.Repository.Profiles[0]))
                {
                    VerifyTrayRestorePreservesLaunchControls(fixture.Form, launchInProgress: true);
                    VerifyVisibleStopButton(fixture.Form, profile.Type, size);
                }
            }

            Stopwatch cancellationTime = Stopwatch.StartNew();
            Click(Field<AntdUI.Button>(fixture.Form, "_stopConnectionButton"));
            Require(cancellationTime.Elapsed < TimeSpan.FromSeconds(1), "Stop blocked the UI click handler.");
            PumpUntil(() => !Field<bool>(fixture.Form, "_primaryOperationInFlight"), "Stop did not release the primary operation guard.");
            Require(token.IsCancellationRequested, $"Stop did not cancel {profile.Type}.");
            Require(!attempt.Release.Task.IsCompleted, "Stop had to wait for the blocked native-plan worker.");
            RequireIdleControls(fixture.Form);
            if (ReferenceEquals(profile, fixture.Repository.Profiles[0]))
            {
                VerifyTrayRestorePreservesLaunchControls(fixture.Form, launchInProgress: false);
                RequireIdleControls(fixture.Form);
            }
            fixture.Release(attempt);
            Require(fixture.ProcessStarts == 0, "A canceled connection still launched a client after its blocked worker returned.");
        }
    }

    private static void VerifyTrayRestorePreservesLaunchControls(MainForm form, bool launchInProgress)
    {
        FlowLayoutPanel toolbar = Field<FlowLayoutPanel>(form, "_toolbar");
        string CaptureLayout() => string.Join(";", toolbar.Controls.Cast<Control>().Select(control =>
            $"{control.Name}:{control.Visible}:{control.Enabled}:{(control.Visible ? control.Bounds.ToString() : "hidden")}"));
        string before = CaptureLayout();
        CancellationToken token = launchInProgress
            ? Field<CancellationTokenSource>(form, "_launchCancellation").Token
            : default;

        Click(Field<AntdUI.Button>(form, "_minimizeToTrayButton"));
        Pump();
        Require(!form.Visible && !form.IsDisposed, "Tray minimize did not preserve the live main window.");
        ContextMenuStrip tray = Field<ContextMenuStrip>(form, "_trayMenu");
        ((ToolStripMenuItem)tray.Items.Cast<ToolStripItem>().Single(item => Equals(item.Tag, "tray-open"))).PerformClick();
        Pump();

        Require(form.Visible && CaptureLayout() == before,
            $"Tray restore changed primary toolbar order or geometry while launchInProgress={launchInProgress}.\nBefore: {before}\nAfter: {CaptureLayout()}");
        Require(!token.IsCancellationRequested, "Tray hide/restore unexpectedly canceled the pending launch.");
        Require(toolbar.Controls.GetChildIndex(Field<AntdUI.Button>(form, "_stopConnectionButton")) ==
                toolbar.Controls.GetChildIndex(Field<AntdUI.Button>(form, "_connectButton")) + 1,
            "Tray handle recreation moved the hidden Connect/Stop pair out of their designer slots.");
    }

    private static void VerifyVisibleStopButton(MainForm form, ConnectionType type, Size size)
    {
        AntdUI.Button stop = Field<AntdUI.Button>(form, "_stopConnectionButton");
        FlowLayoutPanel toolbar = Field<FlowLayoutPanel>(form, "_toolbar");
        Require(stop.Visible && stop.Enabled && stop.Parent == toolbar && !stop.Loading,
            $"Stop is unavailable for {type} at {size}.");
        Require(!string.IsNullOrWhiteSpace(stop.Text) && stop.AccessibleName == L.Get("Main.Command.StopConnection"),
            "Stop must retain a localized visible label and its full accessible name.");
        Require(toolbar.ClientRectangle.Contains(stop.Bounds), $"Stop is clipped at {size}: {stop.Bounds} outside {toolbar.ClientRectangle}.");
        Require(form.ClientRectangle.Contains(form.RectangleToClient(stop.RectangleToScreen(stop.ClientRectangle))),
            "Stop escaped the visible main window.");
        Require(!Field<AntdUI.Button>(form, "_connectButton").Visible, "Connect was not replaced by the independent stop button.");
        Require(!Field<AntdUI.Button>(form, "_moreButton").Enabled, "The test did not exercise stop while the overflow menu was disabled.");
        Require(!Field<AntdUI.Table>(form, "_connectionTable").Enabled, "The launch operation did not lock conflicting table actions.");
        Require(Field<AntdUI.Label>(form, "_viewStatus").Text?.Contains(type.ToString(), StringComparison.Ordinal) == true,
            "The pending connection is missing from the persistent status text.");
    }

    private static void VerifyImmediateExit(bool minimizeToTray)
    {
        using Fixture fixture = new(TimeSpan.FromSeconds(20), minimizeToTray);
        BlockedPlan attempt = fixture.BeginConnection(fixture.Repository.Profiles.Single(profile => profile.Type == ConnectionType.ToDesk));
        CancellationToken token = Field<CancellationTokenSource>(fixture.Form, "_launchCancellation").Token;
        if (minimizeToTray)
        {
            fixture.Form.Close();
            Pump();
            Require(!fixture.Form.IsDisposed && !fixture.Form.Visible && !token.IsCancellationRequested,
                "Close-to-tray incorrectly canceled the connection or closed the form.");
        }

        Stopwatch closingTime = Stopwatch.StartNew();
        if (minimizeToTray)
        {
            ContextMenuStrip tray = Field<ContextMenuStrip>(fixture.Form, "_trayMenu");
            ((ToolStripMenuItem)tray.Items.Cast<ToolStripItem>().Single(item => Equals(item.Tag, "tray-exit"))).PerformClick();
        }
        else
        {
            fixture.Form.Close();
        }
        Require(closingTime.Elapsed < TimeSpan.FromSeconds(1), "Application exit blocked on the external client worker.");
        Require(fixture.Form.IsDisposed && token.IsCancellationRequested, "A real exit did not immediately cancel and dispose the form.");
        Require(!attempt.Release.Task.IsCompleted, "A real exit waited for the blocked connection worker.");
        fixture.Release(attempt);
        Require(fixture.ProcessStarts == 0, "The canceled exit later launched a client.");
    }

    private static void VerifyPendingSaveExit()
    {
        using Fixture fixture = new(TimeSpan.FromSeconds(20));
        BlockedPlan attempt = fixture.BeginConnection(fixture.Repository.Profiles[0]);
        CancellationToken token = Field<CancellationTokenSource>(fixture.Form, "_launchCancellation").Token;
        TaskCompletionSource pendingSave = fixture.Repository.PauseNextSave();
        Task save = Invoke<Task>(fixture.Form, "SaveWindowBoundsAsync");
        try
        {
            Require(Field<int>(fixture.Form, "_windowBoundsSaveCount") > 0 && !save.IsCompleted,
                "The exit test did not keep a real window-bounds save pending.");
            fixture.Form.Close();
            PumpUntil(() => !Field<bool>(fixture.Form, "_primaryOperationInFlight"), "Exit did not cancel the pending launch.");
            Require(token.IsCancellationRequested && !fixture.Form.IsDisposed && Field<bool>(fixture.Form, "_exitRequestedAfterOperation"),
                "Exit bypassed a pending save while canceling the launch.");
            Require(!attempt.Release.Task.IsCompleted, "Canceling for exit waited for the native worker.");
        }
        finally
        {
            pendingSave.TrySetResult();
        }
        PumpUntil(() => save.IsCompleted && fixture.Form.IsDisposed, "The application did not exit after its pending save completed.");
        save.GetAwaiter().GetResult();
        fixture.Release(attempt);
    }

    private static void VerifyUnrelatedPrimaryOperationExit()
    {
        using Fixture fixture = new(TimeSpan.FromSeconds(20));
        TaskCompletionSource operation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task primary = Invoke<Task>(fixture.Form, "RunPrimaryOperationAsync", new Func<Task>(() => operation.Task), "Test save");
        try
        {
            fixture.Form.Close();
            Pump();
            Require(!fixture.Form.IsDisposed && Field<bool>(fixture.Form, "_exitRequestedAfterOperation"),
                "An unrelated primary save/import operation lost its deferred-exit protection.");
        }
        finally
        {
            operation.TrySetResult();
        }
        PumpUntil(() => primary.IsCompleted && fixture.Form.IsDisposed, "Deferred exit did not resume after the unrelated primary operation.");
        primary.GetAwaiter().GetResult();
    }

    private static void VerifyTimeoutRestoresControls()
    {
        using Fixture fixture = new(TimeSpan.FromMilliseconds(300));
        BlockedPlan attempt = fixture.BeginConnection(fixture.Repository.Profiles.Single(profile => profile.Type == ConnectionType.Custom));
        // Hosted runners may spend longer than 300 ms scheduling work or painting controls. Passing
        // real time must not expire this test's clock before the blocked worker has been observed.
        // / CI 调度或界面绘制可能超过 300 毫秒；真实时间流逝不能抢先触发测试的虚拟截止时间。
        Stopwatch slowRunner = Stopwatch.StartNew();
        while (slowRunner.Elapsed < TimeSpan.FromMilliseconds(600))
        {
            Pump();
            Thread.Sleep(5);
        }
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(299));
        Pump();
        Require(Field<bool>(fixture.Form, "_primaryOperationInFlight"), "The connection expired before its controlled deadline.");
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(1));
        PumpUntil(() => !Field<bool>(fixture.Form, "_primaryOperationInFlight"), "Connection timeout did not release the UI guard.");
        Require(!attempt.Release.Task.IsCompleted, "The timeout waited for the blocked worker to finish.");
        RequireIdleControls(fixture.Form);
        fixture.Release(attempt);
        Require(fixture.ProcessStarts == 0, "A timed-out connection launched after its worker returned.");
    }

    private static void RequireIdleControls(MainForm form)
    {
        AntdUI.Button connect = Field<AntdUI.Button>(form, "_connectButton");
        Require(connect.Visible && connect.Enabled && !connect.Loading && !Field<AntdUI.Button>(form, "_stopConnectionButton").Visible,
            "Cancellation or timeout did not restore the selected connection controls.");
        AntdUI.Table table = Field<AntdUI.Table>(form, "_connectionTable");
        AntdUI.Button quick = Field<AntdUI.Button>(form, "_quickButton");
        Require(table.Enabled && quick.Enabled,
            $"Cancellation or timeout left conflicting actions disabled: table={table.Enabled}, quick={quick.Enabled}, " +
            $"quickVisible={quick.Visible}, quickLoading={quick.Loading}, quickParent={quick.Parent?.Enabled}, " +
            $"guard={Field<bool>(form, "_primaryOperationInFlight")}, formDisposed={form.IsDisposed}.");
    }

    private static Size ScaleSize(MainForm form, Size logical)
    {
        float scale = form.DeviceDpi <= 0 ? 1F : form.DeviceDpi / 96F;
        return new Size((int)Math.Round(logical.Width * scale), (int)Math.Round(logical.Height * scale));
    }

    private static void Click(Control button) => typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(button, [EventArgs.Empty]);

    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static T Invoke<T>(object target, string name, params object?[] arguments) =>
        (T)target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, arguments)!;

    private static void Pump() => System.Windows.Forms.Application.DoEvents();

    private static void PumpUntil(Func<bool> condition, string message)
    {
        Stopwatch deadline = Stopwatch.StartNew();
        while (!condition() && deadline.Elapsed < TimeSpan.FromSeconds(5))
        {
            Pump();
            Thread.Sleep(5);
        }
        Require(condition(), message);
        Pump();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class BlockedPlan
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Returned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly SingleInstanceCoordinator _instance;
        private readonly List<BlockedPlan> _attempts = [];
        private BlockedPlan? _currentAttempt;
        private int _processStarts;
        internal MainForm Form { get; }
        internal MemoryRepository Repository { get; }
        internal FakeTimeProvider Clock { get; } = new();
        internal int ProcessStarts => Volatile.Read(ref _processStarts);

        internal Fixture(TimeSpan timeout, bool minimizeToTray = false)
        {
            string name = "Local\\RemoteHubStudio.LaunchUiTests." + Guid.NewGuid().ToString("N");
            _instance = new SingleInstanceCoordinator(name, name + ".Activate");
            Repository = new MemoryRepository(minimizeToTray);
            WorkspaceService workspace = new(Repository);
            workspace.InitializeAsync().GetAwaiter().GetResult();
            AppDataPaths paths = new(Path.Combine(Path.GetTempPath(), "RemoteHubStudio.LaunchUiTests", Guid.NewGuid().ToString("N")));
            ToDeskClientStartup startup = new(_ => new ToDeskClientState(1, true, TimeSpan.FromMinutes(1)),
                _ => throw new InvalidOperationException("The UI test must not start ToDesk."),
                TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(10), TimeSpan.Zero);
            ConnectionLaunchService launch = new(paths.TemporaryDirectory, startup, timeout,
                startProcess: _ =>
                {
                    Interlocked.Increment(ref _processStarts);
                    throw new InvalidOperationException("A canceled UI regression must not start a real process.");
                },
                planFactory: (_, _) =>
                {
                    BlockedPlan attempt = Volatile.Read(ref _currentAttempt)
                        ?? throw new InvalidOperationException("Missing blocked launch attempt.");
                    attempt.Started.TrySetResult();
                    try
                    {
                        attempt.Release.Task.GetAwaiter().GetResult();
                        return new LaunchPlan("test-client.exe", []);
                    }
                    finally { attempt.Returned.TrySetResult(); }
                },
                timeProvider: Clock);
            Form = new MainForm(workspace, launch, new WorkspaceTransferService(), new ConnectionStatusService(),
                new ExpirationService(), paths, _instance);
            Form.Show();
            Pump();
        }

        internal BlockedPlan BeginConnection(ConnectionProfile profile)
        {
            Require(SynchronizationContext.Current is WindowsFormsSynchronizationContext,
                $"The UI regression lacks a WinForms synchronization context: {SynchronizationContext.Current?.GetType().Name ?? "null"}.");
            BlockedPlan attempt = new();
            _attempts.Add(attempt);
            Volatile.Write(ref _currentAttempt, attempt);
            typeof(MainForm).GetMethod("SelectConnection", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Form, [profile.Id]);
            Click(Field<AntdUI.Button>(Form, "_connectButton"));
            PumpUntil(() => attempt.Started.Task.IsCompleted, "The background launch worker did not start.");
            Require(Field<bool>(Form, "_primaryOperationInFlight"), "The connection unexpectedly finished before cancellation.");
            return attempt;
        }

        internal void Release(BlockedPlan attempt)
        {
            attempt.Release.TrySetResult();
            PumpUntil(() => attempt.Returned.Task.IsCompleted, "The released launch-plan worker did not return.");
        }

        public void Dispose()
        {
            Form.Dispose();
            // Always release workers before unwinding the fixture, including assertion failures.
            // / 即使断言失败，也先释放全部后台任务，避免测试结束后遗留阻塞。
            foreach (BlockedPlan attempt in _attempts) attempt.Release.TrySetResult();
            foreach (BlockedPlan attempt in _attempts.Where(attempt => attempt.Started.Task.IsCompleted))
                PumpUntil(() => attempt.Returned.Task.IsCompleted, "Fixture cleanup could not release a blocked worker.");
            _instance.Dispose();
        }
    }

    private sealed class MemoryRepository(bool minimizeToTray) : IWorkspaceRepository
    {
        private Task? _nextSave;
        internal List<ConnectionProfile> Profiles { get; } = Enum.GetValues<ConnectionType>()
            .Select(type => new ConnectionProfile { Name = $"UI launch {type}", Type = type, Host = "launch-test.invalid" }).ToList();

        public Task<WorkspaceLoadResult> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(
            new WorkspaceLoadResult(new AppDataDocument
            {
                Settings = new AppSettings { MinimizeToTray = minimizeToTray, Theme = AppTheme.Light },
                Connections = Profiles
            }));

        internal TaskCompletionSource PauseNextSave()
        {
            TaskCompletionSource save = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _nextSave = save.Task;
            return save;
        }

        public async Task SaveAsync(AppDataDocument document, CancellationToken cancellationToken = default)
        {
            Task? delay = _nextSave;
            _nextSave = null;
            if (delay is not null) await delay.WaitAsync(cancellationToken);
        }
    }
}
