using Depot.Interface;

namespace Depot.Designer;

/// <summary>
/// Which of a document's tabs and panes the canvas shows. Hudini draws one tab and one pane at a time, the first of
/// each in a game, so selecting something in another one, or the tab or pane itself, shows that one instead.
/// </summary>
/// <param name="Tab">The tab shown, by name, or <see langword="null"/> for the document's first.</param>
/// <param name="Pane">The pane shown, by name, or <see langword="null"/> for the document's first.</param>
public sealed record ShownParts(string? Tab = null, string? Pane = null)
{
    /// <summary>
    /// What to show once <paramref name="element"/> is selected: the tab it's in, or names with a toolbar item's
    /// <c>tab</c>, or is; and the pane it's in, names with a control's <c>pane</c>, or is. Anything else leaves what's
    /// shown as it was.
    /// </summary>
    public ShownParts Selecting(UIDocument document, int? element)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (element is not { } index || document.Xml is not { } xml || index < 0 || index >= xml.Children.Count)
            return this;

        var selected = xml.Children[index];
        var parent = selected.Parent >= 0 && selected.Parent < xml.Children.Count ? xml.Children[selected.Parent] : null;
        var tab = selected.Name switch
        {
            "Tab" => selected["name"],
            "ToolbarItem" => selected["tab"],
            _ => parent?.Name == "Tab" ? parent["name"] : null,
        };
        var pane = selected.Name == "Pane" ? selected["name"]
            : parent?.Name == "Pane" ? parent["name"]
            : FrameEdit.IsFramed(selected.Name) ? selected["pane"]
            : null;
        return new ShownParts(tab ?? Tab, pane ?? Pane);
    }

    /// <summary>
    /// What's shown, without a tab or pane <paramref name="document"/> no longer has, such as one renamed or deleted,
    /// which depot-render would refuse to show. Text Hudini can't read shows nothing in particular.
    /// </summary>
    public ShownParts In(UIDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var script = document.Script;
        return new ShownParts(
            Tab is { } tab && script?.Tabs.Any(each => each.Name == tab) == true ? tab : null,
            Pane is { } pane && script?.Panes.Any(each => each.Name == pane) == true ? pane : null);
    }
}
