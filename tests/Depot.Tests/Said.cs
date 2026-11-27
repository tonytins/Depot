using System.Text.RegularExpressions;

namespace Depot.Tests;

/// <summary>
/// What the table says for a key, for tests to look for, so they check which words Depot chose without pinning how
/// they're worded. The words are meant to change without touching code, so a test that copies them breaks for nothing.
/// </summary>
internal static class Said
{
    /// <summary>
    /// A pattern for <paramref name="key"/>'s words in which each <c>%s</c> matches anything, for when a test can't know
    /// what Depot fills in, such as an exception's message.
    /// </summary>
    public static Regex Pattern(string key)
    {
        Assert.True(Words.Read()!.TryGetValue(key, out var text), $"The table has no '{key}'.");
        var parts = text.Split("%s").Select(Regex.Escape);
        return new Regex(string.Join(".*?", parts), RegexOptions.Singleline);
    }
}
