namespace RemoteHubStudio.UI.Theme;

/// <summary>
/// Defines the semantic colors shared by AntdUI controls and native WinForms hosts. / 定义 AntdUI 控件与原生 WinForms 容器共享的语义颜色。
/// </summary>
public sealed record ThemePalette
{
    public required Color Primary { get; init; }

    public required Color Success { get; init; }

    public required Color Warning { get; init; }

    public required Color Error { get; init; }

    public required Color Info { get; init; }

    public required Color TextPrimary { get; init; }

    public required Color TextSecondary { get; init; }

    public required Color TextTertiary { get; init; }

    public required Color TextDisabled { get; init; }

    public required Color WindowBackground { get; init; }

    public required Color LayoutBackground { get; init; }

    public required Color ContainerBackground { get; init; }

    public required Color ElevatedBackground { get; init; }

    public required Color Fill { get; init; }

    public required Color FillSecondary { get; init; }

    public required Color FillTertiary { get; init; }

    public required Color FillQuaternary { get; init; }

    public required Color Border { get; init; }

    public required Color BorderSubtle { get; init; }

    public required Color BorderDisabled { get; init; }

    public required Color HoverBackground { get; init; }

    public required Color SpotlightBackground { get; init; }

    public required Color SpotlightText { get; init; }

    public required Color SwitchHandleBackground { get; init; }
}
