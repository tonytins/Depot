using System.Globalization;
using Depot.Interface;
using Depot.Rendering;

namespace Depot.Designer;

/// <summary>Writes a frame change into a document's <c>x</c>, <c>y</c>, <c>width</c>, and <c>height</c>.</summary>
public static class FrameEdit
{
    /// <summary>Whether an element has a frame the designer can move and resize: a control Hudini draws, or a visual.</summary>
    public static bool IsFramed(string element) => element == "Visual" || HudiniSubset.Controls.Contains(element);

    /// <summary>
    /// The handles an element can be resized by: every one for a control or visual; the right and bottom edges and
    /// their corner for the window or panel, whose top left stays where the game puts it; none for anything else.
    /// </summary>
    public static IReadOnlyList<Handle> HandlesFor(string element) => element switch
    {
        "Window" or "Panel" => [Handle.Right, Handle.BottomRight, Handle.Bottom],
        _ when IsFramed(element) => FrameDrag.Handles,
        _ => [],
    };

    /// <summary>
    /// Moves and resizes an element by <paramref name="change"/>, writing only the attributes that change. A size the
    /// element doesn't write, such as a button sized by its image, starts from the size it was drawn at.
    /// </summary>
    /// <param name="drawn">Where Hudini drew the element, for the sizes it doesn't write.</param>
    /// <returns>The edited document, or <paramref name="document"/> when nothing changes.</returns>
    /// <exception cref="ArgumentException">The element has no frame to change.</exception>
    /// <exception cref="UIScriptException">Hudini would refuse the result.</exception>
    public static UIDocument Apply(UIDocument document, int element, FrameRect drawn, FrameChange change)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(drawn);
        var children = document.Xml?.Children
            ?? throw new ArgumentException("The document isn't one Hudini reads.", nameof(document));
        if (element < 0 || element >= children.Count)
            throw new ArgumentOutOfRangeException(nameof(element), element, "The document has no such element.");
        var target = children[element];
        if (HandlesFor(target.Name).Count == 0)
            throw new ArgumentException($"<{target.Name}> has no frame to move or resize.", nameof(element));

        // The window and the panel don't move; they only grow and shrink.
        if (!IsFramed(target.Name))
            change = change with { X = 0, Y = 0 };

        var edited = document;
        foreach (var (attribute, by, fallback) in (ReadOnlySpan<(string, float, float)>)
                 [("x", change.X, 0), ("y", change.Y, 0), ("width", change.Width, drawn.Width), ("height", change.Height, drawn.Height)])
        {
            if (by == 0)
                continue;
            var current = Number(target[attribute]) ?? fallback;
            edited = edited.SetAttribute(element, attribute, Write(current + by));
        }

        return edited;
    }

    /// <summary>A number as a document writes it: whole numbers without a point, others to three places.</summary>
    public static string Write(float value) =>
        value == MathF.Round(value)
            ? ((int)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.###", CultureInfo.InvariantCulture);

    internal static float? Number(string? value) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
}
