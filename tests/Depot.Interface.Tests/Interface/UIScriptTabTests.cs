using Depot.Interface;

namespace Depot.Interface.Tests.Interface;

/// <summary>Tabs, which toolbar items switch, and the sidebar of panes Hudini reads, as the spec draws them.</summary>
public sealed class UIScriptTabTests
{
    private const string Box = """x="0" y="0" width="10" height="10" """;

    private static string Document(string body) => $"""
        <UIScript>
          <Window width="300" height="200"/>
        {body}
        </UIScript>
        """;

    private const string Tabbed = $"""
          <Image name="logo" path="logo.png"/>
          <ToolbarItem name="cityTab" title="City" tab="city"/>
          <ToolbarItem name="refresh" title="Refresh"/>
          <ToolbarItem name="lawsTab" title="Ordinances" tab="laws"/>
          <Text name="always" {Box}/>
          <Tab name="city">
            <Text name="population" {Box}/>
            <Visual name="badge" image="logo" {Box}/>
          </Tab>
          <Tab name="laws">
            <Button name="curfew" {Box}/>
          </Tab>
        """;

    [Fact]
    public void ReadsTabsTheirContentsAndTheItemsThatNameThem()
    {
        var script = UIScript.Parse(Document(Tabbed));

        Assert.Equal(["city", "laws"], script.Tabs.Select(tab => tab.Name));
        Assert.Equal(["city", null, "laws"], script.ToolbarItems.Select(item => item.Tab));
        Assert.Equal("city", script.Control("population")!.Tab);
        Assert.Equal("laws", script.Control("curfew")!.Tab);
        Assert.Null(script.Control("always")!.Tab);
        Assert.Equal("city", Assert.Single(script.Visuals).Tab);
        Assert.Empty(script.IgnoredAttributes);
    }

    [Fact]
    public void ATabMayHoldNothing()
    {
        var script = UIScript.Parse(Document("""<ToolbarItem name="mapTab" title="Map" tab="map"/><Tab name="map"/>"""));

        Assert.Equal("map", Assert.Single(script.Tabs).Name);
    }

    [Theory]
    [InlineData("""<Tab name="map"/>""", "No toolbar item names the tab \"map\"")]
    [InlineData("""<ToolbarItem name="a" title="A" tab="map"/><ToolbarItem name="b" title="B" tab="map"/><Tab name="map"/>""", "More than one toolbar item names the tab \"map\"")]
    [InlineData("""<ToolbarItem name="a" title="A" tab="map"/>""", "\"map\"")]
    public void EachTabHasExactlyOneItem(string body, string expected)
    {
        var refusal = Assert.Throws<UIScriptException>(() => UIScript.Parse(Document(body)));

        Assert.Contains(expected, refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TabsOnlyNest()
    {
        var refusal = Assert.Throws<UIScriptException>(() => UIScript.Parse(Document(
            $"""<ToolbarItem name="a" title="A" tab="t"/><Tab name="t"/><Button name="b" {Box} tab="t"/>""")));

        Assert.Contains("put it inside the <Tab>", refusal.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""<Image name="i" path="i.png"/>""", "<Image> can't be inside <Tab>")]
    [InlineData("""<Tab name="u"/>""", "<Tab> can't be inside <Tab>")]
    [InlineData("""<Pane name="p" title="P"/>""", "<Pane> can't be inside <Tab>")]
    public void ATabHoldsOnlyControlsAndVisuals(string inside, string expected)
    {
        var refusal = Assert.Throws<UIScriptException>(() => UIScript.Parse(Document(
            $"""<ToolbarItem name="a" title="A" tab="t"/><Tab name="t">{inside}</Tab>""")));

        Assert.Contains(expected, refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AControlInATabCantAlsoBeInAPane()
    {
        var refusal = Assert.Throws<UIScriptException>(() => UIScript.Parse(Document($"""
              <Sidebar name="s" width="100"/><Pane name="p" title="P"/>
              <ToolbarItem name="a" title="A" tab="t"/>
              <Tab name="t"><Button name="b" {Box} pane="p"/></Tab>
            """)));

        Assert.Contains("inside the tab \"t\", so it can't also be in a pane", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TabNamesAreUniqueAmongTheOthers()
    {
        var refusal = Assert.Throws<UIScriptException>(() => UIScript.Parse(Document(
            $"""<ToolbarItem name="a" title="A" tab="city"/><Tab name="city"/><Text name="city" {Box}/>""")));

        Assert.Contains("city", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APanelHasNoTabs()
    {
        var refusal = Assert.Throws<UIScriptException>(() => UIScript.Parse(
            """<UIScript><Panel width="300" height="40" edge="bottom"/><Tab name="t"/></UIScript>"""));

        Assert.Contains("no tabs", refusal.Message, StringComparison.Ordinal);
    }

    // What Depot holds documents to: the subset Hudini reads.

    [Fact]
    public void TabsAndASidebarOfPanesAreInsideTheSubset()
    {
        var document = UIDocument.Read(Document(Tabbed + $"""
              <Sidebar name="places" width="120" edge="right"/>
              <Pane name="general" title="General"><TextField name="name" {Box}/></Pane>
              <Pane name="advanced" title="Advanced"/>
              <Button name="reset" {Box} pane="advanced"/>
            """));

        Assert.True(document.IsEditable, document.Problem?.Message);
    }

    [Theory]
    [InlineData("""<Sidebar name="s" width="100"/>""")]
    [InlineData($"""<Sidebar name="s" width="100"><Button name="b" {Box}/></Sidebar>""")]
    public void OnlyASidebarOfPanesIsInsideTheSubset(string sidebar)
    {
        var document = UIDocument.Read(Document(sidebar));

        Assert.False(document.IsEditable);
        Assert.Contains("Hudini reads a sidebar of panes", document.Problem!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HudiniSkipsASidebarsEdgeAndReadsAControlsPane()
    {
        Assert.True(HudiniSubset.Skips("Sidebar", "edge"));
        Assert.False(HudiniSubset.Skips("Button", "pane"));
        Assert.Contains(HudiniSubset.For("Tab"), info => info.Name == "name");
    }

    [Fact]
    public void MovingAControlIntoATabPutsItThereAndDropsItsPane()
    {
        var document = UIDocument.Read(Document($"""
              <Sidebar name="s" width="100"/>
              <Pane name="p" title="P"/>
              <ToolbarItem name="a" title="A" tab="t"/>
              <Tab name="t"/>
              <Button name="b" {Box} pane="p"/>
            """));
        var xml = document.Xml!;
        var button = xml.Children.ToList().FindIndex(element => element["name"] == "b");
        var tab = xml.Children.ToList().FindIndex(element => element.Name == "Tab");

        var (moved, _) = document.MoveInto(button, tab);

        Assert.Equal("t", moved.Script!.Control("b")!.Tab);
        Assert.Null(moved.Script.Control("b")!.Pane);
        Assert.DoesNotContain("pane=", moved.Text, StringComparison.Ordinal);
    }
}
