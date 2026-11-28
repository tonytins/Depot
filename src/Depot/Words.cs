using CSTNet;

namespace Depot;

/// <summary>
/// Depot's words, read from <c>uitext/english.dir/_100_depot.cst</c> beside the app, as the Venture Design Language
/// asks, so a label changes in the table rather than in code. English is the only language for now.
/// </summary>
public static class Words
{
    /// <summary>The table's number, from its file name, <c>_100_depot.cst</c>.</summary>
    public const int TableId = 100;

    /// <summary>Where the table is, beside the app.</summary>
    public static string TablePath { get; } = Path.Combine(AppContext.BaseDirectory, "uitext", "english.dir", "_100_depot.cst");

    // Read once, the first time a word is asked for. A table that can't be read leaves every word showing its key,
    // so the gap is plain to see rather than blank, and Depot still runs.
    private static readonly Lazy<CSTTable?> Table = new(() =>
    {
        try
        {
            return CSTTable.Parse(File.ReadAllText(TablePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    });

    /// <summary>The table as read, or <see langword="null"/> when it couldn't be, for tests to check.</summary>
    public static CSTTable? Read() => Table.Value;

    /// <summary>
    /// The words for <paramref name="key"/>, a <see cref="TextKeys"/> constant, with its <c>%s</c> placeholders
    /// filled with <paramref name="values"/> in order. A key the table doesn't have gives back the key.
    /// </summary>
    public static string Get(string key, params object[] values)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (Table.Value is not { } table || !table.TryGetValue(key, out var text))
            return key;
        return values.Length == 0 ? text : CST.Format(text, values);
    }
}

/// <summary>Depot's words in XAML: <c>{local:Words 'bar undo'}</c>, read from the table when the window loads.</summary>
public sealed class WordsExtension(string key) : Avalonia.Markup.Xaml.MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => Words.Get(key);
}
