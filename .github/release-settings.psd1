@{
    # Default for scheduled/tag builds and the "repository-default" manual option.
    # 定时/标签构建及手动选择 repository-default 时使用此配置。
    # self-contained: bundle .NET / 附带运行时，适合面向普通用户发布（推荐）。
    # framework-dependent: require .NET 10 Windows Desktop Runtime / 需要预装桌面运行时。
    # both: publish both variants for each RID / 每种架构同时生成两种部署包。
    DeploymentMode = 'self-contained'

    # This WinForms application only supports these Windows RIDs.
    # x86 = Windows 32-bit process; arm64 is different from unsupported win-arm (32-bit).
    RuntimeIdentifiers = @('win-x86', 'win-x64', 'win-arm64')

    # No RID + UseAppHost=false: Windows-only AnyCPU DLL, always framework-dependent.
    # “可移植”不等于跨操作系统，也不能附带一套通用 CPU 运行时。
    IncludePortable = $true
}
