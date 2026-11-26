using Depot.Interface.Xml;

namespace Depot.Interface;

/// <summary>
/// A Hudini appearance: the images, styles, and scroll bar art a game's windows share, which a game reads every window
/// with. It's a Hudini feature, not part of UIScript; see Hudini's <c>docs/design/2026-11-25-appearance.md</c>.
/// </summary>
/// <remarks>
/// Depot reads only what it needs to hold a window to what Hudini reads: the names of the appearance's images and
/// styles, which a window may use without defining. Everything else about the file, such as its colors, its scroll
/// bar's numbers, and its fonts, Hudini checks when depot-render draws with it, so the two can't disagree. Depot
/// still refuses what it can see is wrong with a font's or <c>&lt;Fonts&gt;</c>'s place in the file, as it does for
/// the other elements; see Hudini's <c>docs/design/2026-11-25-fonts.md</c>.
/// </remarks>
public sealed class HudiniAppearance
{
    /// <summary>The newest appearance version this reader reads.</summary>
    public const int SupportedVersion = 1;

    private HudiniAppearance(IReadOnlySet<string> images, IReadOnlySet<string> styles, bool hasScrollBar)
    {
        ImageNames = images;
        StyleNames = styles;
        HasScrollBar = hasScrollBar;
    }

    /// <summary>The names of the appearance's images, which a window's visuals and buttons may name.</summary>
    public IReadOnlySet<string> ImageNames { get; }

    /// <summary>The names of the appearance's styles, which a window's controls may name.</summary>
    public IReadOnlySet<string> StyleNames { get; }

    /// <summary>Whether it gives lists and text areas scroll bar art.</summary>
    public bool HasScrollBar { get; }

    /// <summary>Reads an appearance from its text.</summary>
    /// <exception cref="UIScriptException">It isn't an appearance Hudini would read; the message says why.</exception>
    public static HudiniAppearance Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        FlatXmlDocument document;
        try
        {
            document = FlatXml.Parse(text, allowNesting: true);
        }
        catch (FlatXmlException exception)
        {
            throw new UIScriptException(exception.Reason, exception.Line);
        }

        var root = document.Root;
        if (root.Name != "Appearance")
            throw new UIScriptException($"An appearance's root element must be <Appearance>, not <{root.Name}>.", root.Line);
        if (root["version"] is { } written)
        {
            if (!int.TryParse(written, out var version) || version < 1)
                throw new UIScriptException($"<Appearance> can't have \"{written}\" for version.", root.Line);
            if (version > SupportedVersion)
                throw new UIScriptException(
                    $"This is appearance version {version}, and this reader reads up to {SupportedVersion}.", root.Line);
        }

        HashSet<string> images = new(StringComparer.Ordinal);
        HashSet<string> styles = new(StringComparer.Ordinal);
        HashSet<string> fonts = new(StringComparer.Ordinal);
        var scrollBars = 0;
        var fontSlots = 0;
        foreach (var element in document.Children)
        {
            if (element.Parent != FlatXmlDocument.RootIndex)
                throw Misplaced(element.Name, element.Line);
            switch (element.Name)
            {
                case "Image":
                    Add(images, element);
                    break;
                case "Style":
                    Add(styles, element);
                    break;
                case "ScrollBar":
                    if (++scrollBars > 1)
                        throw new UIScriptException("An appearance has at most one <ScrollBar>.", element.Line);
                    break;
                case "Font":
                    // A style's font means the theme's fonts by these names.
                    if (element["name"] is "interface" or "monospace")
                        throw new UIScriptException(
                            $"A <Font> can't be called \"{element["name"]}\", which a style's font already means.", element.Line);
                    Add(fonts, element);
                    break;
                case "Fonts":
                    if (++fontSlots > 1)
                        throw new UIScriptException("An appearance has at most one <Fonts>.", element.Line);
                    break;
                default:
                    throw Misplaced(element.Name, element.Line);
            }
        }

        return new HudiniAppearance(images, styles, scrollBars == 1);
    }

    /// <summary>Reads a UTF-8 file.</summary>
    /// <exception cref="UIScriptException">It isn't an appearance Hudini would read.</exception>
    /// <exception cref="IOException">The file couldn't be read.</exception>
    public static HudiniAppearance Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Parse(File.ReadAllText(path, System.Text.Encoding.UTF8));
    }

    private static void Add(HashSet<string> names, FlatXmlElement element)
    {
        var name = element["name"] ?? throw new UIScriptException($"<{element.Name}> needs a name.", element.Line);
        if (!names.Add(name))
            throw new UIScriptException($"The name \"{name}\" is used by more than one <{element.Name}>.", element.Line);
    }

    private static UIScriptException Misplaced(string name, int line) =>
        new($"<{name}> doesn't belong there: an appearance holds <Image>, <Style>, <Font>, <Fonts>, and <ScrollBar>, directly under its root.", line);
}
