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

    // Menus and items are found by their keys in the table, so the tests don't depend on how they're worded.
    private static MenuCommand Command(MainWindow window, string menuKey, string itemKey) =>
        Titled(window, menuKey, Words.Get(itemKey));

    // Insert's controls go by their element's name, which isn't in the table.
    private static MenuCommand Insert(MainWindow window, string element) => Titled(window, TextKeys.MenuInsert, element);

    private static MenuCommand Titled(MainWindow window, string menuKey, string title) =>
        AppMenu.Find(window.Menus, Words.Get(menuKey), title) ?? throw new InvalidOperationException($"There's no {menuKey} ▸ {title}.");

    private static bool IsOn(MenuCommand command) => command.IsEnabled?.Invoke() ?? true;

    private static bool Enabled(MainWindow window, string menuKey, string itemKey) => IsOn(Command(window, menuKey, itemKey));

    private static readonly string[] MenuTitles =
        [.. new[] { TextKeys.MenuFile, TextKeys.MenuEdit, TextKeys.MenuInsert, TextKeys.MenuArrange, TextKeys.MenuView, TextKeys.MenuHelp }.Select(key => Words.Get(key))];

    [Fact]
    public void TheMenusAreTheStandardOnesAndDepotsOwn() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);

        Assert.Equal(MenuTitles, window.Menus.Select(menu => menu.Title));
        Assert.Equal(new KeyGesture(Key.O, KeyModifiers.Control), Command(window, TextKeys.MenuFile, TextKeys.ItemOpen).Gesture);
        Assert.Equal(new KeyGesture(Key.W, KeyModifiers.Control), Command(window, TextKeys.MenuFile, TextKeys.ItemCloseWindow).Gesture);
        Assert.Equal(new KeyGesture(Key.Z, KeyModifiers.Control | KeyModifiers.Shift), Command(window, TextKeys.MenuEdit, TextKeys.ItemRedo).Gesture);
    });

    [Fact]
    public void WithNothingOpenOnlyWhatNeedsNoDocumentCanRun() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);

        Assert.True(Enabled(window, TextKeys.MenuFile, TextKeys.ItemOpen));
        Assert.False(Enabled(window, TextKeys.MenuFile, TextKeys.ItemSave));
        Assert.False(Enabled(window, TextKeys.MenuEdit, TextKeys.ItemUndo));
        Assert.False(Enabled(window, TextKeys.MenuEdit, TextKeys.ItemDelete));
        Assert.False(IsOn(Insert(window, "Button")));
        Assert.False(Enabled(window, TextKeys.MenuArrange, TextKeys.ItemBringForward));
    });

    [Fact]
    public void SelectingAnElementLetsItBeEditedAndArranged() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);
        window.Open(folder.File("budget.uix", Budget));
        Assert.True(IsOn(Insert(window, "Button")));
        Assert.False(Enabled(window, TextKeys.MenuEdit, TextKeys.ItemDuplicate));

        window.Select(1);

        Assert.True(Enabled(window, TextKeys.MenuEdit, TextKeys.ItemDuplicate));
        Assert.True(Enabled(window, TextKeys.MenuArrange, TextKeys.ItemSendBackward));
        Command(window, TextKeys.MenuEdit, TextKeys.ItemDuplicate).Run();
        Assert.Contains("ok2", window.Text);
        Assert.True(Enabled(window, TextKeys.MenuFile, TextKeys.ItemSave));
    });

    [Fact]
    public void ViewChoosesTheLookAndShowsWhichIsChosen() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);

        Command(window, TextKeys.MenuView, TextKeys.LookDepotLight).Run();

        Assert.Equal(CanvasLook.DepotLight, window.Look);
        Assert.True(Command(window, TextKeys.MenuView, TextKeys.LookDepotLight).IsChecked!());
        Assert.False(Command(window, TextKeys.MenuView, TextKeys.LookDepot).IsChecked!());
    });

    [Fact]
    public void TheGlobalMenuBarGetsTheSameMenus() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);
        window.Open(folder.File("budget.uix", Budget));
        window.Get<AvaloniaEdit.TextEditor>("Editor").Document.Insert(0, "<!-- edited -->\n");

        var native = NativeMenu.GetMenu(window)!;
        Assert.Equal(MenuTitles, native.Items.OfType<NativeMenuItem>().Select(item => item.Header));
        var file = native.Items.OfType<NativeMenuItem>().First().Menu!;
        // Filled again as the text changed, so Save is on.
        Assert.True(file.Items.OfType<NativeMenuItem>().Single(item => item.Header == Words.Get(TextKeys.ItemSave)).IsEnabled);
    });

    [Fact]
    public void TheGlobalMenuBarLeavesPlainKeysToTheFocusedControl() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);

        var edit = NativeMenu.GetMenu(window)!.Items.OfType<NativeMenuItem>().Single(item => item.Header == Words.Get(TextKeys.MenuEdit)).Menu!;
        var items = edit.Items.OfType<NativeMenuItem>().ToList();

        Assert.Null(items.Single(item => item.Header == Words.Get(TextKeys.ItemDelete)).Gesture);
        Assert.Equal(new KeyGesture(Key.D, KeyModifiers.Control), items.Single(item => item.Header == Words.Get(TextKeys.ItemDuplicate)).Gesture);
        // The dropdown still shows Delete's key.
        Assert.Equal(new KeyGesture(Key.Delete), Command(window, TextKeys.MenuEdit, TextKeys.ItemDelete).Gesture);
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
        var save = file.Items.OfType<MenuItem>().Single(item => (string?)item.Header == Words.Get(TextKeys.ItemSave));

        Assert.False(save.IsEnabled);
        Assert.Equal(new KeyGesture(Key.S, KeyModifiers.Control), save.InputGesture);
    });
}
