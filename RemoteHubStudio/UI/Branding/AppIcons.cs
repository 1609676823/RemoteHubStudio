namespace RemoteHubStudio.UI.Branding;

/// <summary>Owns the shared embedded application artwork for the process lifetime. / 在进程生命周期内持有共享的内嵌应用图标。</summary>
internal static class AppIcons
{
    internal static Icon Application { get; } = LoadIcon("remotehubstudio.ico", SystemInformation.IconSize);
    internal static Icon Tray { get; } = LoadIcon("remotehubstudio-tray.ico", SystemInformation.SmallIconSize);
    internal static string LogoSvg { get; } = LoadText("remotehubstudio.svg");

    private static Icon LoadIcon(string name, Size size)
    {
        using Stream stream = Open(name);
        using Icon icon = new(stream, size);
        return (Icon)icon.Clone();
    }

    private static string LoadText(string name)
    {
        using StreamReader reader = new(Open(name));
        return reader.ReadToEnd();
    }

    private static Stream Open(string name) => typeof(AppIcons).Assembly
        .GetManifestResourceStream("RemoteHubStudio.Assets." + name)
        ?? throw new InvalidOperationException($"Missing application artwork: {name}");
}
