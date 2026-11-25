using Depot.Rendering;

namespace Depot.Tests;

/// <summary>Only the newest request waits while a picture is drawn.</summary>
public sealed class PreviewQueueTests
{
    private static RenderRequest Request(string text) => new() { Document = text, Output = "out.png" };

    private static RenderReply Reply(string text) => new(null, text);

    [Fact]
    public void RequestsMadeWhileDrawingAreDroppedForTheNewest()
    {
        List<string> drawn = [];
        Queue<TaskCompletionSource<RenderReply>> pictures = new();
        PreviewQueue queue = new(request =>
        {
            drawn.Add(request.Document!);
            TaskCompletionSource<RenderReply> picture = new();
            pictures.Enqueue(picture);
            return picture.Task;
        });
        List<string> answered = [];
        queue.Answered += (request, reply) => answered.Add($"{request.Document}:{reply.Error}");

        queue.Request(Request("a"));
        queue.Request(Request("b"));
        queue.Request(Request("c"));
        Assert.True(queue.IsDrawing);
        pictures.Dequeue().SetResult(Reply("A"));
        pictures.Dequeue().SetResult(Reply("C"));

        Assert.Equal(["a", "c"], drawn);
        Assert.Equal(["a:A", "c:C"], answered);
        Assert.False(queue.IsDrawing);
    }

    [Fact]
    public void AFailureIsReportedAndTheQueueGoesOn()
    {
        var fail = true;
        PreviewQueue queue = new(_ => fail
            ? Task.FromException<RenderReply>(new RendererException("gone"))
            : Task.FromResult(Reply("fine")));
        List<string> heard = [];
        queue.Failed += (_, exception) => heard.Add(exception.Message);
        queue.Answered += (_, reply) => heard.Add(reply.Error!);

        queue.Request(Request("a"));
        fail = false;
        queue.Request(Request("b"));

        Assert.Equal(["gone", "fine"], heard);
    }
}
