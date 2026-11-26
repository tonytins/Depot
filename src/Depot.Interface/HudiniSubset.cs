using Depot.Interface.Xml;

namespace Depot.Interface;

/// <summary>
/// The part of UIScript Hudini reads, as the spec's Hudini section draws it: the root, <c>&lt;Window&gt;</c> and
/// <c>&lt;Panel&gt;</c>, resources, styles, visuals, every control but <c>&lt;Shelf&gt;</c> and <c>&lt;Grid&gt;</c>,
/// the title bar's <c>&lt;ToolbarItem&gt;</c> and <c>&lt;TitleField&gt;</c>, tabs, and a sidebar of panes. Depot
/// holds documents to it, so what Depot makes is what a game reads.
/// </summary>
/// <remarks>
/// It follows the spec, not any one release of Hudini. Since 2026-11-25 the two agree on what changed that week:
/// Hudini refuses the scroll sliders and scroll buttons the spec dropped, as the reader here does, and reads a
/// slider's <c>step</c>. A game whose Hudini is older reads the documents Depot makes, but reads <c>step</c> as an
/// ignored attribute. Tabs and sidebars of panes came on 2026-11-25 too; an older Hudini refuses a document with
/// them as having unknown elements.
/// </remarks>
public static class HudiniSubset
{
    /// <summary>
    /// UIScript's elements that Hudini refuses: menus, shelves, grids, and status items. It reads a sidebar only of
    /// panes, which <see cref="Check"/> holds documents to.
    /// </summary>
    public static IReadOnlySet<string> ElementsOutside { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "Menu", "MenuItem", "MenuSeparator", "Shelf", "Grid", "Item", "StatusItem",
    };

    /// <summary>The elements Hudini reads, the root among them.</summary>
    public static IReadOnlyList<string> Elements { get; } =
        [.. UIScriptSchema.Elements.Where(element => !ElementsOutside.Contains(element))];

    /// <summary>The controls Hudini draws.</summary>
    public static IReadOnlyList<string> Controls { get; } =
        [.. UIScriptSchema.Controls.Where(control => !ElementsOutside.Contains(control))];

    /// <summary>
    /// Whether Hudini skips an attribute UIScript has: a window's <c>subtitle</c>, a toolbar item's <c>image</c> and
    /// <c>placement</c>, and a sidebar's <c>edge</c>, so its sidebar is always on the left. A document may still carry
    /// them, and Hudini reads it as if they weren't there.
    /// </summary>
    public static bool Skips(string element, string attribute)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(attribute);
        return (element, attribute) switch
        {
            ("Window", "subtitle") => true,
            ("ToolbarItem", "image" or "placement") => true,
            ("Sidebar", "edge") => true,
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

    /// <summary>
    /// Refuses a document with an element Hudini doesn't read, naming the first and its line, or with a sidebar that
    /// isn't of panes: one whose rows the host fills, or one that holds controls.
    /// </summary>
    /// <exception cref="UIScriptException">The document has something outside the subset.</exception>
    public static void Check(FlatXmlDocument xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        var hasPanes = xml.Children.Any(element => element.Name == "Pane");
        for (var at = 0; at < xml.Children.Count; at++)
        {
            var element = xml.Children[at];
            if (ElementsOutside.Contains(element.Name))
                throw new UIScriptException($"<{element.Name}> is part of UIScript, but outside the subset Hudini reads.", element.Line);
            if (element.Name == "Sidebar" && (!hasPanes || xml.ChildrenOf(at).Length > 0))
                throw new UIScriptException(
                    "Hudini reads a sidebar of panes; a sidebar that holds controls, or whose rows the host fills, is outside the subset Hudini reads.",
                    element.Line);
        }
    }
}
