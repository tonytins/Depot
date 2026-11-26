using Depot.Interface;

namespace Depot.Interface.Tests.Interface;

/// <summary>
/// <see cref="HudiniAppearance"/>, the document a game's windows share in Hudini, and documents read with one: a
/// window may name its images and styles without declaring them.
/// </summary>
public sealed class HudiniAppearanceTests
{
    private const string Shared = """
        <Appearance version="1">
          <Image name="scrollThumb" path="scroll/thumb.png"/>
          <Image name="logo" path="logo.png"/>
          <ScrollBar thumb="scrollThumb" thumbCap="6"/>
          <Style name="chatText" textColor="#FFF99D"/>
        </Appearance>
        """;

    private const string LeaningWindow = """
        <UIScript version="1">
          <Window width="200" height="100"/>
          <Visual name="badge" image="logo" x="0" y="0" width="10" height="10"/>
          <List name="history" x="0" y="20" width="200" height="80" style="chatText"/>
        </UIScript>
        """;

    [Fact]
    public void ReadsTheNamesAWindowMayUse()
    {
        var appearance = HudiniAppearance.Parse(Shared);

        Assert.Equal(["logo", "scrollThumb"], appearance.ImageNames.Order());
        Assert.Equal(["chatText"], appearance.StyleNames);
        Assert.True(appearance.HasScrollBar);
        Assert.False(HudiniAppearance.Parse("<Appearance/>").HasScrollBar);
    }

    [Theory]
    [InlineData("<UIScript version=\"1\"/>", "root element must be <Appearance>")]
    [InlineData("<Appearance version=\"2\"/>", "appearance version 2")]
    [InlineData("<Appearance version=\"one\"/>", "\"one\" for version")]
    [InlineData("<Appearance><Button name=\"b\"/></Appearance>", "<Button> doesn't belong there")]
    [InlineData("<Appearance><Style name=\"s\"><Image name=\"i\" path=\"i.png\"/></Style></Appearance>", "<Image> doesn't belong there")]
    [InlineData("<Appearance><Image name=\"a\" path=\"a.png\"/><Image name=\"a\" path=\"b.png\"/></Appearance>", "more than one <Image>")]
    [InlineData("<Appearance><ScrollBar/><ScrollBar/></Appearance>", "at most one <ScrollBar>")]
    [InlineData("<Appearance><Style textColor=\"#000000\"/></Appearance>", "<Style> needs a name")]
    public void RefusesWhatHudiniRefuses(string text, string reason)
    {
        var exception = Assert.Throws<UIScriptException>(() => HudiniAppearance.Parse(text));
        Assert.Contains(reason, exception.Message);
    }

    [Fact]
    public void AWindowLeaningOnSharedNamesReadsOnlyWithTheAppearance()
    {
        var alone = UIDocument.Read(LeaningWindow);
        Assert.False(alone.IsEditable);
        Assert.Contains("logo", alone.Problem!.Message);

        var appearance = HudiniAppearance.Parse(Shared);
        var read = UIDocument.Read(LeaningWindow, appearance);
        Assert.True(read.IsEditable);
        Assert.Same(appearance, read.Appearance);
        Assert.Same(appearance, read.Script!.Appearance);

        // A name neither declares is still refused.
        var missing = UIDocument.Read(LeaningWindow.Replace("chatText", "missing", StringComparison.Ordinal), appearance);
        Assert.Contains("missing", missing.Problem!.Message);
    }

    [Fact]
    public void EditsReadTheResultWithTheSameAppearance()
    {
        var appearance = HudiniAppearance.Parse(Shared);
        var read = UIDocument.Read(LeaningWindow, appearance);
        var list = read.Xml!.Children.ToList().FindIndex(element => element.Name == "List");

        var edited = read.SetAttribute(list, "width", "180");

        Assert.True(edited.IsEditable);
        Assert.Same(appearance, edited.Appearance);
    }

    [Fact]
    public void AWindowsOwnDeclarationsStillRead()
    {
        // A window that declares a shared name itself overrides it, and reads with or without the appearance.
        const string overriding = """
            <UIScript version="1">
              <Window width="200" height="100"/>
              <Style name="chatText" fontSize="title"/>
              <List name="history" x="0" y="0" width="200" height="80" style="chatText"/>
            </UIScript>
            """;

        Assert.True(UIDocument.Read(overriding).IsEditable);
        Assert.True(UIDocument.Read(overriding, HudiniAppearance.Parse(Shared)).IsEditable);
        Assert.NotNull(UIScript.Parse(overriding, HudiniAppearance.Parse(Shared)));
    }
}
