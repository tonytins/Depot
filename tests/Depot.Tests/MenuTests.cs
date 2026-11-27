using Avalonia.Controls;
using Avalonia.Input;
using Depot.Menus;
using Depot.Rendering;

namespace Depot.Tests;

/// <summary>Depot's menus: the standard ones, what they enable, and the two forms they're shown in.</summary>
public sealed class MenuTests : IDisposable
{
    private const string Budget = """
        <UIScript version="1">
          <Window width="200" height="100"/>
          <Button name="ok" x="10" y="20" width="80" height="24" text="OK"/>
        </UIScript>
        """;

    private readonly TemporaryFolder folder = new();

    public void Dispose() => folder.Dispose();

    private static MenuCommand Command(MainWindow window, string menu, string title) =>
        AppMenu.Find(window.Menus, menu, title) ?? throw new InvalidOperationException($"There's no {menu} ▸ {title}.");

    private static bool Enabled(MainWindow window, string menu, string title) => Command(window, menu, title).IsEnabled?.Invoke() ?? true;

    [Fact]
    public void TheMenusAreTheStandardOnesAndDepotsOwn() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);

        Assert.Equal(["File", "Edit", "Insert", "Arrange", "View", "Help"], window.Menus.Select(menu => menu.Title));
        Assert.Equal(new KeyGesture(Key.O, KeyModifiers.Control), Command(window, "File", "Open…").Gesture);
        Assert.Equal(new KeyGesture(Key.W, KeyModifiers.Control), Command(window, "File", "Close Window").Gesture);
        Assert.Equal(new KeyGesture(Key.Z, KeyModifiers.Control | KeyModifiers.Shift), Command(window, "Edit", "Redo").Gesture);
    });

    [Fact]
    public void WithNothingOpenOnlyWhatNeedsNoDocumentCanRun() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);

        Assert.True(Enabled(window, "File", "Open…"));
        Assert.False(Enabled(window, "File", "Save"));
        Assert.False(Enabled(window, "Edit", "Undo"));
        Assert.False(Enabled(window, "Edit", "Delete"));
        Assert.False(Enabled(window, "Insert", "Button"));
        Assert.False(Enabled(window, "Arrange", "Bring Forward"));
    });

    [Fact]
    public void SelectingAnElementLetsItBeEditedAndArranged() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);
        window.Open(folder.File("budget.uix", Budget));
        Assert.True(Enabled(window, "Insert", "Button"));
        Assert.False(Enabled(window, "Edit", "Duplicate"));

        window.Select(1);

        Assert.True(Enabled(window, "Edit", "Duplicate"));
        Assert.True(Enabled(window, "Arrange", "Send Backward"));
        Command(window, "Edit", "Duplicate").Run();
        Assert.Contains("ok2", window.Text);
        Assert.True(Enabled(window, "File", "Save"));
    });

    [Fact]
    public void ViewChoosesTheLookAndShowsWhichIsChosen() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);

        Command(window, "View", "Depot Light").Run();

        Assert.Equal(CanvasLook.DepotLight, window.Look);
        Assert.True(Command(window, "View", "Depot Light").IsChecked!());
        Assert.False(Command(window, "View", "Depot").IsChecked!());
    });

    [Fact]
    public void TheGlobalMenuBarGetsTheSameMenus() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);
        window.Open(folder.File("budget.uix", Budget));
        window.Get<AvaloniaEdit.TextEditor>("Editor").Document.Insert(0, "<!-- edited -->\n");

        var native = NativeMenu.GetMenu(window)!;
        Assert.Equal(["File", "Edit", "Insert", "Arrange", "View", "Help"], native.Items.OfType<NativeMenuItem>().Select(item => item.Header));
        var file = native.Items.OfType<NativeMenuItem>().First().Menu!;
        // Filled again as the text changed, so Save is on.
        Assert.True(file.Items.OfType<NativeMenuItem>().Single(item => item.Header == "Save").IsEnabled);
    });

    [Fact]
    public void ControlSSavesFromAnywhereInTheWindow() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);
        var path = folder.File("budget.uix", Budget);
        window.Open(path);
        window.Get<AvaloniaEdit.TextEditor>("Editor").Document.Insert(0, "<!-- edited -->\n");

        var binding = window.KeyBindings.Single(binding => binding.Gesture == new KeyGesture(Key.S, KeyModifiers.Control));
        binding.Command!.Execute(null);

        Assert.StartsWith("<!-- edited -->", File.ReadAllText(path));
    });

    [Fact]
    public void AFlyoutHasTheMenusAsTheyAreNow() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);

        var flyout = AppMenu.Flyout(window.Menus);
        var file = flyout.Items.OfType<MenuItem>().First();
        var save = file.Items.OfType<MenuItem>().Single(item => (string?)item.Header == "Save");

        Assert.False(save.IsEnabled);
        Assert.Equal(new KeyGesture(Key.S, KeyModifiers.Control), save.InputGesture);
    });
}
