using System.Diagnostics;
using System.Reflection;
using RemoteHubStudio.Application;
using RemoteHubStudio.Configuration;
using RemoteHubStudio.Infrastructure;
using RemoteHubStudio.Infrastructure.ImportExport;
using RemoteHubStudio.Infrastructure.Launch;
using RemoteHubStudio.Infrastructure.Monitoring;
using RemoteHubStudio.Infrastructure.Persistence;
using RemoteHubStudio.Infrastructure.Security;
using RemoteHubStudio.Localization;
using RemoteHubStudio.UI.Main;
using RemoteHubStudio.UI.Theme;

namespace RemoteHubStudio;

/// <summary>
/// Composes and starts the RemoteHubStudio desktop application. / 组装并启动 RemoteHubStudio 桌面应用。
/// </summary>
internal static class Program
{
    /// <summary>
    /// Initializes process-wide UI settings, enforces single-instance behavior, and runs the main window. / 初始化进程级界面设置、实现单实例行为并运行主窗口。
    /// </summary>
    [STAThread]
    private static void Main()
    {
        bool restartRequested = false;
        using (SingleInstanceCoordinator singleInstance = new())
        {
            if (!singleInstance.IsPrimaryInstance)
            {
                singleInstance.SignalPrimaryInstance();
                return;
            }

            AppDataPaths paths = new();
            LanguagePreferenceStore languagePreferenceStore = new(paths);
            L.Initialize(languagePreferenceStore.Load(), paths);
            ApplicationConfiguration.Initialize();

            try
            {
                JsonWorkspaceRepository repository = new(paths, new DpapiCurrentUserProtector());
                WorkspaceService workspace = new(repository);
                workspace.InitializeAsync().GetAwaiter().GetResult();

                ThemeManager.Apply(workspace.GetSettings().Theme);
                using MainForm mainForm = new(
                    workspace,
                    new ConnectionLaunchService(Path.Combine(paths.TemporaryDirectory, "rdp")),
                    new WorkspaceTransferService(),
                    new ConnectionStatusService(),
                    new ExpirationService(),
                    paths,
                    singleInstance);

                _ = mainForm.Handle;
                singleInstance.StartListening();
                System.Windows.Forms.Application.Run(mainForm);
                restartRequested = mainForm.RestartRequested;
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    L.Format("Startup.Failed.Message", exception.Message, paths.DataDirectory),
                    L.Format("Startup.Failed.Title", ProductInfo.Name),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        if (restartRequested)
        {
            RestartProcess();
        }
    }

    /// <summary>Starts a replacement process only after the single-instance mutex has been released. / 仅在单实例互斥体释放后启动替代进程。</summary>
    private static void RestartProcess()
    {
        try
        {
            string executable = Environment.ProcessPath
                ?? throw new InvalidOperationException(L.Get("Restart.ExecutablePathUnknown"));
            ProcessStartInfo startInfo = new()
            {
                FileName = executable,
                UseShellExecute = !Path.GetFileNameWithoutExtension(executable)
                    .Equals("dotnet", StringComparison.OrdinalIgnoreCase),
                WorkingDirectory = AppContext.BaseDirectory
            };

            if (!startInfo.UseShellExecute)
            {
                string entryAssemblyPath = Assembly.GetEntryAssembly()?.Location ?? string.Empty;
                if (string.IsNullOrWhiteSpace(entryAssemblyPath))
                {
                    throw new InvalidOperationException(L.Get("Restart.ExecutablePathUnknown"));
                }

                startInfo.ArgumentList.Add(entryAssemblyPath);
            }

            foreach (string argument in Environment.GetCommandLineArgs().Skip(1))
            {
                startInfo.ArgumentList.Add(argument);
            }

            Process.Start(startInfo);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(
                L.Format("Restart.ManualRequired.Message", exception.Message),
                L.Get("Restart.ManualRequired.Title"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
