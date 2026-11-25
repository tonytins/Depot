using Depot.Rendering;

namespace Depot.Tests;

/// <summary>
/// Starting, restarting, and stopping depot-render, with shell scripts standing in for it. They need a Unix shell, so
/// they don't run on Windows.
/// </summary>
public sealed class RendererTests : IDisposable
{
    private readonly TemporaryFolder folder = new();

    public void Dispose() => folder.Dispose();

    private static RenderRequest Request => new() { Document = "<UIScript/>", Output = "/tmp/out.png" };

    private string Script(string name, string body)
    {
        if (OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The stand-ins for depot-render are shell scripts.");
        var path = folder.File(name, "#!/bin/sh\n" + body + "\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    [Fact]
    public async Task AnswersFromTheRunningProgram()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The stand-in is a shell script.");
        using Renderer renderer = new(Script("answers", """while read line; do echo '{"error":"seen"}'; done"""));

        Assert.Equal("seen", (await renderer.RenderAsync(Request)).Error);
        Assert.Equal("seen", (await renderer.RenderAsync(Request)).Error);
    }

    [Fact]
    public async Task AFailureSaysWhatItSaidAndTheNextRequestStartsItAgain()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The stand-in is a shell script.");
        var count = Path.Combine(folder.Path, "starts");
        // Fails on its first start, saying why; answers once started again.
        using Renderer renderer = new(Script("flaky", $$"""
            echo started >> '{{count}}'
            if [ "$(wc -l < '{{count}}')" -eq 1 ]; then echo 'no display' >&2; exit 1; fi
            while read line; do echo '{"error":"second start"}'; done
            """));

        var failure = await Assert.ThrowsAsync<RendererException>(() => renderer.RenderAsync(Request));
        Assert.Contains("no display", failure.Message);

        Assert.Equal("second start", (await renderer.RenderAsync(Request)).Error);
        Assert.Equal(2, File.ReadAllLines(count).Length);
    }

    [Fact]
    public async Task AProgramThatIsntThereIsReported()
    {
        using Renderer renderer = new(Path.Combine(folder.Path, "missing", RendererLocation.FileName));

        var failure = await Assert.ThrowsAsync<RendererException>(() => renderer.RenderAsync(Request));

        Assert.Contains("Couldn't start depot-render", failure.Message);
    }
}
