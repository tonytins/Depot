using System.IO.Pipelines;
using System.Text.Json;
using Depot.Rendering;

namespace Depot.Tests;

/// <summary>Depot's half of the conversation with <c>depot-render --serve</c>, over pipes in place of the process.</summary>
public sealed class RenderSessionTests
{
    private const string FramesLine = """{"frames":{"content":{"height":100,"width":200,"x":0,"y":30},"elements":[{"element":"Button","frame":{"height":24,"width":80,"x":10,"y":40},"hidden":false,"name":"ok"}],"height":130,"ignoredAttributes":[],"kind":"window","missingImages":["art"],"missingStrings":[],"notes":[],"width":200}}""";

    private static RenderRequest Request => new() { Document = "<UIScript/>", Theme = "depot", Output = "/tmp/out.png", DocumentId = 7 };

    [Fact]
    public async Task WritesARequestAsOneLineOfTheFieldsItHas()
    {
        StringWriter input = new();
        RenderSession session = new(input, new StringReader(FramesLine + "\n"), TimeSpan.FromSeconds(5));

        await session.RenderAsync(Request with { Document = "<UIScript>\n</UIScript>" });

        var line = Assert.Single(input.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries));
        using var json = JsonDocument.Parse(line);
        var names = json.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray();
        // No DocumentId, which is Depot's own, and nothing left empty.
        Assert.Equal(["document", "output", "theme"], names);
        Assert.Equal("<UIScript>\n</UIScript>", json.RootElement.GetProperty("document").GetString());
    }

    [Fact]
    public async Task ReadsWhereEverythingWent()
    {
        RenderSession session = new(new StringWriter(), new StringReader(FramesLine + "\n"), TimeSpan.FromSeconds(5));

        var reply = await session.RenderAsync(Request);

        Assert.Null(reply.Error);
        var frames = Assert.IsType<FrameMap>(reply.Frames);
        Assert.Equal("window", frames.Kind);
        Assert.Equal((200f, 130f), (frames.Width, frames.Height));
        Assert.Equal(new FrameElement("ok", "Button", new FrameRect(10, 40, 80, 24), false), Assert.Single(frames.Elements));
        Assert.Equal(["art"], frames.MissingImages);
    }

    [Fact]
    public async Task ReadsWhyItCouldntDraw()
    {
        RenderSession session = new(new StringWriter(), new StringReader("""{"error":"Hudini can't read the document."}""" + "\n"), TimeSpan.FromSeconds(5));

        var reply = await session.RenderAsync(Request);

        Assert.Equal("Hudini can't read the document.", reply.Error);
        Assert.Null(reply.Frames);
        Assert.False(session.IsBroken);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"frames":null,"error":null}""")]
    public async Task AnAnswerItDoesntGiveBreaksTheSession(string line)
    {
        RenderSession session = new(new StringWriter(), new StringReader(line + "\n"), TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<RendererException>(() => session.RenderAsync(Request));
        Assert.True(session.IsBroken);
        await Assert.ThrowsAsync<RendererException>(() => session.RenderAsync(Request));
    }

    [Fact]
    public async Task StoppingWithoutAnAnswerBreaksTheSession()
    {
        RenderSession session = new(new StringWriter(), new StringReader(""), TimeSpan.FromSeconds(5));

        var failure = await Assert.ThrowsAsync<RendererException>(() => session.RenderAsync(Request));

        Assert.Contains("stopped", failure.Message);
        Assert.True(session.IsBroken);
    }

    [Fact]
    public async Task ALateAnswerBreaksTheSession()
    {
        // A pipe nobody writes to: the answer never comes.
        Pipe silent = new();
        RenderSession session = new(new StringWriter(), new StreamReader(silent.Reader.AsStream()), TimeSpan.FromMilliseconds(100));

        var failure = await Assert.ThrowsAsync<RendererException>(() => session.RenderAsync(Request));

        Assert.Contains("didn't answer within", failure.Message);
        Assert.True(session.IsBroken);
    }
}
