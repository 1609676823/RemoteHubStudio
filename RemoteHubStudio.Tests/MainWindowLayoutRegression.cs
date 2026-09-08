using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.ExceptionServices;
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

/// <summary>Checks that resizing preserves usable groups and every Reset restores the same persisted startup target.</summary>
internal static class MainWindowLayoutRegression
{
    internal static void Run()
    {
        VerifyLayoutCalculations();
        VerifyAlternateDefaultBounds();
        Exception? failure = null;
        Thread thread = new(() =>
        {
            string language = L.RequestedLanguage;
            try
            {
                foreach (string locale in new[] { "en", "zh-Hans" })
                {
                    L.SetLanguage(locale);
                    VerifyWindow(locale, collapsed: false);
                    VerifyWindow(locale, collapsed: true);
                }
                VerifyStartupRoundTrips();
            }
            catch (Exception exception) { failure = exception; }
            finally { L.SetLanguage(language); }
        }) { IsBackground = true, Name = "Main window layout regression" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60))) throw new TimeoutException("Main window layout regression timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        Console.WriteLine("MAIN_WINDOW_LAYOUT_OK (small-window groups, filters, explicit collapse, stable startup reset bounds, repeated/rapid reset, physical bounds across restarts, independent persisted defaults, DPI and monitor bounds)");
    }

    private static void VerifyLayoutCalculations()
    {
        foreach ((float width, int expected) in new (float, int)[]
                 { (1280, 232), (1080, 232), (1079, 200), (900, 200), (899, 184), (720, 184), (719, 168), (360, 168) })
        {
            Require(MainResponsiveLayoutLogic.CalculateSidebarWidth(width, false) == expected,
                $"Expanded groups have the wrong width at {width} logical pixels.");
            Require(MainResponsiveLayoutLogic.CalculateSidebarWidth(width, true) == 72,
                $"The explicit collapsed preference was ignored at {width} logical pixels.");
        }

        foreach ((Rectangle area, int dpi, Rectangle expected) in new (Rectangle, int, Rectangle)[]
                 {
                     (new(0, 0, 1920, 1040), 96, new(320, 120, 1280, 800)),
                     (new(0, 0, 2560, 1440), 144, new(320, 120, 1920, 1200)),
                     (new(-3840, -120, 3840, 2080), 192, new(-3200, 120, 2560, 1600)),
                     (new(-1366, 0, 1366, 728), 96, new(-1323, 8, 1280, 712)),
                     (new(0, 0, 1366, 728), 192, new(16, 16, 1334, 696))
                 })
        {
            Rectangle actual = MainResponsiveLayoutLogic.CreateDefaultWindowBounds(area, dpi);
            Require(actual == expected, $"Default bounds are incorrect at {dpi} DPI on {area}: {actual}, expected {expected}.");
            Require(area.Contains(actual), "Default bounds escaped the target monitor's working area.");
        }
    }

    private static void VerifyWindow(string locale, bool collapsed)
    {
        string name = "Local\\RemoteHubStudio.LayoutTests." + Guid.NewGuid().ToString("N");
        using SingleInstanceCoordinator instance = new(name, name + ".Activate");
        MemoryRepository repository = new(collapsed);
        WorkspaceService workspace = new(repository);
        workspace.InitializeAsync().GetAwaiter().GetResult();
        AppDataPaths paths = new(Path.Combine(Path.GetTempPath(), "RemoteHubStudio.LayoutTests", Guid.NewGuid().ToString("N")));
        using MainForm form = new(workspace, new ConnectionLaunchService(paths.TemporaryDirectory),
            new WorkspaceTransferService(), new ConnectionStatusService(), new ExpirationService(), paths, instance);
        form.Show();
        Pump();
        Screen startupScreen = Screen.FromControl(form);
        Rectangle expectedResetBounds = MainResponsiveLayoutLogic.CreateDefaultWindowBounds(startupScreen.WorkingArea, form.DeviceDpi);
        AntdUI.Button resetButton = Field<AntdUI.Button>(form, "_resetWindowButton");
        AntdUI.Menu navigation = Field<AntdUI.Menu>(form, "_navigation");
        AntdUI.MenuItem groupsRoot = navigation.FindID("groups-root")
            ?? throw new InvalidOperationException("The navigation did not load the in-memory groups.");
        groupsRoot.Expand = true;
        AntdUI.MenuItem selectedGroup = navigation.FindID("group:" + repository.Group.Id)
            ?? throw new InvalidOperationException("The saved group is missing from the navigation.");
        Require(selectedGroup.Visible && selectedGroup.Enabled,
            "The saved group is not visible and selectable.");
        navigation.Select(selectedGroup, false);
        Field<AntdUI.Input>(form, "_searchInput").Text = "Visible";
        Pump();
        RequireFilteredGroup(form, repository);

        foreach (Size size in new[] { new Size(900, 570), new Size(720, 480), new Size(1280, 800) })
        {
            form.Size = ScaleSize(form, size);
            Pump();
            Require(navigation.Visible && navigation.Collapsed == collapsed,
                $"Resizing to {size} changed the explicit sidebar preference ({collapsed}).");
            Require(workspace.GetSettings().SidebarCollapsed == collapsed,
                "Responsive layout rewrote the saved sidebar preference.");
            if (!collapsed)
            {
                Require(Field<Control>(form, "_sidebar").Width >= ScaleSize(form, new Size(168, 1)).Width,
                    "The expanded sidebar became too narrow for group names.");
                Require(groupsRoot.Expand && selectedGroup.Visible && selectedGroup.Enabled,
                    "Resizing hid an expanded group or made it unselectable.");
                navigation.Select(navigation.FindID("group:" + repository.OtherGroup.Id)!, false);
                Pump();
                Require(Field<List<Guid>>(form, "_visibleConnectionOrder").SequenceEqual([repository.OtherProfile.Id]),
                    "A different group could not be selected in the resized window.");
                navigation.Select(selectedGroup, false);
                Pump();
            }
            RequireFilteredGroup(form, repository);
            VerifyResetButtonLayout(form);
            if (!collapsed) CaptureOptionalScreenshot(form, $"groups-{locale}-{size.Width}x{size.Height}.png");
        }

        void RequireResetResult(string scenario)
        {
            Require(form.WindowState == FormWindowState.Normal && form.Bounds == expectedResetBounds,
                $"Reset after {scenario} changed the startup target: {form.Bounds}, expected {expectedResetBounds}.");
            Require(workspace.GetSettings().WindowBounds == expectedResetBounds &&
                    repository.LastSaved?.Settings.WindowBounds == expectedResetBounds,
                $"Reset after {scenario} did not persist the startup target.");
            Require(workspace.GetSettings().SidebarCollapsed == collapsed && navigation.Collapsed == collapsed,
                "Reset overwrote the user's sidebar preference.");
            RequireFilteredGroup(form, repository);
            VerifyResetButtonLayout(form);
        }

        void ResetAndVerify(string scenario)
        {
            int saveCount = repository.SaveCount;
            Click(resetButton);
            PumpUntil(() => repository.SaveCount > saveCount && resetButton.Enabled);
            RequireResetResult(scenario);
        }

        foreach (FormWindowState state in new[] { FormWindowState.Normal, FormWindowState.Maximized })
        {
            form.Size = ScaleSize(form, new Size(900, 570));
            form.Location = new Point(startupScreen.WorkingArea.Left + 24, startupScreen.WorkingArea.Top + 32);
            form.WindowState = state;
            Pump();
            ResetAndVerify($"moving/resizing and entering {state}");
        }

        for (int click = 0; click < 3; click++) ResetAndVerify($"consecutive click {click + 1}");

        form.Size = ScaleSize(form, new Size(900, 570));
        form.Location = new Point(startupScreen.WorkingArea.Left + 40, startupScreen.WorkingArea.Top + 48);
        form.Hide();
        form.Show();
        Pump();
        ResetAndVerify("hiding and showing a moved/resized window");

        Screen? otherScreen = Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName != startupScreen.DeviceName);
        if (otherScreen is not null)
        {
            Rectangle area = otherScreen.WorkingArea;
            form.Location = new Point(area.Left + (area.Width - form.Width) / 2, area.Top + (area.Height - form.Height) / 2);
            Pump();
            Require(Screen.FromControl(form).DeviceName == otherScreen.DeviceName,
                "The multi-monitor reset check could not move the window to the other available monitor.");
            ResetAndVerify("moving to another monitor");
        }

        // Hold the first save open so subsequent clicks really arrive during an asynchronous reset.
        form.Size = ScaleSize(form, new Size(900, 570));
        Pump();
        int savesBeforeRapidClicks = repository.SaveCount;
        TaskCompletionSource pendingSave = repository.PauseNextSave();
        try
        {
            for (int click = 0; click < 5; click++) Click(resetButton);
            Pump();
            Require(repository.SaveCount == savesBeforeRapidClicks,
                "The rapid-reset check did not keep the first save pending.");
            Require(!resetButton.Enabled, "Reset remained clickable while its first save was still pending.");
            Require(form.Bounds == expectedResetBounds,
                "Rapid clicks changed the startup target while its save was pending.");
        }
        finally
        {
            pendingSave.SetResult();
        }
        PumpUntil(() => repository.SaveCount > savesBeforeRapidClicks && resetButton.Enabled);
        RequireResetResult("rapid clicks during a pending save");
        ResetAndVerify("a further click after rapid resets finish");
    }

    private static void VerifyStartupRoundTrips()
    {
        AppDataPaths paths = new(Path.Combine(Path.GetTempPath(), "RemoteHubStudio.LayoutTests", Guid.NewGuid().ToString("N")));
        MemoryRepository repository = new(collapsed: false);
        Rectangle workingArea = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
        using Form dpiProbe = new() { StartPosition = FormStartPosition.Manual, Location = workingArea.Location };
        _ = dpiProbe.Handle;
        int dpi = dpiProbe.DeviceDpi;
        int Scale(int logical) => (int)Math.Round(logical * dpi / 96F);
        Rectangle initialBounds = MainResponsiveLayoutLogic.CreateDefaultWindowBounds(workingArea, dpi);
        Rectangle? resetBounds = null;

        void PersistLastBounds(Rectangle bounds)
        {
            WorkspaceService workspace = new(repository);
            workspace.InitializeAsync().GetAwaiter().GetResult();
            workspace.UpdateWindowBoundsAsync(bounds).GetAwaiter().GetResult();
        }

        void RestartAndVerify(AppDataPaths dataPaths, Rectangle expectedStartup, Rectangle? expectedDefault, string scenario)
        {
            // Construct both objects anew: reusing one form or workspace misses changes made during
            // native handle creation, and hiding/showing cannot reproduce a full startup restore.
            WorkspaceService workspace = new(repository);
            workspace.InitializeAsync().GetAwaiter().GetResult();
            Require(workspace.GetSettings().WindowBounds == expectedStartup,
                $"The repository did not restore the last saved physical bounds for {scenario}.");
            string name = "Local\\RemoteHubStudio.LayoutTests.Restart." + Guid.NewGuid().ToString("N");
            using SingleInstanceCoordinator instance = new(name, name + ".Activate");
            using MainForm form = new(workspace, new ConnectionLaunchService(dataPaths.TemporaryDirectory),
                new WorkspaceTransferService(), new ConnectionStatusService(), new ExpirationService(), dataPaths, instance);
            form.Show();
            Pump();
            Require(form.WindowState == FormWindowState.Normal && form.Bounds == expectedStartup,
                $"Startup after {scenario} changed persisted physical bounds: {form.Bounds}, expected {expectedStartup}. " +
                "Native window frame dimensions must not be subtracted again at each startup.");

            Rectangle expectedReset = expectedDefault ??
                MainResponsiveLayoutLogic.CreateDefaultWindowBounds(Screen.FromControl(form).WorkingArea, form.DeviceDpi);
            resetBounds ??= expectedReset;
            AntdUI.Button resetButton = Field<AntdUI.Button>(form, "_resetWindowButton");
            int savesBeforeReset = repository.SaveCount;
            Click(resetButton);
            PumpUntil(() => repository.SaveCount > savesBeforeReset && resetButton.Enabled);
            Require(form.Bounds == expectedReset && workspace.GetSettings().WindowBounds == expectedReset &&
                    repository.LastSaved?.Settings.WindowBounds == expectedReset,
                $"Reset after {scenario} did not restore and save the same default: {form.Bounds}, expected {expectedReset}.");
            Require(new WindowPlacementStore(dataPaths).LoadDefaultBounds() == expectedReset,
                $"Reset after {scenario} did not preserve its default in the application's data directory.");
        }

        PersistLastBounds(initialBounds);
        RestartAndVerify(paths, initialBounds, expectedDefault: null, "loading saved bounds before native handle creation");
        Rectangle originalDefault = resetBounds!.Value;
        for (int restart = 1; restart <= 3; restart++)
        {
            RestartAndVerify(paths, originalDefault, originalDefault, $"reset and full restart {restart}");

            Rectangle movedBounds = MainResponsiveLayoutLogic.ClampWindowBounds(
                new Rectangle(workingArea.Left + 32 + restart * 11, workingArea.Top + 40 + restart * 7, Scale(900), Scale(570)),
                workingArea, new Size(Scale(720), Scale(480)), new Size(Scale(900), Scale(570)), Scale(8));
            PersistLastBounds(movedBounds);
            RestartAndVerify(paths, movedBounds, originalDefault, $"saving a moved/smaller window and full restart {restart}");
        }

        // A separate portable installation must use its own saved default, even when both
        // windows run in the same process and restore the same last-used workspace bounds.
        AppDataPaths otherPaths = new(Path.Combine(Path.GetTempPath(), "RemoteHubStudio.LayoutTests", Guid.NewGuid().ToString("N")));
        Rectangle otherDefault = CreateAlternateDefaultBounds(originalDefault, workingArea, dpi);
        new WindowPlacementStore(otherPaths).SaveDefaultBounds(otherDefault);
        RestartAndVerify(otherPaths, originalDefault, otherDefault, "starting a separate data directory with its own default");
        Require(new WindowPlacementStore(paths).LoadDefaultBounds() == originalDefault,
            "Resetting a second data directory overwrote the first installation's default.");
        RestartAndVerify(paths, otherDefault, originalDefault, "returning to the original data directory");
        Require(new WindowPlacementStore(otherPaths).LoadDefaultBounds() == otherDefault,
            "Returning to the original data directory overwrote the second installation's default.");
    }

    /// <summary>Exercises the CI desktop geometry without depending on the local display. / 固定覆盖 CI 桌面尺寸，不依赖本机显示器。</summary>
    private static void VerifyAlternateDefaultBounds()
    {
        foreach ((Rectangle area, int dpi, Rectangle expected) in new (Rectangle, int, Rectangle)[]
                 {
                     (new(0, 0, 1024, 720), 96, new(9, 9, 900, 570)),
                     (new(-1024, -120, 1024, 720), 96, new(-1015, -111, 900, 570)),
                     (new(0, 0, 1920, 1040), 96, new(321, 121, 900, 570)),
                     (new(0, 0, 1920, 1040), 144, new(13, 13, 1350, 855)),
                     (new(0, 0, 2560, 1440), 192, new(17, 17, 1800, 1140)),
                     // Below the preferred minimum, only the full reachable area fits.
                     // / 小于首选最小尺寸时，只能使用完整可达区域。
                     (new(0, 0, 640, 480), 96, new(8, 8, 624, 464))
                 })
        {
            Rectangle original = MainResponsiveLayoutLogic.CreateDefaultWindowBounds(area, dpi);
            Rectangle alternate = CreateAlternateDefaultBounds(original, area, dpi);
            Require(alternate == expected,
                $"Alternate reset fixture is incorrect at {dpi} DPI on {area}: {alternate}, expected {expected}.");
        }

        // Reproduce the failed runner's exact coordinates: the old fixture crossed the 8px margin.
        // / 精确重现失败日志：旧测试数据越过 8 像素边距，启动时回到 (8,8) 是正确行为。
        Rectangle restored = MainResponsiveLayoutLogic.ClampWindowBounds(
            new Rectangle(9, 9, 1008, 704), new Rectangle(0, 0, 1024, 720),
            new Size(720, 480), new Size(1280, 800), margin: 8);
        Require(restored == new Rectangle(8, 8, 1008, 704),
            "Startup must clamp a window crossing the working-area margin without shrinking its physical size.");
    }

    private static Rectangle CreateAlternateDefaultBounds(Rectangle original, Rectangle workingArea, int dpi)
    {
        int Scale(int logical) => (int)Math.Round(logical * dpi / 96F, MidpointRounding.AwayFromZero);
        // A full-size reset already fills the CI runner's reachable area. Moving it by one pixel
        // makes startup legitimately clamp it back. Use a smaller, reachable reset fixture so
        // exact restart equality still detects real frame-size drift and shared-store bugs.
        // / CI 的默认窗口已占满可达区域；改用较小且经边距校正的测试窗口，继续严格验证
        // 重启前后边界相等，区分真实的边框缩减、存储串用与正常的位置校正。
        return MainResponsiveLayoutLogic.ClampWindowBounds(
            new Rectangle(original.X + 1, original.Y + 1, Scale(900), Scale(570)),
            workingArea, new Size(Scale(720), Scale(480)),
            new Size(Scale(1280), Scale(800)), Scale(8));
    }

    private static void RequireFilteredGroup(MainForm form, MemoryRepository repository)
    {
        Require(Field<string>(form, "_activeView") == "group:" + repository.Group.Id &&
                Field<AntdUI.Input>(form, "_searchInput").Text == "Visible" &&
                Field<List<Guid>>(form, "_visibleConnectionOrder").SequenceEqual([repository.Profile.Id]),
            "Resizing or resetting lost the selected group or its search-filtered connection.");
    }

    private static void VerifyResetButtonLayout(MainForm form)
    {
        AntdUI.PageHeader header = Field<AntdUI.PageHeader>(form, "_header");
        AntdUI.Button reset = Field<AntdUI.Button>(form, "_resetWindowButton");
        Require(reset.Parent == header && reset.Visible && reset.Enabled && !string.IsNullOrWhiteSpace(reset.Text) &&
                !string.IsNullOrWhiteSpace(reset.IconSvg) && !string.IsNullOrWhiteSpace(reset.AccessibleName),
            "The reset button must remain discoverable with text, an icon and an accessible name.");
        Require(header.ClientRectangle.Contains(reset.Bounds), "The reset button is clipped outside the title bar.");
        foreach (string fieldName in new[] { "_settingsButton", "_minimizeToTrayButton" })
        {
            Control button = Field<Control>(form, fieldName);
            Require(!reset.Bounds.IntersectsWith(button.Bounds), $"The reset button overlaps {fieldName}.");
        }
        Require(reset.Right <= header.ClientSize.Width - ScaleSize(form, new Size(144, 1)).Width,
            "The reset button overlaps the standard minimize, maximize or close controls.");
    }

    private static void CaptureOptionalScreenshot(MainForm form, string filename)
    {
        string? directory = Environment.GetEnvironmentVariable("REMOTEHUBSTUDIO_LAYOUT_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        // Render the client controls directly: Form.DrawToBitmap adds an artificial native
        // title bar over AntdUI's custom header when running outside the app message loop.
        using Bitmap bitmap = new(form.ClientSize.Width, form.ClientSize.Height);
        using (Graphics graphics = Graphics.FromImage(bitmap)) graphics.Clear(form.BackColor);
        foreach (Control control in form.Controls.Cast<Control>().Reverse())
        {
            if (control.Visible) control.DrawToBitmap(bitmap, control.Bounds);
        }
        bitmap.Save(Path.Combine(directory, filename), ImageFormat.Png);
    }

    private static Size ScaleSize(MainForm form, Size size)
    {
        float scale = form.DeviceDpi <= 0 ? 1F : form.DeviceDpi / 96F;
        return new Size((int)Math.Round(size.Width * scale), (int)Math.Round(size.Height * scale));
    }

    private static void Click(Control button) => typeof(Control)
        .GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, [EventArgs.Empty]);

    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void Pump() => System.Windows.Forms.Application.DoEvents();

    private static void PumpUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Pump();
            Thread.Sleep(10);
        }
        Require(condition(), "The reset operation did not finish persisting window bounds.");
        Pump();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class MemoryRepository : IWorkspaceRepository
    {
        private readonly bool _collapsed;
        private Task? _nextSaveDelay;
        internal ConnectionGroup Group { get; } = new() { Name = "生产环境" };
        internal ConnectionGroup OtherGroup { get; } = new() { Name = "测试环境" };
        internal ConnectionProfile Profile { get; }
        internal ConnectionProfile OtherProfile { get; }
        internal AppDataDocument? LastSaved { get; private set; }
        internal int SaveCount { get; private set; }

        internal MemoryRepository(bool collapsed)
        {
            _collapsed = collapsed;
            Profile = new() { Name = "Visible production connection", GroupId = Group.Id, Host = "production.invalid" };
            OtherProfile = new() { Name = "Visible test connection", GroupId = OtherGroup.Id, Host = "test.invalid" };
        }

        public Task<WorkspaceLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkspaceLoadResult(LastSaved ?? new AppDataDocument
            {
                Settings = new AppSettings { SidebarCollapsed = _collapsed, Theme = AppTheme.Light },
                Groups = [Group, OtherGroup],
                Connections = [Profile, OtherProfile, new ConnectionProfile
                {
                    Name = "Filtered-out production connection", GroupId = Group.Id, Host = "hidden.invalid"
                }]
            }));

        internal TaskCompletionSource PauseNextSave()
        {
            TaskCompletionSource pendingSave = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _nextSaveDelay = pendingSave.Task;
            return pendingSave;
        }

        public async Task SaveAsync(AppDataDocument document, CancellationToken cancellationToken = default)
        {
            Task? delay = _nextSaveDelay;
            _nextSaveDelay = null;
            if (delay is not null) await delay.WaitAsync(cancellationToken);
            LastSaved = document;
            SaveCount++;
        }
    }
}
