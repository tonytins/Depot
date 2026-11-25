using Avalonia.Controls;
using AvaloniaEdit;
using Depot.Rendering;

namespace Depot.Tests;

/// <summary>Depot's window, off-screen: opening, editing, and saving, and the canvas when depot-render is there.</summary>
public sealed class MainWindowTests : IDisposable
{
    private const string Budget = """
        <UIScript version="1">
          <Window width="200" height="100"/>
          <Button name="ok" x="10" y="20" width="80" height="24" text="OK"/>
        </UIScript>
        """;

    private readonly TemporaryFolder folder = new();

    public void Dispose() => folder.Dispose();

    private static string StatusOf(MainWindow window) => window.Get<TextBlock>("Status").Text ?? "";

    [Fact]
    public void OpensAUIScriptDocumentToEdit() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);
        var path = folder.File("budget.uix", Budget);

        Assert.True(window.Open(path));

        Assert.Equal(Budget, window.Text);
        Assert.False(window.Get<TextEditor>("Editor").IsReadOnly);
        Assert.Equal("budget.uix — Depot", window.Title);
        Assert.False(window.HasUnsavedChanges);
        Assert.False(window.Get<Button>("SaveButton").IsEnabled);
    });

    [Fact]
    public void EditsAreMarkedUntilSaved() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);
        var path = folder.File("budget.uix", Budget);
        window.Open(path);

        window.Get<TextEditor>("Editor").Document.Insert(0, "<!-- edited -->\n");

        Assert.True(window.HasUnsavedChanges);
        Assert.Equal("budget.uix (edited) — Depot", window.Title);
        Assert.True(window.Get<Button>("SaveButton").IsEnabled);

        Assert.True(window.Save());
        Assert.Equal("<!-- edited -->\n" + Budget, File.ReadAllText(path));
        Assert.Equal("budget.uix — Depot", window.Title);
        Assert.False(window.Get<Button>("SaveButton").IsEnabled);
    });

    [Theory]
    [InlineData(UnsavedChoice.Cancel, true, false)]
    [InlineData(UnsavedChoice.DontSave, false, false)]
    [InlineData(UnsavedChoice.Save, false, true)]
    public void ClosingWithUnsavedChangesAsksFirst(UnsavedChoice choice, bool staysOpen, bool saved) => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);
        var path = folder.File("budget.uix", Budget);
        window.Open(path);
        window.Show();
        window.Get<TextEditor>("Editor").Document.Insert(0, "<!-- edited -->\n");

        window.Close();
        TestApp.RunUntil(() => window.OwnedWindows.Count == 1, TimeSpan.FromSeconds(5));
        Assert.True(window.IsVisible);

        Assert.IsType<UnsavedDialog>(window.OwnedWindows[0]).Close(choice);
        TestApp.RunUntil(() => window.OwnedWindows.Count == 0, TimeSpan.FromSeconds(5));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(staysOpen, window.IsVisible);
        Assert.Equal(saved, File.ReadAllText(path).StartsWith("<!-- edited -->", StringComparison.Ordinal));
    });

    [Fact]
    public void UisFilesAreShownReadOnly() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);

        Assert.True(window.Open(folder.File("stand.uis", "<Begin>\n")));

        Assert.True(window.Get<TextEditor>("Editor").IsReadOnly);
        Assert.False(window.HasUnsavedChanges);
        Assert.False(window.Save());
        Assert.Contains("doesn't change it", StatusOf(window));
    });

    [Fact]
    public void ADocumentThatWontOpenLeavesTheOpenOneAlone() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);
        window.Open(folder.File("budget.uix", Budget));

        Assert.False(window.Open(Path.Combine(folder.Path, "missing.uix")));

        Assert.Equal(Budget, window.Text);
        Assert.Contains("There's no file", StatusOf(window));
        Assert.Contains("problem", window.Get<TextBlock>("Status").Classes);
    });

    [Fact]
    public void WithoutDepotRenderTheCanvasSaysSo() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);

        window.Open(folder.File("budget.uix", Budget));

        Assert.Contains("didn't find depot-render", window.Get<TextBlock>("CanvasMessage").Text);
    });

    [Fact]
    public void TheCanvasStartsInTheDepotLook() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);

        Assert.Equal(CanvasLook.Depot, window.Look);
        Assert.Equal(["Depot", "Depot Light", "Hudini Standard"], window.Get<ComboBox>("LookPicker").ItemsSource!.Cast<string>());
    });

    [Fact]
    public void TheStatusBarSaysWhatHudiniLeftOut()
    {
        FrameMap frames = new("window", 200, 130, new FrameRect(0, 30, 200, 100), [], ["art"], ["s1", "s2"], ["Button.sparkle (line 3)"], []);

        var said = MainWindow.Describe(frames, document: null);

        Assert.Contains("Hudini ignores Button.sparkle (line 3).", said);
        Assert.Contains("Images not found, outlined in red: art.", said);
        Assert.Contains("2 strings show their table and index", said);
        Assert.Equal("200 × 130, as Hudini draws it.", MainWindow.Describe(frames with { MissingImages = [], MissingStrings = [], IgnoredAttributes = [] }, null));
    }

    // With depot-render built and a display to draw on, the whole way round: the text drawn, a typo reported, and
    // the picture back once it's fixed.
    [Fact]
    public void DrawsAsYouType()
    {
        var renderer = RendererLocation.Find();
        Assert.SkipWhen(renderer is null, "depot-render isn't built.");
        Assert.SkipWhen(
            !OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS() && Environment.GetEnvironmentVariable("DISPLAY") is null,
            "depot-render needs a display.");

        TestApp.Run(() =>
        {
            MainWindow window = new(renderer);
            List<RenderReply> replies = [];
            window.Answered += (_, reply) => replies.Add(reply);

            window.Open(folder.File("budget.uix", Budget));
            TestApp.RunUntil(() => replies.Count == 1, TimeSpan.FromSeconds(20));
            Assert.Null(replies[0].Error);
            Assert.Equal("ok", Assert.Single(window.Frames!.Elements).Name);
            Assert.NotNull(window.Get<Avalonia.Controls.Image>("Canvas").Source);

            var editor = window.Get<TextEditor>("Editor");
            editor.Document.Replace(0, editor.Text.Length, Budget.Replace("</UIScript>", "</UIScrip>"));
            TestApp.RunUntil(() => replies.Count == 2, TimeSpan.FromSeconds(20));
            Assert.NotNull(replies[1].Error);
            Assert.Contains("problem", window.Get<TextBlock>("Status").Classes);

            editor.Document.Replace(0, editor.Text.Length, Budget.Replace("width=\"80\"", "width=\"120\""));
            TestApp.RunUntil(() => replies.Count == 3, TimeSpan.FromSeconds(20));
            Assert.Equal(120, Assert.Single(window.Frames!.Elements).Frame.Width);
            window.Close();
        });
    }
}
