using Avalonia;

namespace Depot.Tests;

/// <summary><see cref="TitleBarPanel"/> places the title as a Venture title bar does.</summary>
public sealed class TitleBarPanelTests
{
    [Fact]
    public void ATitleThatFitsIsCentredInTheWholeBar()
    {
        Assert.Equal(new Rect(450, 0, 100, 42), TitleBarPanel.TitleBounds(1000, 42, 300, 100, 100));
    }

    [Fact]
    public void ATitleThatWouldMeetAnEndIsCentredInTheRoomBetweenThem()
    {
        // Centred in the bar it would start at 450, inside the 500-pixel start items, so it moves right.
        var bounds = TitleBarPanel.TitleBounds(1000, 42, 500, 100, 100);

        Assert.Equal(new Rect(512 + ((376 - 100) / 2.0), 0, 100, 42), bounds);
    }

    [Fact]
    public void ATitleWiderThanTheRoomIsCutToIt()
    {
        Assert.Equal(new Rect(162, 0, 76, 42), TitleBarPanel.TitleBounds(400, 42, 150, 150, 300));
    }

    [Fact]
    public void NoRoomLeavesNoTitle()
    {
        Assert.Equal(0, TitleBarPanel.TitleBounds(200, 42, 150, 150, 80).Width);
    }
}
