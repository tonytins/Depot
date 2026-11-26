namespace Depot;

/// <summary>Depot's command line: <c>Depot [--appearance &lt;file&gt;] [document]</c>.</summary>
public static class StartupArguments
{
    /// <summary>
    /// The document to open and the appearance to read with, from the arguments Depot was started with. The first
    /// argument that isn't an option is the document; a later one is ignored, as before options existed.
    /// </summary>
    public static (string? Document, string? Appearance) Read(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        string? document = null;
        string? appearance = null;
        for (var at = 0; at < arguments.Count; at++)
        {
            var argument = arguments[at];
            if (argument == "--appearance")
            {
                // A trailing --appearance names nothing, so it isn't taken for the document.
                if (at + 1 < arguments.Count)
                    appearance = arguments[++at];
            }
            else if (argument.StartsWith("--appearance=", StringComparison.Ordinal))
                appearance = argument["--appearance=".Length..];
            else
                document ??= argument;
        }

        return (document, appearance);
    }
}
