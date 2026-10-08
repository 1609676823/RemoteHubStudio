using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using RemoteHubStudio.Application;
using RemoteHubStudio.Domain;
using RemoteHubStudio.Localization;
using RemoteHubStudio.UI.Controls;
using RemoteHubStudio.UI.Dialogs;
using RemoteHubStudio.UI.Dialogs.ConnectionEditors;
using RemoteHubStudio.UI.Main;

namespace RemoteHubStudio.Tests;

/// <summary>Verifies saved ordering through the real table and connection editor. / 通过真实表格与连接编辑器验证保存的排序。</summary>
internal static class ConnectionSortOrderUiRegression
{
    internal static void Run()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            string language = L.RequestedLanguage;
            try
            {
                VerifyTableOrdering();
                VerifyLegacyOrdering();
                foreach (string locale in new[] { "en", "zh-Hans" })
                {
                    L.SetLanguage(locale);
                    VerifyEditor();
                }
            }
            catch (Exception exception) { failure = exception; }
            finally { L.SetLanguage(language); }
        }) { IsBackground = true, Name = "Connection sort-order UI regression" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("Connection sort-order UI regression timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        Console.WriteLine("CONNECTION_SORT_ORDER_UI_OK (table ordering, legacy defaults, selection, search, editor Int32 values and responsive layout in 2 languages)");
    }

    private static void VerifyTableOrdering()
    {
        ConnectionProfile favoriteLater = Profile("Match favorite later", 20, favorite: true);
        ConnectionProfile favoriteFirst = Profile("Match favorite first", -2, favorite: true);
        ConnectionProfile minimum = Profile("Match minimum", int.MinValue);
        ConnectionProfile alpha = Profile("Match Alpha", 5);
        ConnectionProfile beta = Profile("Match Beta", 5);
        ConnectionProfile hidden = Profile("Other connection", 0);
        ConnectionProfile maximum = Profile("Match maximum", int.MaxValue);
        using TableFixture fixture = new([maximum, beta, favoriteLater, hidden, minimum, favoriteFirst, alpha]);
        AssertOrder(fixture.Form, favoriteFirst, favoriteLater, minimum, hidden, alpha, beta, maximum);

        Invoke(fixture.Form, "SelectConnection", beta.Id);
        ConnectionProfile changed = fixture.Workspace.GetConnection(beta.Id)!;
        changed.SortOrder = -10;
        Require(fixture.Workspace.UpdateConnectionAsync(changed).GetAwaiter().GetResult(), "Updating a selected connection failed.");
        Invoke(fixture.Form, "RefreshConnectionTable");
        AssertOrder(fixture.Form, favoriteFirst, favoriteLater, minimum, beta, hidden, alpha, maximum);
        Require(((IReadOnlyList<Guid>)Invoke(fixture.Form, "GetSelectedConnectionIds")!).SequenceEqual([beta.Id]),
            "Changing sort order moved selection to another connection.");

        Field<AntdUI.Input>(fixture.Form, "_searchInput").Text = "Match";
        Invoke(fixture.Form, "RefreshConnectionTable");
        AssertOrder(fixture.Form, favoriteFirst, favoriteLater, minimum, beta, alpha, maximum);
        Require(((IReadOnlyList<Guid>)Invoke(fixture.Form, "GetSelectedConnectionIds")!).SequenceEqual([beta.Id]),
            "Filtering lost the still-visible selected connection.");
    }

    private static void VerifyLegacyOrdering()
    {
        ConnectionProfile beta = new() { Name = "Beta", Host = "legacy.invalid" };
        ConnectionProfile alpha = new() { Name = "Alpha", Host = "legacy.invalid" };
        ConnectionProfile favorite = new() { Name = "Zulu", Host = "legacy.invalid", IsFavorite = true };
        using TableFixture fixture = new([beta, alpha, favorite]);
        AssertOrder(fixture.Form, favorite, alpha, beta);
    }

    private static void VerifyEditor()
    {
        using (ConnectionEditorForm add = new(null, [], ConnectionEditorMode.Add))
        {
            AntdUI.InputNumber input = Field<AntdUI.InputNumber>(add, "_sortOrderInput");
            Require(input.Value == 0, "New connection ordering did not default to zero.");
            Field<AntdUI.Input>(add, "_nameInput").Text = "New connection";
            Field<RdpConnectionTypeOptionsPage>(add, "_rdpPage").LoadFrom(Profile("New connection", 0));
            Require(CreateResult(add).SortOrder == 0, "The new connection result did not retain the default order.");
            VerifyEditorLayout(add);
        }

        foreach (int value in new[] { -25, int.MinValue, int.MaxValue })
        {
            ConnectionProfile original = Profile("Edited connection", value);
            using ConnectionEditorForm edit = new(original, [], ConnectionEditorMode.Edit);
            AntdUI.InputNumber input = Field<AntdUI.InputNumber>(edit, "_sortOrderInput");
            Require(input.Value == value && CreateResult(edit).SortOrder == value,
                $"Opening or saving the editor changed order {value}.");
            int replacement = value == int.MaxValue ? int.MinValue : int.MaxValue;
            input.Value = replacement;
            ConnectionProfile result = CreateResult(edit);
            Require(result.SortOrder == replacement && result.Id == original.Id && original.SortOrder == value,
                "Editing the order did not preserve the new value and original connection identity.");
        }
    }

    private static void VerifyEditorLayout(ConnectionEditorForm form)
    {
        ResponsiveFieldGrid grid = Field<ResponsiveFieldGrid>(form, "_basicsGrid");
        AntdUI.InputNumber input = Field<AntdUI.InputNumber>(form, "_sortOrderInput");
        AntdUI.Label label = Field<AntdUI.Label>(form, "_sortOrderLabel");
        Require(input.Minimum == int.MinValue && input.Maximum == int.MaxValue && input.DecimalPlaces == 0,
            "The order editor cannot represent the complete integer range.");
        Require(!input.WheelModifyEnabled && !string.IsNullOrWhiteSpace(input.AccessibleName) &&
                !string.IsNullOrWhiteSpace(input.AccessibleDescription),
            "The order editor is missing its accessible explanation or permits accidental wheel changes.");

        foreach (int width in new[] { 1180, 680 })
        {
            float scale = form.DeviceDpi <= 0 ? 1F : form.DeviceDpi / 96F;
            form.ClientSize = new Size((int)Math.Round(width * scale), (int)Math.Round(760 * scale));
            LayoutTree(form);
            form.PerformLayout();
            grid.PerformLayout();
            Require(grid.ColumnCount == (width > 1000 ? 4 : 2), "The editor did not reflow between wide and narrow layouts.");
            Require(input.Parent == grid && label.Parent == grid && input.Width > 0 && input.Height > 0 &&
                    grid.ClientRectangle.Contains(input.Bounds) && !label.Bounds.IntersectsWith(input.Bounds),
                $"The order editor is clipped or overlaps its label at width {width}.");
        }
    }

    private static void LayoutTree(Control control)
    {
        control.CreateControl();
        _ = control.Handle;
        foreach (Control child in control.Controls) LayoutTree(child);
        control.PerformLayout();
    }

    private static ConnectionProfile CreateResult(ConnectionEditorForm form)
    {
        object?[] arguments = [null];
        Require((bool)Invoke(form, "TryCreateResult", arguments)!, "A valid connection was rejected by the editor.");
        return (ConnectionProfile)arguments[0]!;
    }

    private static ConnectionProfile Profile(string name, int order, bool favorite = false) =>
        new() { Name = name, Host = "sort-order.invalid", SortOrder = order, IsFavorite = favorite };

    private static void AssertOrder(MainForm form, params ConnectionProfile[] expected)
    {
        ConnectionTableRow[] rows = (ConnectionTableRow[])Field<AntdUI.Table>(form, "_connectionTable").DataSource!;
        Require(rows.Select(row => row.Id).SequenceEqual(expected.Select(profile => profile.Id)),
            "Unexpected table order: " + string.Join(", ", rows.Select(row => row.Name)));
    }

    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static object? Invoke(object target, string name, params object?[] arguments) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, arguments);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class TableFixture : IDisposable
    {
        internal MainForm Form { get; } = new();
        internal WorkspaceService Workspace { get; }

        internal TableFixture(List<ConnectionProfile> profiles)
        {
            Workspace = new WorkspaceService(new MemoryRepository(profiles));
            Workspace.InitializeAsync().GetAwaiter().GetResult();
            typeof(MainForm).GetField("_workspace", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Form, Workspace);
            typeof(MainForm).GetField("_expirationService", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Form, new ExpirationService());
            Invoke(Form, "RefreshConnectionTable");
        }

        public void Dispose() => Form.Dispose();
    }

    private sealed class MemoryRepository(List<ConnectionProfile> profiles) : IWorkspaceRepository
    {
        public Task<WorkspaceLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkspaceLoadResult(new AppDataDocument { Connections = profiles }));

        public Task SaveAsync(AppDataDocument document, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
