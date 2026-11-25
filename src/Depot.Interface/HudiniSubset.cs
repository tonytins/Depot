using Depot.Interface.Xml;

namespace Depot.Interface;

/// <summary>
/// The part of UIScript Hudini reads, as the spec's Hudini section draws it: the root, <c>&lt;Window&gt;</c> and
/// <c>&lt;Panel&gt;</c>, resources, styles, visuals, every control but <c>&lt;Shelf&gt;</c> and <c>&lt;Grid&gt;</c>,
/// and the title bar's <c>&lt;ToolbarItem&gt;</c> and <c>&lt;TitleField&gt;</c>. Depot holds documents to it, so what
/// Depot makes is what a game reads.
/// </summary>
/// <remarks>
/// It follows the spec, not Hudini's current release, where the two differ: Hudini still reads the scroll sliders
/// the spec dropped, which the reader here refuses, and doesn't know a slider's <c>step</c> yet, which it skips.
/// </remarks>
public static class HudiniSubset
{
    /// <summary>UIScript's elements that Hudini refuses: menus, sidebars, panes, shelves, grids, and status items.</summary>
    public static IReadOnlySet<string> ElementsOutside { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "Menu", "MenuItem", "MenuSeparator", "Sidebar", "Pane", "Shelf", "Grid", "Item", "StatusItem",
    };

    /// <summary>The elements Hudini reads, the root among them.</summary>
    public static IReadOnlyList<string> Elements { get; } =
        [.. UIScriptSchema.Elements.Where(element => !ElementsOutside.Contains(element))];

    /// <summary>The controls Hudini draws.</summary>
    public static IReadOnlyList<string> Controls { get; } =
        [.. UIScriptSchema.Controls.Where(control => !ElementsOutside.Contains(control))];

    /// <summary>
    /// Whether Hudini skips an attribute UIScript has: a window's <c>subtitle</c>, a toolbar item's <c>image</c> and
    /// <c>placement</c>, and a control's <c>pane</c>, since there are no panes. A document may still carry them, and
    /// Hudini reads it as if they weren't there.
    /// </summary>
    public static bool Skips(string element, string attribute)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(attribute);
        return (element, attribute) switch
        {
            ("Window", "subtitle") => true,
            ("ToolbarItem", "image" or "placement") => true,
            (_, "pane") when UIScriptSchema.Controls.Contains(element) => true,
            _ => false,
        };
    }

    /// <summary>The attributes Hudini reads on <paramref name="element"/>, in an inspector's order; none for an element it refuses.</summary>
    public static IReadOnlyList<UIAttributeInfo> For(string element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return ElementsOutside.Contains(element)
            ? []
            : [.. UIScriptSchema.For(element).Where(info => !Skips(element, info.Name))];
    }

    /// <summary>Refuses a document with an element Hudini doesn't read, naming the first and its line.</summary>
    /// <exception cref="UIScriptException">The document has an element outside the subset.</exception>
    public static void Check(FlatXmlDocument xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        foreach (var element in xml.Children)
        {
            if (ElementsOutside.Contains(element.Name))
                throw new UIScriptException($"<{element.Name}> is part of UIScript, but outside the subset Hudini reads.", element.Line);
        }
    }
}
