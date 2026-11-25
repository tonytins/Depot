using Depot.Designer;
using Depot.Interface;
using Depot.Rendering;

namespace Depot.Tests;

/// <summary>Dragging frames, and writing what changed into the document.</summary>
public sealed class FrameEditTests
{
    private static readonly FrameRect Box = new(10, 20, 80, 40);

    [Fact]
    public void DraggingTheMiddleMoves() => Assert.Equal(new FrameChange(5, -3, 0, 0), FrameDrag.Change(Box, Handle.None, 5, -3));

    [Fact]
    public void ACornerMovesAndGrowsTogether() =>
        Assert.Equal(new FrameChange(5, 5, -5, -5), FrameDrag.Change(Box, Handle.TopLeft, 5, 5));

    [Fact]
    public void AFrameNeverShrinksBelowAPixel()
    {
        Assert.Equal(1, FrameDrag.Change(Box, Handle.Right, -500, 0).ApplyTo(Box).Width);
        Assert.Equal(1, FrameDrag.Change(Box, Handle.Top, 0, 500).ApplyTo(Box).Height);
    }

    [Fact]
    public void AHandleIsFoundAtItsCentreAndALittleAround()
    {
        Assert.Equal(Handle.BottomRight, FrameDrag.HandleAt(Box, 90, 60, FrameDrag.Handles));
        Assert.Equal(Handle.BottomRight, FrameDrag.HandleAt(Box, 94, 64, FrameDrag.Handles));
        Assert.Equal(Handle.None, FrameDrag.HandleAt(Box, 50, 40, FrameDrag.Handles));
        Assert.Equal(Handle.None, FrameDrag.HandleAt(Box, 10, 20, [Handle.Right]));
    }

    [Fact]
    public void WritesOnlyWhatChangedAndLeavesTheRestAsWritten()
    {
        var document = UIDocument.Read("""
            <UIScript version="1">
              <Window width="200" height="100"/>
              <!-- The one button. -->
              <Button name="ok"   x="10" y="20" width="80" height="40" text="OK"/>
            </UIScript>
            """);

        var moved = FrameEdit.Apply(document, 1, Box, new FrameChange(5, 0, 0, 0));

        Assert.Equal(document.Text.Replace("x=\"10\"", "x=\"15\""), moved.Text);
    }

    [Fact]
    public void ASizeTheDocumentLeavesOutStartsFromTheDrawnSize()
    {
        var document = UIDocument.Read("""<UIScript version="1"><Window width="200" height="100"/><Image name="i" path="i.png"/><Button name="b" x="0" y="0" image="i"/></UIScript>""");

        var grown = FrameEdit.Apply(document, 2, new FrameRect(0, 30, 32, 16), new FrameChange(0, 0, 8, 0));

        Assert.Equal("40", grown.Xml!.Children[2]["width"]);
        Assert.Null(grown.Xml.Children[2]["height"]);
    }

    [Fact]
    public void TheWindowGrowsButDoesntMove()
    {
        var document = UIDocument.Read("""<UIScript version="1"><Window width="200" height="100"/></UIScript>""");

        var grown = FrameEdit.Apply(document, 0, new FrameRect(0, 30, 200, 100), new FrameChange(5, 5, 20, 10));

        Assert.Equal(("220", "110"), (grown.Xml!.Children[0]["width"], grown.Xml.Children[0]["height"]));
        Assert.Equal([Handle.Right, Handle.BottomRight, Handle.Bottom], FrameEdit.HandlesFor("Window"));
    }

    [Fact]
    public void OnlyFramedElementsMove()
    {
        var document = UIDocument.Read("""<UIScript version="1"><Window width="200" height="100"/><Style name="s"/></UIScript>""");

        Assert.Throws<ArgumentException>(() => FrameEdit.Apply(document, 1, Box, new FrameChange(1, 0, 0, 0)));
        Assert.Empty(FrameEdit.HandlesFor("ToolbarItem"));
        Assert.False(FrameEdit.IsFramed("Shelf"));
    }
}
