using Depot.Designer;
using Depot.Interface;
using Depot.Rendering;

namespace Depot.Tests;

/// <summary>Finding elements on the canvas from depot-render's frames, by name.</summary>
public sealed class DesignMapTests
{
    private static readonly UIDocument Document = UIDocument.Read("""
        <UIScript version="1">
          <Window width="200" height="100"/>
          <Style name="s"/>
          <Image name="art" path="art.png"/>
          <Visual name="art" image="art" x="0" y="0" width="200" height="100"/>
          <Button name="back" x="10" y="10" width="80" height="40"/>
          <Button name="front" x="50" y="20" width="80" height="40"/>
        </UIScript>
        """);

    private static FrameMap Frames(params FrameElement[] elements) =>
        new("window", 200, 130, new FrameRect(0, 30, 200, 100), elements, [], [], [], []);

    private static readonly FrameMap Drawn = Frames(
        new("art", "Visual", new FrameRect(0, 30, 200, 100), false),
        new("back", "Button", new FrameRect(10, 40, 80, 40), false),
        new("front", "Button", new FrameRect(50, 50, 80, 40), false));

    [Fact]
    public void FindsTheFrontmostElementAtAPoint()
    {
        DesignMap map = new(Document, Drawn);

        Assert.Equal(5, map.ElementAt(60, 60));   // front, drawn over back
        Assert.Equal(4, map.ElementAt(15, 45));   // back alone
        Assert.Equal(3, map.ElementAt(150, 120)); // the visual behind them
    }

    [Fact]
    public void TheWindowIsAnywhereElseOnThePictureAndNothingIsOffIt()
    {
        DesignMap map = new(Document, Frames());

        Assert.Equal(0, map.Window);
        Assert.Equal(0, map.ElementAt(5, 5));     // the title bar
        Assert.Null(map.ElementAt(250, 50));
        Assert.Equal(new FrameRect(0, 30, 200, 100), map.BoundsOf(0));
    }

    [Fact]
    public void ElementsAreMatchedByKindAndName()
    {
        // The image and the visual share a name; only the visual is drawn. A frame for an element the document
        // no longer has, from a picture drawn before an edit, finds nothing.
        DesignMap map = new(Document, Frames(
            new("art", "Visual", new FrameRect(0, 30, 200, 100), false),
            new("gone", "Button", new FrameRect(0, 30, 10, 10), false)));

        Assert.Null(map.BoundsOf(2));
        Assert.Equal(new FrameRect(0, 30, 200, 100), map.BoundsOf(3));
        Assert.Equal(3, map.ElementAt(5, 35));
        Assert.Null(map.BoundsOf(1)); // a style isn't drawn
    }

    [Fact]
    public void HiddenElementsCantBeFound()
    {
        DesignMap map = new(Document, Frames(new FrameElement("front", "Button", new FrameRect(50, 50, 80, 40), true)));

        Assert.Null(map.BoundsOf(5));
        Assert.Equal(0, map.ElementAt(60, 60));
    }

    [Fact]
    public void OnlyADocumentHudiniReadsIsMapped() =>
        Assert.Throws<ArgumentException>(() => new DesignMap(UIDocument.Read("<UIScript><Menu/></UIScript>"), Frames()));
}
