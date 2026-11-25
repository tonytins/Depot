using Depot.Interface;
using Depot.Rendering;

namespace Depot.Designer;

/// <summary>
/// Where Hudini drew each element of a document, so a point on the canvas finds the element there and an element
/// finds where to draw its selection. Elements are named by their index among the document's elements, as
/// <see cref="UIDocument"/>'s edits name them.
/// </summary>
/// <remarks>
/// depot-render reports frames by name, so they're matched to the document's elements by name: a frame map from a
/// picture drawn a moment before an edit still finds the elements that didn't move. Elements Hudini doesn't report,
/// such as toolbar items, aren't on the map, and are selected from the outline.
/// </remarks>
public sealed class DesignMap
{
    private readonly List<(int Element, FrameRect Bounds)> drawn = [];
    private readonly FrameMap frames;

    /// <summary>Maps <paramref name="document"/> as depot-render reported drawing it.</summary>
    /// <exception cref="ArgumentException">The document isn't one Hudini reads.</exception>
    public DesignMap(UIDocument document, FrameMap frames)
    {
        ArgumentNullException.ThrowIfNull(document);
        this.frames = frames ?? throw new ArgumentNullException(nameof(frames));
        if (!document.IsEditable || document.Xml is null)
            throw new ArgumentException("Only a document Hudini reads is drawn, so only one can be mapped.", nameof(document));

        var children = document.Xml.Children;
        Dictionary<(string Element, string Name), int> byName = [];
        for (var index = 0; index < children.Count; index++)
        {
            var element = children[index];
            if (element.Name is "Window" or "Panel")
                Window ??= index;
            else if (FrameEdit.IsFramed(element.Name) && element["name"] is { } name)
                byName.TryAdd((element.Name, name), index);
        }

        // In the order Hudini drew them, visuals then controls, so later entries are in front.
        foreach (var element in frames.Elements)
        {
            if (!element.Hidden && byName.TryGetValue((element.Element, element.Name), out var index))
                drawn.Add((index, element.Frame));
        }
    }

    /// <summary>The <c>&lt;Window&gt;</c> or <c>&lt;Panel&gt;</c> element's index, selected by clicking its background.</summary>
    public int? Window { get; }

    /// <summary>The window's content on the canvas: below its title bar, or all of a panel.</summary>
    public FrameRect Content => frames.Content;

    /// <summary>
    /// The element at a point on the canvas: the frontmost control or visual there, or the window anywhere else on it,
    /// its bar included. <see langword="null"/> off the picture.
    /// </summary>
    public int? ElementAt(float x, float y)
    {
        for (var index = drawn.Count - 1; index >= 0; index--)
        {
            if (drawn[index].Bounds.Contains(x, y))
                return drawn[index].Element;
        }

        return new FrameRect(0, 0, frames.Width, frames.Height).Contains(x, y) ? Window : null;
    }

    /// <summary>Where an element was drawn, or <see langword="null"/> for one that isn't drawn, such as a style.</summary>
    public FrameRect? BoundsOf(int element)
    {
        if (element == Window)
            return Content;

        foreach (var (index, bounds) in drawn)
        {
            if (index == element)
                return bounds;
        }

        return null;
    }
}
