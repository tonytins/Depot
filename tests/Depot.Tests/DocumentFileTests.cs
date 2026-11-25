using System.Text;
using Depot.Documents;

namespace Depot.Tests;

/// <summary>Opening and saving documents, keeping what Depot didn't change.</summary>
public sealed class DocumentFileTests : IDisposable
{
    private readonly TemporaryFolder folder = new();

    public void Dispose() => folder.Dispose();

    [Fact]
    public void SavingWritesTheTextExactlyAsEdited()
    {
        var path = folder.File("a.uix", "<UIScript>\r\n</UIScript>\r\n");
        var document = DocumentFile.Open(path);

        document.Save("<UIScript version=\"1\">\r\n</UIScript>\r\n");

        Assert.Equal("<UIScript version=\"1\">\r\n</UIScript>\r\n", File.ReadAllText(path));
        Assert.Equal("<UIScript version=\"1\">\r\n</UIScript>\r\n", document.Text);
        Assert.Single(Directory.GetFiles(folder.Path));
    }

    [Fact]
    public void AByteOrderMarkIsKeptButNotEdited()
    {
        var path = Path.Combine(folder.Path, "marked.uix");
        File.WriteAllBytes(path, [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("<UIScript/>")]);

        var document = DocumentFile.Open(path);
        Assert.Equal("<UIScript/>", document.Text);
        document.Save("<UIScript />");

        Assert.Equal([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("<UIScript />")], File.ReadAllBytes(path));
    }

    [Fact]
    public void UisFilesAreReadOnly()
    {
        var document = DocumentFile.Open(folder.File("stand.uis", "<Begin>"));

        Assert.True(document.IsUis);
        Assert.True(document.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => document.Save("changed"));
        Assert.Equal("<Begin>", File.ReadAllText(document.Path));
    }

    [Fact]
    public void AUisFileThatIsntUtf8IsShownAnyway()
    {
        var path = Path.Combine(folder.Path, "old.uis");
        File.WriteAllBytes(path, [.. Encoding.ASCII.GetBytes("caf"), 0xE9]);

        Assert.Equal("café", DocumentFile.Open(path).Text);
    }

    [Fact]
    public void AUIScriptFileThatIsntUtf8IsRefused()
    {
        var path = Path.Combine(folder.Path, "latin.uix");
        File.WriteAllBytes(path, [.. Encoding.ASCII.GetBytes("<UIScript>caf"), 0xE9]);

        var failure = Assert.Throws<DocumentException>(() => DocumentFile.Open(path));
        Assert.Contains("isn't UTF-8", failure.Message);
    }

    [Fact]
    public void AMissingFileIsReported() =>
        Assert.Contains("There's no file", Assert.Throws<DocumentException>(() => DocumentFile.Open(Path.Combine(folder.Path, "nope.uix"))).Message);

    [Fact]
    public void ADocumentTooLargeIsRefusedUnread()
    {
        var path = Path.Combine(folder.Path, "huge.uix");
        using (var stream = File.Create(path))
            stream.SetLength(DocumentFile.MaximumSize + 1);

        Assert.Contains("up to", Assert.Throws<DocumentException>(() => DocumentFile.Open(path)).Message);
    }

    [Fact]
    public void AFailedSaveLeavesTheFileAsItWas()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Folder permissions are set the Unix way.");
            return;
        }

        var path = folder.File("a.uix", "<UIScript/>");
        var document = DocumentFile.Open(path);
        File.SetUnixFileMode(folder.Path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            Assert.Throws<DocumentException>(() => document.Save("changed"));
        }
        finally
        {
            File.SetUnixFileMode(folder.Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Assert.Equal("<UIScript/>", File.ReadAllText(path));
        Assert.Equal("<UIScript/>", document.Text);
    }
}
