using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Depot.Designer;
using Depot.Interface;

namespace Depot.Tests;

/// <summary>Depot's words: the table, its keys, and that the window shows words rather than keys.</summary>
public sealed partial class WordsTests
{
    private static readonly string[] Keys =
    [
        .. typeof(TextKeys).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!),
    ];

    [Fact]
    public void TheTableReadsWithoutProblems()
    {
        var table = Words.Read();

        Assert.NotNull(table);
        Assert.Empty(table.Problems);
    }

    [Fact]
    public void EveryKeyIsInTheTableAndEveryEntryHasAKey()
    {
        var entries = Words.Read()!.Entries.Select(entry => entry.Key).ToHashSet();

        Assert.Equal([], Keys.Where(key => !entries.Contains(key)));
        Assert.Equal([], entries.Where(key => key is null || !Keys.Contains(key)));
    }

    [Fact]
    public void TheWindowsXamlNamesOnlyKeysTheTableHas()
    {
        var xaml = File.ReadAllText(Path.Combine(SourceFolder(), "src", "Depot", "MainWindow.axaml"));
        var named = WordsInXaml().Matches(xaml).Select(match => match.Groups[1].Value).ToList();

        Assert.NotEmpty(named);
        Assert.Equal([], named.Where(key => !Keys.Contains(key)));
    }

    [Fact]
    public void PlaceholdersAreFilledInOrder()
    {
        Assert.Equal("budget.uix (edited) — Depot", Words.Get(TextKeys.TitleDocumentEdited, "budget.uix"));
        Assert.Equal("320 × 244, as Hudini draws it, showing the Main tab and the Roads pane.",
            Words.Get(TextKeys.StatusSizeTabPane, "320", "244", "Main", "Roads"));
    }

    [Fact]
    public void AKeyTheTableLacksShowsItself()
    {
        Assert.Equal("no such words", Words.Get("no such words"));
    }

    [Fact]
    public void EveryInspectorGroupHasAHeading()
    {
        foreach (var group in Enum.GetValues<UIAttributeGroup>())
            Assert.DoesNotContain(InspectorPanel.GroupTitle(group), Keys);
    }

    [Fact]
    public void TheWindowShowsWordsNotKeys() => TestApp.Run(() =>
    {
        MainWindow window = new(rendererPath: null);
        window.Show();

        var shown = window.GetVisualDescendants().SelectMany(Said).Where(text => !string.IsNullOrEmpty(text)).ToList();
        shown.Add(window.Title ?? "");

        Assert.Contains("Undo", shown);
        Assert.Equal([], shown.Where(Keys.Contains));
        window.Close();
    });

    private static IEnumerable<string?> Said(Visual visual)
    {
        if (visual is TextBlock block)
            yield return block.Text;
        if (visual is ContentControl { Content: string content })
            yield return content;
        if (visual is Control control && ToolTip.GetTip(control) is string tip)
            yield return tip;
    }

    // The repository's root, found from the test's output folder, so the test reads the XAML as written.
    private static string SourceFolder([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", ".."));

    [GeneratedRegex(@"\{local:Words '([^']+)'\}")]
    private static partial Regex WordsInXaml();
}
