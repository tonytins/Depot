using Avalonia.Controls;

namespace Depot.Tests;

/// <summary>Depot reading and drawing documents with a Hudini appearance, as a game reads its windows.</summary>
public sealed class AppearanceTests : IDisposable
{
    private const string Shared = """
        <Appearance version="1">
          <Style name="chatText" textColor="#FFF99D"/>
        </Appearance>
        """;

    private const string Chat = """
        <UIScript version="1">
          <Window width="200" height="100"/>
          <List name="history" x="0" y="0" width="200" height="80" style="chatText"/>
        </UIScript>
        """;

    private readonly TemporaryFolder folder = new();

    public void Dispose() => folder.Dispose();

    private static string StatusOf(MainWindow window) => window.Get<TextBlock>("Status").Text ?? "";

    [Fact]
    public void ADocumentLeaningOnAnAppearanceIsDesignedOnceOneIsChosen() => TestApp.Run(() =>
    {
        FakeRenderer renderer = new();
        MainWindow window = new(renderer.RenderAsync);
        Assert.True(window.Open(folder.File("chat.uix", Chat)));
        Assert.False(window.Design!.IsEditable);

        var appearance = folder.File("appearance.xml", Shared);
        Assert.True(window.UseAppearance(appearance));

        Assert.Equal(appearance, window.AppearancePath);
        Assert.True(window.Design!.IsEditable);
        Assert.Equal(appearance, renderer.Requests[^1].Appearance);
        Assert.Equal("Appearance: appearance.xml ▾", window.Get<Button>("AppearanceButton").Content);
        Assert.NotNull(window.Map);

        Assert.True(window.UseAppearance(null));
        Assert.Null(window.AppearancePath);
        Assert.False(window.Design!.IsEditable);
        Assert.Null(renderer.Requests[^1].Appearance);
    });

    [Fact]
    public void AnAppearanceHudiniWouldRefuseIsntUsed() => TestApp.Run(() =>
    {
        FakeRenderer renderer = new();
        MainWindow window = new(renderer.RenderAsync);
        window.Open(folder.File("chat.uix", Chat));
        var good = folder.File("appearance.xml", Shared);
        window.UseAppearance(good);

        Assert.False(window.UseAppearance(folder.File("broken.xml", "<Appearance><Button name=\"b\"/></Appearance>")));
        Assert.Contains("can't use the appearance broken.xml", StatusOf(window));
        Assert.Equal(good, window.AppearancePath);

        Assert.False(window.UseAppearance(Path.Combine(folder.Path, "missing.xml")));
        Assert.Equal(good, window.AppearancePath);
    });

    [Fact]
    public void AUisFileIsDrawnWithoutTheAppearance() => TestApp.Run(() =>
    {
        FakeRenderer renderer = new();
        MainWindow window = new(renderer.RenderAsync);
        window.UseAppearance(folder.File("appearance.xml", Shared));
        window.Open(folder.File("bar.uis", "<AddText \"Label\" position=(0,0) size=(10,10)>"));

        Assert.All(renderer.Requests, request => Assert.Null(request.Appearance));
    });
}
