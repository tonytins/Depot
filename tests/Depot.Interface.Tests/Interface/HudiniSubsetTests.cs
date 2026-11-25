using Depot.Interface;

namespace Depot.Interface.Tests.Interface;

/// <summary>The part of UIScript Hudini reads, which Depot holds documents to.</summary>
public sealed class HudiniSubsetTests
{
    [Fact]
    public void LeavesOutWhatTheSpecSaysHudiniRefuses()
    {
        foreach (var outside in (string[])["Menu", "MenuItem", "MenuSeparator", "Sidebar", "Pane", "Shelf", "Grid", "Item", "StatusItem"])
        {
            Assert.DoesNotContain(outside, HudiniSubset.Elements);
            Assert.Empty(HudiniSubset.For(outside));
        }

        Assert.Equal(["Text", "FormattedText", "Button", "TextField", "TextArea", "List", "ProgressBar", "Slider", "Region"], HudiniSubset.Controls);
        foreach (var inside in (string[])["UIScript", "Window", "Panel", "Image", "String", "Style", "Visual", "ToolbarItem", "TitleField"])
            Assert.Contains(inside, HudiniSubset.Elements);
    }

    [Theory]
    [InlineData("Window", "subtitle")]
    [InlineData("ToolbarItem", "image")]
    [InlineData("ToolbarItem", "placement")]
    [InlineData("Button", "pane")]
    [InlineData("Slider", "pane")]
    public void SkipsWhatHudiniSkips(string element, string attribute)
    {
        Assert.True(HudiniSubset.Skips(element, attribute));
        Assert.DoesNotContain(HudiniSubset.For(element), info => info.Name == attribute);
        Assert.Contains(UIScriptSchema.For(element), info => info.Name == attribute);
    }

    [Fact]
    public void KeepsTheRestOfAnElementsAttributes()
    {
        Assert.False(HudiniSubset.Skips("Window", "width"));
        Assert.Equal(
            UIScriptSchema.For("Slider").Count - 1,
            HudiniSubset.For("Slider").Count);
        Assert.Contains(HudiniSubset.For("Slider"), info => info.Name == "step");
    }

    [Fact]
    public void EveryExampleIsInsideIt()
    {
        var examples = Path.Combine(AppContext.BaseDirectory, "Interface", "Fixtures", "Hudini");
        foreach (var file in Directory.EnumerateFiles(examples, "*.uix", SearchOption.AllDirectories))
        {
            var document = UIDocument.Load(file);
            Assert.True(document.IsEditable, $"{Path.GetFileName(file)}: {document.Problem?.Message}");
        }
    }
}
