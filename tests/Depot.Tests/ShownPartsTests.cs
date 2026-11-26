using Avalonia;
using Avalonia.Controls;
using Depot.Designer;
using Depot.Interface;

namespace Depot.Tests;

/// <summary>Which tab and pane the canvas shows, which follow the selection.</summary>
public sealed class ShownPartsTests : IDisposable
{
    private const string CityPanel = """
        <UIScript version="1">
          <Window width="300" height="120"/>
          <ToolbarItem name="cityTab" title="City" tab="city"/>
          <ToolbarItem name="lawsTab" title="Ordinances" tab="laws"/>
          <ToolbarItem name="refresh" title="Refresh"/>
          <Sidebar name="side" width="100"/>
          <Pane name="general" title="General"/>
          <Pane name="advanced" title="Advanced">
            <Button name="reset" x="10" y="60" width="80" height="24"/>
          </Pane>
          <Button name="flat" x="100" y="60" width="80" height="24" pane="general"/>
          <Tab name="city">
            <Text name="population" x="10" y="10" width="80" height="14"/>
          </Tab>
          <Tab name="laws">
            <Button name="curfew" x="10" y="10" width="80" height="24"/>
          </Tab>
          <Text name="always" x="10" y="90" width="80" height="14"/>
        </UIScript>
        """;

    private readonly TemporaryFolder folder = new();

    public void Dispose() => folder.Dispose();

    private static int IndexOf(UIDocument document, string name) =>
        document.Xml!.Children.ToList().FindIndex(element => element["name"] == name);

    [Theory]
    [InlineData("curfew", "laws", null)]
    [InlineData("laws", "laws", null)]
    [InlineData("lawsTab", "laws", null)]
    [InlineData("reset", null, "advanced")]
    [InlineData("advanced", null, "advanced")]
    [InlineData("flat", null, "general")]
    [InlineData("always", null, null)]
    [InlineData("refresh", null, null)]
    [InlineData("side", null, null)]
    public void ASelectionShowsTheTabOrPaneItsIn(string selected, string? tab, string? pane)
    {
        var document = UIDocument.Read(CityPanel);

        var shown = new ShownParts().Selecting(document, IndexOf(document, selected));

        Assert.Equal(new ShownParts(tab, pane), shown);
    }

    [Fact]
    public void ASelectionOutsideEveryTabAndPaneKeepsWhatsShown()
    {
        var document = UIDocument.Read(CityPanel);
        ShownParts shown = new("laws", "advanced");

        Assert.Equal(shown, shown.Selecting(document, IndexOf(document, "always")));
        Assert.Equal(shown, shown.Selecting(document, null));
        // A pane's control leaves the tab, and a tab's leaves the pane.
        Assert.Equal(new ShownParts("city", "advanced"), shown.Selecting(document, IndexOf(document, "population")));
        Assert.Equal(new ShownParts("laws", "general"), shown.Selecting(document, IndexOf(document, "flat")));
    }

    [Fact]
    public void ATabOrPaneTheTextNoLongerHasIsLetGo()
    {
        var renamed = UIDocument.Read(CityPanel.Replace("\"laws\"", "\"rules\"", StringComparison.Ordinal));

        Assert.Equal(new ShownParts(null, "advanced"), new ShownParts("laws", "advanced").In(renamed));
        Assert.Equal(new ShownParts(), new ShownParts("city", "general").In(UIDocument.Read("<UIScript>")));
    }

    [Fact]
    public void SelectingFromTheOutlineDrawsItsTabAndOutlinesIt() => TestApp.Run(() =>
    {
        FakeRenderer renderer = new();
        MainWindow window = new(renderer.RenderAsync);
        Assert.True(window.Open(folder.File("city.uix", CityPanel)));
        var curfew = IndexOf(window.Design!, "curfew");
        Assert.Null(window.Map!.BoundsOf(curfew));

        window.Get<ListBox>("Outline").SelectedIndex = curfew;

        Assert.Equal("laws", renderer.Requests[^1].Tab);
        Assert.Equal(curfew, window.Selection);
        Assert.NotNull(window.Map!.BoundsOf(curfew));
        Assert.Contains("showing the laws tab", window.Get<TextBlock>("Status").Text, StringComparison.Ordinal);

        // A tab renamed in the text, with its item, is let go of, so the next picture shows the first again rather
        // than failing.
        window.Apply(UIDocument.Read(window.Design!.Text.Replace("\"laws\"", "\"rules\"", StringComparison.Ordinal)));
        Assert.Null(renderer.Requests[^1].Tab);
        Assert.DoesNotContain("can't", window.Get<TextBlock>("Status").Text, StringComparison.Ordinal);
    });
}
