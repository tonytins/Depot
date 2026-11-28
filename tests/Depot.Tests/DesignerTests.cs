using Avalonia;
using Avalonia.Controls;
using Depot.Designer;

namespace Depot.Tests;

/// <summary>Designing in the window, drawn by a stand-in for depot-render: select, move, resize, inspect, and undo.</summary>
public sealed class DesignerTests : IDisposable
{
    private const string Budget = """
        <UIScript version="1">
          <Window width="200" height="100"/>
          <Button name="ok" x="10" y="20" width="80" height="24" text="OK"/>
        </UIScript>
        """;

    private readonly TemporaryFolder folder = new();

    public void Dispose() => folder.Dispose();

    private (MainWindow Window, FakeRenderer Renderer) Open(string text = Budget)
    {
        FakeRenderer renderer = new();
        MainWindow window = new(renderer.RenderAsync);
        Assert.True(window.Open(folder.File("budget.uix", text)));
        Assert.NotNull(window.Map);
        return (window, renderer);
    }

    private static string StatusOf(MainWindow window) => window.Get<TextBlock>("Status").Text ?? "";

    // Enter commits an inspector field, as leaving it does.
    private static void PressEnter(TextBox box) =>
        box.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });

    [Fact]
    public void AClickSelectsWhatsThereAndTheInspectorShowsIt() => TestApp.Run(() =>
    {
        var (window, _) = Open();

        window.PressCanvas(new Point(20, 60));
        window.ReleaseCanvas();

        Assert.Equal(1, window.Selection);
        Assert.Equal("ok", window.Inspector.Fields["name"].Editor is TextBox box ? box.Text : null);
        Assert.Equal(1, window.Get<ListBox>("Outline").SelectedIndex);

        window.PressCanvas(new Point(150, 100));
        window.ReleaseCanvas();
        Assert.Equal(0, window.Selection);
    });

    [Fact]
    public void ADragMovesAndOneUndoTakesItBack() => TestApp.Run(() =>
    {
        var (window, renderer) = Open();

        window.PressCanvas(new Point(20, 60));
        window.DragCanvas(new Point(25, 62));
        window.DragCanvas(new Point(30, 64));
        Assert.Equal(Budget, window.Text); // the text waits for the drag to end
        Assert.Contains(renderer.Requests, request => request.Document!.Contains("x=\"20\"", StringComparison.Ordinal));
        window.ReleaseCanvas();

        Assert.Equal(Budget.Replace("x=\"10\" y=\"20\"", "x=\"20\" y=\"24\""), window.Text);
        Assert.Equal(new Depot.Rendering.FrameRect(20, 54, 80, 24), window.Map!.BoundsOf(1));

        window.Undo();
        Assert.Equal(Budget, window.Text);
    });

    [Fact]
    public void AHandleResizes() => TestApp.Run(() =>
    {
        var (window, _) = Open();
        window.Select(1);

        // The bottom-right handle of the button at (10, 50) to (90, 74).
        window.PressCanvas(new Point(90, 74));
        window.DragCanvas(new Point(110, 80));
        window.ReleaseCanvas();

        Assert.Contains("width=\"100\" height=\"30\"", window.Text);
        Assert.Contains("x=\"10\" y=\"20\"", window.Text);
    });

    [Fact]
    public void TheWindowResizesByItsEdge() => TestApp.Run(() =>
    {
        var (window, _) = Open();
        window.Select(0);

        window.PressCanvas(new Point(200, 80));
        window.DragCanvas(new Point(240, 80));
        window.ReleaseCanvas();

        Assert.Contains("<Window width=\"240\" height=\"100\"/>", window.Text);
    });

    [Fact]
    public void ArrowsNudgeAndDeleteAndDuplicateEdit() => TestApp.Run(() =>
    {
        var (window, _) = Open();
        window.Select(1);

        Assert.True(window.Nudge(1, 0));
        Assert.Contains("x=\"11\"", window.Text);

        Assert.True(window.DuplicateSelection());
        Assert.Equal(2, window.Selection);
        Assert.Contains("name=\"ok2\"", window.Text);

        Assert.True(window.DeleteSelection());
        Assert.Null(window.Selection);
        Assert.DoesNotContain("ok2", window.Text);
    });

    [Fact]
    public void AddingSelectsWhatsAdded() => TestApp.Run(() =>
    {
        var (window, _) = Open();

        Assert.True(window.Add("Slider"));

        Assert.Equal(2, window.Selection);
        Assert.Contains("<Slider name=\"slider\"", window.Text);
        Assert.NotNull(window.Map!.BoundsOf(2));
    });

    [Fact]
    public void TheInspectorEditsAndSaysWhyAChangeIsRefused() => TestApp.Run(() =>
    {
        var (window, _) = Open();
        window.Select(1);
        var text = Assert.IsType<TextBox>(window.Inspector.Fields["text"].Editor);
        var width = Assert.IsType<TextBox>(window.Inspector.Fields["width"].Editor);

        text.Text = "Done";
        PressEnter(text);
        Assert.Contains("text=\"Done\"", window.Text);

        width.Text = "wide";
        PressEnter(width);
        Assert.NotNull(window.Inspector.Fields["width"].Problem);
        Assert.Contains("width=\"80\"", window.Text);
    });

    [Fact]
    public void TheInspectorLeavesOutWhatHudiniSkips() => TestApp.Run(() =>
    {
        var (window, _) = Open(Budget.Replace("<Window width=\"200\" height=\"100\"/>", "<Window width=\"200\" height=\"100\" subtitle=\"Hi\"/>"));
        window.Select(0);

        // Kept, under Other, since the file has it, but not offered as something Hudini reads.
        Assert.Contains("subtitle", window.Inspector.Fields.Keys);
        Assert.DoesNotContain(Interface.HudiniSubset.For("Window"), info => info.Name == "subtitle");
    });

    [Fact]
    public void ADocumentHudiniRefusesIsEditedOnlyAsText() => TestApp.Run(() =>
    {
        FakeRenderer renderer = new();
        MainWindow window = new(renderer.RenderAsync);
        window.Open(folder.File("menus.uix", Budget.Replace("</UIScript>", "<Menu name=\"m\" title=\"M\"/></UIScript>")));

        Assert.Null(window.Map);
        Assert.False(window.Add("Button"));
        Assert.Contains("outside the subset Hudini reads", window.Get<TextBlock>("InspectorHint").Text);
        Assert.False(window.Get<Button>("AddButton").IsEnabled);
    });

    [Fact]
    public void SavingTextHudiniRefusesSavesItAndWarns() => TestApp.Run(() =>
    {
        var (window, _) = Open();
        var editor = window.Get<AvaloniaEdit.TextEditor>("Editor");

        editor.Document.Replace(0, editor.Text.Length, "<UIScript version=\"1\"><Window width=\"wide\" height=\"1\"/></UIScript>");
        Assert.True(window.Save());

        Assert.Matches(Said.Pattern(TextKeys.StatusSavedUnreadable), StatusOf(window));
        Assert.Contains("problem", window.Get<TextBlock>("Status").Classes);
    });

    [Fact]
    public void AnEditHudiniWouldRefuseIsntMade() => TestApp.Run(() =>
    {
        var (window, _) = Open();
        window.Select(0);

        Assert.False(window.DeleteSelection());

        Assert.Equal(Budget, window.Text);
        Assert.Matches(Said.Pattern(TextKeys.StatusRefused), StatusOf(window));
    });
}
