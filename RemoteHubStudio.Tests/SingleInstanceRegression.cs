using System.Diagnostics;
using System.Reflection;
using RemoteHubStudio.Infrastructure;

namespace RemoteHubStudio.Tests;

internal static class SingleInstanceRegression
{
    internal static int RunProbe(string[] args)
    {
        using SingleInstanceCoordinator instance = new(args[1], args[2]);
        Console.WriteLine(instance.IsPrimaryInstance ? "primary" : "secondary");
        Console.Out.Flush();
        if (args[3] == "abandon") Environment.Exit(0);
        if (args[3] == "hold") Console.ReadLine();
        else instance.SignalPrimaryInstance();
        return 0;
    }

    internal static void Run()
    {
        string name = "Local\\RemoteHubStudio.Tests." + Guid.NewGuid().ToString("N");
        string activation = name + ".Activate";
        using AutoResetEvent activated = new(false);
        using SingleInstanceCoordinator primary = new(name, activation);
        Require(primary.IsPrimaryInstance, "First process must own the mutex.");

        // Request activation before the listener exists, as when a user double-launches at startup.
        using (Process early = StartProbe(name, activation, "signal"))
        {
            Require(ReadLine(early) == "secondary", "A duplicate launch became a second primary.");
            WaitForExit(early);
        }
        primary.ActivationRequested += (_, _) => activated.Set();
        primary.StartListening();
        primary.StartListening();
        Require(activated.WaitOne(5000), "An activation request during startup was lost.");

        for (int index = 0; index < 3; index++)
        {
            using Process later = StartProbe(name, activation, "signal");
            Require(ReadLine(later) == "secondary", "A repeated launch became a second primary.");
            WaitForExit(later);
            Require(activated.WaitOne(5000), "The primary stopped listening after an activation.");
        }

        // Keep another process's mutex handle alive after ownership is released.
        // createdNew alone cannot detect this valid takeover.
        using (Process holder = StartProbe(name, activation, "hold"))
        {
            Require(ReadLine(holder) == "secondary", "The holder became primary before shutdown.");
            primary.Dispose();
            using SingleInstanceCoordinator replacement = new(name, activation);
            Require(replacement.IsPrimaryInstance, "A stale secondary handle prevented restart.");
            holder.StandardInput.WriteLine("release");
            holder.StandardInput.Flush();
            WaitForExit(holder);
        }

        // Retain the kernel object while its owner crashes, then acquire the abandoned mutex.
        using Mutex retained = new(false, name);
        using (Process crashed = StartProbe(name, activation, "abandon"))
        {
            Require(ReadLine(crashed) == "primary", "Crash probe failed to acquire ownership.");
            WaitForExit(crashed);
        }
        using SingleInstanceCoordinator recovered = new(name, activation);
        Require(recovered.IsPrimaryInstance, "An abandoned mutex prevented recovery.");
        Console.WriteLine("SINGLE_INSTANCE_OK (startup activation, repeated launches, restart, abandoned owner)");
    }

    private static Process StartProbe(string name, string activation, string mode)
    {
        string executable = Environment.ProcessPath!;
        ProcessStartInfo start = new(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        foreach (string arg in new[] { "--single-instance-probe", name, activation, mode })
            start.ArgumentList.Add(arg);
        return Process.Start(start)!;
    }

    internal static void RequestActivation(string name, string activation)
    {
        using Process process = StartProbe(name, activation, "signal");
        Require(ReadLine(process) == "secondary", "The activation probe unexpectedly became primary.");
        WaitForExit(process);
    }

    private static string? ReadLine(Process process) => process.StandardOutput.ReadLineAsync()
        .WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();

    private static void WaitForExit(Process process)
    {
        if (!process.WaitForExit(10000))
        {
            process.Kill();
            throw new TimeoutException("Single-instance probe did not exit.");
        }
        Require(process.ExitCode == 0, "Probe failed: " + process.StandardError.ReadToEnd());
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
