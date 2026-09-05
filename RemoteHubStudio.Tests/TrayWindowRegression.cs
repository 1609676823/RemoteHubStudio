using System.Drawing;
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

internal static class TrayWindowRegression
{
    internal static void Run()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            string language = L.RequestedLanguage;
            try
            {
                foreach (string locale in new[] { "en", "zh-Hans" })
                {
                    L.SetLanguage(locale);
                    foreach (int width in new[] { 1180, 760 }) VerifyWindowLifecycle(width);
                }
            }
            catch (Exception exception) { failure = exception; }
            finally { L.SetLanguage(language); }
        }) { IsBackground = true, Name = "Tray window regression" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60))) throw new TimeoutException("Tray window regression timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        Console.WriteLine("TRAY_WINDOW_OK (layout parity, clipping, process activation, normal/minimized/maximized, tray, close, exit; 2 languages x 2 widths)");
    }

    private static void VerifyWindowLifecycle(int width)
    {
        string name = "Local\\RemoteHubStudio.TrayTests." + Guid.NewGuid().ToString("N");
        using SingleInstanceCoordinator instance = new(name, name + ".Activate");
        MemoryRepository repository = new();
        WorkspaceService workspace = new(repository);
        workspace.InitializeAsync().GetAwaiter().GetResult();
        AppDataPaths paths = new(Path.Combine(Path.GetTempPath(), "RemoteHubStudio.TrayTests", Guid.NewGuid().ToString("N")));
        using MainForm form = new(workspace, new ConnectionLaunchService(paths.TemporaryDirectory),
            new WorkspaceTransferService(), new ConnectionStatusService(), new ExpirationService(), paths, instance);
        form.ClientSize = new Size(width, 760);
        form.Show();
        Pump();
        Field<AntdUI.Input>(form, "_searchInput").Text = "Restore";
        typeof(MainForm).GetMethod("SelectConnection", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, [repository.Profile.Id]);
        Pump();
        Require(Field<AntdUI.Button>(form, "_editButton").Enabled, "The selected connection must enable Edit before restore.");
        instance.StartListening();
        NotifyIcon tray = Field<NotifyIcon>(form, "_notifyIcon");
        ContextMenuStrip menu = Field<ContextMenuStrip>(form, "_trayMenu");
        Require(form.Icon is not null && tray.Icon is not null && tray.Visible, "Application/tray artwork was not assigned.");
        Require(Field<AntdUI.PageHeader>(form, "_header").MinimizeBox, "The ordinary minimize button disappeared.");
        string initialLayout = CaptureToolbarLayout(form);
        WakeByAnotherProcess(instance, name);
        RequireSameToolbarLayout(form, initialLayout, "relaunch while already visible");

        form.WindowState = FormWindowState.Minimized;
        Pump();
        Require(form.Visible && form.ShowInTaskbar && form.WindowState == FormWindowState.Minimized,
            "Normal minimize must remain on the taskbar.");
        Open(menu);
        Require(form.Visible && form.ShowInTaskbar && form.WindowState == FormWindowState.Normal, "Tray Open did not restore normal minimize.");
        RequireSameToolbarLayout(form, initialLayout, "normal minimize / tray Open");

        ClickTrayButton(form);
        Require(!form.Visible && !form.ShowInTaskbar && tray.Visible, "Tray minimize left a taskbar window or removed the tray icon.");
        DoubleClick(form, MouseButtons.Right);
        Require(!form.Visible, "Right double-click unexpectedly restored the window.");
        DoubleClick(form, MouseButtons.Left);
        Require(form.Visible && form.ShowInTaskbar, "Left double-click did not restore the window.");
        RequireSameToolbarLayout(form, initialLayout, "tray minimize / double-click");
        for (int cycle = 0; cycle < 3; cycle++)
        {
            ClickTrayButton(form);
            WakeByAnotherProcess(instance, name);
            RequireSameToolbarLayout(form, initialLayout, $"tray minimize / relaunch {cycle + 1}");
        }
        form.WindowState = FormWindowState.Minimized;
        Pump();
        WakeByAnotherProcess(instance, name);
        RequireSameToolbarLayout(form, initialLayout, "normal minimize / relaunch");

        form.WindowState = FormWindowState.Maximized;
        Pump();
        string maximizedLayout = CaptureToolbarLayout(form);
        ClickTrayButton(form);
        Open(menu);
        Require(form.WindowState == FormWindowState.Maximized, "Tray restore lost the maximized state.");
        RequireSameToolbarLayout(form, maximizedLayout, "maximized tray restore");
        form.WindowState = FormWindowState.Minimized;
        Pump();
        Open(menu);
        Require(form.WindowState == FormWindowState.Maximized, "Normal minimize/restore lost the maximized state.");
        RequireSameToolbarLayout(form, maximizedLayout, "maximized normal minimize / tray Open");
        ClickTrayButton(form);
        WakeByAnotherProcess(instance, name);
        RequireSameToolbarLayout(form, maximizedLayout, "maximized tray / relaunch");

        form.WindowState = FormWindowState.Normal;
        AppSettings settings = workspace.GetSettings();
        settings.MinimizeToTray = true;
        workspace.UpdateSettingsAsync(settings).GetAwaiter().GetResult();
        form.Close();
        Pump();
        Require(!form.IsDisposed && !form.Visible && tray.Visible, "Close-to-tray closed the application.");
        Open(menu);
        RequireSameToolbarLayout(form, initialLayout, "close-to-tray / Open");
        ((ToolStripMenuItem)menu.Items.Cast<ToolStripItem>().Single(item => Equals(item.Tag, "tray-exit"))).PerformClick();
        Pump();
        Require(form.IsDisposed && !tray.Visible, "Tray Exit did not bypass close-to-tray and dispose the icon.");
    }

    private static void ClickTrayButton(MainForm form)
    {
        AntdUI.Button button = Field<AntdUI.Button>(form, "_minimizeToTrayButton");
        typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, [EventArgs.Empty]);
        Pump();
    }

    private static string CaptureToolbarLayout(MainForm form)
    {
        List<string> layout = [];
        foreach (string name in new[] { "_header", "_sidebar", "_contentPanel", "_connectionTable", "_viewStatus" })
        {
            Control control = Field<Control>(form, name);
            layout.Add($"{name}: {control.Bounds}, visible={control.Visible}");
        }
        foreach (string name in new[] { "_toolbar", "_secondaryToolbar" })
        {
            FlowLayoutPanel toolbar = Field<FlowLayoutPanel>(form, name);
            layout.Add($"{name}: {toolbar.Bounds}");
            foreach (Control control in toolbar.Controls)
            {
                // Positions of intentionally hidden overflow commands are not part of the displayed UI.
                layout.Add($"{control.Name}: {(control.Visible ? control.Bounds.ToString() : "hidden")}, visible={control.Visible}, enabled={control.Enabled}, text={control.Text}");
                if (control.Visible && control is AntdUI.Button)
                {
                    Require(toolbar.ClientRectangle.Contains(control.Bounds),
                        $"{control.Name} is clipped: {control.Bounds} outside {toolbar.ClientRectangle}.");
                }
            }
        }
        return string.Join(Environment.NewLine, layout);
    }

    private static void WakeByAnotherProcess(SingleInstanceCoordinator instance, string name)
    {
        using ManualResetEventSlim received = new(false);
        EventHandler handler = (_, _) => received.Set();
        instance.ActivationRequested += handler;
        try
        {
            SingleInstanceRegression.RequestActivation(name, name + ".Activate");
            Require(received.Wait(TimeSpan.FromSeconds(5)), "The duplicate launch did not reach the main window.");
            Pump(); // Dispatch the UI callback posted by MainForm's real activation listener.
        }
        finally { instance.ActivationRequested -= handler; }
    }

    private static void RequireSameToolbarLayout(MainForm form, string expected, string scenario)
    {
        string actual = CaptureToolbarLayout(form);
        Require(actual == expected, $"Toolbar changed after {scenario}.\nBefore:\n{expected}\nAfter:\n{actual}");
    }

    private static void DoubleClick(MainForm form, MouseButtons button)
    {
        typeof(NotifyIcon).GetMethod("OnMouseDoubleClick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Field<NotifyIcon>(form, "_notifyIcon"), [new MouseEventArgs(button, 2, 0, 0, 0)]);
        Pump();
    }

    private static void Open(ContextMenuStrip menu)
    {
        ((ToolStripMenuItem)menu.Items[0]).PerformClick();
        Pump();
    }

    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void Pump() => System.Windows.Forms.Application.DoEvents();

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class MemoryRepository : IWorkspaceRepository
    {
        internal ConnectionProfile Profile { get; } = new() { Name = "Restore test", Host = "restore-test.invalid" };
        public Task<WorkspaceLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkspaceLoadResult(new AppDataDocument { Connections = [Profile] }));
        public Task SaveAsync(AppDataDocument document, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
