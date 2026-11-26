using System.Text.Json;
using System.Text.Json.Serialization;

namespace Depot.Rendering;

/// <summary>A look depot-render can draw a document in. Hudini's themes of the same names.</summary>
public enum CanvasLook
{
    /// <summary>The look Depot gives new documents: Hudini's slate and amber, softened after Adwaita.</summary>
    Depot,

    /// <summary>The light twin of <see cref="Depot"/>.</summary>
    DepotLight,

    /// <summary>Hudini's standard look, what a game gets when it doesn't choose one.</summary>
    Standard,
}

/// <summary>Names and titles for <see cref="CanvasLook"/>.</summary>
public static class CanvasLooks
{
    /// <summary>The name depot-render takes for the look.</summary>
    public static string Name(this CanvasLook look) => look switch
    {
        CanvasLook.Depot => "depot",
        CanvasLook.DepotLight => "depot-light",
        CanvasLook.Standard => "standard",
        _ => throw new ArgumentOutOfRangeException(nameof(look), look, "There's no such look."),
    };

    /// <summary>The look's name as Depot shows it.</summary>
    public static string Title(this CanvasLook look) => look switch
    {
        CanvasLook.Depot => "Depot",
        CanvasLook.DepotLight => "Depot Light",
        CanvasLook.Standard => "Hudini Standard",
        _ => throw new ArgumentOutOfRangeException(nameof(look), look, "There's no such look."),
    };
}

/// <summary>
/// One request to depot-render: a document, by its text or its path, and where to write the picture. Written as one
/// line of JSON, as <c>depot-render --serve</c> reads.
/// </summary>
public sealed record RenderRequest
{
    /// <summary>The document's text, for a document being edited.</summary>
    public string? Document { get; init; }

    /// <summary>A document on disk, drawn as it's saved.</summary>
    public string? Path { get; init; }

    /// <summary>How the document is written, <c>uix</c> or <c>uis</c>; by default, from the path's extension.</summary>
    public string? Format { get; init; }

    /// <summary>The look, by <see cref="CanvasLooks.Name"/>.</summary>
    public string? Theme { get; init; }

    /// <summary>The title a window's bar shows.</summary>
    public string? Title { get; init; }

    /// <summary>The folder the document's image paths start from.</summary>
    public string? Directory { get; init; }

    /// <summary>A Hudini appearance file the document is read with, as a game reads its windows.</summary>
    public string? Appearance { get; init; }

    /// <summary>The tab to show, by name; the document's first without one.</summary>
    public string? Tab { get; init; }

    /// <summary>The pane to show, by name; the document's first without one.</summary>
    public string? Pane { get; init; }

    /// <summary>Where to write the PNG.</summary>
    public required string Output { get; init; }

    /// <summary>
    /// Which open document the request is for, so a picture that arrives after another document is opened isn't
    /// shown over it. Depot's own; depot-render never sees it.
    /// </summary>
    [JsonIgnore]
    public int DocumentId { get; init; }
}

/// <summary>A rectangle in the picture, in pixels from its top-left corner.</summary>
public sealed record FrameRect(float X, float Y, float Width, float Height);

/// <summary>One visual or control, and where Hudini draws it.</summary>
/// <param name="Element">Its element in the document, such as <c>Button</c> or <c>Visual</c>.</param>
public sealed record FrameElement(string Name, string Element, FrameRect Frame, bool Hidden);

/// <summary>Where Hudini placed everything a document draws, as depot-render reports it.</summary>
/// <param name="Kind"><c>window</c> or <c>panel</c>.</param>
/// <param name="Sidebar">Where a window's sidebar is drawn, or <see langword="null"/> without one.</param>
/// <param name="Tab">The tab shown, or <see langword="null"/> for a document without tabs.</param>
/// <param name="Pane">The pane shown, or <see langword="null"/> for a document without panes.</param>
public sealed record FrameMap(
    string Kind,
    float Width,
    float Height,
    FrameRect Content,
    IReadOnlyList<FrameElement> Elements,
    IReadOnlyList<string> MissingImages,
    IReadOnlyList<string> MissingStrings,
    IReadOnlyList<string> IgnoredAttributes,
    IReadOnlyList<string> Notes,
    FrameRect? Sidebar = null,
    string? Tab = null,
    string? Pane = null);

/// <summary>depot-render's answer to a request: where everything went, or why it couldn't draw.</summary>
public sealed record RenderReply(FrameMap? Frames, string? Error)
{
    /// <summary>Reads one line of depot-render's answers.</summary>
    /// <exception cref="RendererException">The line isn't an answer depot-render gives.</exception>
    public static RenderReply Parse(string line)
    {
        RenderReply? reply;
        try
        {
            reply = JsonSerializer.Deserialize<RenderReply>(line, RenderJson.Options);
        }
        catch (JsonException exception)
        {
            throw new RendererException($"depot-render answered with something Depot can't read: {Shorten(line)}", exception);
        }

        if (reply is null || (reply.Frames is null) == (reply.Error is null))
            throw new RendererException($"depot-render answered with neither a picture nor an error: {Shorten(line)}");
        return reply;
    }

    private static string Shorten(string line) => line.Length <= 200 ? line : line[..200] + "…";
}

/// <summary>How Depot and depot-render write JSON to each other.</summary>
public static class RenderJson
{
    /// <summary>Camel-cased names, and no empty fields, so a request carries only what it says.</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>depot-render couldn't be started, stopped, or didn't answer as it should.</summary>
public sealed class RendererException : Exception
{
    public RendererException(string message) : base(message) { }

    public RendererException(string message, Exception inner) : base(message, inner) { }
}
