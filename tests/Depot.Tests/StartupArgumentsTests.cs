namespace Depot.Tests;

/// <summary>Depot's command line: a document to open, and an appearance to read it with.</summary>
public sealed class StartupArgumentsTests
{
    [Theory]
    [InlineData(new string[0], null, null)]
    [InlineData(new[] { "chat.uix" }, "chat.uix", null)]
    [InlineData(new[] { "--appearance", "look.xml", "chat.uix" }, "chat.uix", "look.xml")]
    [InlineData(new[] { "chat.uix", "--appearance", "look.xml" }, "chat.uix", "look.xml")]
    [InlineData(new[] { "--appearance=look.xml" }, null, "look.xml")]
    [InlineData(new[] { "chat.uix", "--appearance" }, "chat.uix", null)]
    [InlineData(new[] { "chat.uix", "other.uix" }, "chat.uix", null)]
    public void ReadsTheDocumentAndTheAppearance(string[] arguments, string? document, string? appearance)
    {
        Assert.Equal((document, appearance), StartupArguments.Read(arguments));
    }
}
