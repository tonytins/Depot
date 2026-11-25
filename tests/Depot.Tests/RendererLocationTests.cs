using Depot.Rendering;

namespace Depot.Tests;

/// <summary>Where Depot looks for depot-render, in order.</summary>
public sealed class RendererLocationTests : IDisposable
{
    private readonly TemporaryFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string Make(params string[] parts)
    {
        var path = Path.Combine([folder.Path, .. parts]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
        return path;
    }

    private string Folder(params string[] parts)
    {
        var path = Path.Combine([folder.Path, .. parts]);
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void TheEnvironmentWinsEvenOverAProgramBesideDepot()
    {
        var app = Folder("app");
        Make("app", RendererLocation.FileName);

        Assert.Equal("/somewhere/depot-render", RendererLocation.Find("/somewhere/depot-render", app, null));
    }

    [Fact]
    public void BesideDepotComesFirstOtherwise()
    {
        var app = Folder("app");
        var beside = Make("app", RendererLocation.FileName);
        var onPath = Make("bin", RendererLocation.FileName);

        Assert.Equal(beside, RendererLocation.Find(null, app, Path.GetDirectoryName(onPath)));
    }

    [Fact]
    public void ADevelopmentBuildIsFoundFromDepotsBuildFolder()
    {
        var app = Folder("checkout", "src", "Depot", "bin", "Debug", "net10.0");
        var built = Make("checkout", "depot-render", ".build", "debug", RendererLocation.FileName);

        Assert.Equal(built, RendererLocation.Find("", app, null));
    }

    [Fact]
    public void TheNewerOfTwoBuildsIsUsed()
    {
        var app = Folder("checkout", "src", "Depot", "bin", "Debug", "net10.0");
        var release = Make("checkout", "depot-render", ".build", "release", RendererLocation.FileName);
        var debug = Make("checkout", "depot-render", ".build", "debug", RendererLocation.FileName);
        File.SetLastWriteTimeUtc(release, DateTime.UtcNow.AddHours(-1));

        Assert.Equal(debug, RendererLocation.Find(null, app, null));
    }

    [Fact]
    public void ThePathIsLast()
    {
        var app = Folder("app");
        var onPath = Make("bin", RendererLocation.FileName);

        Assert.Equal(onPath, RendererLocation.Find(null, app, string.Join(Path.PathSeparator, Folder("empty"), Path.GetDirectoryName(onPath))));
    }

    [Fact]
    public void NowhereIsNull() => Assert.Null(RendererLocation.Find(null, Folder("app"), Folder("empty")));
}
