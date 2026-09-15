using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RemoteHubStudio.Domain;
using RemoteHubStudio.UI.Controls;
using RemoteHubStudio.UI.Dialogs;
using RemoteHubStudio.UI.Dialogs.ConnectionEditors;

namespace RemoteHubStudio.Tests;

/// <summary>
/// Replays only InitializeComponent on a fresh derived base, as the source designer does.
/// Constructor smoke tests alone cannot detect controls created outside InitializeComponent.
/// / 在新建的基类上仅重放 InitializeComponent，覆盖构造函数烟雾测试无法发现的空白设计页。
/// </summary>
internal static class DesignerSourceRegression
{
    internal static void Run()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                Type[] types = typeof(ResponsiveDialogWindow).Assembly.GetTypes()
                    .Where(type => !type.IsAbstract && typeof(Control).IsAssignableFrom(type) &&
                        type.GetMethod("InitializeComponent", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) is not null)
                    .OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
                foreach (Type type in types)
                {
                    VerifySourceTree(type);
                }

                VerifyUnregisteredGridSurvivesDpiChange();
                VerifyDesignerDoesNotRepositionDialog();
                VerifyPrecreatedRdpPagePreservesProfile();
                Console.WriteLine($"DESIGNER_SOURCE_OK ({types.Length} surfaces)");
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }) { IsBackground = true, Name = "WinForms source designer regression" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60)))
        {
            throw new TimeoutException("Source designer regression timed out.");
        }

        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void VerifySourceTree(Type type)
    {
        using Stream stream = typeof(DesignerSourceRegression).Assembly
            .GetManifestResourceStream($"DesignerSources.{type.Name}.Designer.cs")
            ?? throw new InvalidOperationException($"Missing designer source for {type.Name}.");
        using StreamReader reader = new(stream);
        CompilationUnitSyntax syntax = CSharpSyntaxTree.ParseText(reader.ReadToEnd()).GetCompilationUnitRoot();
        MethodDeclarationSyntax initialize = syntax.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(method => method.Identifier.ValueText == "InitializeComponent");
        if (typeof(AntdUI.Window).IsAssignableFrom(type))
        {
            Require(!initialize.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                .Any(assignment => assignment.Left is IdentifierNameSyntax { Identifier.ValueText: "Size" }),
                $"{type.Name}: AntdUI.Window hides Size from serialization; serialize ClientSize instead.");
        }
        // Event handlers belong to the edited source type. Their bodies are deliberately not executed.
        // / 事件处理程序属于被编辑类型；重放不执行业务事件处理代码。
        StatementSyntax[] statements = initialize.Body!.Statements
            .Where(statement => statement is not ExpressionStatementSyntax
            {
                Expression: AssignmentExpressionSyntax assignment
            } || !assignment.IsKind(SyntaxKind.AddAssignmentExpression)).ToArray();
        string body = initialize.WithBody(SyntaxFactory.Block(statements)).ToFullString();
        FieldInfo[] componentFields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(field => typeof(IComponent).IsAssignableFrom(field.FieldType) || typeof(IContainer).IsAssignableFrom(field.FieldType)).ToArray();
        string fields = string.Join("\n", componentFields.Select(field => $"private global::{field.FieldType.FullName} {field.Name} = null!;"));
        string code = $$"""
            using System;
            using System.ComponentModel;
            using System.Drawing;
            using System.Windows.Forms;
            using RemoteHubStudio.UI.Controls;
            using RemoteHubStudio.UI.Dialogs.ConnectionEditors;
            public sealed class SourcePreview : global::{{type.BaseType!.FullName}}
            {
                {{fields}}
                public SourcePreview() { InitializeComponent(); }
                {{body}}
            }
            """;
        string[] references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Concat([typeof(Control).Assembly.Location, typeof(ResponsiveDialogWindow).Assembly.Location, typeof(AntdUI.Input).Assembly.Location])
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        CSharpCompilation compilation = CSharpCompilation.Create(
            "DesignerPreview_" + type.Name,
            [CSharpSyntaxTree.ParseText(code)],
            references.Select(reference => MetadataReference.CreateFromFile(reference)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using MemoryStream assemblyStream = new();
        Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(assemblyStream);
        Require(result.Success, $"{type.Name}: source layout failed to compile: {string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");
        Type previewType = Assembly.Load(assemblyStream.ToArray()).GetType("SourcePreview")!;
        using Control root = LicenseManager.CreateWithContext(previewType, new DesignLicenseContext()) as Control
            ?? throw new InvalidOperationException($"Cannot create source preview for {type.Name}.");
        root.CreateControl();
        _ = root.Handle;
        LayoutTree(root);
        HashSet<Control> controls = Descendants(root).ToHashSet();
        foreach (FieldInfo field in previewType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .Where(field => typeof(Control).IsAssignableFrom(field.FieldType) && !typeof(ToolStripDropDown).IsAssignableFrom(field.FieldType)))
        {
            Require(field.GetValue(root) is Control control && controls.Contains(control),
                $"{type.Name}.{field.Name}: missing from the source designer tree (constructor-only creation or lost parent).");
        }

        foreach (ResponsiveFieldGrid grid in controls.OfType<ResponsiveFieldGrid>())
        {
            int count = grid.Controls.Count;
            RaiseDpiChanged(grid);
            Require(grid.Controls.Count == count, $"{type.Name}.{grid.Name}: DPI notification erased source controls.");
        }

        if (type != typeof(ConnectionTypeOptionsPage))
        {
            Require(controls.Count > 0, $"{type.Name}: empty source designer surface.");
        }
        foreach (Control leaf in controls.Where(control => control is AntdUI.Input or AntdUI.Button or AntdUI.Switch or AntdUI.Label))
        {
            Require(leaf.Width > 0 && leaf.Height > 0, $"{type.Name}.{leaf.Name}: zero-size control {leaf.Bounds}.");
            if (leaf.Name != "_moreButton" && leaf.Parent is Control parent)
            {
                Require(parent.ClientRectangle.Contains(leaf.Bounds),
                    $"{type.Name}.{leaf.Name}: clipped by {parent.Name}: {leaf.Bounds} outside {parent.ClientRectangle}.");
            }
        }

        // Optional local artifacts let the audit inspect the exact source-replayed tree without launching app services.
        // / 可选本地截图只绘制重放后的控件树，不启动应用服务。
        string? output = Environment.GetEnvironmentVariable("REMOTEHUB_DESIGNER_AUDIT_DIR");
        if (!string.IsNullOrEmpty(output))
        {
            Directory.CreateDirectory(output);
            using Panel renderHost = new() { Size = root.Size };
            using Container designContainer = new();
            if (root is Form form)
            {
                form.TopLevel = false;
                form.Site = new DesignSite(form, designContainer);
            }
            renderHost.Controls.Add(root);
            root.Visible = true;
            renderHost.CreateControl();
            LayoutTree(root);
            using Bitmap bitmap = new(root.Width, root.Height);
            root.DrawToBitmap(bitmap, new Rectangle(Point.Empty, root.Size));
            bitmap.Save(Path.Combine(output, type.Name + ".png"));
        }
        Console.WriteLine($"DESIGNER_SOURCE {type.Name}: {controls.Count} controls");
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void LayoutTree(Control root)
    {
        root.CreateControl();
        root.PerformLayout();
        foreach (Control child in root.Controls) LayoutTree(child);
        root.PerformLayout();
    }

    private static void VerifyUnregisteredGridSurvivesDpiChange()
    {
        using ResponsiveFieldGrid grid = new();
        using AntdUI.Input input = new();
        using AntdUI.Label label = new();
        grid.Controls.Add(label, 0, 0);
        grid.Controls.Add(input, 1, 0);
        grid.Width = 900;
        RaiseDpiChanged(grid);
        Require(grid.Controls.Count == 2, "DPI changes before Site/field registration must preserve designer controls.");
    }

    private static void RaiseDpiChanged(ResponsiveFieldGrid grid) =>
        typeof(ResponsiveFieldGrid).GetMethod("OnDpiChangedAfterParent", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(grid, [EventArgs.Empty]);

    private static void VerifyDesignerDoesNotRepositionDialog()
    {
        using ResponsiveDialogWindow dialog = new();
        using Container container = new();
        container.Add(dialog);
        dialog.Site = new DesignSite(dialog, container);
        dialog.Location = new Point(15, 15);
        Size before = dialog.Size;
        typeof(ResponsiveDialogWindow).GetMethod("OnShown", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dialog, [EventArgs.Empty]);
        Require(dialog.Location == new Point(15, 15) && dialog.Size == before, "Design-time OnShown must not center or resize the source surface.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void VerifyPrecreatedRdpPagePreservesProfile()
    {
        // The source designer needs a real RDP child. At runtime that same child must
        // receive the saved profile, even though the lazy factory no longer creates it.
        // / 设计器预置的 RDP 页必须继续载入已保存配置，不能保留新建页的默认值。
        ConnectionProfile saved = new()
        {
            Name = "Designer regression",
            Host = "designer-regression.invalid",
            Port = 3395,
            Username = "sample-user",
            Password = "sample-value",
            Rdp = new RdpOptions { FullScreen = false, DesktopWidth = 1920, DesktopHeight = 1080, RedirectDrives = true },
            Options = new() { ["audit-option"] = "preserved" }
        };
        using ConnectionEditorForm dialog = new(saved, null, ConnectionEditorMode.Edit);
        MethodInfo createResult = typeof(ConnectionEditorForm).GetMethod("TryCreateResult", BindingFlags.Instance | BindingFlags.NonPublic)!;
        void VerifyResult()
        {
            object?[] arguments = [null];
            Require(createResult.Invoke(dialog, arguments) is true, "Precreated RDP page must accept a valid saved profile.");
            ConnectionProfile actual = (ConnectionProfile)arguments[0]!;
            Require(actual.Host == saved.Host && actual.Port == saved.Port && actual.Username == saved.Username && actual.Password == saved.Password,
                "Precreated RDP page lost saved endpoint/authentication values.");
            Require(!actual.Rdp.FullScreen && actual.Rdp.DesktopWidth == 1920 && actual.Rdp.DesktopHeight == 1080 && actual.Rdp.RedirectDrives,
                "Precreated RDP page lost saved session options.");
            Require(actual.Options.TryGetValue("audit-option", out string? value) && value == "preserved",
                "Precreated RDP page lost the raw option draft.");
        }
        VerifyResult();
        AntdUI.Select typeSelect = (AntdUI.Select)typeof(ConnectionEditorForm)
            .GetField("_typeSelect", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)!;
        typeSelect.SelectedValue = ConnectionType.Putty;
        typeSelect.SelectedValue = ConnectionType.RemoteDesktop;
        VerifyResult();
    }

    private sealed class DesignLicenseContext : LicenseContext
    {
        public override LicenseUsageMode UsageMode => LicenseUsageMode.Designtime;
    }

    private sealed class DesignSite(IComponent component, IContainer container) : ISite
    {
        public IComponent Component => component;
        public IContainer Container => container;
        public bool DesignMode => true;
        public string? Name { get; set; }
        public object? GetService(Type serviceType) => null;
    }
}
