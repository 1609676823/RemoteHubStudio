using System.Diagnostics;
using System.Collections.ObjectModel;

namespace RemoteHubStudio.Infrastructure.Launch;

/// <summary>
/// Describes one shell-free process launch and its temporary artifacts. / 描述一次不经过命令行外壳的进程启动及其临时文件。
/// </summary>
public sealed class LaunchPlan
{
    private readonly ReadOnlyCollection<string> _arguments;
    private readonly ReadOnlyCollection<string> _temporaryFiles;

    /// <summary>
    /// Initializes an immutable launch plan. / 初始化不可变的启动计划。
    /// </summary>
    /// <param name="executablePath">Resolved executable path. / 已解析的可执行文件路径。</param>
    /// <param name="arguments">Individual process argument tokens. / 独立的进程参数标记。</param>
    /// <param name="temporaryFiles">Temporary files to remove after process exit. / 进程退出后需要删除的临时文件。</param>
    /// <param name="containsSensitiveData">Whether arguments or temporary files contain a password. / 参数或临时文件是否包含密码。</param>
    public LaunchPlan(
        string executablePath,
        IEnumerable<string> arguments,
        IEnumerable<string>? temporaryFiles = null,
        bool containsSensitiveData = false)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path cannot be empty. / 可执行文件路径不能为空。", nameof(executablePath));
        }

        ArgumentNullException.ThrowIfNull(arguments);
        _arguments = Array.AsReadOnly(arguments.Select(argument => argument ?? string.Empty).ToArray());
        _temporaryFiles = Array.AsReadOnly(temporaryFiles?.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray() ?? []);
        ExecutablePath = executablePath;
        ContainsSensitiveData = containsSensitiveData;
    }

    /// <summary>Gets the resolved executable path. / 获取已解析的可执行文件路径。</summary>
    public string ExecutablePath { get; }

    /// <summary>Gets the ordered argument tokens. / 获取有序参数标记。</summary>
    public IReadOnlyList<string> Arguments => _arguments;

    /// <summary>Gets temporary files owned by this launch. / 获取本次启动拥有的临时文件。</summary>
    public IReadOnlyList<string> TemporaryFiles => _temporaryFiles;

    /// <summary>Gets whether this plan contains password material permitted by current settings. / 获取计划是否包含当前设置允许的密码材料。</summary>
    public bool ContainsSensitiveData { get; }

    /// <summary>
    /// Creates a secure process start descriptor using ArgumentList and no shell. / 使用 ArgumentList 且不调用外壳来创建安全的进程启动描述。
    /// </summary>
    /// <returns>Configured process start information. / 配置完成的进程启动信息。</returns>
    public ProcessStartInfo CreateStartInfo()
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = ExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = false
        };

        string? executableDirectory = Path.GetDirectoryName(ExecutablePath);
        if (!string.IsNullOrWhiteSpace(executableDirectory) && Directory.Exists(executableDirectory))
        {
            startInfo.WorkingDirectory = executableDirectory;
        }

        foreach (string argument in _arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
