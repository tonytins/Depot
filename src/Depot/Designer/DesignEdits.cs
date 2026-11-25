using System.Globalization;
using Depot.Interface;
using Depot.Interface.Xml;

namespace Depot.Designer;

/// <summary>A control the library adds: its element, and the size and text it starts with.</summary>
/// <param name="Element">The element, such as <c>Button</c>.</param>
/// <param name="Text">The text it starts with, or <see langword="null"/> for a control that shows none.</param>
public sealed record LibraryControl(string Element, int Width, int Height, string? Text);

/// <summary>
/// The designer's edits beyond a frame: adding from the library, duplicating, and moving an element in front of or
/// behind the others. Deleting is <see cref="UIDocument.RemoveElement"/> itself. Each gives the edited document and
/// the element to select after it, and each is refused, with Hudini's reason, when Hudini wouldn't read the result.
/// </summary>
public static class DesignEdits
{
    /// <summary>How far a new or duplicated control is set off from one already in its place.</summary>
    public const int Step = 8;

    /// <summary>The size a new picture is drawn at.</summary>
    private const int PictureSize = 64;

    /// <summary>The controls the library offers, every one Hudini draws, in the order it lists them.</summary>
    public static IReadOnlyList<LibraryControl> Library { get; } =
    [
        new("Text", 100, 20, "Text"),
        new("FormattedText", 160, 40, "Formatted text"),
        new("Button", 80, 24, "Button"),
        new("TextField", 140, 24, null),
        new("TextArea", 180, 100, null),
        new("List", 140, 120, null),
        new("ProgressBar", 140, 12, null),
        new("Slider", 140, 20, null),
        new("Region", 100, 60, null),
    ];

    /// <summary>The parts of a window that aren't controls, which the library adds too.</summary>
    public static IReadOnlyList<(string Element, string Title)> Parts { get; } =
    [
        ("Visual", "Picture"),
        ("ToolbarItem", "Toolbar Item"),
        ("TitleField", "Title Field"),
    ];

    /// <summary>
    /// Adds a control in front of everything else, centred in the window, or set off down and right from one already
    /// there, named after its kind (<c>button</c>, <c>button2</c>, and so on); or one of the <see cref="Parts"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The library has no such element.</exception>
    /// <exception cref="UIScriptException">Hudini wouldn't read the result, such as a second title field.</exception>
    public static (UIDocument Document, int Element) Add(UIDocument document, string element)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);
        var script = document.Script ?? throw new InvalidOperationException("Only a document Hudini reads can be edited.");
        var end = document.Xml!.Children.Count;
        switch (element)
        {
            case "ToolbarItem":
                return Insert(document, end, element, [new("name", UniqueName(document, "item")), new("title", "Item")]);
            case "TitleField":
                return Insert(document, end, element, [new("name", UniqueName(document, "title")), new("placeholder", "Title")]);
            case "Visual":
                return AddPicture(document, script);
        }

        var control = Library.FirstOrDefault(item => item.Element == element)
            ?? throw new ArgumentException($"The library has no <{element}>.", nameof(element));
        var (x, y) = FreePlace(document, Centred(script, control.Width, control.Height));
        List<KeyValuePair<string, string>> attributes =
        [
            new("name", UniqueName(document, char.ToLowerInvariant(element[0]) + element[1..])),
            new("x", Number(x)), new("y", Number(y)),
            new("width", Number(control.Width)), new("height", Number(control.Height)),
        ];
        if (control.Text is { } text)
            attributes.Add(new("text", text));
        return Insert(document, end, element, attributes);
    }

    /// <summary>
    /// Copies an element just after it, with a new name and, for one with a frame, set off down and right. Comments
    /// aren't copied, and the copy is written on one line.
    /// </summary>
    /// <exception cref="ArgumentException">The element has no name, so a copy couldn't be told apart, such as the window.</exception>
    /// <exception cref="UIScriptException">Hudini wouldn't read the result, such as a second title field.</exception>
    public static (UIDocument Document, int Element) Duplicate(UIDocument document, int element)
    {
        ArgumentNullException.ThrowIfNull(document);
        var original = Child(document, element);
        if (original["name"] is not { } name)
            throw new ArgumentException($"<{original.Name}> has no name, so it can't be copied.", nameof(element));

        var copyName = UniqueName(document, TrimNumber(name));
        var framed = FrameEdit.IsFramed(original.Name) && FrameEdit.Number(original["x"]) is not null && FrameEdit.Number(original["y"]) is not null;
        List<KeyValuePair<string, string>> attributes = [];
        foreach (var (attribute, value) in original.Attributes)
        {
            attributes.Add(attribute switch
            {
                "name" => new(attribute, copyName),
                "x" or "y" when framed => new(attribute, FrameEdit.Write(FrameEdit.Number(value)!.Value + Step)),
                _ => new(attribute, value),
            });
        }

        var place = document.Xml!.End(element);
        return (document.InsertElement(place, original.Name, attributes, original.Parent), place);
    }

    /// <summary>
    /// Moves a control or visual in front of the next one drawn after it, so it covers it; one already in front stays.
    /// Elements that aren't drawn, such as styles, are passed over.
    /// </summary>
    /// <exception cref="UIScriptException">Hudini wouldn't read the result.</exception>
    public static (UIDocument Document, int Element) BringForward(UIDocument document, int element) => Restack(document, element, 1);

    /// <summary>Moves a control or visual behind the one drawn before it; one already at the back stays.</summary>
    /// <exception cref="UIScriptException">Hudini wouldn't read the result.</exception>
    public static (UIDocument Document, int Element) SendBackward(UIDocument document, int element) => Restack(document, element, -1);

    /// <summary>A name no element in the document has: <paramref name="stem"/>, or it with the lowest number from 2 that's free.</summary>
    public static string UniqueName(UIDocument document, string stem)
    {
        ArgumentNullException.ThrowIfNull(document);
        HashSet<string> taken = [.. document.Xml?.Children.Select(child => child["name"]).OfType<string>() ?? []];
        if (!taken.Contains(stem))
            return stem;

        for (var number = 2; ; number++)
        {
            var candidate = stem + number.ToString(CultureInfo.InvariantCulture);
            if (!taken.Contains(candidate))
                return candidate;
        }
    }

    /// <summary>
    /// What an element is stacked among: visuals, all drawn behind the controls; controls; and toolbar items, whose
    /// order is left to right along the title bar.
    /// </summary>
    private static string? Layer(string element) => element switch
    {
        "Visual" or "ToolbarItem" => element,
        _ when FrameEdit.IsFramed(element) => "control",
        _ => null,
    };

    private static (UIDocument, int) Restack(UIDocument document, int element, int direction)
    {
        ArgumentNullException.ThrowIfNull(document);
        var xml = document.Xml ?? throw new InvalidOperationException("Only a document Hudini reads can be edited.");
        if (Layer(Child(document, element).Name) is not { } layer)
            return (document, element);

        var siblings = xml.ChildrenOf(xml.Children[element].Parent);
        for (var place = Array.IndexOf(siblings, element) + direction; place >= 0 && place < siblings.Length; place += direction)
        {
            var index = siblings[place];
            if (Layer(xml.Children[index].Name) == layer)
            {
                var moved = document.MoveElement(element, index);
                return (moved, moved.Xml!.ChildrenOf(xml.Children[element].Parent)[place]);
            }
        }

        return (document, element);
    }

    /// <summary>
    /// Adds an <c>&lt;Image&gt;</c> after the other resources and a <c>&lt;Visual&gt;</c> of the same name showing it,
    /// centred. Its path is a placeholder to change in the inspector; until then Hudini outlines it in red.
    /// </summary>
    private static (UIDocument, int) AddPicture(UIDocument document, UIScript script)
    {
        var xml = document.Xml!;
        var name = UniqueName(document, "picture");
        var after = 0;
        foreach (var child in xml.ChildrenOf(UIDocument.Root))
        {
            if (xml.Children[child].Name is "Window" or "Panel" or "Image" or "String" or "Style")
                after = xml.End(child);
        }

        var withImage = document.InsertElement(after, "Image", [new("name", name), new("path", "picture.png")]);
        var (x, y) = FreePlace(withImage, Centred(script, PictureSize, PictureSize));
        var end = withImage.Xml!.Children.Count;
        return Insert(withImage, end, "Visual",
            [new("name", name), new("image", name), new("x", Number(x)), new("y", Number(y)), new("width", Number(PictureSize)), new("height", Number(PictureSize))]);
    }

    private static (int X, int Y) Centred(UIScript script, int width, int height) =>
        ((int)MathF.Max(0, MathF.Round((script.Window.Size.Width - width) / 2)),
         (int)MathF.Max(0, MathF.Round((script.Window.Size.Height - height) / 2)));

    private static (UIDocument, int) Insert(UIDocument document, int index, string element, List<KeyValuePair<string, string>> attributes) =>
        (document.InsertElement(index, element, attributes), index);

    private static FlatXmlElement Child(UIDocument document, int element)
    {
        var children = document.Xml?.Children ?? throw new InvalidOperationException("Only a document Hudini reads can be edited.");
        if (element < 0 || element >= children.Count)
            throw new ArgumentOutOfRangeException(nameof(element), element, "The document has no such element.");
        return children[element];
    }

    /// <summary>The first place from <paramref name="start"/>, stepping down and right, that no framed element starts at.</summary>
    private static (int X, int Y) FreePlace(UIDocument document, (int X, int Y) start)
    {
        HashSet<(float, float)> starts = [];
        foreach (var child in document.Xml!.Children)
        {
            if (FrameEdit.IsFramed(child.Name) && FrameEdit.Number(child["x"]) is { } left && FrameEdit.Number(child["y"]) is { } top)
                starts.Add((left, top));
        }

        var (x, y) = start;
        // Bounded, since a document has only so many elements.
        for (var tries = 0; tries <= starts.Count && starts.Contains((x, y)); tries++)
            (x, y) = (x + Step, y + Step);
        return (x, y);
    }

    private static string TrimNumber(string name)
    {
        var end = name.Length;
        while (end > 1 && char.IsAsciiDigit(name[end - 1]))
            end--;
        return name[..end];
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
