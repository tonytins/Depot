using Depot.Designer;
using Depot.Interface;

namespace Depot.Tests;

/// <summary>Adding, copying, and restacking, inside Hudini's subset.</summary>
public sealed class DesignEditsTests
{
    private static UIDocument Window(string body = "") =>
        UIDocument.Read($"""<UIScript version="1"><Window width="200" height="100"/>{body}</UIScript>""");

    [Fact]
    public void TheLibraryOffersOnlyWhatHudiniDraws()
    {
        Assert.Equal(HudiniSubset.Controls, DesignEdits.Library.Select(control => control.Element));
        Assert.DoesNotContain(DesignEdits.Parts, part => HudiniSubset.ElementsOutside.Contains(part.Element));
    }

    [Fact]
    public void AControlIsAddedCentredAndNamedAfterItsKind()
    {
        var (document, element) = DesignEdits.Add(Window(), "Button");

        var added = document.Xml!.Children[element];
        Assert.Equal(("Button", "button", "60", "38"), (added.Name, added["name"], added["x"], added["y"]));
        Assert.Equal("Button", added["text"]);
    }

    [Fact]
    public void ASecondIsSetOffAndNumbered()
    {
        var (once, _) = DesignEdits.Add(Window(), "Button");
        var (twice, element) = DesignEdits.Add(once, "Button");

        var added = twice.Xml!.Children[element];
        Assert.Equal(("button2", "68", "46"), (added["name"], added["x"], added["y"]));
    }

    [Fact]
    public void APictureAddsItsImageAmongTheResources()
    {
        var (document, element) = DesignEdits.Add(Window("""<Style name="s"/><Button name="b" x="0" y="0" width="1" height="1"/>"""), "Visual");

        var names = document.Xml!.Children.Select(child => child.Name).ToArray();
        Assert.Equal(["Window", "Style", "Image", "Button", "Visual"], names);
        Assert.Equal("picture", document.Xml.Children[element]["image"]);
    }

    [Fact]
    public void ASecondTitleFieldIsRefusedWithHudinisReason()
    {
        var (once, _) = DesignEdits.Add(Window(), "TitleField");

        Assert.Throws<UIScriptException>(() => DesignEdits.Add(once, "TitleField"));
    }

    [Fact]
    public void WhatTheLibraryDoesntHaveIsRefused() =>
        Assert.Throws<ArgumentException>(() => DesignEdits.Add(Window(), "Shelf"));

    [Fact]
    public void ACopyIsSetOffWithANewName()
    {
        var document = Window("""<Button name="ok2" x="10" y="10" width="40" height="20" text="OK"/>""");

        var (copied, element) = DesignEdits.Duplicate(document, 1);

        var copy = copied.Xml!.Children[element];
        Assert.Equal(("ok", "18", "18", "OK"), (copy["name"], copy["x"], copy["y"], copy["text"]));
        Assert.Throws<ArgumentException>(() => DesignEdits.Duplicate(document, 0));
    }

    [Fact]
    public void RestackingPassesOverWhatIsntDrawn()
    {
        var document = Window("""<Button name="a" x="0" y="0" width="1" height="1"/><Style name="s"/><Button name="b" x="0" y="0" width="1" height="1"/>""");

        var (forward, element) = DesignEdits.BringForward(document, 1);

        Assert.Equal(["Window", "Style", "Button", "Button"], forward.Xml!.Children.Select(child => child.Name));
        Assert.Equal("a", forward.Xml.Children[element]["name"]);
        Assert.Same(forward, DesignEdits.BringForward(forward, element).Document);
    }
}
